using System;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;

namespace LoginGuard
{
    // Uygulama yapilandirmasi. ProgramData altinda JSON olarak saklanir (kullanici Ayarlar'dan duzenler).
    public class Config
    {
        public string BotToken = "";
        public string ChatId = "";
        public int VideoDurationSec = 5;
        public string CameraName = "";   // bos ise otomatik tespit
        public bool Enabled = true;

        public static readonly string DataDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LoginGuard");
        public static readonly string ConfigPath = Path.Combine(DataDir, "config.json");
        public static readonly string LogPath = Path.Combine(DataDir, "loginguard.log");

        public static string InstallDir
        {
            get { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }
        }

        public string FfmpegPath()
        {
            string local = Path.Combine(InstallDir, "ffmpeg.exe");
            if (File.Exists(local)) return local;
            return "ffmpeg"; // PATH'e birak
        }

        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(BotToken) && !string.IsNullOrEmpty(ChatId);
        }

        public static Config Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var cfg = new JavaScriptSerializer().Deserialize<Config>(json);
                    if (cfg != null) return cfg;
                }
            }
            catch { }
            return new Config();
        }

        public void Save()
        {
            Directory.CreateDirectory(DataDir);
            string json = new JavaScriptSerializer().Serialize(this);
            File.WriteAllText(ConfigPath, json);
        }
    }
}
