using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

public class EpicApiManager
{
    private const string ClientId = "34a2bf34190e4a38b15d2a7f51c0903f";
    private const string ClientSecret = "2514beb347a24b5fb40b596176840c6a";
    private const string AuthHeader = "Basic MzRhMmJmMzQxOTBlNGEzOGIxNWQyYTdmNTFjMDkwM2Y6MjUxNGJlYjM0N2EyNGI1ZmI0MGI1OTYxNzY4NDBjNmE=";

    private static readonly string ConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SteamEpicSync");
    private static readonly string CredentialsPath = Path.Combine(ConfigDir, "credentials.json");
    private static readonly string LibraryCachePath = Path.Combine(ConfigDir, "epic_library.json");

    public string AccessToken { get; private set; }
    public string RefreshToken { get; private set; }
    public string AccountId { get; private set; }
    public string DisplayName { get; private set; }
    public bool IsLoggedIn { get; private set; }

    public EpicApiManager()
    {
        LoadCredentials();
    }

    public static string GetLoginUrl()
    {
        return string.Format(
            "https://www.epicgames.com/id/login?redirectUrl=https%3A%2F%2Fwww.epicgames.com%2Fid%2Fapi%2Fredirect%3Fclient_id%3D{0}%26response_type%3Dcode",
            ClientId
        );
    }

    public bool LoginWithCode(string code)
    {
        try
        {
            string body = string.Format("grant_type=authorization_code&code={0}", code);
            return RequestTokens(body);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error logging in: " + ex.Message);
            return false;
        }
    }

    public bool RefreshSession()
    {
        if (string.IsNullOrEmpty(RefreshToken))
        {
            return false;
        }

        try
        {
            string body = string.Format("grant_type=refresh_token&refresh_token={0}", RefreshToken);
            return RequestTokens(body);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error refreshing session: " + ex.Message);
            Logout();
            return false;
        }
    }

    public void Logout()
    {
        AccessToken = null;
        RefreshToken = null;
        AccountId = null;
        DisplayName = null;
        IsLoggedIn = false;

        try
        {
            if (File.Exists(CredentialsPath))
            {
                File.Delete(CredentialsPath);
            }
            if (File.Exists(LibraryCachePath))
            {
                File.Delete(LibraryCachePath);
            }
        }
        catch { }
    }

