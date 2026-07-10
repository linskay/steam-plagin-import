using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class SteamShortcut
{
    public string Index { get; set; }
    public Dictionary<string, object> Fields { get; set; }

    public SteamShortcut()
    {
        Fields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    public string AppName
    {
        get { return GetField<string>("AppName", ""); }
        set { Fields["AppName"] = value; }
    }
    public string Exe
    {
        get { return GetField<string>("Exe", ""); }
        set { Fields["Exe"] = value; }
    }
    public string StartDir
    {
        get { return GetField<string>("StartDir", ""); }
        set { Fields["StartDir"] = value; }
    }
    public string Icon
    {
        get { return GetField<string>("icon", ""); }
        set { Fields["icon"] = value; }
    }
    public string LaunchOptions
    {
        get { return GetField<string>("LaunchOptions", ""); }
        set { Fields["LaunchOptions"] = value; }
    }
    public int AppId
    {
        get { return GetField<int>("appid", 0); }
        set { Fields["appid"] = value; }
    }

    private T GetField<T>(string name, T defaultVal)
    {
        object val;
        if (Fields.TryGetValue(name, out val))
        {
            if (val is T)
            {
                T typedVal = (T)val;
                return typedVal;
            }
        }
        return defaultVal;
    }

    public void PopulateDefaults()
    {
        if (!Fields.ContainsKey("appid")) Fields["appid"] = 0;
        if (!Fields.ContainsKey("AppName")) Fields["AppName"] = "";
        if (!Fields.ContainsKey("Exe")) Fields["Exe"] = "";
        if (!Fields.ContainsKey("StartDir")) Fields["StartDir"] = "";
        if (!Fields.ContainsKey("icon")) Fields["icon"] = "";
        if (!Fields.ContainsKey("ShortcutPath")) Fields["ShortcutPath"] = "";
        if (!Fields.ContainsKey("LaunchOptions")) Fields["LaunchOptions"] = "";
        if (!Fields.ContainsKey("IsDevkit")) Fields["IsDevkit"] = 0;
        if (!Fields.ContainsKey("DevkitGameID")) Fields["DevkitGameID"] = "";
        if (!Fields.ContainsKey("DevkitAppID")) Fields["DevkitAppID"] = 0;
        if (!Fields.ContainsKey("LastPlayTime")) Fields["LastPlayTime"] = 0;
        if (!Fields.ContainsKey("tags")) Fields["tags"] = new Dictionary<string, object>();
        if (!Fields.ContainsKey("AllowDesktopConfig")) Fields["AllowDesktopConfig"] = 1;
        if (!Fields.ContainsKey("AllowOverlay")) Fields["AllowOverlay"] = 1;
        if (!Fields.ContainsKey("OpenVR")) Fields["OpenVR"] = 0;
        if (!Fields.ContainsKey("Devkit")) Fields["Devkit"] = 0;
        if (!Fields.ContainsKey("LocalContentProviderApp")) Fields["LocalContentProviderApp"] = 0;
    }
}

public class VdfParser
{
    public static List<SteamShortcut> ReadShortcuts(string filePath)
    {
        var shortcuts = new List<SteamShortcut>();
        if (!File.Exists(filePath))
            return shortcuts;

        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            int pos = 0;

            if (bytes.Length < 10) return shortcuts;

            byte rootType = bytes[pos++];
            if (rootType != 0x00) return shortcuts;

            string rootKey = ReadNullTerminatedString(bytes, ref pos);
            if (rootKey != "shortcuts") return shortcuts;

            while (pos < bytes.Length)
            {
                byte type = bytes[pos++];
                if (type == 0x08)
                    break;

                string index = ReadNullTerminatedString(bytes, ref pos);
                var shortcut = new SteamShortcut { Index = index };
                shortcut.Fields = ReadMapContent(bytes, ref pos);
                shortcuts.Add(shortcut);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error reading shortcuts.vdf: " + ex.Message);
        }

        return shortcuts;
    }

    private static Dictionary<string, object> ReadMapContent(byte[] bytes, ref int pos)
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        while (pos < bytes.Length)
        {
            byte type = bytes[pos++];
            if (type == 0x08)
                break;

            string key = ReadNullTerminatedString(bytes, ref pos);
            if (type == 0x01)
            {
                string val = ReadNullTerminatedString(bytes, ref pos);
                dict[key] = val;
            }
            else if (type == 0x02)
            {
                if (pos + 4 <= bytes.Length)
                {
                    int val = BitConverter.ToInt32(bytes, pos);
                    pos += 4;
                    dict[key] = val;
                }
            }
            else if (type == 0x00)
            {
                dict[key] = ReadMapContent(bytes, ref pos);
            }
        }
        return dict;
    }

    private static string ReadNullTerminatedString(byte[] bytes, ref int pos)
    {
        int start = pos;
        while (pos < bytes.Length && bytes[pos] != 0)
        {
            pos++;
        }
        string val = Encoding.UTF8.GetString(bytes, start, pos - start);
        if (pos < bytes.Length) pos++;
        return val;
    }

    public static void WriteShortcuts(string filePath, List<SteamShortcut> shortcuts)
    {
        string dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write((byte)0x00);
            WriteNullTerminatedString(bw, "shortcuts");

            for (int i = 0; i < shortcuts.Count; i++)
            {
                var s = shortcuts[i];
                bw.Write((byte)0x00);
                WriteNullTerminatedString(bw, i.ToString());

                WriteMapContent(bw, s.Fields);
                bw.Write((byte)0x08);
            }

            bw.Write((byte)0x08);
            bw.Flush();

            File.WriteAllBytes(filePath, ms.ToArray());
        }
    }

    private static void WriteMapContent(BinaryWriter bw, Dictionary<string, object> dict)
    {
        foreach (var kvp in dict)
        {
            if (kvp.Value is string)
            {
                string str = (string)kvp.Value;
                bw.Write((byte)0x01);
                WriteNullTerminatedString(bw, kvp.Key);
                WriteNullTerminatedString(bw, str);
            }
            else if (kvp.Value is int)
            {
                int val = (int)kvp.Value;
                bw.Write((byte)0x02);
                WriteNullTerminatedString(bw, kvp.Key);
                bw.Write(val);
            }
            else if (kvp.Value is Dictionary<string, object>)
            {
                Dictionary<string, object> subDict = (Dictionary<string, object>)kvp.Value;
                bw.Write((byte)0x00);
                WriteNullTerminatedString(bw, kvp.Key);
                WriteMapContent(bw, subDict);
                bw.Write((byte)0x08);
            }
        }
    }

    private static void WriteNullTerminatedString(BinaryWriter bw, string str)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(str);
        bw.Write(bytes);
        bw.Write((byte)0);
    }
}
