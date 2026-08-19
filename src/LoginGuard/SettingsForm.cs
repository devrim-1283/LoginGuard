using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace LoginGuard
{
    // Ayarlar penceresi: Telegram token, chat_id, video suresi, kamera secimi + Test butonu.
    // Kamera listesi UI'yi bloklamadan arka planda yuklenir.
    public class SettingsForm : Form
    {
        private readonly Config _cfg;
        private TextBox _token;
        private CheckBox _showToken;
        private TextBox _chat;
        private NumericUpDown _dur;
        private ComboBox _cam;
        private Button _test;
        private Button _save;
        private Label _status;

        public SettingsForm(Config cfg)
        {
            _cfg = cfg;
            BuildUi();
            LoadValues();
            Shown += (s, e) => LoadCamerasAsync();
        }

        private void BuildUi()
        {
            Text = "LoginGuard - Ayarlar";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false; MinimizeBox = false;
            ShowInTaskbar = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9.5f);
            ClientSize = new Size(540, 470);
            Padding = new Padding(0);
            BackColor = Color.White;

            const int L = 24;              // sol kenar
            const int W = 492;             // icerik genisligi
            int y = 22;

            // Baslik
            var header = new Label
            {
                Left = L, Top = y, Width = W, Height = 30,
                Text = "LoginGuard Ayarlari",
                Font = new Font("Segoe UI Semibold", 13.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 90, 158)
            };
            Controls.Add(header);
            y += 44;

            // Bot Token
            Controls.Add(MakeLabel("Telegram Bot Token", L, y, W - 90));
            _showToken = new CheckBox
            {
                Left = L + W - 84, Top = y - 2, Width = 84, Height = 22,
                Text = "Goster", Font = new Font("Segoe UI", 8.5f)
            };
            _showToken.CheckedChanged += (s, e) => _token.UseSystemPasswordChar = !_showToken.Checked;
            Controls.Add(_showToken);
            y += 24;
            _token = new TextBox { Left = L, Top = y, Width = W, Height = 28, UseSystemPasswordChar = true };
            Controls.Add(_token);
            y += 38;

            // Chat ID
            Controls.Add(MakeLabel("Chat ID (bota mesaj atan kullanicinin id'si)", L, y, W));
            y += 24;
            _chat = new TextBox { Left = L, Top = y, Width = W, Height = 28 };
            Controls.Add(_chat);
            y += 38;

            // Video suresi
            Controls.Add(MakeLabel("Video suresi (saniye)", L, y, 200));
            y += 24;
            _dur = new NumericUpDown { Left = L, Top = y, Width = 90, Height = 28, Minimum = 1, Maximum = 60, Value = 5 };
            Controls.Add(_dur);
            y += 40;

            // Kamera
            Controls.Add(MakeLabel("Kamera", L, y, W));
            y += 24;
            _cam = new ComboBox { Left = L, Top = y, Width = W, Height = 28, DropDownStyle = ComboBoxStyle.DropDownList };
            _cam.Items.Add("(kameralar yukleniyor...)");
            _cam.SelectedIndex = 0;
            _cam.Enabled = false;
            Controls.Add(_cam);
            y += 46;

            // Butonlar
            _test = new Button { Left = L, Top = y, Width = 165, Height = 34, Text = "Baglantiyi Test Et" };
            _test.Click += OnTest;
            Controls.Add(_test);

            var cancel = new Button { Left = L + W - 110, Top = y, Width = 110, Height = 34, Text = "Iptal", DialogResult = DialogResult.Cancel };
            Controls.Add(cancel);

            _save = new Button { Left = L + W - 232, Top = y, Width = 116, Height = 34, Text = "Kaydet", DialogResult = DialogResult.OK };
            _save.Click += OnSave;
            _save.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            Controls.Add(_save);
            y += 46;

            // Durum
            _status = new Label { Left = L, Top = y, Width = W, Height = 44, ForeColor = Color.DimGray, Text = "" };
            Controls.Add(_status);
            y += 46;

            // Yardim linki
            var help = new LinkLabel
            {
                Left = L, Top = y, Width = W, Height = 22,
                Text = "Bot token nasil alinir? @BotFather'da /newbot",
                Font = new Font("Segoe UI", 8.5f)
            };
            help.LinkClicked += (s, e) => OpenUrl("https://t.me/BotFather");
            Controls.Add(help);

            AcceptButton = _save;
            CancelButton = cancel;
        }

        private Label MakeLabel(string text, int x, int y, int w)
        {
            return new Label { Left = x, Top = y, Width = w, Height = 20, Text = text, ForeColor = Color.FromArgb(60, 60, 60) };
        }

        private void LoadValues()
        {
            _token.Text = _cfg.BotToken;
            _chat.Text = _cfg.ChatId;
            _dur.Value = Math.Max(1, Math.Min(60, _cfg.VideoDurationSec));
        }

        private void LoadCamerasAsync()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                System.Collections.Generic.List<string> devices;
                try { devices = CaptureEngine.ListVideoDevices(_cfg); }
                catch { devices = new System.Collections.Generic.List<string>(); }

                BeginInvoke((Action)(() =>
                {
                    _cam.Items.Clear();
                    _cam.Items.Add("(otomatik tespit)");
                    foreach (string name in devices) _cam.Items.Add(name);
                    _cam.Enabled = true;

                    int idx = 0;
                    if (!string.IsNullOrEmpty(_cfg.CameraName))
                    {
                        int found = _cam.Items.IndexOf(_cfg.CameraName);
                        if (found < 0) { _cam.Items.Add(_cfg.CameraName); found = _cam.Items.Count - 1; }
                        idx = found;
                    }
                    _cam.SelectedIndex = idx;
                }));
            });
        }

        private void OnTest(object sender, EventArgs e)
        {
            _status.ForeColor = Color.DimGray;
            _status.Text = "Test mesaji gonderiliyor...";
            _test.Enabled = false;
            string token = _token.Text.Trim(), chat = _chat.Text.Trim();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                bool ok = new Telegram(token, chat).SendMessage("LoginGuard baglanti testi - calisiyor.");
                BeginInvoke((Action)(() =>
                {
                    _status.ForeColor = ok ? Color.Green : Color.Firebrick;
                    _status.Text = ok
                        ? "Basarili! Telegram'a test mesaji dustu."
                        : "Basarisiz. Token/chat_id'yi kontrol edin ve bota once /start deyin.";
                    _test.Enabled = true;
                }));
            });
        }

        private void OnSave(object sender, EventArgs e)
        {
            _cfg.BotToken = _token.Text.Trim();
            _cfg.ChatId = _chat.Text.Trim();
            _cfg.VideoDurationSec = (int)_dur.Value;
            _cfg.CameraName = (_cam.SelectedIndex <= 0 || !_cam.Enabled) ? "" : _cam.SelectedItem.ToString();
            try { _cfg.Save(); }
            catch (Exception ex)
            {
                MessageBox.Show("Kaydedilemedi: " + ex.Message + "\n(Kurulum yonetici olarak yapildi mi?)",
                    "LoginGuard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        }

        private static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }
    }
}
