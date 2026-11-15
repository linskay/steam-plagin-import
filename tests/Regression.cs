using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Web.Script.Serialization;
class Regression
{
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    static void Main(string[] args)
    {
        string dir = args[0]; Directory.CreateDirectory(dir);
        AppSettings.PathOverride = Path.Combine(dir, "settings.json");
        Check(EpicApiManager.ParseAuthorizationCode("{\"authorizationCode\":\"test-code\"}") == "test-code", "JSON login code");
        Check(EpicApiManager.ParseAuthorizationCode("https://www.epicgames.com/?code=a%2Bb") == "a+b", "URL login code");
        Check(EpicApiManager.ParseAuthorizationCode("https://www.epicgames.com/id/login") == "", "Reject login URL without code");
        var json = new JavaScriptSerializer();
        var item = json.Deserialize<Dictionary<string, object>>("{\"title\":\"Game \\\"Deluxe\\\"\",\"categories\":[{\"path\":\"games\"}],\"customAttributes\":{\"Other\":{\"value\":\"nested\"}},\"keyImages\":[{\"type\":\"DieselGameBoxTall\",\"url\":\"https://example.com/portrait.jpg\"},{\"type\":\"DieselGameBox\",\"url\":\"https://example.com/wide.jpg\"}]}");
        var game = EpicApiManager.ParseCatalogItem(item, "app", "namespace", "catalog");
        Check(game != null && game.DisplayName == "Game \"Deluxe\"" && !game.IsInstalled, "Nested JSON and uninstalled game");
        Check(game.PortraitUrl.EndsWith("portrait.jpg") && game.LandscapeUrl.EndsWith("wide.jpg"), "Artwork selection");
        var restored = json.Deserialize<EpicGame>(json.Serialize(game));
        Check(restored.CatalogNamespace == "namespace" && restored.CatalogItemId == "catalog" && restored.AppName == "app", "Cache retains launch identity");
        item["categories"] = json.DeserializeObject("[{\"path\":\"addons\"}]");
        Check(EpicApiManager.ParseCatalogItem(item, "dlc", "ns", "id") == null, "DLC excluded");
        var shortcut = new SteamShortcut(); shortcut.PopulateDefaults(); shortcut.AppName = "Existing \"Game\""; shortcut.AppId = unchecked((int)0x81234567);
        shortcut.Fields["tags"] = new Dictionary<string, object> { { "0", "Epic Games" } };
        string vdf = Path.Combine(dir, "shortcuts.vdf");
        VdfParser.WriteShortcuts(vdf, new List<SteamShortcut> { shortcut });
        // Validate the whole Binary KeyValues document independently of ReadShortcuts.
        byte[] document = File.ReadAllBytes(vdf);
        int position = 0;
        ReadDocument(document, ref position);
        Check(position == document.Length, "Complete Binary KeyValues document including outer terminator");
        var shortcuts = VdfParser.ReadShortcuts(vdf);
        Check(shortcuts.Count == 1 && shortcuts[0].AppName == shortcut.AppName && shortcuts[0].AppId == shortcut.AppId, "VDF round trip preserves existing shortcut");
        VdfParser.WriteShortcuts(vdf, shortcuts);
        Check(File.Exists(vdf + ".bak"), "Backup before replacement");
        File.WriteAllText(vdf, "corrupt"); bool rejected = false;
        try { VdfParser.ReadShortcuts(vdf); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "Corrupt VDF blocks overwrite");
        game.PortraitUrl = game.LandscapeUrl = null;
        SteamArtwork.Save(dir, 123, game);
        using (var image = Image.FromFile(Path.Combine(dir, "123p.png"))) Check(image.Width == 600 && image.Height == 900, "Portrait generated");
        using (var image = Image.FromFile(Path.Combine(dir, "123.png"))) Check(image.Width == 920 && image.Height == 430, "Landscape generated");
        using (var image = Image.FromFile(Path.Combine(dir, "123_hero.png"))) Check(image.Width == 1920 && image.Height == 620, "Hero generated");
        using (var icon = new Icon(SteamArtwork.IconPath(dir, 123))) Check(icon.Width == 64 && icon.Height == 64, "Native ICO thumbnail generated for uninstalled game");
        shortcut.Icon = SteamArtwork.IconPath(dir, 123);
        VdfParser.WriteShortcuts(vdf, new List<SteamShortcut> { shortcut });
        Check(VdfParser.ReadShortcuts(vdf)[0].Icon == shortcut.Icon, "Thumbnail path persists in Steam shortcut");
        string config = Path.Combine(dir, "config");
        Directory.CreateDirectory(Path.Combine(config, "cloudstorage"));
        string collectionPath = Path.Combine(config, "cloudstorage", "cloud-storage-namespace-1.json");
        File.WriteAllText(collectionPath, "[[\"other-setting\",{\"key\":\"other-setting\",\"value\":\"preserve-me\"}]]");
        string localPath = Path.Combine(config, "localconfig.vdf");
        File.WriteAllText(localPath, "\\\"bSortedByRecent\\\":true,\\\"bGroupedByCollection\\\":false");
        SteamCollection.Update(config, new List<SteamShortcut> { shortcut });
        var entries = json.Deserialize<List<object[]>>(File.ReadAllText(collectionPath));
        Check(entries.Count == 2 && Convert.ToString(entries[0][0]) == "other-setting", "Collection update preserves unrelated Steam settings");
        var metadata = (Dictionary<string, object>)entries[1][1];
        var collection = json.Deserialize<Dictionary<string, object>>(Convert.ToString(metadata["value"]));
        Check(Convert.ToString(collection["name"]) == SteamCollection.Name && Convert.ToString(collection["added"]).Length > 0, "EGS collection created in code");
        Check(File.ReadAllText(collectionPath).Contains(unchecked((uint)shortcut.AppId).ToString()), "Collection uses unsigned Steam shortcut IDs");
        Check(File.ReadAllText(localPath).Contains("\\\"bSortedByRecent\\\":false"), "Collection sorting replaces activity grouping");
        SteamCollection.Update(config, new List<SteamShortcut> { shortcut });
        Check(json.Deserialize<List<object[]>>(File.ReadAllText(collectionPath)).Count == 2, "Collection sync is idempotent");
        string dirtyPath = Path.Combine(config, "cloudstorage", "cloud-storage-namespace-1.modified.json");
        Check(json.Deserialize<List<string>>(File.ReadAllText(dirtyPath)).Count == 1, "Collection queued for Steam cloud sync");
        var configSettings = AppSettings.Load();
        Check(configSettings.LaunchWithSteam && !configSettings.SyncOnSteamStart && configSettings.CollectionName == "Есть в EGS", "Default Steam startup settings");
        configSettings.LaunchWithSteam = false; configSettings.SyncOnSteamStart = true; configSettings.CollectionName = "Мои игры Epic"; configSettings.Save();
        Check(!AppSettings.Load().LaunchWithSteam && AppSettings.Load().SyncOnSteamStart, "Startup options persist");
        string collectionId = Convert.ToString(collection["id"]);
        SteamCollection.Update(config, new List<SteamShortcut> { shortcut });
        entries = json.Deserialize<List<object[]>>(File.ReadAllText(collectionPath));
        collection = json.Deserialize<Dictionary<string, object>>(Convert.ToString(((Dictionary<string, object>)entries[1][1])["value"]));
        Check(entries.Count == 2 && Convert.ToString(collection["id"]) == collectionId && Convert.ToString(collection["name"]) == "Мои игры Epic", "Renaming preserves collection identity without duplicates");
        configSettings.CollectionName = "Ещё одно название"; configSettings.Save(); SteamCollection.Update(config, new List<SteamShortcut> { shortcut });
        Check(json.Deserialize<List<object[]>>(File.ReadAllText(collectionPath)).Count == 2, "Repeated renames reuse the same collection");
        bool invalidName = false; try { AppSettings.ValidateName("  "); } catch (ArgumentException) { invalidName = true; }
        Check(invalidName, "Empty collection names are rejected");
        Check(SteamSession.ShouldExit(true, false, false, false) && !SteamSession.ShouldExit(false, false, false, false) && !SteamSession.ShouldExit(true, false, true, false), "Steam shutdown exits utility except during managed restart or standalone setup");
        string skipPath = Path.Combine(dir, "startup-skip.txt"); File.WriteAllText(skipPath, DateTime.UtcNow.AddMinutes(1).Ticks.ToString());
        Check(SteamSession.ConsumeStartupSkip() && !SteamSession.ConsumeStartupSkip(), "Startup sync restart token is consumed exactly once");
        game.Description = "<script>this must remain plain text</script>";
        game.Developer = "Developer"; game.Publisher = "Publisher"; game.SteamAppId = "1206060";
        SteamUiSnapshot.Save(dir, "12345", "\"C:\\SteamEpicSync.exe\"", new List<EpicGame> { game });
        string snapshotText = File.ReadAllText(Path.Combine(dir, "config", "SteamEpicSync", "12345.json"));
        var snapshotData = json.Deserialize<Dictionary<string, object>>(snapshotText);
        Check(Convert.ToString(snapshotData["steamAccountId"]) == "12345" && snapshotText.Contains("1206060") && snapshotText.Contains("installed"), "Snapshot includes installation state, store ID and account isolation");
        Check(!snapshotText.Contains("access_token") && !snapshotText.Contains("refresh_token") && !snapshotText.Contains("InstallLocation"), "UI snapshot excludes credentials and local game paths");
        var candidates = json.DeserializeObject("[{\"id\":1206060,\"name\":\"Happy Game\"},{\"id\":42,\"name\":\"Happy Game Deluxe\"}]") as object[];
        Check(SteamStoreMatcher.Match("Happy Game™", candidates) == "1206060", "Store matcher accepts exact title and ignores trademark");
        Check(SteamStoreMatcher.Match("Happy", candidates) == "", "Store matcher rejects partial titles");
        Check(SteamStoreMatcher.Match("Happy Game", json.DeserializeObject("[{\"id\":1,\"name\":\"Happy Game\"},{\"id\":2,\"name\":\"Happy Game\"}]") as object[]) == "", "Store matcher rejects ambiguous titles");
        string descriptionCache = Path.Combine(dir, "description-cache.json");
        File.WriteAllText(descriptionCache, "{\"1206060\":{\"Description\":\"A real game description\",\"Developer\":\"Studio\",\"Publisher\":\"Publisher\"}}");
        game.Description = game.DisplayName;
        SteamStoreMatcher.EnrichDescriptions(new List<EpicGame> { game }, descriptionCache);
        Check(game.Description == "A real game description" && game.DescriptionSource == "Steam Store", "Placeholder descriptions use labeled cached Steam metadata without network");
        Console.WriteLine("All regression checks passed.");
    }
    static void ReadDocument(byte[] bytes, ref int position)
    {
        while (position < bytes.Length)
        {
            byte type = bytes[position++];
            if (type == 8) return;
            ReadString(bytes, ref position);
            if (type == 0) ReadDocument(bytes, ref position);
            else if (type == 1) ReadString(bytes, ref position);
            else if (type == 2) position += 4;
            else if (type == 7) position += 8;
            else throw new Exception("Unknown Binary KeyValues type");
            if (position > bytes.Length) throw new Exception("Truncated value");
        }
        throw new Exception("Missing Binary KeyValues document terminator");
    }
    static void ReadString(byte[] bytes, ref int position)
    {
        while (position < bytes.Length) if (bytes[position++] == 0) return;
        throw new Exception("Unterminated Binary KeyValues string");
    }
}
