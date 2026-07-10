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

    private System.Threading.Timer steamMonitorTimer;
    private int checkFailCount = 0;

    public TrayApplication(
        string exePath, 
        Action onSync, 
        Action onShowDashboard,
        Func<bool> checkStartup,
        Action<bool> toggleStartup)
    {
        currentExePath = exePath;
        onSyncRequested = onSync;
        onShowDashboardRequested = onShowDashboard;
        checkStartupStatus = checkStartup;
        toggleStartupStatus = toggleStartup;

        InitializeTray();

        // Start checking if Steam is running every 5 seconds
        steamMonitorTimer = new System.Threading.Timer(CheckSteamProcess, null, 5000, 5000);
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

        exitItem = new ToolStripMenuItem("Выход", null, (s, e) => Exit());

        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(syncItem);
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
        try
        {
            using (Bitmap bmp = new Bitmap(16, 16))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // Dark background circle
                using (Brush bgBrush = new SolidBrush(Color.FromArgb(20, 24, 30)))
                {
                    g.FillEllipse(bgBrush, 0, 0, 15, 15);
                }

                // Bright blue outline
                using (Pen borderPen = new Pen(Color.FromArgb(0, 120, 215), 1.5f))
                {
                    g.DrawEllipse(borderPen, 1, 1, 13, 13);
                }

                // Vibrant green center dot (representing EGS sync status)
                using (Brush dotBrush = new SolidBrush(Color.FromArgb(0, 200, 83)))
                {
                    g.FillEllipse(dotBrush, 5, 5, 5, 5);
                }

                return Icon.FromHandle(bmp.GetHicon());
            }
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    public void UpdateStartupMenuItem()
    {
        if (startupItem != null && checkStartupStatus != null)
        {
            startupItem.Checked = checkStartupStatus();
        }
    }

    private void CheckSteamProcess(object state)
    {
        try
        {
            var steamProcesses = Process.GetProcessesByName("steam");
            if (steamProcesses.Length == 0)
            {
                checkFailCount++;
                // If Steam is missing for 6 consecutive checks (30 seconds), exit
                if (checkFailCount >= 6)
                {
                    if (notifyIcon != null && notifyIcon.ContextMenuStrip != null && notifyIcon.ContextMenuStrip.InvokeRequired)
                    {
                        notifyIcon.ContextMenuStrip.Invoke(new MethodInvoker(Exit));
                    }
                    else
                    {
                        Exit();
                    }
                }
            }
            else
            {
                checkFailCount = 0;
            }
        }
        catch
        {
            // ignore
        }
    }

    private void Exit()
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
