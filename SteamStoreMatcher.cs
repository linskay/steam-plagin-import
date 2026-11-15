using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

public static class SteamStoreMatcher
{
    public static string Normalize(string title)
    {
        return Regex.Replace(Regex.Replace(title ?? "", "[™®©]", "").Normalize(NormalizationForm.FormKC).ToLowerInvariant(), @"[\s\p{P}\p{S}]+", "");
    }
    public static string Match(string title, System.Collections.IEnumerable items)
    {
        string id = null;
        foreach (object item in items)
        {
            var candidate = item as Dictionary<string, object>;
            if (candidate == null || !candidate.ContainsKey("name") || !candidate.ContainsKey("id")) continue;
            if (Normalize(title) != Normalize(Convert.ToString(candidate["name"]))) continue;
            string next = Convert.ToString(candidate["id"]);
            if (!Regex.IsMatch(next, @"^\d+$")) continue;
            if (id != null && id != next) return ""; // Ambiguous names never get an ownership badge.
            id = next;
        }
        return id ?? "";
    }
    public static void Enrich(List<EpicGame> games, string cachePath)
    {
        var json = new JavaScriptSerializer { MaxJsonLength = 33554432 };
        var matches = new Dictionary<string, string>();
        try { if (File.Exists(cachePath)) matches = json.Deserialize<Dictionary<string, string>>(File.ReadAllText(cachePath, Encoding.UTF8)) ?? matches; } catch { }
        int failures = 0;
        foreach (var game in games)
        {
            string key = Normalize(game.DisplayName), match;
            if (matches.TryGetValue(key, out match)) { game.SteamAppId = match; continue; }
            if (failures >= 3) continue; // Store outage must not prevent library synchronization.
            try
            {
                var request = (HttpWebRequest)WebRequest.Create("https://store.steampowered.com/api/storesearch/?term=" + Uri.EscapeDataString(game.DisplayName) + "&l=english&cc=us");
                request.Timeout = request.ReadWriteTimeout = 10000;
                request.UserAgent = "SteamEpicSync/1.0";
                using (var response = request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var result = json.Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
                    object items;
                    if (result == null || !result.TryGetValue("items", out items)) continue;
                    game.SteamAppId = Match(game.DisplayName, items as System.Collections.IEnumerable ?? new object[0]);
                    if (!string.IsNullOrEmpty(game.SteamAppId)) matches[key] = game.SteamAppId;
                }
                failures = 0;
            }
            catch { failures++; }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
        File.WriteAllText(cachePath, json.Serialize(matches), new UTF8Encoding(false));
        EnrichDescriptions(games, cachePath + ".descriptions.json");
    }
    public class Metadata
    {
        public string Description { get; set; }
        public string Developer { get; set; }
        public string Publisher { get; set; }
    }
    public static void EnrichDescriptions(List<EpicGame> games, string path)
    {
        var json = new JavaScriptSerializer { MaxJsonLength = 33554432 };
        var cache = new Dictionary<string, Metadata>();
        try { if (File.Exists(path)) cache = json.Deserialize<Dictionary<string, Metadata>>(File.ReadAllText(path, Encoding.UTF8)) ?? cache; } catch { }
        int failures = 0;
        foreach (var game in games)
        {
            if (!string.IsNullOrWhiteSpace(game.Description) && Normalize(game.Description) != Normalize(game.DisplayName)) continue;
            game.Description = "";
            if (string.IsNullOrEmpty(game.SteamAppId)) continue;
            Metadata data;
            if (!cache.TryGetValue(game.SteamAppId, out data) && failures < 3)
            {
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create("https://store.steampowered.com/api/appdetails?appids=" + game.SteamAppId + "&l=russian");
                    request.Timeout = request.ReadWriteTimeout = 10000;
                    using (var response = request.GetResponse())
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        var root = json.Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
                        var result = root[game.SteamAppId] as Dictionary<string, object>;
                        if (result == null || !Convert.ToBoolean(result["success"])) continue;
                        var details = result["data"] as Dictionary<string, object>;
                        object raw;
                        if (details == null || !details.TryGetValue("about_the_game", out raw)) continue;
                        string text = WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(Convert.ToString(raw), @"<(br\s*/?|/p|/div|/h[1-6]|/li)\s*>", "\n", RegexOptions.IgnoreCase), "<[^>]*>", "")).Trim();
                        if (text.Length > 24000) text = text.Substring(0, 24000);
                        data = new Metadata { Description = text, Developer = First(details, "developers"), Publisher = First(details, "publishers") };
                        cache[game.SteamAppId] = data;
                    }
                    failures = 0;
                }
                catch { failures++; }
            }
            if (data == null) continue;
            game.Description = data.Description;
            game.DescriptionSource = "Steam Store";
            if (!string.IsNullOrEmpty(data.Developer)) game.Developer = data.Developer;
            if (!string.IsNullOrEmpty(data.Publisher)) game.Publisher = data.Publisher;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, json.Serialize(cache), new UTF8Encoding(false));
    }
    private static string First(Dictionary<string, object> map, string key)
    {
        object value;
        if (map.TryGetValue(key, out value) && value is System.Collections.IEnumerable)
            foreach (object entry in (System.Collections.IEnumerable)value) return Convert.ToString(entry);
        return "";
    }
}
