param(
    [string] $ProjectRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent)
)

# Deterministic estimate, not a measured playtest. Run from PowerShell without Unity.
$ErrorActionPreference = 'Stop'
$rules = Join-Path $ProjectRoot 'Assets/_Game/Scripts/Progression/GameBalance.cs'
if (-not ('GameBalance' -as [type])) { Add-Type -Path $rules }

function Get-CatalogCost($folder) {
    $assets = Get-ChildItem (Join-Path $ProjectRoot $folder) -Filter '*.asset' -Recurse -File
    $sum = 0
    foreach ($asset in $assets) {
        $price = Select-String -LiteralPath $asset.FullName -Pattern '^  price: (\d+)$'
        if (-not $price) { throw "Missing price: $($asset.Name)" }
        $sum += [int]$price.Matches[0].Groups[1].Value
    }
    return $sum
}

$weaponCost = Get-CatalogCost 'Assets/Data/Weapons'
$skinCost = Get-CatalogCost 'Assets/Data/Clothing'
$abilityCost = Get-CatalogCost 'Assets/Data/Abilities'
$upgradeCost = 0
for ($i = 0; $i -lt 10; $i++) { $upgradeCost += [GameBalance]::UpgradeCost($i) * 10 }
$totalCost = $weaponCost + $skinCost + $abilityCost + $upgradeCost
$xpTarget = [GameBalance]::LevelXp(50)
if ($totalCost -ne 31980 -or $xpTarget -ne 34692) { throw 'Unexpected catalog or XP curve.' }
if ([GameBalance]::RunXp(0) -ne 0 -or [GameBalance]::RunXp(10) - [GameBalance]::RunXp(9) -ne 103) {
    throw 'Invalid clear reward.'
}

$intensity = @(0.97, 1.00, 0.98, 1.02, 0.96, 1.00, 0.99, 1.06)

function Get-ExpectedRun([int]$limit, [double]$pace, [double]$pickup, [double]$perfect) {
    $coins = 0.0; $seconds = 25.0; $kills = 0; $bosses = 0
    for ($wave = 1; $wave -le $limit; $wave++) {
        $boss = $wave % 10 -eq 0
        if ($boss) {
            $n = 8
        } elseif ($wave -eq 3) {
            $n = 16
        } else {
            $floor = 10 + [Math]::Min($wave - 1, 4) * 5 + [Math]::Min([Math]::Max($wave - 5, 0) * 1.5, 14)
            $tier = if ($wave -gt 3 -and $wave % 2 -eq 0) { 1.1 } else { 0.9 }
            $compressed = 76.5 * (1 - [Math]::Exp(-((5 + ($wave - 1) * 3) * 1.7) / 76.5))
            $n = [Math]::Min(94, [Math]::Round([Math]::Max($floor,
                $compressed * $intensity[($wave - 1) % 8] * $tier * (1 + 0.008 * ($wave - 1)))))
        }
        $coins += [GameBalance]::WaveCoins($wave, $false) + [GameBalance]::PerfectCoinBonus * $perfect
        $coins += $n * [GameBalance]::CoinDropChance * (1 + [GameBalance]::LargeCoinChance) * $pickup
        $coins += 1.6 # Expected optional event rewards; explicit modelling assumption.
        if ($boss) {
            $coins += [GameBalance]::BossLootCoins * $pickup
            $seconds += 50 * $pace
            $bosses++
            $kills++
        } else {
            $seconds += (16 + 0.47 * $n) * $pace
        }
        $kills += $n
    }
    [pscustomobject]@{ Coins = $coins; Xp = [GameBalance]::RunXp($limit); Seconds = $seconds
        Kills = $kills; Bosses = $bosses; Waves = $limit }
}

function Get-Completion($name, $pace, $pickup, $perfect) {
    $earned = 0.0; $xp = 0.0; $kills = 0.0; $bosses = 0.0; $waves = 0.0; $runs = 0.0
    $walletHours = 0.0; $xpHours = 0.0
    for ($h = 0.0; $h -lt 24; $h += 0.25) {
        $limit = if ($h -lt 1) { 10 } elseif ($h -lt 3) { 20 } elseif ($h -lt 6) { 30 } elseif ($h -lt 9) { 40 } else { 50 }
        $r = Get-ExpectedRun $limit $pace $pickup $perfect
        $n = 900 / $r.Seconds
        $earned += $r.Coins * $n; $xp += $r.Xp * $n
        $kills += $r.Kills * $n; $bosses += $r.Bosses * $n; $waves += $r.Waves * $n; $runs += $n
        $bonus = [Math]::Min(($h + 0.25) * 120, 1200)
        if ($kills -ge 10000) { $bonus += 1000 }
        if ($bosses -ge 50) { $bonus += 1500 }
        if ($waves -ge 750) { $bonus += 1500 }
        if ($runs -ge 35) { $bonus += 2000 }
        if ($walletHours -eq 0 -and $earned + $bonus -ge $totalCost) { $walletHours = $h + 0.25 }
        if ($xpHours -eq 0 -and $xp -ge $xpTarget) { $xpHours = $h + 0.25 }
        if ($walletHours -gt 0 -and $xpHours -gt 0) {
            return [pscustomobject]@{ Profile = $name; CatalogHours = $walletHours; Level50Hours = $xpHours
                CompleteHours = [Math]::Max($walletHours, $xpHours) }
        }
    }
    throw "$name did not complete in 24 modeled hours."
}

"Weapons: $weaponCost; skins: $skinCost; abilities: $abilityCost; upgrades: $upgradeCost; total: $totalCost; level 50 XP: $xpTarget"
$profiles = @(
    (Get-Completion 'Fast' 0.84 0.82 0.35)
    (Get-Completion 'Typical' 1.00 0.72 0.25)
    (Get-Completion 'Slow' 1.15 0.60 0.15)
)
$profiles | Format-Table -AutoSize
$typical = $profiles | Where-Object Profile -eq 'Typical'
if ($typical.CompleteHours -lt 9 -or $typical.CompleteHours -gt 12) {
    throw 'Typical profile is outside the 9–12 hour target.'
}
'ECONOMY_MODEL_OK (no ads; estimation assumptions from Docs/ECONOMY.md)'
