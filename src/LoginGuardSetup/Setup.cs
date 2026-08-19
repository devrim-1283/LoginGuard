using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace LoginGuardSetup
{
    // LoginGuard kurulum programi (requireAdministrator).
    // Program Files'a kurar, ffmpeg saglar, ProgramData+ACL, SYSTEM gorevi (4625) ve otomatik baslatma kaydeder.
    internal static class Setup
    {
        private const string AppName = "LoginGuard";
        private const string TaskName = "LoginGuard";
        private static readonly string InstallDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LoginGuard");
        private static readonly string DataDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LoginGuard");
        // Bundle yoksa ffmpeg buradan indirilir (kendi release'imiz):
        private const string FfmpegUrl =
            "https://github.com/devrim-1283/LoginGuard/releases/latest/download/ffmpeg.exe";

        private static int Main(string[] args)
        {
            try
            {
                var opts = ParseArgs(args);
                if (opts.ContainsKey("uninstall")) { Uninstall(); return 0; }

                Console.WriteLine("=== LoginGuard Kurulum ===");
                string setupDir = AppContext.BaseDirectory;

                Directory.CreateDirectory(InstallDir);
                Directory.CreateDirectory(DataDir);

                // 1) Uygulama exe'si
                Console.WriteLine("[1/6] LoginGuard.exe kopyalaniyor...");
                string srcApp = Path.Combine(setupDir, "LoginGuard.exe");
                if (!File.Exists(srcApp))
                    throw new FileNotFoundException("LoginGuard.exe kurulum yaninda bulunamadi: " + srcApp);
                string dstApp = Path.Combine(InstallDir, "LoginGuard.exe");
                File.Copy(srcApp, dstApp, true);

                // 2) ffmpeg
                Console.WriteLine("[2/6] ffmpeg saglaniyor...");
                string dstFf = Path.Combine(InstallDir, "ffmpeg.exe");
                string srcFf = Path.Combine(setupDir, "ffmpeg.exe");
                if (File.Exists(srcFf)) File.Copy(srcFf, dstFf, true);
                else if (!File.Exists(dstFf)) DownloadFfmpeg(dstFf);
                Console.WriteLine("      ffmpeg: " + dstFf);

                // 3) ProgramData ACL (SYSTEM+Admin tam, Users degistirme -> tray ayarlari yazabilsin)
                Console.WriteLine("[3/6] Veri klasoru ve izinler ayarlaniyor...");
                SetDataAcl(DataDir);
                CleanupLegacy();

                // 4) config (varsa koru; token/chat verildiyse yaz)
                Console.WriteLine("[4/6] Yapilandirma...");
                WriteConfig(dstFf, opts);

                // 5) Zamanlanmis gorev (4625 -> LoginGuard.exe --capture)
                Console.WriteLine("[5/6] Zamanlanmis gorev (Event 4625) kaydediliyor...");
                RegisterTask(dstApp);

                // 6) Otomatik baslatma (tray, her oturum acilisinda)
                Console.WriteLine("[6/6] Otomatik baslatma kaydediliyor...");
                RegisterAutostart(dstApp);

                // Tray'i baslat
                TryStartTray(dstApp);

                Console.WriteLine();
                Console.WriteLine("KURULUM TAMAM. Tepside (sag alt) LoginGuard ikonu gorunmeli.");
                Console.WriteLine("Yapilandirma icin ikon > Ayarlar. Test: Win+L, yanlis parola.");
                if (!IsSilent(opts)) { Console.WriteLine("Cikmak icin Enter..."); Console.ReadLine(); }
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("HATA: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine("Cikmak icin Enter...");
                Console.ReadLine();
                return 1;
            }
        }

        private static void DownloadFfmpeg(string dest)
        {
            Console.WriteLine("      ffmpeg indiriliyor (release)...");
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (var wc = new WebClient()) wc.DownloadFile(FfmpegUrl, dest);
        }

        private static void SetDataAcl(string dir)
        {
            var di = new DirectoryInfo(dir);
            var sec = di.GetAccessControl();
            sec.SetAccessRuleProtection(true, false); // kalitimsiz, acik kurallar

            var inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

            sec.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            sec.AddAccessRule(new FileSystemAccessRule(admins, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            sec.AddAccessRule(new FileSystemAccessRule(users, FileSystemRights.Modify, inherit, PropagationFlags.None, AccessControlType.Allow));
            di.SetAccessControl(sec);
        }

        private static void CleanupLegacy()
        {
            // Eski PowerShell kurulumundan kalanlar
            TryDelete(Path.Combine(DataDir, "capture-and-notify.ps1"));
            string oldFf = Path.Combine(DataDir, "ffmpeg.exe");
            if (File.Exists(oldFf)) TryDelete(oldFf);
        }

        private static void WriteConfig(string ffmpegPath, Dictionary<string, string> opts)
        {
            string configPath = Path.Combine(DataDir, "config.json");
            var ser = new JavaScriptSerializer();
            Dictionary<string, object> cfg;
            if (File.Exists(configPath))
            {
                try { cfg = ser.Deserialize<Dictionary<string, object>>(File.ReadAllText(configPath)); }
                catch { cfg = new Dictionary<string, object>(); }
            }
            else cfg = new Dictionary<string, object>();

            if (opts.ContainsKey("token")) cfg["BotToken"] = opts["token"];
            if (opts.ContainsKey("chat")) cfg["ChatId"] = opts["chat"];
            if (opts.ContainsKey("duration")) cfg["VideoDurationSec"] = int.Parse(opts["duration"]);

            if (!cfg.ContainsKey("BotToken")) cfg["BotToken"] = "";
            if (!cfg.ContainsKey("ChatId")) cfg["ChatId"] = "";
            if (!cfg.ContainsKey("VideoDurationSec")) cfg["VideoDurationSec"] = 5;
            if (!cfg.ContainsKey("CameraName")) cfg["CameraName"] = DetectCamera(ffmpegPath);
            if (!cfg.ContainsKey("Enabled")) cfg["Enabled"] = true;

            File.WriteAllText(configPath, ser.Serialize(cfg));
        }

        private static string DetectCamera(string ffmpegPath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = "-hide_banner -list_devices true -f dshow -i dummy",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                using (var p = Process.Start(psi))
                {
                    string err = p.StandardError.ReadToEnd();
                    p.WaitForExit(15000);
                    foreach (string line in err.Split('\n'))
                    {
                        if (line.IndexOf("(video)", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        int q1 = line.IndexOf('"'); if (q1 < 0) continue;
                        int q2 = line.IndexOf('"', q1 + 1); if (q2 < 0) continue;
                        return line.Substring(q1 + 1, q2 - q1 - 1);
                    }
                }
            }
            catch { }
            return "";
        }

        private static void RegisterTask(string appExe)
        {
            string subscription =
                "&lt;QueryList&gt;&lt;Query Id=\"0\" Path=\"Security\"&gt;&lt;Select Path=\"Security\"&gt;*[System[EventID=4625]]&lt;/Select&gt;&lt;/Query&gt;&lt;/QueryList&gt;";
            var xml = new StringBuilder();
            xml.Append("<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n");
            xml.Append("<Task version=\"1.4\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\r\n");
            xml.Append("  <RegistrationInfo><Description>LoginGuard - basarisiz giriste webcam yakalama</Description></RegistrationInfo>\r\n");
            xml.Append("  <Triggers><EventTrigger><Enabled>true</Enabled>\r\n");
            xml.Append("    <Subscription>").Append(subscription).Append("</Subscription>\r\n");
            xml.Append("    <ValueQueries><Value name=\"RecordId\">Event/System/EventRecordID</Value></ValueQueries>\r\n");
            xml.Append("  </EventTrigger></Triggers>\r\n");
            xml.Append("  <Principals><Principal id=\"Author\"><UserId>S-1-5-18</UserId><RunLevel>HighestAvailable</RunLevel></Principal></Principals>\r\n");
            xml.Append("  <Settings>\r\n");
            xml.Append("    <MultipleInstancesPolicy>Parallel</MultipleInstancesPolicy>\r\n");
            xml.Append("    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>\r\n");
            xml.Append("    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>\r\n");
            xml.Append("    <AllowHardTerminate>true</AllowHardTerminate>\r\n");
            xml.Append("    <StartWhenAvailable>true</StartWhenAvailable>\r\n");
            xml.Append("    <ExecutionTimeLimit>PT2M</ExecutionTimeLimit>\r\n");
            xml.Append("    <Enabled>true</Enabled><Hidden>true</Hidden>\r\n");
            xml.Append("  </Settings>\r\n");
            xml.Append("  <Actions Context=\"Author\"><Exec>\r\n");
            xml.Append("    <Command>\"").Append(appExe).Append("\"</Command>\r\n");
            xml.Append("    <Arguments>--capture --record \"$(RecordId)\"</Arguments>\r\n");
            xml.Append("  </Exec></Actions>\r\n");
            xml.Append("</Task>\r\n");

            string xmlPath = Path.Combine(Path.GetTempPath(), "loginguard-task.xml");
            File.WriteAllText(xmlPath, xml.ToString(), Encoding.Unicode);
            RunProcess("schtasks.exe", "/Create /TN \"" + TaskName + "\" /XML \"" + xmlPath + "\" /F");
            TryDelete(xmlPath);
        }

        private static void RegisterAutostart(string appExe)
        {
            using (var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (key != null) key.SetValue(AppName, "\"" + appExe + "\"");
            }
        }

        private static void TryStartTray(string appExe)
        {
            try { Process.Start(new ProcessStartInfo(appExe) { UseShellExecute = true }); } catch { }
        }

        private static void Uninstall()
        {
            Console.WriteLine("=== LoginGuard Kaldirma ===");
            RunProcess("schtasks.exe", "/Delete /TN \"" + TaskName + "\" /F");
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null && key.GetValue(AppName) != null) key.DeleteValue(AppName, false);
                }
            }
            catch { }
            RunProcess("taskkill.exe", "/IM LoginGuard.exe /F");
            TryDeleteDir(InstallDir);
            TryDeleteDir(DataDir);
            Console.WriteLine("Kaldirildi.");
        }

        // ---- yardimcilar ----
        private static Dictionary<string, string> ParseArgs(string[] args)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string a in args)
            {
                string s = a.TrimStart('/', '-');
                int eq = s.IndexOfAny(new[] { '=', ':' });
                if (eq > 0) d[s.Substring(0, eq)] = s.Substring(eq + 1);
                else d[s] = "true";
            }
            return d;
        }

        private static bool IsSilent(Dictionary<string, string> opts)
        {
            return opts.ContainsKey("silent") || opts.ContainsKey("s");
        }

        private static void RunProcess(string file, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(file, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    string o = p.StandardOutput.ReadToEnd();
                    string e = p.StandardError.ReadToEnd();
                    p.WaitForExit(30000);
                    if (!string.IsNullOrWhiteSpace(o)) Console.WriteLine("      " + o.Trim());
                    if (!string.IsNullOrWhiteSpace(e)) Console.WriteLine("      " + e.Trim());
                }
            }
            catch (Exception ex) { Console.WriteLine("      (" + file + " hata: " + ex.Message + ")"); }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static void TryDeleteDir(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
        }
    }
}
