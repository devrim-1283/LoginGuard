using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace LoginGuard
{
    // Ayarlar penceresi: Telegram token, chat_id, video suresi, kamera secimi + Test butonu.
    public class SettingsForm : Form
    {
        private readonly Config _cfg;
        private TextBox _token;
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
        }

        private void BuildUi()
        {
            Text = "LoginGuard - Ayarlar";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false; MinimizeBox = false;
            ClientSize = new Size(460, 360);
            Font = new Font("Segoe UI", 9f);

            int x = 18, w = 424, y = 16;

            AddLabel("Telegram Bot Token (@BotFather'dan /newbot):", x, y); y += 20;
            _token = new TextBox { Left = x, Top = y, Width = w }; Controls.Add(_token); y += 30;

            AddLabel("Chat ID (bota mesaj atan kullanicinin id'si):", x, y); y += 20;
            _chat = new TextBox { Left = x, Top = y, Width = w }; Controls.Add(_chat); y += 30;

            AddLabel("Video suresi (saniye):", x, y); y += 20;
            _dur = new NumericUpDown { Left = x, Top = y, Width = 100, Minimum = 1, Maximum = 60, Value = 5 };
            Controls.Add(_dur); y += 32;

            AddLabel("Kamera:", x, y); y += 20;
            _cam = new ComboBox { Left = x, Top = y, Width = w, DropDownStyle = ComboBoxStyle.DropDownList };
            Controls.Add(_cam); y += 34;

            _test = new Button { Left = x, Top = y, Width = 150, Height = 30, Text = "Baglantiyi Test Et" };
            _test.Click += OnTest; Controls.Add(_test);

            _save = new Button { Left = x + 160, Top = y, Width = 120, Height = 30, Text = "Kaydet", DialogResult = DialogResult.OK };
            _save.Click += OnSave; Controls.Add(_save);

            var cancel = new Button { Left = x + 290, Top = y, Width = 120, Height = 30, Text = "Iptal", DialogResult = DialogResult.Cancel };
            Controls.Add(cancel);
            y += 40;

            _status = new Label { Left = x, Top = y, Width = w, Height = 40, ForeColor = Color.DimGray, Text = "" };
            Controls.Add(_status);

            AcceptButton = _save;
            CancelButton = cancel;

            PopulateCameras();
        }

        private void AddLabel(string text, int x, int y)
        {
            Controls.Add(new Label { Left = x, Top = y, Width = 424, Height = 18, Text = text });
        }

        private void PopulateCameras()
        {
            _cam.Items.Clear();
            _cam.Items.Add("(otomatik tespit)");
            try
            {
                foreach (string name in CaptureEngine.ListVideoDevices(_cfg))
                    _cam.Items.Add(name);
            }
            catch { }
            _cam.SelectedIndex = 0;
        }

        private void LoadValues()
        {
            _token.Text = _cfg.BotToken;
            _chat.Text = _cfg.ChatId;
            _dur.Value = Math.Max(1, Math.Min(60, _cfg.VideoDurationSec));
            if (!string.IsNullOrEmpty(_cfg.CameraName))
            {
                int idx = _cam.Items.IndexOf(_cfg.CameraName);
                if (idx < 0) { _cam.Items.Add(_cfg.CameraName); idx = _cam.Items.Count - 1; }
                _cam.SelectedIndex = idx;
            }
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
            _cfg.CameraName = (_cam.SelectedIndex <= 0) ? "" : _cam.SelectedItem.ToString();
            try { _cfg.Save(); }
            catch (Exception ex)
            {
                MessageBox.Show("Kaydedilemedi: " + ex.Message + "\n(Yonetici olarak kurulum yapildi mi?)",
                    "LoginGuard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        }
    }
}
