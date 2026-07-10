Write-Host "=============================================="
Write-Host "Compiling Steam Epic Sync (C# / WPF / WinForms)"
Write-Host "=============================================="
Write-Host ""

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    Write-Error "Error: C# Compiler (csc.exe) not found at: $csc"
    exit 1
}

Write-Host "Compiling..."
& $csc /target:winexe /out:SteamEpicSync.exe /lib:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF /r:PresentationCore.dll /r:PresentationFramework.dll /r:WindowsBase.dll /r:System.Xaml.dll /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll Program.cs VdfParser.cs EpicSyncManager.cs GameLauncher.cs TrayApplication.cs MainWindow.xaml.cs EpicApiManager.cs LoginWindow.cs

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Error "[ERROR] Compilation FAILED!"
    exit 1
}

Write-Host ""
Write-Host "[SUCCESS] Compilation successful!"
Write-Host "Output: SteamEpicSync.exe"
Write-Host ""
