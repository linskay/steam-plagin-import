using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

public class TrayApplication : ApplicationContext
{
    private NotifyIcon notifyIcon;
    private ContextMenuStrip contextMenu;
    private ToolStripMenuItem syncItem;
    private ToolStripMenuItem openItem;
    private ToolStripMenuItem startupItem;
    private ToolStripMenuItem exitItem;

    private string currentExePath;
    private Action onSyncRequested;
    private Action onShowDashboardRequested;
    private Func<bool> checkStartupStatus;
    private Action<bool> toggleStartupStatus;

    private System.Windows.Forms.Timer steamMonitorTimer;
    private bool hasSeenSteam;


    public TrayApplication(
        string exePath,
        Action onSync,
        Action onShowDashboard,
        Func<bool> checkStartup,
        Action<bool> toggleStartup,
        bool steamSession = false)
    {
        currentExePath = exePath;
        onSyncRequested = onSync;
        onShowDashboardRequested = onShowDashboard;
        checkStartupStatus = checkStartup;
        toggleStartupStatus = toggleStartup;

        InitializeTray();

        hasSeenSteam = steamSession || SteamSession.IsRunning();
        steamMonitorTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        steamMonitorTimer.Tick += (s, e) => {
            bool running = SteamSession.IsRunning();
            if (running) { hasSeenSteam = true; SteamSession.AcknowledgeSteamStarted(); }
            if (SteamSession.ShouldExit(hasSeenSteam, running, SteamSession.Restarting, SteamSession.InRestartGrace)) Exit();
        };
        steamMonitorTimer.Start();
    }

    private void InitializeTray()
    {
        contextMenu = new ContextMenuStrip();

        openItem = new ToolStripMenuItem("Панель управления", null, (s, e) => { if (onShowDashboardRequested != null) onShowDashboardRequested(); });
        openItem.Font = new Font(openItem.Font, FontStyle.Bold);

        syncItem = new ToolStripMenuItem("Синхронизировать сейчас", null, (s, e) => { if (onSyncRequested != null) onSyncRequested(); });

        startupItem = new ToolStripMenuItem("Запуск при старте Windows", null, (s, e) => {
            bool current = startupItem.Checked;
            startupItem.Checked = !current;
            if (toggleStartupStatus != null) toggleStartupStatus(!current);
        });
        startupItem.Checked = checkStartupStatus != null ? checkStartupStatus() : false;

        ToolStripMenuItem shortcutItem = new ToolStripMenuItem("⚡ Ярлык на рабочий стол (2077)", null, (s, e) => {
            CyberpunkTheme.CreateDesktopShortcut(currentExePath);
            ShowNotification("Cyberpunk 2077", "Ярлык успешно создан на рабочем столе!", ToolTipIcon.Info);
        });

        exitItem = new ToolStripMenuItem("Выход", null, (s, e) => Exit());

        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(syncItem);
        contextMenu.Items.Add(shortcutItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(startupItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        notifyIcon = new NotifyIcon
        {
            Icon = CreateDynamicIcon(),
            ContextMenuStrip = contextMenu,
            Text = "Steam Epic Sync",
            Visible = true
        };

        notifyIcon.DoubleClick += (s, e) => { if (onShowDashboardRequested != null) onShowDashboardRequested(); };
    }

    public void ShowNotification(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        notifyIcon.ShowBalloonTip(3000, title, text, icon);
    }

    private Icon CreateDynamicIcon()
    {
        return CyberpunkTheme.GetTrayIcon();
    }

    public void UpdateStartupMenuItem()
    {
        if (startupItem != null && checkStartupStatus != null)
        {
            startupItem.Checked = checkStartupStatus();
        }
    }

    public void Exit()
    {
        if (steamMonitorTimer != null)
        {
            steamMonitorTimer.Dispose();
            steamMonitorTimer = null;
        }
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        Application.Exit();
        Environment.Exit(0);
    }
}
