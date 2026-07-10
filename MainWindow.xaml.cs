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
    private ListBox gamesListBox;
    private TextBlock statusTxt;
    
    private string currentExePath;
    private bool hasShownTrayMessage = false;

    public MainWindowController(string exePath)
    {
        currentExePath = exePath;
        LoadXaml();
        BindEvents();
        RefreshData();
    }

    private void LoadXaml()
    {
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

        if (minBtn != null)
        {
            minBtn.Click += (s, e) => Window.Hide();
        }

        if (closeBtn != null)
        {
            closeBtn.Click += (s, e) =>
            {
                Window.Hide();
                if (!hasShownTrayMessage)
                {
                    Program.ShowTrayBalloon("Steam Epic Sync", "Приложение свернуто в трей и продолжает работу.", System.Windows.Forms.ToolTipIcon.Info);
                    hasShownTrayMessage = true;
                }
            };
        }

        if (syncBtn != null)
        {
            syncBtn.Click += (s, e) => TriggerSync();
        }

        if (startupChk != null)
        {
            startupChk.IsChecked = Program.IsRunOnStartup();
            startupChk.Click += (s, e) =>
            {
                bool check = startupChk.IsChecked ?? false;
                Program.SetRunOnStartup(check);
                Program.UpdateTrayMenu();
            };
        }

        if (closeEgsChk != null)
        {
            closeEgsChk.IsChecked = Program.GetCloseEgsSetting();
            closeEgsChk.Click += (s, e) =>
            {
                Program.SetCloseEgsSetting(closeEgsChk.IsChecked ?? true);
            };
        }
    }

    public void RefreshData()
    {
        try
        {
            var games = EpicSyncManager.FindInstalledGames();
            statusTxt.Text = "Найдено установленных игр: " + games.Count;

            gamesListBox.Items.Clear();
            foreach (var game in games)
            {
                var grid = new Grid { Margin = new Thickness(8, 6, 8, 6) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Game Icon Border
                var imgBorder = new Border
                {
                    CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(Color.FromRgb(30, 32, 44)),
                    Width = 32,
                    Height = 32,
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                var img = new Image { Width = 24, Height = 24 };
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
                    Text = game.InstallLocation,
                    Foreground = new SolidColorBrush(Color.FromRgb(108, 114, 129)),
                    FontSize = 10,
                    Margin = new Thickness(0, 2, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                infoStack.Children.Add(nameTxt);
                infoStack.Children.Add(dirTxt);
                Grid.SetColumn(infoStack, 1);
                grid.Children.Add(infoStack);

                // Status Badge
                var statusLabel = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(20, 0, 200, 83)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(100, 0, 200, 83)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 4, 8, 4),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var statusLabelText = new TextBlock
                {
                    Text = "Готова к импорту",
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 200, 83)),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold
                };
                statusLabel.Child = statusLabelText;
                Grid.SetColumn(statusLabel, 2);
                grid.Children.Add(statusLabel);

                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(24, 26, 36)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(34, 36, 48)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Child = grid,
                    Margin = new Thickness(0, 0, 0, 6)
                };

                gamesListBox.Items.Add(border);
            }
        }
        catch (Exception ex)
        {
            statusTxt.Text = "Ошибка загрузки: " + ex.Message;
        }
    }

    public void TriggerSync()
    {
        statusTxt.Text = "Синхронизация...";
        syncBtn.IsEnabled = false;

        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var games = EpicSyncManager.FindInstalledGames();
                int added, removed;
                string res = EpicSyncManager.Sync(currentExePath, games, out added, out removed);

                Window.Dispatcher.Invoke(() =>
                {
                    statusTxt.Text = string.Format("Синхронизация завершена. Добавлено: {0}, Удалено: {1}", added, removed);
                    syncBtn.IsEnabled = true;
                    RefreshData();

                    Program.ShowTrayBalloon("Синхронизация успешна",
                        string.Format("Синхронизировано игр: {0}.\nПожалуйста, перезапустите Steam, если он запущен.", games.Count));
                });
            }
            catch (Exception ex)
            {
                Window.Dispatcher.Invoke(() =>
                {
                    statusTxt.Text = "Ошибка: " + ex.Message;
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
}
