# One scripted Dogfight over a real LAN link: a Linux machine (the Steam Deck) hosts headless on
# its own LAN address, this PC joins on the hidden desktop, both with --debug-net-trace. Deploy the
# tree's Linux build to $DeckDir first (ExportRelease.ps1 -Linux), with deck-host.sh beside it.
# The PC never hosts: a socket it binds is what raises a firewall prompt. -Shape is the PC's
# --net-shape value (soak50, soak100, soak200 or latency,jitter,loss); the Deck stays unshaped.
param(
    [string]$Name = "sortie",
    [int]$Seconds = 400,
    [string]$Deck = "192.168.178.50",
    [string]$SshTarget = "deck@steamdeck",
    [string]$DeckDir = "~/CSVM-tmp",
    [int]$Port = 48720,
    [string]$Shape = "",
    [string[]]$Extra = @()
)
$ErrorActionPreference = "Stop"
if (-not $env:CSVM_DATA_ROOT) { $env:CSVM_DATA_ROOT = "Z:\CSVM" }
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$common = @("--vs", "--vs-time=10", "--vs-kills=0", "--no-det", "--debug-net-trace", "--mute", "--net-port-base=$Port") + $Extra
# A bank, then a steady pull: a wide turn that crashes about once a minute, where the soak's
# climbing roll crashes about four times.
$hostArgs = $common + @("--net-host=${Deck}:$Port", "--hold=0,0.6,0,1@0.5;0.5,0,0,1")
$guestArgs = $common + @("--net-join=${Deck}:$Port", "--hold=0,-0.6,0,1@0.5;0.5,0,0,1")
if ($Shape) { $guestArgs += "--net-shape=$Shape" }
$hostSecs = $Seconds + 40
$remote = "$DeckDir/deck-host.sh $Name $hostSecs -- " + (($hostArgs | ForEach-Object { "'" + $_ + "'" }) -join " ")
$job = Start-Job -ScriptBlock { param($t, $r) ssh $t $r } -ArgumentList $SshTarget, $remote
# The host waits up to 30 s for its first guest; the guest needs the host's socket open first.
Start-Sleep -Seconds 4
& (Join-Path $repo "RunProbe.ps1") -TimeoutSec $Seconds @guestArgs
Wait-Job $job -Timeout ($hostSecs + 30) | Out-Null
Receive-Job $job
Remove-Job $job -Force
