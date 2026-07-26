Write-Host "=============================================="
Write-Host "Compiling Steam Epic Sync (C# / WPF / WinForms)"
Write-Host "=============================================="
Write-Host ""

$netVersion = "v4.0.30319"
$cscPath64 = "C:\Windows\Microsoft.NET\Framework64\$netVersion\csc.exe"
$cscPath32 = "C:\Windows\Microsoft.NET\Framework\$netVersion\csc.exe"

$csc = ""
$wpfLib = ""

if (Test-Path $cscPath64) {
    $csc = $cscPath64
    $wpfLib = "C:\Windows\Microsoft.NET\Framework64\$netVersion\WPF"
} elseif (Test-Path $cscPath32) {
    $csc = $cscPath32
    $wpfLib = "C:\Windows\Microsoft.NET\Framework\$netVersion\WPF"
} else {
    Write-Error "Error: C# Compiler (csc.exe) not found."
    exit 1
}

Write-Host "Using compiler: $csc"
Write-Host "WPF Lib: $wpfLib"
Write-Host "Compiling..."

& $csc /target:winexe /out:SteamEpicSync.exe /lib:$wpfLib /r:PresentationCore.dll /r:PresentationFramework.dll /r:WindowsBase.dll /r:System.Xaml.dll /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Security.dll /resource:MainWindow.xaml Program.cs VdfParser.cs EpicSyncManager.cs GameLauncher.cs TrayApplication.cs MainWindow.xaml.cs EpicApiManager.cs LoginWindow.cs

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Error "[ERROR] Compilation FAILED!"
    exit 1
}

Write-Host ""
Write-Host "[SUCCESS] Compilation successful!"
Write-Host "Output: SteamEpicSync.exe"
Write-Host ""
