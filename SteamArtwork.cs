using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;

public static class SteamArtwork
{
    public static void Save(string gridDir, uint appId, EpicGame game)
    {
        Directory.CreateDirectory(gridDir);
        var downloads = new System.Collections.Generic.Dictionary<string, byte[]>();
        string id = appId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Render(Path.Combine(gridDir, id + "p.png"), 600, 900, game.PortraitUrl ?? game.LandscapeUrl, game.DisplayName, downloads);
        Render(Path.Combine(gridDir, id + ".png"), 920, 430, game.LandscapeUrl ?? game.PortraitUrl, game.DisplayName, downloads);
        Render(Path.Combine(gridDir, id + "_hero.png"), 1920, 620, game.LandscapeUrl ?? game.PortraitUrl, game.DisplayName, downloads);
        SaveIcon(gridDir, appId);
    }
    public static string IconPath(string gridDir, uint appId)
    {
        return Path.Combine(gridDir, appId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "_icon.ico");
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr handle);

    static void SaveIcon(string gridDir, uint appId)
    {
        string path = IconPath(gridDir, appId);
        if (File.Exists(path)) return;
        string sourcePath = Path.Combine(gridDir, appId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "p.png");
        using (var source = Image.FromFile(sourcePath))
        using (var bitmap = new Bitmap(64, 64))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            // Crop the game artwork itself; the full cover's text band is illegible at list size.
            int size = Math.Min(source.Width, source.Height);
            graphics.DrawImage(source, new Rectangle(0, 0, 64, 64), new Rectangle((source.Width - size) / 2, (source.Height - size) / 2, size, size), GraphicsUnit.Pixel);
            IntPtr handle = bitmap.GetHicon();
            try
            {
                using (var icon = Icon.FromHandle(handle))
                using (var stream = File.Create(path + ".tmp")) icon.Save(stream);
                File.Move(path + ".tmp", path);
            }
            finally { DestroyIcon(handle); }
        }
    }
    static void Render(string path, int width, int height, string url, string title, System.Collections.Generic.Dictionary<string, byte[]> downloads)
    {
        // Preserve artwork already chosen by the user. Generated fallback art is retried later.
        bool fallback = File.Exists(path + ".fallback");
        if (File.Exists(path) && !fallback) return;
        Image source = null; MemoryStream memory = null;
        try
        {
            Uri uri;
            if (Uri.TryCreate(url, UriKind.Absolute, out uri) && uri.Scheme == "https")
            {
                byte[] cached;
                if (downloads.TryGetValue(url, out cached))
                {
                    memory = new MemoryStream(cached); source = Image.FromStream(memory);
                }
                else
                {
                var request = (HttpWebRequest)WebRequest.Create(uri); request.Timeout = request.ReadWriteTimeout = 15000;
                using (var response = request.GetResponse())
                using (var stream = response.GetResponseStream())
                {
                    memory = new MemoryStream(); byte[] buffer = new byte[8192]; int count;
                    while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (memory.Length + count > 20971520) throw new InvalidDataException("Изображение больше 20 МБ.");
                        memory.Write(buffer, 0, count);
                    }
                    downloads[url] = memory.ToArray(); memory.Position = 0; source = Image.FromStream(memory);
                }
                }
            }
        }
        catch (WebException) { }
        catch (ArgumentException) { }
        try
        {
            using (var bitmap = new Bitmap(width, height))
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.FromArgb(24, 29, 40));
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                if (source != null)
                {
                    float scale = Math.Max((float)width / source.Width, (float)height / source.Height);
                    float w = source.Width * scale, h = source.Height * scale;
                    g.DrawImage(source, (width - w) / 2, (height - h) / 2, w, h);
                }
                else
                {
                    using (var font = new Font("Segoe UI", width / 20f, FontStyle.Bold))
                    using (var brush = new SolidBrush(Color.White))
                    using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString(title, font, brush, new RectangleF(35, 30, width - 70, height - 110), format);
                }
                int band = Math.Max(48, height / 13);
                using (var brush = new SolidBrush(Color.FromArgb(235, 12, 15, 20))) g.FillRectangle(brush, 0, height - band, width, band);
                using (var pen = new Pen(Color.FromArgb(95, 178, 235), Math.Max(4, width / 150))) g.DrawRectangle(pen, 3, 3, width - 6, height - 6);
                using (var font = new Font("Segoe UI", band / 3f, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.White))
                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString("EPIC GAMES", font, brush, new RectangleF(0, height - band, width, band), format);
                string temp = path + ".tmp";
                bitmap.Save(temp, ImageFormat.Png);
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
                if (source == null) File.WriteAllText(path + ".fallback", "");
                else if (fallback) File.Delete(path + ".fallback");
            }
        }
        finally { if (source != null) source.Dispose(); if (memory != null) memory.Dispose(); }
    }
}
