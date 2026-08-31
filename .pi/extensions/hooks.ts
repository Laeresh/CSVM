import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { isToolCallEventType } from "@earendil-works/pi-coding-agent";
import { exec, execFile } from "child_process";
import { promisify } from "util";

const pExec = promisify(exec);
const pExecFile = promisify(execFile);

export default function (pi: ExtensionAPI) {
  // 1. Shell syntax check hook (PreToolUse for Bash|PowerShell)
  pi.on("tool_call", async (event, ctx) => {
    if (isToolCallEventType("bash", event) || isToolCallEventType("powershell", event)) {
      const result = await runPowerShellHook(ctx.cwd, event);
      if (result.blocked) {
        ctx.ui.notify(result.message, "error");
        return { block: true, reason: result.reason };
      }
    }
  });

  // 2. Bash guard hook (PreToolUse for Bash)
  pi.on("tool_call", async (event, ctx) => {
    if (isToolCallEventType("bash", event)) {
      const result = await runPowerShellBashGuard(ctx.cwd, event);
      if (result.blocked) {
        ctx.ui.notify(result.message, "info");
        return { block: true, reason: result.reason };
      }
    }
  });

  // 3. dotnet format before tests hook (PreToolUse for Bash|PowerShell)
  pi.on("tool_call", async (event, ctx) => {
    if (isToolCallEventType("bash", event) || isToolCallEventType("powershell", event)) {
      const result = await runPowerShellDotnetFormat(ctx.cwd, event);
      if (result.blocked) {
        ctx.ui.notify(result.message, "error");
        return { block: true, reason: result.reason };
      }
    }
  });

  // 4. Content checks before a commit: encoding, item IDs, golden prose, comment caps.
  //    All four live in CheckCommitContent.ps1 at the repo root rather than being reimplemented
  //    here. Three hand-maintained copies drifted - this file never received the comment-cap
  //    check at all - so the checks are now written once and called by every harness.
  pi.on("tool_call", async (event, ctx) => {
    if (isToolCallEventType("bash", event) || isToolCallEventType("powershell", event)) {
      const result = await runContentGate(ctx.cwd, event);
      if (result.blocked) {
        ctx.ui.notify(result.message, "error");
        return { block: true, reason: result.reason };
      }
    }
  });
}

interface HookResult {
  blocked: boolean;
  message: string;
  reason: string;
}

const NOT_BLOCKED: HookResult = { blocked: false, message: "", reason: "" };

async function runPowerShellHook(cwd: string, event: any): Promise<HookResult> {
  const command = event.input.command;
  if (!command) return NOT_BLOCKED;

  // Pass tool name and command to PowerShell via JSON
  const inputJson = JSON.stringify({
    tool_name: event.toolName,
    tool_input: { command: command }
  });

  const psScript = `\$j = [Console]::In.ReadToEnd() | ConvertFrom-Json; \$c = \$j.tool_input.command; if (-not \$c) { exit 0 }; \$q = [char]39; if (\$j.tool_name -eq 'Bash' -and (\$c -match ('@' + \$q + '\\r?\\n')) -and (\$c -match ('(^|\\n)' + \$q + '@'))) { [Console]::Error.WriteLine('BLOCKED: PowerShell here-string (@' + \$q + ' ... ' + \$q + '@) in the Bash tool, which takes a heredoc. Bash treats the @ lines as literal text instead of failing, so the content lands corrupted (this has silently mangled commit messages). Use a heredoc: cmd <<' + \$q + 'EOF' + \$q + ' ... EOF. For a multi-line commit message prefer Write to a file, then: git commit -F <file>.'); exit 2 }; if (\$j.tool_name -eq 'PowerShell' -and ((\$c -match ('<<-?\\s*' + \$q + '?[A-Za-z_]')) -or \$c.Contains('/dev/null'))) { [Console]::Error.WriteLine('BLOCKED: bash syntax (heredoc or /dev/null) in the PowerShell tool. Use a here-string @' + \$q + ' ... ' + \$q + '@ with the closing delimiter at column 0, and \$null instead of /dev/null.'); exit 2 }; exit 0`;

  try {
    const { stderr, exitCode } = await pExec(`powershell -Command ${escapePowerShellCommand(psScript)}`, {
      cwd,
      input: inputJson,
      timeout: 15000
    });

    if (exitCode === 2) {
      return {
        blocked: true,
        message: stderr || "Shell syntax check failed",
        reason: "shell syntax error"
      };
    }
  } catch (error: any) {
    if (error.code === 2) {
      return {
        blocked: true,
        message: error.message || "Shell syntax check failed",
        reason: "shell syntax error"
      };
    }
  }

  return NOT_BLOCKED;
}

