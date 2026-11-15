using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.Generic;

public class MainWindowController
{
    public Window Window { get; private set; }

    private Button syncBtn;
    private Button minBtn;
    private Button closeBtn;
    private CheckBox startupChk;
    private CheckBox closeEgsChk;
    private CheckBox steamStartupChk;
    private CheckBox startupSyncChk;
    private TextBox collectionNameBox;
    private ListBox gamesListBox;
    private TextBlock statusTxt;

    private Button epicLoginBtn;
    private TextBlock epicStatusTxt;
    private EpicApiManager epicApi;
    private Button uninstallBtn;

    private Button createShortcutBtn;
    private TextBlock shortcutStatusTxt;

    private string currentExePath;
    private bool hasShownTrayMessage = false;

    public MainWindowController(string exePath)
    {
        currentExePath = exePath;
        epicApi = new EpicApiManager();
        LoadXaml();
        try
        {
            string iconPath = CyberpunkTheme.EnsureAppIcon(currentExePath);
            if (File.Exists(iconPath))
            {
                Window.Icon = BitmapFrame.Create(new Uri(iconPath));
            }
        }
        catch { }
        BindEvents();
        RefreshData();
    }

    private void LoadXaml()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        string resourceName = null;
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (name.Equals("MainWindow.xaml", StringComparison.OrdinalIgnoreCase))
            {
                resourceName = name;
                break;
            }
        }

        if (resourceName != null)
        {
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    Window = (Window)XamlReader.Load(stream);
                    return;
                }
            }
        }

        // Fallback to disk file if resource not found
        string xamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MainWindow.xaml");
        if (!File.Exists(xamlPath))
        {
            xamlPath = "MainWindow.xaml";
        }

        using (var fs = new FileStream(xamlPath, FileMode.Open, FileAccess.Read))
        {
            Window = (Window)XamlReader.Load(fs);
        }
    }

    private void BindEvents()
    {
        var titleBar = (FrameworkElement)Window.FindName("TitleBar");
        syncBtn = (Button)Window.FindName("SyncBtn");
        minBtn = (Button)Window.FindName("MinBtn");
        closeBtn = (Button)Window.FindName("CloseBtn");
        startupChk = (CheckBox)Window.FindName("StartupChk");
        closeEgsChk = (CheckBox)Window.FindName("CloseEgsChk");
        steamStartupChk = (CheckBox)Window.FindName("SteamStartupChk");
        startupSyncChk = (CheckBox)Window.FindName("StartupSyncChk");
        collectionNameBox = (TextBox)Window.FindName("CollectionNameBox");
        createShortcutBtn = (Button)Window.FindName("CreateShortcutBtn");
        shortcutStatusTxt = (TextBlock)Window.FindName("ShortcutStatusTxt");

        UpdateShortcutStatus();

        if (createShortcutBtn != null)
        {
            createShortcutBtn.Click += (s, e) =>
            {
                CyberpunkTheme.PromptDesktopShortcut(Window, currentExePath, true);
                UpdateShortcutStatus();
            };
        }

        Window.Loaded += (s, e) =>
        {
            UpdateShortcutStatus();
            try
            {
                var settings = AppSettings.Load();
                if (!settings.DesktopShortcutPrompted && !CyberpunkTheme.HasDesktopShortcut())
                {
                    CyberpunkTheme.PromptDesktopShortcut(Window, currentExePath, false);
                    settings.DesktopShortcutPrompted = true;
                    settings.Save();
                    UpdateShortcutStatus();
                }
            }
            catch { }
        };

        RefreshSettings();

        // Auto-save on any checkbox click immediately
        if (steamStartupChk != null)
        {
            steamStartupChk.Click += (s, e) => SaveAllSettings();
        }

        if (startupSyncChk != null)
        {
            startupSyncChk.Click += (s, e) => SaveAllSettings();
        }

        if (startupChk != null)
        {
            startupChk.Click += (s, e) => SaveAllSettings();
        }

        if (closeEgsChk != null)
        {
            closeEgsChk.Click += (s, e) => SaveAllSettings();
        }

        if (collectionNameBox != null)
        {
            collectionNameBox.LostFocus += (s, e) => SaveAllSettings();
        }

        var saveBtn = (Button)Window.FindName("SaveSettingsBtn");
        if (saveBtn != null)
        {
            saveBtn.Click += (s, e) => SaveAllSettings();
        }

        gamesListBox = (ListBox)Window.FindName("GamesListBox");
        statusTxt = (TextBlock)Window.FindName("StatusTxt");

        if (titleBar != null)
        {
            titleBar.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                    Window.DragMove();
            };
        }

        // Minimize button: hides window to tray
        if (minBtn != null)
        {
            minBtn.Click += (s, e) =>
            {
                SaveAllSettings();
                Window.Hide();
                if (!hasShownTrayMessage)
                {
                    Program.ShowTrayBalloon("Steam Epic Sync // 2077", "Приложение свернуто в трей и продолжает работу.", System.Windows.Forms.ToolTipIcon.Info);
                    hasShownTrayMessage = true;
                }
            };
        }

        // Close button: saves settings and fully closes/exits application
        if (closeBtn != null)
        {
            closeBtn.Click += (s, e) =>
            {
                SaveAllSettings();
                Program.ExitApplication();
            };
        }

        if (syncBtn != null)
        {
            syncBtn.Click += (s, e) => TriggerSync();
        }

        epicLoginBtn = (Button)Window.FindName("EpicLoginBtn");
        epicStatusTxt = (TextBlock)Window.FindName("EpicStatusTxt");
        UpdateEpicAccountStatus();

        if (epicLoginBtn != null)
        {
            epicLoginBtn.Click += (s, e) =>
            {
                if (epicApi.IsLoggedIn)
                {
                    epicApi.Logout();
                    UpdateEpicAccountStatus();
                    RefreshData();
                }
                else
                {
                    var loginWin = new LoginWindow();
                    loginWin.Owner = Window;
                    if (loginWin.ShowDialog() == true && !string.IsNullOrEmpty(loginWin.AuthorizationCode))
                    {
                        statusTxt.Text = "[ ПОДКЛЮЧЕНИЕ К СЕТИ EPIC GAMES... ]";

                        bool success = false;
                        try
                        {
                            success = epicApi.LoginWithCode(loginWin.AuthorizationCode);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(Window, "Ошибка соединения: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }

                        if (success)
                        {
                            UpdateEpicAccountStatus();
                            RefreshData();
                            TriggerSync();
                        }
                        else
                        {
                            MessageBox.Show(Window, "Не удалось подключиться к аккаунту Epic Games.\n" + epicApi.LastError, "Ошибка авторизации", MessageBoxButton.OK, MessageBoxImage.Error);
                            statusTxt.Text = "[ ОШИБКА ВХОДА В EPIC GAMES ]";
                        }
                    }
                }
            };
        }

        uninstallBtn = (Button)Window.FindName("UninstallBtn");
        if (uninstallBtn != null)
        {
            uninstallBtn.Click += (s, e) =>
            {
                TriggerUninstall();
            };
        }
    }

    public void SaveAllSettings()
    {
        try
        {
            var settings = AppSettings.Load();
            if (steamStartupChk != null) settings.LaunchWithSteam = steamStartupChk.IsChecked == true;
            if (startupSyncChk != null) settings.SyncOnSteamStart = startupSyncChk.IsChecked == true;
            if (collectionNameBox != null)
            {
                string raw = (collectionNameBox.Text ?? "").Trim();
                if (!string.IsNullOrEmpty(raw))
                {
                    try { settings.CollectionName = AppSettings.ValidateName(raw); }
                    catch { }
                }
            }
            settings.Save();

            if (startupChk != null)
            {
                Program.SetRunOnStartup(startupChk.IsChecked == true);
                Program.UpdateTrayMenu();
            }

            if (closeEgsChk != null)
            {
                Program.SetCloseEgsSetting(closeEgsChk.IsChecked == true);
            }

            var msg = (TextBlock)Window.FindName("SettingsMessage");
            if (msg != null)
            {
                msg.Text = "[НАСТРОЙКИ СОХРАНЕНЫ]";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error saving settings: " + ex.Message);
        }
    }

    public void UpdateShortcutStatus()
    {
        if (shortcutStatusTxt == null) return;
        bool exists = CyberpunkTheme.HasDesktopShortcut();
        if (exists)
        {
            shortcutStatusTxt.Text = "[ СТАТУС: ЯРЛЫК УСТАНОВЛЕН НА РАБОЧЕМ СТОЛЕ ]";
            shortcutStatusTxt.Foreground = new SolidColorBrush(Color.FromRgb(252, 238, 10)); // Cyberpunk Yellow
        }
        else
        {
            shortcutStatusTxt.Text = "[ СТАТУС: ЯРЛЫК НЕ СОЗДАН ]";
            shortcutStatusTxt.Foreground = new SolidColorBrush(Color.FromRgb(122, 132, 153));
        }
    }

    public void RefreshSettings()
    {
        var settings = AppSettings.Load();
        if (steamStartupChk != null) steamStartupChk.IsChecked = settings.LaunchWithSteam;
        if (startupSyncChk != null) startupSyncChk.IsChecked = settings.SyncOnSteamStart;
        if (collectionNameBox != null) collectionNameBox.Text = settings.CollectionName;
        if (startupChk != null) startupChk.IsChecked = Program.IsRunOnStartup();
        if (closeEgsChk != null) closeEgsChk.IsChecked = Program.GetCloseEgsSetting();
    }

    private void UpdateEpicAccountStatus()
    {
        if (epicApi != null && epicStatusTxt != null && epicLoginBtn != null)
        {
            if (epicApi.IsLoggedIn)
            {
                epicStatusTxt.Text = string.Format("[ ПОДКЛЮЧЕН: {0} ]", epicApi.DisplayName);
                epicStatusTxt.Foreground = new SolidColorBrush(Color.FromRgb(252, 238, 10)); // Cyber Yellow
                epicLoginBtn.Content = "ВЫЙТИ ИЗ АККАУНТА";
            }
            else
            {
                epicStatusTxt.Text = "[ СТАТУС: НЕ АВТОРИЗОВАН ]";
                epicStatusTxt.Foreground = new SolidColorBrush(Color.FromRgb(166, 180, 201));
                epicLoginBtn.Content = "ПОДКЛЮЧИТЬ АККАУНТ";
            }
        }
    }

    private List<EpicGame> GetCombinedGamesList()
    {
        return EpicSyncManager.GetLibrary(epicApi, false);
    }

    public void RefreshData()
    {
        try
        {
            var games = GetCombinedGamesList();
            int installedCount = 0;
            foreach (var g in games)
            {
                if (g.IsInstalled) installedCount++;
            }

            statusTxt.Text = string.Format("[ БИБЛИОТЕКА: {0} ИГР // УСТАНОВЛЕНО: {1} // MATRIX READY ]", games.Count, installedCount);

            gamesListBox.Items.Clear();
            foreach (var game in games)
            {
                var grid = new Grid { Margin = new Thickness(8, 6, 8, 6) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Game Icon Border
                var imgBorder = new Border
                {
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(Color.FromRgb(20, 23, 33)),
                    BorderBrush = new SolidColorBrush(game.IsInstalled ? Color.FromRgb(252, 238, 10) : Color.FromRgb(0, 240, 255)),
                    BorderThickness = new Thickness(1),
                    Width = 32,
                    Height = 32,
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                var img = new Image { Width = 24, Height = 24 };
                if (game.IsInstalled && !string.IsNullOrEmpty(game.InstallLocation) && !string.IsNullOrEmpty(game.LaunchExecutable))
                {
                    string iconPath = Path.Combine(game.InstallLocation, game.LaunchExecutable.Replace('/', '\\'));
                    if (File.Exists(iconPath))
                    {
                        try
                        {
                            using (var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(iconPath))
                            using (var bmp = sysIcon.ToBitmap())
                            {
                                var stream = new MemoryStream();
                                bmp.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                                stream.Position = 0;
                                var bitmapImage = new BitmapImage();
                                bitmapImage.BeginInit();
                                bitmapImage.StreamSource = stream;
                                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                                bitmapImage.EndInit();
                                img.Source = bitmapImage;
                            }
                        }
                        catch
                        {
                            // Fallback to null (empty icon)
                        }
                    }
                }
                imgBorder.Child = img;
                Grid.SetColumn(imgBorder, 0);
                grid.Children.Add(imgBorder);

                // Text Info
                var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
                var nameTxt = new TextBlock
                {
                    Text = game.DisplayName,
                    Foreground = Brushes.White,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold
                };
                var dirTxt = new TextBlock
                {
                    Text = game.IsInstalled ? game.InstallLocation : "Требуется загрузка перед запуском",
                    Foreground = new SolidColorBrush(Color.FromRgb(122, 132, 153)),
                    FontSize = 10,
                    Margin = new Thickness(0, 2, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                infoStack.Children.Add(nameTxt);
                infoStack.Children.Add(dirTxt);
                Grid.SetColumn(infoStack, 1);
                grid.Children.Add(infoStack);

                // Status Badge in Cyberpunk Style
                Border statusLabel;
                if (game.IsInstalled)
                {
                    statusLabel = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(30, 252, 238, 10)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(252, 238, 10)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(8, 4, 8, 4),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    var statusLabelText = new TextBlock
                    {
                        Text = "[ УСТАНОВЛЕНА // 2077 ]",
                        Foreground = new SolidColorBrush(Color.FromRgb(252, 238, 10)),
                        FontSize = 10,
                        FontWeight = FontWeights.Bold
                    };
                    statusLabel.Child = statusLabelText;
                }
                else
                {
                    statusLabel = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(30, 0, 240, 255)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0, 240, 255)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(8, 4, 8, 4),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    var statusLabelText = new TextBlock
                    {
                        Text = "[ ОБЛАКО // EGS LINK ]",
                        Foreground = new SolidColorBrush(Color.FromRgb(0, 240, 255)),
                        FontSize = 10,
                        FontWeight = FontWeights.Bold
                    };
                    statusLabel.Child = statusLabelText;
                }

                Grid.SetColumn(statusLabel, 2);
                grid.Children.Add(statusLabel);

                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(14, 16, 24)),
                    BorderBrush = new SolidColorBrush(game.IsInstalled ? Color.FromArgb(180, 252, 238, 10) : Color.FromArgb(140, 0, 240, 255)),
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    CornerRadius = new CornerRadius(2),
                    Child = grid,
                    Margin = new Thickness(0, 0, 0, 4)
                };

                gamesListBox.Items.Add(border);
            }
        }
        catch (Exception ex)
        {
            statusTxt.Text = "[ ОШИБКА ЗАГРУЗКИ: " + ex.Message + " ]";
        }
    }

    public void TriggerSync()
    {
        statusTxt.Text = "[ СИНХРОНИЗАЦИЯ БИБЛИОТЕКИ STEAM ✕ EPIC... ]";
        syncBtn.IsEnabled = false;

        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                int added, removed;
                string res = SteamSession.Synchronize(currentExePath, epicApi, out added, out removed);

                Window.Dispatcher.Invoke(() =>
                {
                    statusTxt.Text = string.Format("[ СИНХРОНИЗАЦИЯ ЗАВЕРШЕНА // ДОБАВЛЕНО: {0} // УДАЛЕНО: {1} ]", added, removed);
                    syncBtn.IsEnabled = true;
                    RefreshData();
                    statusTxt.Text = string.IsNullOrEmpty(epicApi.LastError) ? "[ ГОТОВО // СИСТЕМА ОБНОВЛЕНА ]" : "[ ИСПОЛЬЗОВАН КЭШ: " + epicApi.LastError + " ]";
                    if (res.Contains("Обложка ")) MessageBox.Show(Window, res, "Результат синхронизации");

                    Program.ShowTrayBalloon("Steam Epic Sync // 2077",
                        string.Format("Синхронизация завершена. Добавлено игр: {0}.", added));
                });
            }
            catch (Exception ex)
            {
                Window.Dispatcher.Invoke(() =>
                {
                    statusTxt.Text = "[ ОШИБКА: " + ex.Message + " ]";
                    syncBtn.IsEnabled = true;
                });
            }
        });
    }

    public void UpdateStartupCheckbox()
    {
        if (startupChk != null)
        {
            startupChk.IsChecked = Program.IsRunOnStartup();
        }
    }

    public void TriggerUninstall()
    {
        var result = MessageBox.Show(
            Window,
            "Вы действительно хотите удалить все импортированные ярлыки из Steam, убрать утилиту из автозагрузки Windows и полностью стереть все сохраненные настройки и кэши?",
            "Удаление Steam Epic Sync",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning
        );

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                // 1. Disable startup
                Program.SetRunOnStartup(false);

                // 2. Delete EGS closing settings from registry
                try
                {
                    Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\SteamEpicSync", false);
                }
                catch {}

                // 3. Clear Steam shortcuts (pass empty list to delete all ours)
                int added, removed;
                EpicSyncManager.Sync(currentExePath, new List<EpicGame>(), out added, out removed, true);

                // 4. Delete local AppData folder
                string localAppDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SteamEpicSync");
                if (Directory.Exists(localAppDir))
                {
                    Directory.Delete(localAppDir, true);
                }

                // 5. Delete desktop shortcut if exists
                string shortcut = CyberpunkTheme.GetDesktopShortcutPath();
                if (File.Exists(shortcut))
                {
                    try { File.Delete(shortcut); } catch {}
                }

                MessageBox.Show(
                    Window,
                    string.Format("Все настройки, кэши и ярлыки Steam ({0} шт.) были успешно удалены.\n\nТеперь вы можете просто удалить файл SteamEpicSync.exe.", removed),
                    "Удаление завершено",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                // Exit the entire application
                Program.ExitApplication();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Window,
                    "Ошибка при удалении файлов: " + ex.Message,
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
    }
}
