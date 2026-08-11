import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { isToolCallEventType } from "@earendil-works/pi-coding-agent";
import { exec } from "child_process";
import { promisify } from "util";

const pExec = promisify(exec);

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

  // 4. Duplicate item-ID check hook (PreToolUse for Bash|PowerShell with git commit)
  pi.on("tool_call", async (event, ctx) => {
    if (isToolCallEventType("bash", event) || isToolCallEventType("powershell", event)) {
      const result = await runPowerShellDuplicateIdCheck(ctx.cwd, event);
      if (result.blocked) {
        ctx.ui.notify(result.message, "error");
        return { block: true, reason: result.reason };
      }
    }
  });

  // 5. Encoding tripwire hook (PreToolUse for Bash|PowerShell with git commit)
  pi.on("tool_call", async (event, ctx) => {
    if (isToolCallEventType("bash", event) || isToolCallEventType("powershell", event)) {
      const result = await runPowerShellEncodingCheck(ctx.cwd, event);
      if (result.blocked) {
        ctx.ui.notify(result.message, "error");
        return { block: true, reason: result.reason };
      }
    }
  });

  // 6. Golden exercises prose check hook (PreToolUse for Bash|PowerShell with git commit)
  pi.on("tool_call", async (event, ctx) => {
    if (isToolCallEventType("bash", event) || isToolCallEventType("powershell", event)) {
      const result = await runPowerShellGoldenExercisesCheck(ctx.cwd, event);
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

async function runPowerShellHook(cwd: string, event: any): Promise<HookResult> {
  const command = event.input.command;
  if (!command) return { blocked: false, message: "", reason: "" };

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

  return { blocked: false, message: "", reason: "" };
}

async function runPowerShellBashGuard(cwd: string, event: any): Promise<HookResult> {
  const command = event.input.command;
  if (!command) return { blocked: false, message: "", reason: "" };

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

  return { blocked: false, message: "", reason: "" };
}

async function runPowerShellDotnetFormat(cwd: string, event: any): Promise<HookResult> {
  const command = event.input.command;
  if (!command) return { blocked: false, message: "", reason: "" };

  if (!command.match(/RunTests\.ps1|dotnet\s+test|git\s+commit/)) {
    return { blocked: false, message: "", reason: "" };
  }

  const inputJson = JSON.stringify({
    tool_name: event.toolName,
    tool_input: { command: command }
  });

  const psScript = `\$j = [Console]::In.ReadToEnd() | ConvertFrom-Json; if (\$j.tool_input.command -match 'RunTests\\.ps1|dotnet\\s+test|git\\s+commit') { dotnet format 'CSVM/CSVM.csproj' --verbosity quiet; \$buildOutput = dotnet build 'CSVM/CSVM.csproj' --verbosity quiet --nologo -t:Rebuild 2>&1; \$remaining = \$buildOutput | Select-String -Pattern 'SA\\d{4}' | Where-Object { \$_.Line -notmatch 'SA0001' }; if (\$remaining) { \$remaining | ForEach-Object { [Console]::Error.WriteLine(\$_.Line) }; [Console]::Error.WriteLine('StyleCop warnings dotnet format could not auto-fix remain above (SA0001 excluded) - fix them by hand, then retry.'); exit 2 } }; exit 0`;

  try {
    const { stderr, exitCode } = await pExec(`powershell -Command ${escapePowerShellCommand(psScript)}`, {
      cwd,
      input: inputJson,
      timeout: 300000
    });

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
        message: error.message || "dotnet format/style check failed",
        reason: "StyleCop warnings remain"
      };
    }
  }

  return { blocked: false, message: "", reason: "" };
}

async function runPowerShellDuplicateIdCheck(cwd: string, event: any): Promise<HookResult> {
  const command = event.input.command;
  if (!command || !command.match(/git\s+commit/)) {
    return { blocked: false, message: "", reason: "" };
  }

  const root = await getGitRoot(cwd);
  if (!root) return { blocked: false, message: "", reason: "" };

  const inputJson = JSON.stringify({
    tool_name: event.toolName,
    tool_input: { command: command }
  });

  const psScript = `\$j = [Console]::In.ReadToEnd() | ConvertFrom-Json; if (\$j.tool_input.command -notmatch 'git\\s+commit') { exit 0 }; \$root = git rev-parse --show-toplevel 2>\$null; if (-not \$root) { exit 0 }; \$defs = @(); \$b = Join-Path \$root 'backlog.md'; if (Test-Path \$b) { \$defs += @(Select-String -Path \$b -Pattern '^\\s*-\\s+\`(BL-\\d+)\`' -AllMatches | ForEach-Object { \$_.Matches } | ForEach-Object { \$_.Groups[1].Value }) }; \$p = Join-Path \$root 'playtest.md'; if (Test-Path \$p) { \$defs += @(Select-String -Path \$p -Pattern '^\\s*(?:-\\s+|\\|\\s*)\`((?:PT|CAP)-\\d+)\`' -AllMatches | ForEach-Object { \$_.Matches } | ForEach-Object { \$_.Groups[1].Value }) }; \$dupes = \$defs | Group-Object | Where-Object { \$_.Count -gt 1 }; if (\$dupes) { \$dupes | ForEach-Object { [Console]::Error.WriteLine('Duplicate item ID defined more than once: ' + \$_.Name) }; [Console]::Error.WriteLine('backlog.md/playtest.md define the same ID twice - mint a fresh ID with ./New-ItemId.ps1 and renumber the later mint before committing.'); exit 2 }; exit 0`;

  try {
    const { stderr, exitCode } = await pExec(`powershell -Command ${escapePowerShellCommand(psScript)}`, {
      cwd,
      input: inputJson,
      timeout: 30000
    });

    if (exitCode === 2) {
      return {
        blocked: true,
        message: stderr || "Duplicate item ID check failed",
        reason: "duplicate item IDs"
      };
    }
  } catch (error: any) {
    if (error.code === 2) {
      return {
        blocked: true,
        message: error.message || "Duplicate item ID check failed",
        reason: "duplicate item IDs"
      };
    }
  }

  return { blocked: false, message: "", reason: "" };
}

async function runPowerShellEncodingCheck(cwd: string, event: any): Promise<HookResult> {
  const command = event.input.command;
  if (!command || !command.match(/git\s+commit/)) {
    return { blocked: false, message: "", reason: "" };
  }

  const root = await getGitRoot(cwd);
  if (!root) return { blocked: false, message: "", reason: "" };

  const inputJson = JSON.stringify({
    tool_name: event.toolName,
    tool_input: { command: command }
  });

  const psScript = `\$j = [Console]::In.ReadToEnd() | ConvertFrom-Json; if (\$j.tool_input.command -notmatch 'git\\s+commit') { exit 0 }; \$root = git rev-parse --show-toplevel 2>\$null; if (-not \$root) { exit 0 }; \$changed = @(git diff --name-only HEAD --diff-filter=ACM) + @(git ls-files --others --exclude-standard); \$hi = 0x0152,0x0153,0x0160,0x0161,0x0178,0x017D,0x017E,0x0192,0x02C6,0x02DC,0x2013,0x2014,0x2018,0x2019,0x201A,0x201C,0x201D,0x201E,0x2020,0x2021,0x2022,0x2026,0x2030,0x2039,0x203A,0x20AC,0x2122; \$follow = ('{0}-{1}' -f [char]0x80, [char]0xBF) + ((\$hi | ForEach-Object { [string][char]\$_ }) -join ''); \$rx = [regex]('[' + [char]0xC2 + [char]0xC3 + [char]0xE2 + '][' + \$follow + ']'); \$bad = @(); foreach (\$f in (\$changed | Sort-Object -Unique)) { if (\$f -notmatch '\\.(cs|md|json|ps1|gdshader|gdshaderinc|txt|csproj|sln|tscn|tres|cfg|godot|yml|yaml)\$') { continue }; \$p = Join-Path \$root \$f; if (-not (Test-Path -LiteralPath \$p -PathType Leaf)) { continue }; try { \$t = [IO.File]::ReadAllText(\$p) } catch { continue }; \$m = \$rx.Match(\$t); if (\$m.Success) { \$line = (\$t.Substring(0, \$m.Index) -split [string][char]10).Count; \$bad += (\$f + ':' + \$line) } }; if (\$bad) { \$bad | ForEach-Object { [Console]::Error.WriteLine('Mojibake (UTF-8 read as ANSI and re-saved) about to be committed: ' + \$_) }; [Console]::Error.WriteLine('Cause: a PowerShell file write without UTF-8 - a Get-Content/Set-Content round-trip without -Encoding utf8, or a BOM-less file read as ANSI. Repair the corrupted characters before committing; edit repo text with the Read/Edit/Write tools, and pass -Encoding utf8 on both read and write whenever PowerShell must touch a repo text file.'); exit 2 }; exit 0`;

  try {
    const { stderr, exitCode } = await pExec(`powershell -Command ${escapePowerShellCommand(psScript)}`, {
      cwd,
      input: inputJson,
      timeout: 60000
    });

    if (exitCode === 2) {
      return {
        blocked: true,
        message: stderr || "Encoding check failed",
        reason: "mojibake detected"
      };
    }
  } catch (error: any) {
    if (error.code === 2) {
      return {
        blocked: true,
        message: error.message || "Encoding check failed",
        reason: "mojibake detected"
      };
    }
  }

  return { blocked: false, message: "", reason: "" };
}

async function runPowerShellGoldenExercisesCheck(cwd: string, event: any): Promise<HookResult> {
  const command = event.input.command;
  if (!command || !command.match(/git\s+commit/)) {
    return { blocked: false, message: "", reason: "" };
  }

  const root = await getGitRoot(cwd);
  if (!root) return { blocked: false, message: "", reason: "" };

  const manifestPath = `${root}/analysis/goldens/manifest.json`;
  
  // Check if manifest exists
  const { exec } = await import("child_process");
  const { promisify } = await import("util");
  const pExecCheck = promisify(exec);
  
  try {
    await pExecCheck(`test -f "${manifestPath}"`, { cwd: root, shell: "/bin/bash" });
  } catch {
    // Manifest doesn't exist, skip check
    return { blocked: false, message: "", reason: "" };
  }

  const inputJson = JSON.stringify({
    tool_name: event.toolName,
    tool_input: { command: command }
  });

  const psScript = `\$j = [Console]::In.ReadToEnd() | ConvertFrom-Json; if (\$j.tool_input.command -notmatch 'git\\s+commit') { exit 0 }; \$root = git rev-parse --show-toplevel 2>\$null; if (-not \$root) { exit 0 }; \$p = Join-Path \$root 'analysis/goldens/manifest.json'; if (-not (Test-Path -LiteralPath \$p -PathType Leaf)) { exit 0 }; try { \$m = [IO.File]::ReadAllText(\$p) | ConvertFrom-Json } catch { [Console]::Error.WriteLine('analysis/goldens/manifest.json does not parse as JSON - fix it before committing.'); exit 2 }; \$bad = @(); foreach (\$s in \$m.shots) { \$e = [string]\$s.exercises; if (\$e.Length -gt 250) { \$bad += (\$s.name + ': exercises is ' + \$e.Length + ' chars, cap is 250') }; if (\$e -match '(BL|PT|CAP)-\\d+|PLAN-|[Aa]lso exercises|\\d{4}-\\d{2}-\\d{2}') { \$bad += (\$s.name + ': exercises carries history or an item id -- ' + \$Matches[0]) } }; if (\$bad) { \$bad | ForEach-Object { [Console]::Error.WriteLine('BLOCKED: ' + \$_) }; [Console]::Error.WriteLine('analysis/goldens/manifest.json: exercises describes what a shot covers TODAY - one sentence, under 250 chars, no item ids, no dates, no past-change deltas. REWRITE the field on a re-pin, never append; the history belongs in the commit message and git log -p. See analysis/goldens/README.md.'); exit 2 }; exit 0`;

  try {
    const { stderr, exitCode } = await pExec(`powershell -Command ${escapePowerShellCommand(psScript)}`, {
      cwd,
      input: inputJson,
      timeout: 30000
    });

    if (exitCode === 2) {
      return {
        blocked: true,
        message: stderr || "Golden exercises check failed",
        reason: "golden exercises validation failed"
      };
    }
  } catch (error: any) {
    if (error.code === 2) {
      return {
        blocked: true,
        message: error.message || "Golden exercises check failed",
        reason: "golden exercises validation failed"
      };
    }
  }

  return { blocked: false, message: "", reason: "" };
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