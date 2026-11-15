param([switch]$InstallRuntime)
$ErrorActionPreference = 'Stop'
$steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam').SteamPath
if (!(Test-Path -LiteralPath $steam)) { throw 'Steam installation not found.' }
if (Get-Process steam -ErrorAction SilentlyContinue) { throw 'Close Steam completely using Steam > Exit, then run this installer again.' }
$module = Join-Path $PSScriptRoot 'dist\com.linskay.steamepicsync.star'
if (!(Test-Path -LiteralPath $module)) { throw 'Build the module first: npm ci; npm run build.' }
$runtime = Join-Path $steam 'millennium\lib\millennium.dll'
if (!(Test-Path -LiteralPath $runtime)) {
    if (!$InstallRuntime) { throw 'Millennium is required. Re-run with -InstallRuntime to install the pinned official v3.6.3 runtime.' }
    if (Test-Path -LiteralPath (Join-Path $steam 'wsock32.dll')) { throw 'Existing wsock32.dll detected. It will not be overwritten.' }
    $stage = Join-Path $env:TEMP ('SteamEpicSync-runtime-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory $stage | Out-Null
    $archive = Join-Path $stage 'runtime.zip'
    Invoke-WebRequest -UseBasicParsing 'https://github.com/SteamClientHomebrew/Millennium/releases/download/v3.6.3/millennium-v3.6.3-windows-x86_64.zip' -OutFile $archive
    $expected = 'D0363F4AA5BFA44AECF4643FE5AF8FC35055D126A940FB46EF9158A4C261463D'
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expected) { throw 'Millennium archive checksum mismatch.' }
    $expanded = Join-Path $stage 'expanded'
    Expand-Archive -LiteralPath $archive -DestinationPath $expanded
    Copy-Item -LiteralPath (Join-Path $expanded 'millennium') -Destination (Join-Path $steam 'millennium') -Recurse
    Copy-Item -LiteralPath (Join-Path $expanded 'wsock32.dll') -Destination (Join-Path $steam 'wsock32.dll')
}
$plugins = Join-Path $steam 'millennium\plugins'
$configDir = Join-Path $steam 'millennium\config'
New-Item -ItemType Directory -Force $plugins,$configDir | Out-Null
Copy-Item -LiteralPath $module -Destination (Join-Path $plugins 'com.linskay.steamepicsync.star') -Force
$configPath = Join-Path $configDir 'config.json'
$config = if (Test-Path -LiteralPath $configPath) { Get-Content -Raw -Encoding UTF8 -LiteralPath $configPath | ConvertFrom-Json } else { [PSCustomObject]@{} }
if (!$config.PSObject.Properties['plugins']) { $config | Add-Member -NotePropertyName plugins -NotePropertyValue ([PSCustomObject]@{}) }
$enabled = if ($config.plugins.PSObject.Properties['enabledPlugins']) { @($config.plugins.enabledPlugins) } else { @() }
if ($enabled -notcontains 'com.linskay.steamepicsync') { $enabled += 'com.linskay.steamepicsync' }
$config.plugins | Add-Member -NotePropertyName enabledPlugins -NotePropertyValue @($enabled) -Force
if (Test-Path -LiteralPath $configPath) { Copy-Item -LiteralPath $configPath -Destination ($configPath + '.SteamEpicSync.bak') -Force }
[IO.File]::WriteAllText($configPath, ($config | ConvertTo-Json -Depth 30), (New-Object Text.UTF8Encoding($false)))
$tool = Join-Path (Split-Path $PSScriptRoot) 'SteamEpicSync.exe'
if (Test-Path -LiteralPath $tool) {
    $settingsDir = Join-Path $steam 'config\SteamEpicSync'
    New-Item -ItemType Directory -Force $settingsDir | Out-Null
    $settingsPath = Join-Path $settingsDir 'settings.json'
    $settings = if (Test-Path -LiteralPath $settingsPath) { Get-Content -Raw -Encoding UTF8 -LiteralPath $settingsPath | ConvertFrom-Json } else { [PSCustomObject]@{LaunchWithSteam=$true; SyncOnSteamStart=$false; CollectionName='Есть в EGS'} }
    $settings | Add-Member -NotePropertyName ExePath -NotePropertyValue $tool -Force
    [IO.File]::WriteAllText($settingsPath, ($settings | ConvertTo-Json -Depth 10), (New-Object Text.UTF8Encoding($false)))
}
Write-Host 'Steam Epic Sync module installed and enabled. Synchronize your library, then start Steam.'
