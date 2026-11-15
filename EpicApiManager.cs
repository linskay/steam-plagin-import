using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using System.Security.Cryptography;

public class EpicApiManager
{
    const string ClientId = "34a02cf8f4414e29b15921876da36f9a";
    const string ClientSecret = "daafbccc737745039dffe53d94fc76cf";
    static readonly string ConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SteamEpicSync");
    static readonly string CredentialsPath = Path.Combine(ConfigDir, "credentials.json");
    static readonly string LibraryCachePath = Path.Combine(ConfigDir, "epic_library.json");
    readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 33554432 };
    DateTime expires = DateTime.MinValue;
    public string AccessToken { get; private set; }
    public string RefreshToken { get; private set; }
    public string AccountId { get; private set; }
    public string DisplayName { get; private set; }
    public bool IsLoggedIn { get { return !string.IsNullOrEmpty(RefreshToken); } }
    public string LastError { get; private set; }
    public bool LibraryIsFresh { get; private set; }
    public EpicApiManager()
    {
        ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
        try
        {
            if (!File.Exists(CredentialsPath)) return;
            byte[] bytes = File.ReadAllBytes(CredentialsPath);
            try { bytes = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser); } catch (CryptographicException) { }
            var data = Map(json.DeserializeObject(Encoding.UTF8.GetString(bytes)));
            RefreshToken = Text(data, "refresh_token"); AccountId = Text(data, "account_id"); DisplayName = Text(data, "displayName");
        }
        catch (Exception ex) { LastError = ex.Message; }
    }
    public static string GetLoginUrl() { return "https://www.epicgames.com/id/login?redirectUrl=" + Uri.EscapeDataString("https://www.epicgames.com/id/api/redirect?clientId=" + ClientId + "&responseType=code"); }
    public static string ParseAuthorizationCode(string input)
    {
        input = (input ?? "").Trim();
        if (input.StartsWith("{")) return Text(Map(new JavaScriptSerializer().DeserializeObject(input)), "authorizationCode");
        Uri uri;
        if (Uri.TryCreate(input, UriKind.Absolute, out uri))
        {
            foreach (string pair in uri.Query.TrimStart('?').Split('&'))
            {
                string[] parts = pair.Split(new char[] { '=' }, 2);
                if (parts.Length == 2 && (parts[0] == "code" || parts[0] == "authorizationCode")) return Uri.UnescapeDataString(parts[1]);
            }
            return "";
        }
        return System.Text.RegularExpressions.Regex.IsMatch(input, "^[a-zA-Z0-9_-]{16,512}$") ? input : "";
    }
    public bool LoginWithCode(string code) { return RequestTokens("grant_type=authorization_code&code=" + Uri.EscapeDataString(code)); }
    public bool RefreshSession() { return IsLoggedIn && RequestTokens("grant_type=refresh_token&refresh_token=" + Uri.EscapeDataString(RefreshToken)); }
    bool RequestTokens(string body)
    {
        try
        {
            var data = Map(json.DeserializeObject(Send("https://account-public-service-prod03.ol.epicgames.com/account/api/oauth/token", body + "&token_type=eg1", true, false)));
            if (Text(data, "access_token").Length == 0 || Text(data, "refresh_token").Length == 0) throw new InvalidDataException("Epic не вернул токены сессии.");
            AccessToken = Text(data, "access_token"); RefreshToken = Text(data, "refresh_token"); AccountId = Text(data, "account_id"); DisplayName = Text(data, "displayName");
            int seconds; expires = DateTime.UtcNow.AddSeconds(int.TryParse(Text(data, "expires_in"), out seconds) ? Math.Max(0, seconds - 60) : 300);
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllBytes(CredentialsPath, ProtectedData.Protect(Encoding.UTF8.GetBytes(json.Serialize(data)), null, DataProtectionScope.CurrentUser));
            LastError = null; return true;
        }
        catch (Exception ex) { LastError = ex.Message; return false; }
    }
    string Send(string url, string body, bool token, bool retry)
    {
        var request = (HttpWebRequest)WebRequest.Create(url);
        request.Timeout = request.ReadWriteTimeout = 20000;
        request.UserAgent = "UELauncher/11.0.1-14907503+++Portal+Release-Live Windows/10.0.19041.1.256.64bit";
        request.Headers["Authorization"] = token ? "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(ClientId + ":" + ClientSecret)) : "bearer " + AccessToken;
        if (body != null)
        {
            request.Method = "POST"; request.ContentType = "application/x-www-form-urlencoded";
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            using (var stream = request.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
        }
        try { using (var response = request.GetResponse()) using (var reader = new StreamReader(response.GetResponseStream())) return reader.ReadToEnd(); }
        catch (WebException ex)
        {
            var response = ex.Response as HttpWebResponse;
            if (!token && retry && response != null && response.StatusCode == HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                if (RefreshSession()) return Send(url, body, false, false);
                throw new InvalidOperationException("Сессия Epic истекла. Подключите аккаунт заново. " + LastError);
            }
            if (response != null)
            {
                using (response) using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var error = Map(json.DeserializeObject(reader.ReadToEnd()));
                    throw new InvalidOperationException("Epic HTTP " + (int)response.StatusCode + ": " + Text(error, "errorCode") + " " + Text(error, "errorMessage"));
                }
            }
            throw;
        }
    }
    public List<EpicGame> FetchLibrary()
    {
        LibraryIsFresh = false;
        try
        {
            if (!IsLoggedIn) return LoadCachedLibrary();
            if (DateTime.UtcNow >= expires && !RefreshSession()) return LoadCachedLibrary();
            var assets = Items(json.DeserializeObject(Send("https://launcher-public-service-prod06.ol.epicgames.com/launcher/api/public/assets/Windows?label=Live", null, false, true)));
            var games = new List<EpicGame>();
            foreach (var asset in assets)
            {
                string app = Text(asset, "appName"), ns = Text(asset, "namespace"), id = Text(asset, "catalogItemId");
                if (app.Length == 0 || id.Length == 0 || games.Exists(g => g.AppName == app)) continue;
                var catalog = Map(json.DeserializeObject(Send("https://catalog-public-service-prod06.ol.epicgames.com/catalog/api/shared/namespace/" + Uri.EscapeDataString(ns) + "/bulk/items?id=" + Uri.EscapeDataString(id) + "&includeMainGameDetails=true&locale=ru", null, false, true)));
                object item;
                if (!catalog.TryGetValue(id, out item)) throw new InvalidDataException("Epic не вернул описание " + app);
                var game = ParseCatalogItem(Map(item), app, ns, id);
                if (game != null) games.Add(game);
            }
            SteamStoreMatcher.Enrich(games, Path.Combine(ConfigDir, "steam-matches.json"));
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(LibraryCachePath, json.Serialize(games), Encoding.UTF8);
            LibraryIsFresh = true; LastError = null; return games;
        }
        catch (Exception ex) { LastError = ex.Message; return LoadCachedLibrary(); }
    }
    internal static EpicGame ParseCatalogItem(Dictionary<string, object> item, string app, string ns, string id)
    {
        if (ns == "ue" || app.StartsWith("UE_", StringComparison.OrdinalIgnoreCase)) return null;
        foreach (var category in Items(Value(item, "categories")))
        {
            string path = Text(category, "path").ToLowerInvariant();
            if (path.Contains("dlc") || path.Contains("addons") || path.Contains("editors") || path.Contains("plugins")) return null;
        }
        if (Text(item, "title").Length == 0) return null;
        var game = new EpicGame { AppName = app, DisplayName = Text(item, "title"), CatalogNamespace = ns, CatalogItemId = id, InstallLocation = "", LaunchExecutable = "", IsInstalled = false };
        string description = Text(item, "longDescription");
        if (string.IsNullOrWhiteSpace(description)) description = Text(item, "description");
        game.Description = System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(
            System.Text.RegularExpressions.Regex.Replace(description, @"<(br\s*/?|/p|/div)\s*>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase), "<[^>]*>", "")).Trim();
        game.Developer = Text(item, "developer");
        game.DescriptionSource = "Epic Games";
        game.Publisher = Text(item, "publisher");
        foreach (var image in Items(Value(item, "keyImages")))
        {
            string type = Text(image, "type").ToLowerInvariant(), url = Text(image, "url");
            if (type.Contains("tall") || type.Contains("vertical")) game.PortraitUrl = url;
            else if (type.Contains("wide") || type.Contains("horizontal")) game.LandscapeUrl = url;
            if (game.LandscapeUrl == null && type == "dieselgamebox") game.LandscapeUrl = url;
        }
        return game;
    }
    public List<EpicGame> LoadCachedLibrary()
    {
        try
        {
            if (File.Exists(LibraryCachePath))
            {
                var games = json.Deserialize<List<EpicGame>>(File.ReadAllText(LibraryCachePath, Encoding.UTF8)) ?? new List<EpicGame>();
                games.RemoveAll(g => g.CatalogNamespace == "ue" || (g.AppName ?? "").StartsWith("UE_", StringComparison.OrdinalIgnoreCase));
                return games;
            }
        }
        catch (Exception ex) { LastError = "Кэш повреждён: " + ex.Message; }
        return new List<EpicGame>();
    }
    public void Logout()
    {
        AccessToken = RefreshToken = AccountId = DisplayName = null; expires = DateTime.MinValue;
        if (File.Exists(CredentialsPath)) File.Delete(CredentialsPath);
        if (File.Exists(LibraryCachePath)) File.Delete(LibraryCachePath);
    }
    static object Value(Dictionary<string, object> map, string key) { object value; return map.TryGetValue(key, out value) ? value : null; }
    static Dictionary<string, object> Map(object value) { return value as Dictionary<string, object> ?? new Dictionary<string, object>(); }
    static string Text(Dictionary<string, object> map, string key) { return Convert.ToString(Value(map, key)) ?? ""; }
    static IEnumerable<Dictionary<string, object>> Items(object value)
    {
        var list = value as IEnumerable;
        if (list != null) foreach (object item in list) if (item is Dictionary<string, object>) yield return Map(item);
    }
}
