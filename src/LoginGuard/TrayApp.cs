using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LoginGuard
{
    // Sistem tepsisi (sag alt) uygulamasi: durum ikonu, ayarlar, test, log erisimi.
    // Windows oturum acilisinda baslar; PC baslangic/durma ve oturum olaylarini loglar.
    public class TrayApp : ApplicationContext
    {
        private readonly NotifyIcon _tray;
        private Config _cfg;
        private ToolStripMenuItem _enabledItem;
        private ToolStripMenuItem _statusItem;

        public TrayApp()
        {
            _cfg = Config.Load();

            _tray = new NotifyIcon
            {
                Icon = IconFactory.CreateTrayIcon(_cfg.Enabled),
                Text = "LoginGuard",
                Visible = true,
                ContextMenuStrip = BuildMenu()
            };
            _tray.DoubleClick += (s, e) => OpenSettings();

            Logger.Log("Tray baslatildi (oturum: " + Environment.UserName + ").");
            PcEventLogger.Backfill();
            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.SessionEnding += OnSessionEnding;

            if (!_cfg.IsConfigured())
            {
                _tray.ShowBalloonTip(5000, "LoginGuard", "Kurulum icin Telegram bilgilerini girin (Ayarlar).", ToolTipIcon.Info);
                OpenSettings();
            }
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();

            _statusItem = new ToolStripMenuItem("LoginGuard") { Enabled = false };
            menu.Items.Add(_statusItem);
            menu.Items.Add(new ToolStripSeparator());

            _enabledItem = new ToolStripMenuItem("Etkin", null, (s, e) => ToggleEnabled())
            {
                Checked = _cfg.Enabled,
                CheckOnClick = true
            };
            menu.Items.Add(_enabledItem);

            menu.Items.Add(new ToolStripMenuItem("Ayarlar...", null, (s, e) => OpenSettings()));
            menu.Items.Add(new ToolStripMenuItem("Test Yakalama", null, (s, e) => TestCapture()));
            menu.Items.Add(new ToolStripMenuItem("Logu Ac", null, (s, e) => OpenPath(Config.LogPath)));
            menu.Items.Add(new ToolStripMenuItem("Klasoru Ac", null, (s, e) => OpenPath(Config.DataDir)));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Cikis", null, (s, e) => ExitApp()));

            menu.Opening += (s, e) => UpdateStatusText();
            return menu;
        }

        private void UpdateStatusText()
        {
            string state = _cfg.Enabled ? "Etkin" : "Devre disi";
            string conf = _cfg.IsConfigured() ? "yapilandirildi" : "YAPILANDIRILMADI";
            _statusItem.Text = "LoginGuard - " + state + " (" + conf + ")";
        }

        private void ToggleEnabled()
        {
            _cfg.Enabled = _enabledItem.Checked;
            _cfg.Save();
            _tray.Icon = IconFactory.CreateTrayIcon(_cfg.Enabled);
            Logger.Log("Durum degistirildi: " + (_cfg.Enabled ? "Etkin" : "Devre disi"));
        }

        private void OpenSettings()
        {
            using (var form = new SettingsForm(_cfg))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    _cfg = Config.Load();
                    _enabledItem.Checked = _cfg.Enabled;
                    _tray.Icon = IconFactory.CreateTrayIcon(_cfg.Enabled);
                    _tray.ShowBalloonTip(3000, "LoginGuard", "Ayarlar kaydedildi.", ToolTipIcon.Info);
                }
            }
        }

        private void TestCapture()
        {
            if (!_cfg.IsConfigured()) { OpenSettings(); return; }
            _tray.ShowBalloonTip(3000, "LoginGuard", "Test yakalama basladi (foto+video)...", ToolTipIcon.Info);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { CaptureEngine.Run("", true); } catch (Exception ex) { Logger.Log("Test hata: " + ex.Message); }
            });
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            switch (e.Reason)
            {
                case SessionSwitchReason.SessionLock: Logger.Log("Oturum kilitlendi."); break;
                case SessionSwitchReason.SessionUnlock: Logger.Log("Oturum kilidi acildi."); break;
                case SessionSwitchReason.SessionLogon: Logger.Log("Oturum acildi (logon)."); break;
                case SessionSwitchReason.SessionLogoff: Logger.Log("Oturum kapandi (logoff)."); break;
            }
        }

        private void OnSessionEnding(object sender, SessionEndingEventArgs e)
        {
            Logger.Log("Oturum/PC kapaniyor (" + e.Reason + ").");
        }

        private static void OpenPath(string path)
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
        }

        private void ExitApp()
        {
            Logger.Log("Tray kapatildi (kullanici).");
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.SessionEnding -= OnSessionEnding;
            _tray.Visible = false;
            _tray.Dispose();
            ExitThread();
        }
    }
}
