using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

public static class SteamUiSnapshot
{
    public static void Save(string steamPath, string userId, string quotedExe, List<EpicGame> games)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(userId, @"^\d+$")) throw new ArgumentException("Invalid Steam account ID.");
        var entries = new List<object>();
        foreach (var game in games)
            entries.Add(new {
                appId = unchecked((uint)EpicSyncManager.CalculateAppId(quotedExe, game.DisplayName)).ToString(System.Globalization.CultureInfo.InvariantCulture),
                name = game.DisplayName,
                steamAppId = game.SteamAppId ?? "",
                installed = game.IsInstalled,
                description = game.Description ?? "",
                descriptionSource = game.DescriptionSource ?? "Epic Games",
                developer = game.Developer ?? "",
                publisher = game.Publisher ?? ""
            });
        string directory = Path.Combine(steamPath, "config", "SteamEpicSync");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, userId + ".json");
        string content = new JavaScriptSerializer { MaxJsonLength = 33554432 }.Serialize(new {
            schemaVersion = 1, steamAccountId = userId, syncedAt = DateTime.UtcNow.ToString("o"), games = entries
        });
        File.WriteAllText(path + ".tmp", content, new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak");
        else File.Move(path + ".tmp", path);
        var settings = AppSettings.Load();
        settings.ExePath = quotedExe.Trim('"');
        settings.Save();
        // Synchronization already requires Steam to be closed, so the module can be updated safely here.
        string packagedModule = Path.Combine(Path.GetDirectoryName(quotedExe.Trim('"')), "millennium-plugin", "dist", "com.linskay.steamepicsync.star");
        if (File.Exists(Path.Combine(steamPath, "millennium", "lib", "millennium.dll")) && File.Exists(packagedModule))
        {
            string plugins = Path.Combine(steamPath, "millennium", "plugins");
            Directory.CreateDirectory(plugins);
            File.Copy(packagedModule, Path.Combine(plugins, "com.linskay.steamepicsync.star"), true);
        }
    }
}
