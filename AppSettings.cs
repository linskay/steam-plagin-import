using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

public class AppSettings
{
    public bool LaunchWithSteam { get; set; }
    public bool SyncOnSteamStart { get; set; }
    public string CollectionName { get; set; }
    public string ExePath { get; set; }
    internal static string PathOverride = null;
    public static string ConfigPath { get { return PathOverride ?? Path.Combine(EpicSyncManager.GetSteamPath(), "config", "SteamEpicSync", "settings.json"); } }
    public AppSettings() { LaunchWithSteam = true; CollectionName = "Есть в EGS"; }
    public static AppSettings Load()
    {
        if (!File.Exists(ConfigPath)) return new AppSettings();
        var settings = new JavaScriptSerializer().Deserialize<AppSettings>(File.ReadAllText(ConfigPath, Encoding.UTF8));
        if (settings == null) throw new InvalidDataException("Не удалось прочитать настройки Steam Epic Sync.");
        settings.CollectionName = ValidateName(settings.CollectionName);
        return settings;
    }
    public static string ValidateName(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0 || name.Length > 80 || name.IndexOfAny(new char[] { '\r', '\n', '\0' }) >= 0)
            throw new ArgumentException("Название коллекции должно содержать от 1 до 80 символов без переносов строк.");
        return name;
    }
    public void Save()
    {
        CollectionName = ValidateName(CollectionName);
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
        string temp = ConfigPath + ".tmp";
        File.WriteAllText(temp, new JavaScriptSerializer().Serialize(this), new UTF8Encoding(false));
        if (File.Exists(ConfigPath)) File.Replace(temp, ConfigPath, ConfigPath + ".bak");
        else File.Move(temp, ConfigPath);
    }
}
