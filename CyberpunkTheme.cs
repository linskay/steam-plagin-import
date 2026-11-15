using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public static class CyberpunkTheme
{
    // Cyberpunk 2077 Official Color Palette
    public static readonly System.Drawing.Color ColorYellow = System.Drawing.Color.FromArgb(252, 238, 10);   // #FCEE0A (Cyberpunk Yellow)
    public static readonly System.Drawing.Color ColorCyan = System.Drawing.Color.FromArgb(0, 240, 255);       // #00F0FF (Neon Cyan)
    public static readonly System.Drawing.Color ColorRed = System.Drawing.Color.FromArgb(255, 0, 60);         // #FF003C (Neon Red / Glitch)
    public static readonly System.Drawing.Color ColorDark = System.Drawing.Color.FromArgb(11, 12, 16);        // #0B0C10 (Night City Obsidian)
    public static readonly System.Drawing.Color ColorCard = System.Drawing.Color.FromArgb(18, 20, 29);        // #12141D (Dark Slate)

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static string GetAppIconPath(string exePath)
    {
        string dir = Path.GetDirectoryName(exePath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SteamEpicSync");
            Directory.CreateDirectory(dir);
        }
        return Path.Combine(dir, "SteamEpicSync.ico");
    }

    public static string EnsureAppIcon(string exePath)
    {
        string iconPath = GetAppIconPath(exePath);
        if (!File.Exists(iconPath))
        {
            try
            {
                GenerateAppIconFile(iconPath);
            }
            catch
            {
                // Fallback: icon will be created on next try
            }
        }
        return iconPath;
    }

    public static void GenerateAppIconFile(string savePath)
    {
        using (var bmp = new Bitmap(128, 128))
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(System.Drawing.Color.Transparent);

            // 1. Chamfered Cyberpunk Polygon (classic angular Night City silhouette)
            System.Drawing.Point[] polygon = new System.Drawing.Point[]
            {
                new System.Drawing.Point(14, 14),
                new System.Drawing.Point(96, 14),
                new System.Drawing.Point(114, 32),
                new System.Drawing.Point(114, 114),
                new System.Drawing.Point(32, 114),
                new System.Drawing.Point(14, 96)
            };

            // Background fill
            using (var bgBrush = new SolidBrush(ColorDark))
            {
                g.FillPolygon(bgBrush, polygon);
            }

            // Tech grid lines in background
            using (var gridPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(30, 0, 240, 255), 1f))
            {
                g.DrawLine(gridPen, 14, 45, 114, 45);
                g.DrawLine(gridPen, 14, 85, 114, 85);
                g.DrawLine(gridPen, 45, 14, 45, 114);
                g.DrawLine(gridPen, 85, 14, 85, 114);
            }

            // Bold Cyberpunk Yellow outer border
            using (var borderPen = new System.Drawing.Pen(ColorYellow, 3.5f))
            {
                borderPen.LineJoin = LineJoin.Miter;
                g.DrawPolygon(borderPen, polygon);
            }

            // Neon Red glitch accent bar on top left
            using (var redBrush = new SolidBrush(ColorRed))
            {
                g.FillRectangle(redBrush, 22, 14, 28, 4);
            }

            // Neon Cyan corner bracket accents
            using (var cyanPen = new System.Drawing.Pen(ColorCyan, 2f))
            {
                // Top-right corner accent
                g.DrawLine(cyanPen, 92, 18, 110, 36);
                // Bottom-left corner accent
                g.DrawLine(cyanPen, 18, 92, 36, 110);
            }

            // Center emblem: Stylized Cyberpunk "SES" typography
            using (var font = new System.Drawing.Font("Arial Black", 20f, System.Drawing.FontStyle.Bold))
            using (var yellowBrush = new SolidBrush(ColorYellow))
            using (var sf = new System.Drawing.StringFormat())
            {
                sf.Alignment = System.Drawing.StringAlignment.Center;
                sf.LineAlignment = System.Drawing.StringAlignment.Center;

                // Subtle neon glow / shadow behind text
                using (var glowBrush = new SolidBrush(System.Drawing.Color.FromArgb(90, 0, 240, 255)))
                {
                    g.DrawString("SES", font, glowBrush, 66, 56, sf);
                }
                g.DrawString("SES", font, yellowBrush, 64, 54, sf);
            }

            // Subtitle: "// 2077 //"
            using (var subFont = new System.Drawing.Font("Arial", 8.5f, System.Drawing.FontStyle.Bold))
            using (var cyanBrush = new SolidBrush(ColorCyan))
            using (var sf = new System.Drawing.StringFormat())
            {
                sf.Alignment = System.Drawing.StringAlignment.Center;
                sf.LineAlignment = System.Drawing.StringAlignment.Center;
                g.DrawString("// 2077 //", subFont, cyanBrush, 64, 86, sf);
            }

            // Mini hazard barcode at the bottom right
            using (var yellowBrush = new SolidBrush(ColorYellow))
            {
                g.FillRectangle(yellowBrush, 75, 100, 3, 7);
                g.FillRectangle(yellowBrush, 81, 100, 2, 7);
                g.FillRectangle(yellowBrush, 85, 100, 4, 7);
                g.FillRectangle(yellowBrush, 92, 100, 2, 7);
                g.FillRectangle(yellowBrush, 96, 100, 5, 7);
            }

            // Save to ICO file
            IntPtr hIcon = bmp.GetHicon();
            try
            {
                using (var icon = System.Drawing.Icon.FromHandle(hIcon))
                using (var fs = new FileStream(savePath + ".tmp", FileMode.Create, FileAccess.Write))
                {
                    icon.Save(fs);
                }
                if (File.Exists(savePath)) File.Delete(savePath);
                File.Move(savePath + ".tmp", savePath);
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }
    }

    public static System.Drawing.Icon GetTrayIcon()
    {
        try
        {
            using (var bmp = new Bitmap(16, 16))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(System.Drawing.Color.Transparent);

                // Angular chamfered shape in 16x16
                System.Drawing.Point[] pts = new System.Drawing.Point[]
                {
                    new System.Drawing.Point(1, 1),
                    new System.Drawing.Point(11, 1),
                    new System.Drawing.Point(14, 4),
                    new System.Drawing.Point(14, 14),
                    new System.Drawing.Point(4, 14),
                    new System.Drawing.Point(1, 11)
                };

                // Dark fill
                using (var bgBrush = new SolidBrush(ColorDark))
                {
                    g.FillPolygon(bgBrush, pts);
                }

                // Cyberpunk Yellow border
                using (var borderPen = new System.Drawing.Pen(ColorYellow, 1.5f))
                {
                    g.DrawPolygon(borderPen, pts);
                }

                // Glowing Neon Cyan center core diamond
                System.Drawing.Point[] corePts = new System.Drawing.Point[]
                {
                    new System.Drawing.Point(7, 4),
                    new System.Drawing.Point(10, 7),
                    new System.Drawing.Point(7, 10),
                    new System.Drawing.Point(4, 7)
                };
                using (var cyanBrush = new SolidBrush(ColorCyan))
                {
                    g.FillPolygon(cyanBrush, corePts);
                }

                // Neon red accent pixel on top left
                using (var redBrush = new SolidBrush(ColorRed))
                {
                    g.FillRectangle(redBrush, 3, 1, 3, 1);
                }

                IntPtr hIcon = bmp.GetHicon();
                var icon = System.Drawing.Icon.FromHandle(hIcon);
                return icon;
            }
        }
        catch
        {
            return System.Drawing.SystemIcons.Application;
        }
    }

    public static string GetDesktopShortcutPath()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        return Path.Combine(desktop, "Steam Epic Sync.lnk");
    }

    public static bool HasDesktopShortcut()
    {
        return File.Exists(GetDesktopShortcutPath());
    }

    public static bool CreateDesktopShortcut(string exePath)
    {
        try
        {
            string iconPath = EnsureAppIcon(exePath);
            string shortcutPath = GetDesktopShortcutPath();

            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return false;

            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember("CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });

            Type shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { exePath });
            shortcutType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(exePath) });
            shortcutType.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { "Steam Epic Sync - Cyberpunk 2077 Edition" });

            if (File.Exists(iconPath))
            {
                shortcutType.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { iconPath + ",0" });
            }

            shortcutType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error creating desktop shortcut: " + ex.Message);
            return false;
        }
    }

    public static bool PromptDesktopShortcut(Window owner, string exePath, bool force)
    {
        bool hasShortcut = HasDesktopShortcut();
        if (hasShortcut && !force) return false;

        // Create Cyberpunk 2077 styled modal dialog
        var dialog = new Window
        {
            Title = "Cyberpunk 2077 // Установка ярлыка",
            Width = 480,
            Height = 270,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Owner = owner,
            ShowInTaskbar = false,
            Topmost = true
        };

        bool result = false;

        var outerBorder = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(11, 12, 16)),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(252, 238, 10)),
            BorderThickness = new Thickness(2),
            Padding = new Thickness(20)
        };

        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

        // Header
        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
        var yellowBadge = new Border
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(252, 238, 10)),
            Padding = new Thickness(6, 2, 6, 2),
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 0, 10, 0)
        };
        yellowBadge.Child = new TextBlock
        {
            Text = "CP // 2077",
            Foreground = System.Windows.Media.Brushes.Black,
            FontWeight = FontWeights.Bold,
            FontSize = 11
        };
        var headerTxt = new TextBlock
        {
            Text = "// СИСТЕМНЫЙ ЗАПРОС: DESKTOP_SHORTCUT //",
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 240, 255)),
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        headerPanel.Children.Add(yellowBadge);
        headerPanel.Children.Add(headerTxt);
        Grid.SetRow(headerPanel, 0);
        mainGrid.Children.Add(headerPanel);

        // Content
        var contentPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var titleTxt = new TextBlock
        {
            Text = "СОЗДАТЬ ЯРЛЫК НА РАБОЧЕМ СТОЛЕ?",
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var descTxt = new TextBlock
        {
            Text = "Вывести ярлык быстрого запуска Steam Epic Sync на рабочий стол с фирменным значком в стиле Cyberpunk 2077?",
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(166, 180, 201)),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 18
        };
        contentPanel.Children.Add(titleTxt);
        contentPanel.Children.Add(descTxt);
        Grid.SetRow(contentPanel, 1);
        mainGrid.Children.Add(contentPanel);

        // Buttons
        var btnGrid = new Grid { Margin = new Thickness(0, 15, 0, 0) };
        btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var confirmBtn = new Button
        {
            Content = "⚡ ДА // СОЗДАТЬ ЯРЛЫК",
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(252, 238, 10)),
            Foreground = System.Windows.Media.Brushes.Black,
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Padding = new Thickness(12, 10, 12, 10),
            Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new Thickness(0, 0, 8, 0)
        };
        confirmBtn.Click += (s, e) =>
        {
            CreateDesktopShortcut(exePath);
            result = true;
            dialog.Close();
        };
        Grid.SetColumn(confirmBtn, 0);
        btnGrid.Children.Add(confirmBtn);

        var cancelBtn = new Button
        {
            Content = "✕ НЕТ // ПРОПУСТИТЬ",
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 23, 31)),
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 240, 255)),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 240, 255)),
            BorderThickness = new Thickness(1),
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Padding = new Thickness(12, 10, 12, 10),
            Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new Thickness(8, 0, 0, 0)
        };
        cancelBtn.Click += (s, e) =>
        {
            result = false;
            dialog.Close();
        };
        Grid.SetColumn(cancelBtn, 1);
        btnGrid.Children.Add(cancelBtn);

        Grid.SetRow(btnGrid, 2);
        mainGrid.Children.Add(btnGrid);

        outerBorder.Child = mainGrid;
        dialog.Content = outerBorder;

        dialog.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                dialog.DragMove();
        };

        dialog.ShowDialog();
        return result;
    }
}