async function runPowerShellBashGuard(cwd: string, event: any): Promise<HookResult> {
  const command = event.input.command;
  if (!command) return NOT_BLOCKED;

  const inputJson = JSON.stringify({
    tool_name: "Bash",
    tool_input: { command: command }
  });

  const psScript = `\$j = [Console]::In.ReadToEnd() | ConvertFrom-Json; \$c = \$j.tool_input.command; if (-not \$c) { exit 0 }; \$segments = \$c -split '&&|\\|\\||;|\\|' | ForEach-Object { \$_.Trim() } | Where-Object { \$_ }; \$other = \$segments | Where-Object { \$_ -notmatch '^(git|gh)\\b' }; if (-not \$other) { exit 0 }; [Console]::Error.WriteLine('Use Powershell instead of bash'); exit 2`;

  try {
    const { stderr, exitCode } = await pExec(`powershell -Command ${escapePowerShellCommand(psScript)}`, {
      cwd,
      input: inputJson,
      timeout: 15000
    });

    if (exitCode === 2) {
      return {
        blocked: true,
        message: stderr || "Use PowerShell instead of bash",
        reason: "non-git bash command"
      };
    }
  } catch (error: any) {
    if (error.code === 2) {
      return {
        blocked: true,
        message: error.message || "Use PowerShell instead of bash",
        reason: "non-git bash command"
      };
    }
  }

  return NOT_BLOCKED;
}

async function runPowerShellDotnetFormat(cwd: string, event: any): Promise<HookResult> {
  const command = event.input.command;
  if (!command) return NOT_BLOCKED;

  if (!command.match(/RunTests\.ps1|dotnet\s+test|git\s+commit/)) {
    return NOT_BLOCKED;
  }

  // The project path must be absolute. A relative one resolves against this process's directory,
  // which is not necessarily the tree the command writes to - and dotnet format WRITES, so a
  // wrong root silently reformats a tree nobody is working in.
  const root = await getCommandRoot(cwd, command);
  if (!root) return NOT_BLOCKED;
  const project = `${root}/CSVM/CSVM.csproj`;

  try {
    const { stderr, exitCode } = await pExecFile(
      "powershell",
      [
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-Command",
        `if (-not (Test-Path -LiteralPath '${project}')) { exit 0 }; dotnet format '${project}' --verbosity quiet; $buildOutput = dotnet build '${project}' --verbosity quiet --nologo -t:Rebuild 2>&1; $remaining = $buildOutput | Select-String -Pattern 'SA\\d{4}' | Where-Object { $_.Line -notmatch 'SA0001' }; if ($remaining) { $remaining | ForEach-Object { [Console]::Error.WriteLine($_.Line) }; [Console]::Error.WriteLine('StyleCop warnings dotnet format could not auto-fix remain above (SA0001 excluded) - fix them by hand, then retry.'); exit 2 }; exit 0`
      ],
      { cwd, timeout: 300000 }
    );

    if (exitCode === 2) {
      return {
        blocked: true,
        message: stderr || "dotnet format/style check failed",
        reason: "StyleCop warnings remain"
      };
    }
  } catch (error: any) {
    if (error.code === 2) {
      return {
        blocked: true,
        message: error.stderr || error.message || "dotnet format/style check failed",
        reason: "StyleCop warnings remain"
      };
    }
  }

  return NOT_BLOCKED;
}

async function runContentGate(cwd: string, event: any): Promise<HookResult> {
  const command = event.input.command;
  if (!command || !command.match(/git\s+commit/)) return NOT_BLOCKED;

  // Only to LOCATE the gate; the gate works out which trees to check for itself.
  const here = await getGitRoot(cwd);
  if (!here) return NOT_BLOCKED;
  const gate = `${here}/CheckCommitContent.ps1`;

  try {
    // execFile, not exec: the command is passed as an argument vector, so nothing in it needs
    // quoting and a commit message cannot break out into the shell.
    const { stderr, exitCode } = await pExecFile(
      "powershell",
      ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", gate, "-Command", command],
      { cwd, timeout: 300000 }
    );

    if (exitCode === 2) {
      return {
        blocked: true,
        message: stderr || "Commit content checks failed",
        reason: "commit content checks failed"
      };
    }
  } catch (error: any) {
    if (error.code === 2) {
      return {
        blocked: true,
        message: error.stderr || error.message || "Commit content checks failed",
        reason: "commit content checks failed"
      };
    }
  }

  return NOT_BLOCKED;
}

// The tree the command names with -C, else the tree this process sits in.
async function getCommandRoot(cwd: string, command: string): Promise<string | null> {
  const m = command.match(/(?:^|\s)-C\s+("[^"]*"|\S+)/);
  if (m) {
    const named = m[1].replace(/^"|"$/g, "");
    try {
      const { stdout } = await pExec("git rev-parse --show-toplevel", { cwd: named });
      return stdout.trim();
    } catch {
      // fall through to this process's tree
    }
  }
  return getGitRoot(cwd);
}

async function getGitRoot(cwd: string): Promise<string | null> {
  try {
    const { stdout } = await pExec("git rev-parse --show-toplevel", { cwd, shell: "/bin/bash" });
    return stdout.trim();
  } catch {
    return null;
  }
}

function escapePowerShellCommand(command: string): string {
  // Escape single quotes and other special characters for PowerShell
  return command.replace(/'/g, "''");
}
