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

  // 4. Content checks before a commit: encoding, item IDs, golden prose, comment caps, doc
  //    entries. All of them live in CheckCommitContent.ps1 at the repo root rather than being
  //    reimplemented here. Three hand-maintained copies drifted - this file never received the
  //    comment-cap check at all - so the checks are now written once and called by every harness.
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

  // The tool name and command are baked into the script as literals rather than piped in as JSON.
  // promisify(exec) has no `input` option - passing one is silently ignored, and the child then
  // blocks on [Console]::In.ReadToEnd() until the timeout kills it, which made this hook a slow
  // no-op rather than a guard.
  const toolName = isToolCallEventType("bash", event) ? "Bash" : "PowerShell";
  const psScript = `\$c = ${psLiteral(command)}; \$toolName = ${psLiteral(toolName)}; if (-not \$c) { exit 0 }; \$q = [char]39; if (\$toolName -eq 'Bash' -and (\$c -match ('@' + \$q + '\\r?\\n')) -and (\$c -match ('(^|\\n)' + \$q + '@'))) { [Console]::Error.WriteLine('BLOCKED: PowerShell here-string (@' + \$q + ' ... ' + \$q + '@) in the Bash tool, which takes a heredoc. Bash treats the @ lines as literal text instead of failing, so the content lands corrupted (this has silently mangled commit messages). Use a heredoc: cmd <<' + \$q + 'EOF' + \$q + ' ... EOF. For a multi-line commit message prefer Write to a file, then: git commit -F <file>.'); exit 2 }; if (\$toolName -eq 'PowerShell' -and ((\$c -match ('<<-?\\s*' + \$q + '?[A-Za-z_]')) -or \$c.Contains('/dev/null'))) { [Console]::Error.WriteLine('BLOCKED: bash syntax (heredoc or /dev/null) in the PowerShell tool. Use a here-string @' + \$q + ' ... ' + \$q + '@ with the closing delimiter at column 0, and \$null instead of /dev/null.'); exit 2 }; exit 0`;

  try {
    await pExecFile("powershell", ["-NoProfile", "-Command", psScript], { cwd, timeout: 15000 });
  } catch (error: any) {
    if (error.code === 2) {
      return {
        blocked: true,
        message: error.stderr || error.message || "Shell syntax check failed",
        reason: "shell syntax error"
      };
    }
  }

  return NOT_BLOCKED;
}

async function runPowerShellBashGuard(cwd: string, event: any): Promise<HookResult> {
  const command = event.input.command;
  if (!command) return NOT_BLOCKED;

  const psScript = `\$c = ${psLiteral(command)}; if (-not \$c) { exit 0 }; \$segments = \$c -split '&&|\\|\\||;|\\|' | ForEach-Object { \$_.Trim() } | Where-Object { \$_ }; \$other = \$segments | Where-Object { \$_ -notmatch '^(git|gh)\\b' }; if (-not \$other) { exit 0 }; [Console]::Error.WriteLine('Use Powershell instead of bash'); exit 2`;

  try {
    await pExecFile("powershell", ["-NoProfile", "-Command", psScript], { cwd, timeout: 15000 });
  } catch (error: any) {
    if (error.code === 2) {
      return {
        blocked: true,
        message: error.stderr || error.message || "Use PowerShell instead of bash",
        reason: "non-git bash command"
      };
    }
  }

  return NOT_BLOCKED;
}

async function runPowerShellDotnetFormat(cwd: string, event: any): Promise<HookResult> {
  const command = event.input.command;
  if (!command) return NOT_BLOCKED;

  // Only to LOCATE the gate. Whether the command invokes the runner, dotnet test or git commit
  // (rather than merely naming one), and which tree to format, are the gate's own decisions, so
  // every harness fires on the same commands and formats the same tree.
  const here = await getGitRoot(cwd);
  if (!here) return NOT_BLOCKED;
  const gate = `${here}/FormatBeforeTests.ps1`;

  try {
    // A nonzero exit REJECTS with error.code; the resolved value carries only stdout/stderr, so
    // there is no exit code to test on the success path.
    await pExecFile(
      "powershell",
      ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", gate, "-Command", command],
      { cwd, timeout: 300000 }
    );
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
  if (!command) return NOT_BLOCKED;

  // No trigger of its own. Whether a command is a commit is the gate's decision, made once in
  // CheckCommitContent.ps1 and covered by its self-test. The three harnesses each used to test for
  // /git\s+commit/ here, which required the two words to be adjacent and so skipped the gate
  // entirely for "git -C <tree> commit", the form CLAUDE.md prescribes for naming a tree.
  //
  // Only to LOCATE the gate; the gate works out which tree to check for itself.
  const here = await getGitRoot(cwd);
  if (!here) return NOT_BLOCKED;
  const gate = `${here}/CheckCommitContent.ps1`;

  try {
    // execFile, not exec: the command is passed as an argument vector, so nothing in it needs
    // quoting and a commit message cannot break out into the shell. The gate takes -Command
    // rather than reading stdin, because promisify(exec)/execFile cannot write to a child's stdin.
    await pExecFile(
      "powershell",
      ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", gate, "-Command", command],
      { cwd, timeout: 300000 }
    );
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

async function getGitRoot(cwd: string): Promise<string | null> {
  try {
    // No shell override. This runs on Windows, where spawning "/bin/bash" fails ENOENT and made
    // this helper return null every time - which silently disabled every hook built on it.
    const { stdout } = await pExec("git rev-parse --show-toplevel", { cwd });
    return stdout.trim();
  } catch {
    return null;
  }
}

// Embeds a value in a single-quoted PowerShell string literal.
function psLiteral(value: string): string {
  return "'" + value.replace(/'/g, "''") + "'";
}