    private bool RequestTokens(string requestBody)
    {
        try
        {
            var request = (HttpWebRequest)WebRequest.Create("https://account-public-service-prod03.ol.epicgames.com/account/api/oauth/token");
            request.Method = "POST";
            request.Headers["Authorization"] = AuthHeader;
            request.ContentType = "application/x-www-form-urlencoded";

            byte[] bytes = Encoding.UTF8.GetBytes(requestBody);
            request.ContentLength = bytes.Length;

            using (Stream reqStream = request.GetRequestStream())
            {
                reqStream.Write(bytes, 0, bytes.Length);
            }

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
            {
                string json = reader.ReadToEnd();
                
                AccessToken = ExtractJsonValue(json, "access_token");
                RefreshToken = ExtractJsonValue(json, "refresh_token");
                AccountId = ExtractJsonValue(json, "account_id");
                DisplayName = ExtractJsonValue(json, "displayName");

                if (!string.IsNullOrEmpty(AccessToken))
                {
                    IsLoggedIn = true;
                    SaveCredentials();
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Token request failed: " + ex.Message);
        }
        return false;
    }

    public List<EpicGame> FetchLibrary()
    {
        var games = new List<EpicGame>();
        if (!IsLoggedIn && !RefreshSession())
        {
            return LoadCachedLibrary();
        }

        try
        {
            // Set security protocol to TLS 1.2 which is required by Epic Games APIs
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2

            string url = string.Format("https://entitlement-public-service-prod.ol.epicgames.com/entitlement/api/public/noauth/accounts/{0}/entitlements?start=0&count=5000", AccountId);
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Headers["Authorization"] = "bearer " + AccessToken;

            string json = "";
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
            {
                json = reader.ReadToEnd();
            }

            // Extract all entitlements (namespaces and catalogItemIds)
            var entitlements = new List<Entitlement>();
            var blocks = Regex.Matches(json, "\\{[^}]*\\}");
            foreach (Match block in blocks)
            {
                string blockText = block.Value;
                string itemId = ExtractJsonValue(blockText, "catalogItemId");
                string ns = ExtractJsonValue(blockText, "namespace");
                if (!string.IsNullOrEmpty(itemId) && !string.IsNullOrEmpty(ns))
                {
                    entitlements.Add(new Entitlement { CatalogItemId = itemId, Namespace = ns });
                }
            }

            // Group entitlements by namespace to minimize bulk API queries
            var grouped = new Dictionary<string, List<string>>();
            foreach (var ent in entitlements)
            {
                if (!grouped.ContainsKey(ent.Namespace))
                {
                    grouped[ent.Namespace] = new List<string>();
                }
                grouped[ent.Namespace].Add(ent.CatalogItemId);
            }

            // Query catalog in batches for each namespace
            foreach (var kvp in grouped)
            {
                string ns = kvp.Key;
                List<string> ids = kvp.Value;

                // Group IDs in chunks of 50 to avoid URL length issues
                for (int i = 0; i < ids.Count; i += 50)
                {
                    var chunk = ids.GetRange(i, Math.Min(50, ids.Count - i));
                    
                    StringBuilder urlBuilder = new StringBuilder();
                    urlBuilder.AppendFormat("https://catalog-public-service-prod06.ol.epicgames.com/catalog/api/shared/namespace/{0}/bulk/items?", ns);
                    for (int j = 0; j < chunk.Count; j++)
                    {
                        if (j > 0) urlBuilder.Append("&");
                        urlBuilder.Append("id=").Append(chunk[j]);
                    }

                    try
                    {
                        var catalogRequest = (HttpWebRequest)WebRequest.Create(urlBuilder.ToString());
                        catalogRequest.Method = "GET";
                        catalogRequest.Headers["Authorization"] = "bearer " + AccessToken;

                        using (var catalogResponse = (HttpWebResponse)catalogRequest.GetResponse())
                        using (var reader = new StreamReader(catalogResponse.GetResponseStream(), Encoding.UTF8))
                        {
                            string catalogJson = reader.ReadToEnd();
                            ParseCatalogItems(catalogJson, ns, games);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to fetch catalog chunk for namespace " + ns + ": " + ex.Message);
                    }
                }
            }

            if (games.Count > 0)
            {
                SaveLibraryCache(games);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error fetching library: " + ex.Message);
            return LoadCachedLibrary();
        }

        return games;
    }

    private void ParseCatalogItems(string json, string ns, List<EpicGame> games)
    {
        // Each catalog item is in a JSON block
        var blocks = Regex.Matches(json, "\\{[^}]*\\}");
        foreach (Match block in blocks)
        {
            string blockText = block.Value;
            string id = ExtractJsonValue(blockText, "id");
            string title = ExtractJsonValue(blockText, "title");
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(title))
            {
                continue;
            }

            // Exclude items that are DLCs or non-game content by checking title patterns or common namespaces
            if (title.Contains("DLC") || title.Contains("Add-On") || title.Contains("Content Pack") || ns.Contains("editor") || title.Contains("Season Pass"))
            {
                continue;
            }

            // Extract AppName from customAttributes
            string appName = "";
            int idx = blockText.IndexOf("\"LaunchAppName\"");
            if (idx != -1)
            {
                string subText = blockText.Substring(idx);
                appName = ExtractJsonValue(subText, "value");
            }
            else
            {
                // Fallback to other possible fields
                int appNameIdx = blockText.IndexOf("\"AppName\"");
                if (appNameIdx != -1)
                {
                    appName = ExtractJsonValue(blockText.Substring(appNameIdx), "value");
                }
            }

            if (string.IsNullOrEmpty(appName))
            {
                // If we can't find an AppName, it might not be a launchable game, or it is a library entry
                continue;
            }

            // Prevent duplicate entries
            bool exists = false;
            foreach (var g in games)
            {
                if (g.AppName.Equals(appName, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                games.Add(new EpicGame
                {
                    AppName = appName,
                    DisplayName = title,
                    InstallLocation = "", // Remote, not installed locally
                    LaunchExecutable = "",
                    CatalogNamespace = ns,
                    CatalogItemId = id,
                    IsInstalled = false
                });
            }
        }
    }

    private void SaveCredentials()
    {
        try
        {
            if (!Directory.Exists(ConfigDir))
            {
                Directory.CreateDirectory(ConfigDir);
            }

            string json = string.Format(
                "{{\n  \"access_token\": \"{0}\",\n  \"refresh_token\": \"{1}\",\n  \"account_id\": \"{2}\",\n  \"displayName\": \"{3}\"\n}}",
                AccessToken, RefreshToken, AccountId, DisplayName
            );

            byte[] plaintextBytes = Encoding.UTF8.GetBytes(json);
            byte[] encryptedBytes = System.Security.Cryptography.ProtectedData.Protect(
                plaintextBytes,
                null,
                System.Security.Cryptography.DataProtectionScope.CurrentUser
            );

            File.WriteAllBytes(CredentialsPath, encryptedBytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to save credentials: " + ex.Message);
        }
    }

    private void LoadCredentials()
    {
        try
        {
            if (File.Exists(CredentialsPath))
            {
                byte[] encryptedBytes = File.ReadAllBytes(CredentialsPath);
                string json = null;

                try
                {
                    byte[] plaintextBytes = System.Security.Cryptography.ProtectedData.Unprotect(
                        encryptedBytes,
                        null,
                        System.Security.Cryptography.DataProtectionScope.CurrentUser
                    );
                    json = Encoding.UTF8.GetString(plaintextBytes);
                }
                catch
                {
                    // Fallback to reading as plaintext in case it's an old unencrypted file
                    try
                    {
                        json = Encoding.UTF8.GetString(encryptedBytes);
                    }
                    catch { }
                }

                if (!string.IsNullOrEmpty(json))
                {
                    AccessToken = ExtractJsonValue(json, "access_token");
                    RefreshToken = ExtractJsonValue(json, "refresh_token");
                    AccountId = ExtractJsonValue(json, "account_id");
                    DisplayName = ExtractJsonValue(json, "displayName");

                    if (!string.IsNullOrEmpty(RefreshToken))
                    {
                        IsLoggedIn = true;
                    }
                }
            }
        }
        catch
        {
            IsLoggedIn = false;
        }
    }

    private List<EpicGame> LoadCachedLibrary()
    {
        var list = new List<EpicGame>();
        try
        {
            if (File.Exists(LibraryCachePath))
            {
                string json = File.ReadAllText(LibraryCachePath, Encoding.UTF8);
                var matches = Regex.Matches(json, "\\{[^}]*\\}");
                foreach (Match match in matches)
                {
                    string text = match.Value;
                    string appName = ExtractJsonValue(text, "AppName");
                    string displayName = ExtractJsonValue(text, "DisplayName");
                    if (!string.IsNullOrEmpty(appName) && !string.IsNullOrEmpty(displayName))
                    {
                        list.Add(new EpicGame
                        {
                            AppName = appName,
                            DisplayName = displayName,
                            IsInstalled = false
                        });
                    }
                }
            }
        }
        catch { }
        return list;
    }

    private void SaveLibraryCache(List<EpicGame> games)
    {
        try
        {
            if (!Directory.Exists(ConfigDir))
            {
                Directory.CreateDirectory(ConfigDir);
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("[\n");
            for (int i = 0; i < games.Count; i++)
            {
                sb.AppendFormat("  {{\n    \"AppName\": \"{0}\",\n    \"DisplayName\": \"{1}\"\n  }}", 
                    games[i].AppName.Replace("\"", "\\\""), 
                    games[i].DisplayName.Replace("\"", "\\\""));
                
                if (i < games.Count - 1) sb.Append(",\n");
                else sb.Append("\n");
            }
            sb.Append("]");

            File.WriteAllText(LibraryCachePath, sb.ToString(), Encoding.UTF8);
        }
        catch { }
    }

    private static string ExtractJsonValue(string json, string key)
    {
        var match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
        return match.Success ? match.Groups[1].Value : "";
    }

    private class Entitlement
    {
        public string Namespace { get; set; }
        public string CatalogItemId { get; set; }
    }
}
