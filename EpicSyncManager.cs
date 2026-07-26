using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

public class EpicGame
{
    public string DisplayName { get; set; }
    public string InstallLocation { get; set; }
    public string LaunchExecutable { get; set; }
    public string AppName { get; set; }
    public string CatalogNamespace { get; set; }
    public string CatalogItemId { get; set; }
    public bool IsInstalled { get; set; }
}

public class Crc32
{
    private static readonly uint[] Table;

    static Crc32()
    {
        uint poly = 0xedb88320;
        Table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint r = i;
            for (int j = 0; j < 8; j++)
            {
                if ((r & 1) != 0)
                    r = (r >> 1) ^ poly;
                else
                    r >>= 1;
            }
            Table[i] = r;
        }
    }

    public static uint Compute(byte[] bytes)
    {
        uint crc = 0xffffffff;
        foreach (byte b in bytes)
        {
            byte index = (byte)((crc & 0xff) ^ b);
            crc = (crc >> 8) ^ Table[index];
        }
        return ~crc;
    }
}

public class EpicSyncManager
{
    public static List<EpicGame> FindInstalledGames()
    {
        var games = new List<EpicGame>();
        string manifestsPath = @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests";
        if (!Directory.Exists(manifestsPath))
        {
            return games;
        }

        foreach (var file in Directory.GetFiles(manifestsPath, "*.item"))
        {
            try
            {
                string json = File.ReadAllText(file);
                string launchExecutable = ExtractJsonString(json, "LaunchExecutable");
                
                // If LaunchExecutable is empty, it's not a launchable game (e.g. DLC or engine component)
                if (string.IsNullOrEmpty(launchExecutable))
                    continue;

                var game = new EpicGame
                {
                    DisplayName = ExtractJsonString(json, "DisplayName"),
                    InstallLocation = ExtractJsonString(json, "InstallLocation"),
                    LaunchExecutable = launchExecutable,
                    AppName = ExtractJsonString(json, "AppName"),
                    CatalogNamespace = ExtractJsonString(json, "CatalogNamespace"),
                    CatalogItemId = ExtractJsonString(json, "CatalogItemId"),
                    IsInstalled = true
                };

                if (!string.IsNullOrEmpty(game.DisplayName) && !string.IsNullOrEmpty(game.AppName))
                {
                    games.Add(game);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error parsing manifest file " + Path.GetFileName(file) + ": " + ex.Message);
            }
        }

        return games;
    }

    private static string ExtractJsonString(string json, string key)
    {
        string pattern = "\"" + key + "\":\\s*\"(.*?)\"";
        var match = Regex.Match(json, pattern);
        if (match.Success)
        {
            string val = match.Groups[1].Value;
            // Unescape JSON string
            val = val.Replace(@"\\", @"\");
            val = val.Replace(@"\/", @"/");
            val = val.Replace(@"\""", @"""");
            return val;
        }
        return "";
    }

    public static string GetSteamPath()
    {
        string path = "";
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
            {
                if (key != null)
                {
                    path = key.GetValue("SteamPath") as string;
                }
            }
        }
        catch { }

        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            path = @"C:\Program Files (x86)\Steam";
        }
        
        return path;
    }

    public static string GetEpicLauncherExePath()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Epic Games\EOS"))
            {
                if (key != null)
                {
                    string cmd = key.GetValue("ModSdkCommand") as string;
                    if (!string.IsNullOrEmpty(cmd) && File.Exists(cmd))
                        return cmd;
                }
            }
        }
        catch { }

        string[] paths = new string[] {
            @"C:\Program Files\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe",
            @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe",
            @"D:\Program Files\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe"
        };

        foreach (var path in paths)
        {
            if (File.Exists(path)) return path;
        }

        return "";
    }

    public static string Sync(string currentExePath, List<EpicGame> epicGames, out int addedCount, out int removedCount)
    {
        addedCount = 0;
        removedCount = 0;
        
        string steamPath = GetSteamPath();
        string userdataPath = Path.Combine(steamPath, "userdata");
        if (!Directory.Exists(userdataPath))
        {
            return "Steam userdata folder not found.";
        }

        string currentExeDir = Path.GetDirectoryName(currentExePath);
        string quotedCurrentExe = "\"" + currentExePath + "\"";
        string quotedCurrentExeDir = "\"" + currentExeDir + "\\\"";

        var userDirs = Directory.GetDirectories(userdataPath);
        StringBuilder sb = new StringBuilder();

        foreach (var userDir in userDirs)
        {
            string userId = Path.GetFileName(userDir);
            if (!Regex.IsMatch(userId, @"^\d+$")) continue; // Only numeric IDs

            string shortcutsFile = Path.Combine(userDir, @"config\shortcuts.vdf");
            var shortcuts = VdfParser.ReadShortcuts(shortcutsFile);

            // 1. Identify and remove any Epic shortcuts that are no longer in our EGS library
            var finalShortcuts = new List<SteamShortcut>();
            foreach (var s in shortcuts)
            {
                bool isManagedByUs = s.Exe.Equals(quotedCurrentExe, StringComparison.OrdinalIgnoreCase) && 
                                     s.LaunchOptions.StartsWith("--launch ", StringComparison.OrdinalIgnoreCase);

                if (isManagedByUs)
                {
                    string appNameArg = s.LaunchOptions.Substring("--launch ".Length).Trim('\"', ' ');
                    bool stillExists = epicGames.Exists(g => g.AppName.Equals(appNameArg, StringComparison.OrdinalIgnoreCase));
                    if (!stillExists)
                    {
                        removedCount++;
                        continue; // Skip, which removes it
                    }
                }
                finalShortcuts.Add(s);
            }

            // 2. Add or update Epic games in Steam
            foreach (var game in epicGames)
            {
                string launchArgs = "--launch \"" + game.AppName + "\"";
                
                // Check if already in shortcuts
                var existing = finalShortcuts.Find(s => 
                    s.Exe.Equals(quotedCurrentExe, StringComparison.OrdinalIgnoreCase) &&
                    s.LaunchOptions.Equals(launchArgs, StringComparison.OrdinalIgnoreCase));

                string gameIcon = "";
                if (game.IsInstalled && !string.IsNullOrEmpty(game.InstallLocation) && !string.IsNullOrEmpty(game.LaunchExecutable))
                {
                    gameIcon = Path.Combine(game.InstallLocation, game.LaunchExecutable.Replace('/', '\\'));
                }

                int appid = CalculateAppId(quotedCurrentExe, game.DisplayName);

                if (existing == null)
                {
                    var newShortcut = new SteamShortcut();
                    newShortcut.PopulateDefaults();
                    newShortcut.AppName = game.DisplayName;
                    newShortcut.Exe = quotedCurrentExe;
                    newShortcut.StartDir = quotedCurrentExeDir;
                    newShortcut.Icon = gameIcon;
                    newShortcut.LaunchOptions = launchArgs;
                    newShortcut.AppId = appid;
                    
                    newShortcut.Fields["AllowOverlay"] = 0;
                    newShortcut.Fields["AllowDesktopConfig"] = 1;
                    
                    // Add "Epic Games" tag
                    var tags = new Dictionary<string, object>();
                    tags["0"] = "Epic Games";
                    newShortcut.Fields["tags"] = tags;

                    finalShortcuts.Add(newShortcut);
                    addedCount++;
                }
                else
                {
                    // Update icon or appid if needed
                    existing.Icon = gameIcon;
                    existing.AppId = appid;
                    existing.AppName = game.DisplayName;
                    existing.Fields["AllowOverlay"] = 0;
                }
            }

            // Save the shortcuts.vdf back
            try
            {
                VdfParser.WriteShortcuts(shortcutsFile, finalShortcuts);
                sb.AppendLine("Synced " + epicGames.Count + " games for user ID " + userId);
            }
            catch (Exception ex)
            {
                sb.AppendLine("Error writing shortcuts.vdf for user ID " + userId + ": " + ex.Message);
            }
        }

        return sb.ToString();
    }

    private static int CalculateAppId(string exe, string appName)
    {
        string key = exe + appName;
        byte[] bytes = Encoding.UTF8.GetBytes(key);
        uint crc = Crc32.Compute(bytes);
        uint appid = crc | 0x80000000;
        return (int)appid;
    }
}
