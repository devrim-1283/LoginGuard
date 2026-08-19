using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace LoginGuard
{
    // Yakalama motoru: 4625 tetiklendiginde SYSTEM gorevi tarafindan "--capture" ile calistirilir.
    // Foto + kisa video ceker, Telegram'a gonderir. Kamera cakismasini named Mutex ile onler.
    public static class CaptureEngine
    {
        public static void Run(string recordId) { Run(recordId, false); }

        public static void Run(string recordId, bool isTest)
        {
            var cfg = Config.Load();
            if (!cfg.Enabled && !isTest) { Logger.Log("Devre disi, atlandi."); return; }
            if (!cfg.IsConfigured()) { Logger.Log("Yapilandirilmamis (token/chat yok), atlandi."); return; }

            var tg = new Telegram(cfg.BotToken, cfg.ChatId);
            var info = FailedLogon.Read(recordId);
            string text = (isTest ? "[TEST] " : "") + BuildText(info);

            bool owned = false;
            var mtx = new Mutex(false, "Global\\LoginGuardCaptureMutex");
            try
            {
                try { owned = mtx.WaitOne(0); }
                catch (AbandonedMutexException) { owned = true; }

                if (!owned)
                {
                    tg.SendMessage(text + "\n[!] Kamera mesgul (onceki yakalama suruyor); bu deneme icin medya alinamadi.");
                    Logger.Log("Kamera mesgul -> sadece metin uyarisi gonderildi.");
                    return;
                }

                string cam = string.IsNullOrEmpty(cfg.CameraName) ? DetectCamera(cfg) : cfg.CameraName;
                if (string.IsNullOrEmpty(cam))
                {
                    tg.SendMessage(text + "\n[!] Kamera bulunamadi.");
                    Logger.Log("Kamera bulunamadi.");
                    return;
                }

                string tmp = Path.Combine(Path.GetTempPath(), "LoginGuard");
                Directory.CreateDirectory(tmp);
                string photo = Path.Combine(tmp, "photo.jpg");
                string video = Path.Combine(tmp, "video.mp4");
                SafeDelete(photo); SafeDelete(video);

                // 1) FOTO - hizli kanit (birkac kare isinma sonrasi son kareyi yazar)
                RunFfmpeg(cfg, "-hide_banner -loglevel error -y -f dshow -rtbufsize 100M -i \"video=" + cam +
                    "\" -frames:v 8 -update 1 -q:v 3 \"" + photo + "\"", 30000);
                if (File.Exists(photo)) { tg.SendPhoto(photo, text); Logger.Log("Foto cekildi ve gonderildi."); }
                else { tg.SendMessage(text + "\n[!] Foto alinamadi (kamera erisimi engellenmis olabilir)."); Logger.Log("Foto yakalama BASARISIZ."); }

                // 2) VIDEO - kisa kayit
                RunFfmpeg(cfg, "-hide_banner -loglevel error -y -f dshow -rtbufsize 100M -i \"video=" + cam +
                    "\" -t " + cfg.VideoDurationSec + " -pix_fmt yuv420p -movflags +faststart \"" + video + "\"", 60000);
                if (File.Exists(video)) { tg.SendVideo(video, "Kayit: " + cfg.VideoDurationSec + " sn"); Logger.Log("Video cekildi ve gonderildi."); }
                else { Logger.Log("Video yakalama BASARISIZ."); }

                SafeDelete(photo); SafeDelete(video);
            }
            catch (Exception ex)
            {
                Logger.Log("HATA: " + ex.Message);
                try { tg.SendMessage("LoginGuard hata: " + ex.Message); } catch { }
            }
            finally
            {
                if (owned) { try { mtx.ReleaseMutex(); } catch { } }
                mtx.Dispose();
            }
        }

        public static string BuildText(FailedLogon info)
        {
            string comp = Environment.MachineName;
            if (info == null)
            {
                return "[UYARI] Basarisiz giris denemesi\nBilgisayar: " + comp +
                       "\nZaman: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                       "\n(Olay detayi okunamadi / test calistirmasi)";
            }
            string ip = (!string.IsNullOrEmpty(info.IpAddress) && info.IpAddress != "-") ? info.IpAddress : "yerel";
            string ws = string.IsNullOrEmpty(info.Workstation) ? "-" : info.Workstation;
            var sb = new StringBuilder();
            sb.AppendLine("[UYARI] BASARISIZ GIRIS DENEMESI");
            sb.AppendLine();
            sb.AppendLine("Bilgisayar : " + comp);
            sb.AppendLine("Kullanici  : " + info.Domain + "\\" + info.User);
            sb.AppendLine("Zaman      : " + info.Time.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Giris turu : " + FailedLogon.LogonTypeText(info.LogonType));
            sb.AppendLine("Sebep      : " + FailedLogon.SubStatusText(info.SubStatus));
            sb.AppendLine("Kaynak IP  : " + ip);
            sb.AppendLine("Is ist.    : " + ws);
            return sb.ToString();
        }

        public static string DetectCamera(Config cfg)
        {
            var list = ListVideoDevices(cfg);
            return list.Count > 0 ? list[0] : "";
        }

        public static System.Collections.Generic.List<string> ListVideoDevices(Config cfg)
        {
            var result = new System.Collections.Generic.List<string>();
            try
            {
                string outp = RunFfmpegCapture(cfg, "-hide_banner -list_devices true -f dshow -i dummy");
                foreach (string line in outp.Split('\n'))
                {
                    if (line.IndexOf("(video)", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    int q1 = line.IndexOf('"');
                    if (q1 < 0) continue;
                    int q2 = line.IndexOf('"', q1 + 1);
                    if (q2 < 0) continue;
                    string name = line.Substring(q1 + 1, q2 - q1 - 1);
                    if (!result.Contains(name)) result.Add(name);
                }
            }
            catch { }
            return result;
        }

        private static void RunFfmpeg(Config cfg, string args, int timeoutMs)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = cfg.FfmpegPath(),
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                using (var p = Process.Start(psi))
                {
                    // stderr'i tuket ki buffer dolup kilitlenmesin
                    p.BeginErrorReadLine();
                    p.BeginOutputReadLine();
                    if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } }
                }
            }
            catch (Exception ex) { Logger.Log("ffmpeg calistirma hatasi: " + ex.Message); }
        }

        private static string RunFfmpegCapture(Config cfg, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = cfg.FfmpegPath(),
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            using (var p = Process.Start(psi))
            {
                string err = p.StandardError.ReadToEnd();
                string outp = p.StandardOutput.ReadToEnd();
                p.WaitForExit(15000);
                return err + "\n" + outp; // dshow cihaz listesi stderr'e yazilir
            }
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
