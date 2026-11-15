using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
public class LoginWindow : Window
{
    public string AuthorizationCode { get; private set; }
    public LoginWindow()
    {
        Title = "Подключение Epic Games"; Width = 560; Height = 330;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "1. Откройте Epic в браузере и войдите.\n2. Скопируйте authorizationCode из ответа страницы.\n3. Вставьте код или весь JSON ниже. Код одноразовый.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
        var open = new Button { Content = "Открыть вход Epic Games", Margin = new Thickness(0, 0, 0, 16) };
        open.Click += (s, e) => { try { Process.Start(new ProcessStartInfo(EpicApiManager.GetLoginUrl()) { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(this, ex.Message); } };
        panel.Children.Add(open);
        var input = new TextBox { Height = 60, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true }; panel.Children.Add(input);
        var submit = new Button { Content = "Подключить", Margin = new Thickness(0, 16, 0, 0) };
        submit.Click += (s, e) => {
            try {
                string code = EpicApiManager.ParseAuthorizationCode(input.Text);
                if (string.IsNullOrEmpty(code)) throw new FormatException("Вставьте authorizationCode, JSON ответа или адрес с параметром code.");
                AuthorizationCode = code; DialogResult = true;
            } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Проверьте код"); }
        };
        panel.Children.Add(submit); Content = panel;
    }
}
