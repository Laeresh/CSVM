<#
.SYNOPSIS
    Sends commands to a CSVM run launched with --control=<port> and prints one reply line each.

.DESCRIPTION
    Start the game in the background through RunProbe.ps1 with no timeout, then drive it:

        .\scripts\RunProbe.ps1 -TimeoutSec 0 --control=7777 --chapter=C1 --mission=M01 --fly
        .\scripts\Control.ps1 'state'
        .\scripts\Control.ps1 'skip' 'step 120' 'camera 0,500,0 100,0,100' 'shot .scratch\a.png'

    The first command waits up to -WaitSec for the port, so it can follow the launch directly.
    The run starts held once its session exists; the sim only advances on step or run. It quits
    on 'quit' or after 10 idle minutes. Every command is logged as a `[core] control:` line.

    Verbs:
      state                        session, held, frame, time, cutscene, camera pose, plane
      pause | run                  hold the sim, or let it run at the --det rate
      step [N]                     advance N sim frames (default 1); replies once they landed
      skip                         skip the playing cutscene ('declined' when it plays in full)
      ff on|off                    hold or release the cutscene fast-forward
      camera x,y,z x,y,z [fov]     a free camera at the first point looking at the second
      camera off                   give the view back to the game's camera
      shot <path>                  save the next rendered frame as a PNG
      quit                         end the run

.PARAMETER Port
    The --control= port. Default 7777.

.PARAMETER WaitSec
    How long the first connection waits for the run to open its port. Default 300, since a
    chapter world builds for a while before the listener exists.
#>
[CmdletBinding(PositionalBinding = $false)]
param(
    [int]$Port = 7777,
    [int]$WaitSec = 300,
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$Commands = @('state')
)

$ErrorActionPreference = 'Stop'

$deadline = (Get-Date).AddSeconds($WaitSec)
$client = $null
while (-not $client) {
    try {
        $client = New-Object System.Net.Sockets.TcpClient('127.0.0.1', $Port)
    } catch {
        if ((Get-Date) -gt $deadline) { throw "no CSVM run is listening on 127.0.0.1:$Port" }
        Start-Sleep -Milliseconds 500
    }
}

try {
    $stream = $client.GetStream()
    $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::ASCII)
    $writer = New-Object System.IO.StreamWriter($stream, [System.Text.Encoding]::ASCII)
    $writer.NewLine = "`n"
    $writer.AutoFlush = $true
    foreach ($cmd in $Commands) {
        $writer.WriteLine($cmd)
        $reply = $reader.ReadLine()
        if ($null -eq $reply) { throw "the run closed the connection during '$cmd'" }
        Write-Output ("{0} -> {1}" -f $cmd, $reply)
    }
} finally {
    $client.Close()
}
