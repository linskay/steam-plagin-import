$ErrorActionPreference = "Stop"
Push-Location (Split-Path $PSScriptRoot)
try {
    $testDir = Join-Path $env:TEMP ("SteamEpicSync-tests-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory $testDir | Out-Null
    $compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    if (!(Test-Path $compiler)) { $compiler = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
    $testExe = Join-Path $testDir "Regression.exe"
    & $compiler /nologo /target:exe /out:$testExe /r:System.Web.Extensions.dll /r:System.Security.dll /r:System.Drawing.dll tests\Regression.cs EpicApiManager.cs EpicSyncManager.cs VdfParser.cs SteamArtwork.cs SteamCollection.cs SteamUiSnapshot.cs SteamStoreMatcher.cs AppSettings.cs SteamSession.cs
    if ($LASTEXITCODE -ne 0) { throw "Test compilation failed" }
    & $testExe $testDir
    if ($LASTEXITCODE -ne 0) { throw "Regression checks failed" }
    Add-Type -AssemblyName PresentationFramework
    $parseErrors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile((Join-Path (Get-Location) "millennium-plugin/install.ps1"), [ref]$null, [ref]$parseErrors)
    if ($parseErrors) { throw "Module installer PowerShell parsing failed" }
    Write-Host "PASS module installer parses on Windows PowerShell"
    $reader = [System.Xml.XmlReader]::Create((Join-Path (Get-Location) "MainWindow.xaml"))
    try { $window = [System.Windows.Markup.XamlReader]::Load($reader); $window.Close(); Write-Host "PASS WPF XAML loads" } finally { $reader.Dispose() }
    Write-Host "Test artifacts: $testDir"
} finally { Pop-Location }
