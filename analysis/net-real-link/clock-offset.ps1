# This PC's system clock minus the Deck's, read off the earliest-arriving of forty stamps the
# Deck prints (deck-clock.sh). The arrival delay is never negative, so the smallest
# (PC arrival - Deck stamp) is the offset plus the fastest one-way trip, a millisecond or two on a
# LAN. Pass it to analyze.py as --wall-offset in seconds. Take it before and after a run: two
# machines' clocks drift apart by a millisecond or more a minute.
param([string]$SshTarget = "deck@steamdeck", [string]$DeckDir = "~/CSVM-tmp")
$samples = ssh $SshTarget "bash $DeckDir/deck-clock.sh" | ForEach-Object {
    $pc = ([DateTime]::UtcNow.Ticks - 621355968000000000) / 1e7
    $pc - [double]::Parse($_, [Globalization.CultureInfo]::InvariantCulture)
}
$min = ($samples | Measure-Object -Minimum).Minimum
$med = ($samples | Sort-Object)[[int]($samples.Count / 2)]
"PC minus Deck (arrival - stamp): min {0:0.0} ms, median {1:0.0} ms over {2} stamps" -f ($min * 1000), ($med * 1000), $samples.Count
