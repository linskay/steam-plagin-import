using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

public class LoginWindow : Window
{
    private WebBrowser webBrowser;
    public string AuthorizationCode { get; private set; }

    public LoginWindow()
    {
        Title = "Вход в аккаунт Epic Games";
        Width = 520;
        Height = 680;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;

        webBrowser = new WebBrowser();
        webBrowser.Navigating += WebBrowser_Navigating;
        
        // Suppress script error popups
        webBrowser.Navigated += (s, e) =>
        {
            try
            {
                var fi = typeof(WebBrowser).GetField("_axIWebBrowser2", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (fi != null)
                {
                    object axIWebBrowser2 = fi.GetValue(webBrowser);
                    if (axIWebBrowser2 != null)
                    {
                        axIWebBrowser2.GetType().InvokeMember("Silent", 
                            System.Reflection.BindingFlags.SetProperty, null, axIWebBrowser2, new object[] { true });
                    }
                }
            }
            catch { }
        };

        Content = webBrowser;

        // Force navigating to Epic login page
        webBrowser.Navigate(EpicApiManager.GetLoginUrl());
    }

    private void WebBrowser_Navigating(object sender, NavigatingCancelEventArgs e)
    {
        if (e.Uri == null) return;

        string url = e.Uri.ToString();
        if (url.Contains("https://www.epicgames.com/id/api/redirect"))
        {
            var query = e.Uri.Query;
            var match = System.Text.RegularExpressions.Regex.Match(query, "[?&]code=([^&]+)");
            if (match.Success)
            {
                AuthorizationCode = match.Groups[1].Value;
                DialogResult = true;
                Close();
            }
        }
    }
}
