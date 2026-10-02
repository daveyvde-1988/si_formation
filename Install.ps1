param(
    [ValidateRange(0,10000)][int]$FormationTravelDistance,
    [ValidateScript({$_ -eq 0 -or ($_ -ge 0.1 -and $_ -le 5)})][double]$StartDelaySoftLock,
    [ValidateScript({$_ -eq 0 -or ($_ -ge 0.1 -and $_ -le 5)})][double]$StartDelayLock,
    [ValidateScript({$_ -eq 0 -or ($_ -ge 0.1 -and $_ -le 5)})][double]$StartDelaySpeedLock
)
$ErrorActionPreference = 'Stop'
$serverRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (Get-Process -Name Silica -ErrorAction SilentlyContinue) { throw 'Stop the server before installing; no files changed.' }
foreach ($legacy in @('Si_FollowFormation.dll','Si_Formations.dll')) {
    if (Test-Path -LiteralPath (Join-Path $serverRoot ('Mods\'+$legacy))) { throw "Remove the conflicting legacy mod $legacy before installation." }
}
$source = Join-Path $PSScriptRoot 'bin\Release\netstandard2.1\Si_Formation.dll'
$destination = Join-Path $serverRoot 'Mods\Si_Formation.dll'
$shared = Join-Path $serverRoot 'UserData\MelonPreferences.cfg'
$config = Join-Path $serverRoot 'UserData\Formations_cfg\si_formation.cfg'
if (!(Test-Path -LiteralPath $source)) { throw 'Build Si_Formation first.' }
$utf8 = New-Object System.Text.UTF8Encoding($false, $true)
$originalBytes = [IO.File]::ReadAllBytes($shared)
$originalText = $utf8.GetString($originalBytes)
$pattern = '(?ms)^\[Si_Formation\][^\r\n]*\r?\n.*?(?=^\[|\z)'
$section = [regex]::Match($originalText,$pattern)
$hadConfig = Test-Path -LiteralPath $config
if ($hadConfig) {
    $configBytes = [IO.File]::ReadAllBytes($config)
    $configText = $utf8.GetString($configBytes)
} elseif ($section.Success) { $configText = $section.Value }
else { $configText = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Si_Formation.preferences.example.cfg')) }
$configSection = [regex]::Match($configText,$pattern)
if (!$configSection.Success) { throw 'Standalone preferences must contain [Si_Formation].' }
$replacement = $configSection.Value.TrimEnd("`r","`n") + "`r`n"
foreach ($entry in @(@('StartDelaySoftLock','0.2'),@('StartDelayLock','0'),@('StartDelaySpeedLock','0.3'))) {
    if ($replacement -notmatch ('(?m)^'+$entry[0]+'\s*=')) {
        $replacement += '# Seconds between departure rows: 0 disables, otherwise 0.1..5.' + "`r`n" + $entry[0] + ' = ' + $entry[1] + "`r`n"
    }
}
if ($PSBoundParameters.ContainsKey('FormationTravelDistance')) {
    $replacement = [regex]::Replace($replacement,'(?m)^FormationTravelDistance\s*=[^\r\n]*',('FormationTravelDistance = '+$FormationTravelDistance))
}
foreach ($key in @('StartDelaySoftLock','StartDelayLock','StartDelaySpeedLock')) {
    if ($PSBoundParameters.ContainsKey($key)) {
        $value=([double]$PSBoundParameters[$key]).ToString('0.###',[Globalization.CultureInfo]::InvariantCulture)
        $replacement=[regex]::Replace($replacement,('(?m)^'+$key+'\s*=[^\r\n]*'),($key+' = '+$value))
    }
}
$configText = $configText.Substring(0,$configSection.Index)+$replacement+$configText.Substring($configSection.Index+$configSection.Length)
$updatedShared = [regex]::Replace($originalText,$pattern,'')
# Removing our exact section is the only allowed change to the shared file.
if ($section.Success -and $updatedShared -cne ($originalText.Substring(0,$section.Index)+$originalText.Substring($section.Index+$section.Length))) { throw 'Unrelated preferences would change.' }
foreach ($target in @($destination,$shared,$config)) {
    if (Test-Path -LiteralPath $target) {
        $handle=[IO.File]::Open($target,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None);$handle.Dispose()
    }
}
$backup=Join-Path $serverRoot ('Archive\Si_Formation-deploy-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $backup | Out-Null
foreach ($target in @($destination,$shared,$config)) {
    if (Test-Path -LiteralPath $target) {
        $copy=Join-Path $backup ([IO.Path]::GetFileName($target))
        Copy-Item -LiteralPath $target -Destination $copy
        if ((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $copy).Hash) { throw 'Backup hash mismatch.' }
    }
}
if (Get-Process -Name Silica -ErrorAction SilentlyContinue) { throw "Server started during preparation. No deployment performed. Backup: $backup" }
try {
    [IO.File]::WriteAllBytes($config,$utf8.GetBytes($configText))
    if ($updatedShared -cne $originalText) { [IO.File]::WriteAllBytes($shared,$utf8.GetBytes($updatedShared)) }
    Copy-Item -LiteralPath $source -Destination $destination -Force
    if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) { throw 'Deployed DLL hash mismatch.' }
    if ([IO.File]::ReadAllText($shared) -cne $updatedShared) { throw 'Shared configuration verification failed.' }
    if ([IO.File]::ReadAllText($config) -cne $configText) { throw 'Standalone configuration verification failed.' }
} catch {
    [IO.File]::WriteAllBytes($shared,$originalBytes)
    if ($hadConfig) { [IO.File]::WriteAllBytes($config,$configBytes) }
    if (Test-Path -LiteralPath (Join-Path $backup 'Si_Formation.dll')) { Copy-Item -LiteralPath (Join-Path $backup 'Si_Formation.dll') -Destination $destination -Force }
    throw
}
Write-Output "Installed: $destination"
Write-Output "Configuration: $config"
Write-Output "Backup: $backup"
Write-Output 'Verified DLL hash and preserved unrelated preference sections. Server not started.'
