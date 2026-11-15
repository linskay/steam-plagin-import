using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

public static class SteamCollection
{
    public static string Name { get { return AppSettings.Load().CollectionName; } }
    // Steam stores modern collections separately from legacy shortcuts.vdf tags.
    public static void Update(string configDir, List<SteamShortcut> managedShortcuts)
    {
        var json = new JavaScriptSerializer { MaxJsonLength = 33554432 };
        string directory = Path.Combine(configDir, "cloudstorage");
        string path = Path.Combine(directory, "cloud-storage-namespace-1.json");
        string modifiedPath = Path.Combine(directory, "cloud-storage-namespace-1.modified.json");
        var entries = File.Exists(path) ? json.Deserialize<List<object[]>>(File.ReadAllText(path, Encoding.UTF8)) : new List<object[]>();
        var dirty = File.Exists(modifiedPath) ? json.Deserialize<List<string>>(File.ReadAllText(modifiedPath, Encoding.UTF8)) : new List<string>();
        if (entries == null || dirty == null) throw new InvalidDataException("Неизвестный формат коллекций Steam.");
        Dictionary<string, object> metadata = null;
        string id = "uc-SteamEpicSync";
        string marker = Path.Combine(configDir, "SteamEpicSync.collection-id");
        string managedId = File.Exists(marker) ? File.ReadAllText(marker, Encoding.UTF8).Trim() : "uc-SteamEpicSync";
        string desiredName = Name;
        foreach (object[] entry in entries)
        {
            if (entry.Length != 2 || !Convert.ToString(entry[0]).StartsWith("user-collections.")) continue;
            var candidate = entry[1] as Dictionary<string, object>;
            object value;
            if (candidate == null || !candidate.TryGetValue("value", out value) || candidate.ContainsKey("is_deleted")) continue;
            var collection = json.Deserialize<Dictionary<string, object>>(Convert.ToString(value));
            object name;
            if (collection != null && collection.TryGetValue("name", out name) &&
                (Convert.ToString(collection["id"]) == managedId || (!File.Exists(marker) && (Convert.ToString(name) == desiredName || Convert.ToString(name) == "Есть в EGS"))))
            {
                metadata = candidate;
                id = Convert.ToString(collection["id"]);
                break;
            }
        }
        string key = "user-collections." + id;
        if (metadata == null)
        {
            metadata = new Dictionary<string, object> {
                { "key", key }, { "version", "0" },
                { "conflictResolutionMethod", "custom" }, { "strMethodId", "union-collections" }
            };
            entries.Add(new object[] { key, metadata });
        }
        var ids = new HashSet<uint>();
        foreach (var shortcut in managedShortcuts) ids.Add(unchecked((uint)shortcut.AppId));
        string serialized = json.Serialize(new { id = id, name = desiredName, added = new List<uint>(ids), removed = new uint[0] });
        object previous;
        if (!metadata.TryGetValue("value", out previous) || Convert.ToString(previous) != serialized)
        {
            metadata["value"] = serialized;
            metadata["timestamp"] = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            if (!dirty.Contains(key)) dirty.Add(key);
            Directory.CreateDirectory(directory);
            Save(path, json.Serialize(entries));
            Save(modifiedPath, json.Serialize(dirty));
        }
        Directory.CreateDirectory(configDir);
        if (!File.Exists(marker) || File.ReadAllText(marker, Encoding.UTF8) != id) Save(marker, id);
        string localConfig = Path.Combine(configDir, "localconfig.vdf");
        if (File.Exists(localConfig))
        {
            string original = File.ReadAllText(localConfig, Encoding.UTF8);
            string updated = original.Replace("\\\"bSortedByRecent\\\":true", "\\\"bSortedByRecent\\\":false")
                .Replace("\\\"bGroupedByCollection\\\":false", "\\\"bGroupedByCollection\\\":true");
            if (updated != original) Save(localConfig, updated);
        }
    }
    static void Save(string path, string content)
    {
        string temp = path + ".SteamEpicSync.tmp";
        File.WriteAllText(temp, content, new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(temp, path, path + ".SteamEpicSync.bak");
        else File.Move(temp, path);
    }
}
