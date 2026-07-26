using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

public class LoginWindow : Window
{
    public string AuthorizationCode { get; private set; }
    private TextBox codeTextBox;
    private string loginUrl;

    public LoginWindow()
    {
        Title = "Подключение Epic Games";
        Width = 480;
        Height = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;

        loginUrl = EpicApiManager.GetLoginUrl();

        // Create main layout
        var border = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Color.FromRgb(0x12, 0x13, 0x1A)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x22, 0x24, 0x30)),
            BorderThickness = new Thickness(1.5),
            Padding = new Thickness(25)
        };

        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Info & Input
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

        // 1. Title Bar
        var titleTxt = new TextBlock
        {
            Text = "ПОДКЛЮЧЕНИЕ EPIC GAMES",
            Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xC8, 0x53)),
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 15)
        };
        Grid.SetRow(titleTxt, 0);
        mainGrid.Children.Add(titleTxt);

        // 2. Info / Content
        var contentStack = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
        
        var descTxt1 = new TextBlock
        {
            Text = "1. Мы открыли страницу входа Epic Games в вашем браузере по умолчанию.",
            Foreground = Brushes.White,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        contentStack.Children.Add(descTxt1);

        var descTxt2 = new TextBlock
        {
            Text = "2. Войдите в свой аккаунт. После этого вас перенаправит на пустую страницу.",
            Foreground = Brushes.White,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        contentStack.Children.Add(descTxt2);

        var descTxt3 = new TextBlock
        {
            Text = "3. Скопируйте всю адресную строку из браузера (или значение параметра code=...) и вставьте в поле ниже:",
            Foreground = Brushes.White,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };
        contentStack.Children.Add(descTxt3);

        // Manual open link button
        var openBrowserBtn = new Button
        {
            Content = "Открыть страницу авторизации повторно",
            Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x38)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x42, 0x57)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 6, 10, 6),
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 15)
        };
        openBrowserBtn.Click += (s, e) => OpenAuthUrl();
        contentStack.Children.Add(openBrowserBtn);

        // Textbox wrapper
        var tbBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x16, 0x18, 0x22)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x22, 0x24, 0x30)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8)
        };

        codeTextBox = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = Brushes.White,
            FontSize = 12,
            CaretBrush = Brushes.White
        };
        tbBorder.Child = codeTextBox;
        contentStack.Children.Add(tbBorder);

        Grid.SetRow(contentStack, 1);
        mainGrid.Children.Add(contentStack);

        // 3. Actions (Buttons)
        var btnStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancelBtn = new Button
        {
            Content = "Отмена",
            Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2C, 0x38)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x42, 0x57)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(15, 8, 15, 8),
            Margin = new Thickness(0, 0, 10, 0),
            Cursor = Cursors.Hand
        };
        cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };
        btnStack.Children.Add(cancelBtn);

        var loginBtn = new Button
        {
            Content = "Войти",
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(25, 8, 25, 8),
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand
        };
        loginBtn.Click += (s, e) => ConfirmLogin();
        btnStack.Children.Add(loginBtn);

        Grid.SetRow(btnStack, 2);
        mainGrid.Children.Add(btnStack);

        border.Child = mainGrid;
        Content = border;

        // Auto open auth URL on load
        Loaded += (s, e) => OpenAuthUrl();
    }

    private void OpenAuthUrl()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = loginUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Не удалось открыть браузер: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ConfirmLogin()
    {
        string input = codeTextBox.Text.Trim();
        if (string.IsNullOrEmpty(input))
        {
            MessageBox.Show(this, "Пожалуйста, вставьте скопированную ссылку или код авторизации.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Try to extract code from JSON or URL
        string code = input;
        
        // 1. Try to extract from JSON authorizationCode or exchangeCode field
        var jsonMatch = Regex.Match(input, "\"authorizationCode\"\\s*:\\s*\"([a-zA-Z0-9]+)\"");
        if (jsonMatch.Success)
        {
            code = jsonMatch.Groups[1].Value;
        }
        else
        {
            // 2. Try URL parameter code (ignoring quotes and other delimiters)
            var urlMatch = Regex.Match(input, "[?&]code=([a-zA-Z0-9]+)");
            if (urlMatch.Success)
            {
                code = urlMatch.Groups[1].Value;
            }
        }

        // Clean up code from any surrounding quotes or spaces
        code = code.Trim('"', '\'', ' ', '\t', '\r', '\n');

        if (code.Length < 10)
        {
            MessageBox.Show(this, "Похоже, код введен неверно. Пожалуйста, скопируйте всю адресную строку страницы перенаправления.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        AuthorizationCode = code;
        DialogResult = true;
        Close();
    }
}
