@echo off
title Compiling Steam Epic Sync
echo ==============================================
echo Compiling Steam Epic Sync (C# / WPF / WinForms)
echo ==============================================
echo.

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo Error: C# Compiler (csc.exe) not found at:
    echo %CSC%
    echo Please make sure .NET Framework 4.0 or higher is installed.
    exit /b 1
)

echo Compiling...
"%CSC%" /target:winexe /out:SteamEpicSync.exe /r:PresentationCore.dll /r:PresentationFramework.dll /r:WindowsBase.dll /r:System.Xaml.dll /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll Program.cs VdfParser.cs EpicSyncManager.cs GameLauncher.cs TrayApplication.cs MainWindow.xaml.cs

if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Compilation FAILED!
    exit /b 1
)

echo.
echo [SUCCESS] Compilation successful!
echo Output: SteamEpicSync.exe
echo.
