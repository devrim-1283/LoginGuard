using System;
using System.IO;
using System.Net;
using System.Net.Http;

namespace LoginGuard
{
    // Telegram Bot API istemcisi (multipart, HttpClient - harici bagimlilik yok).
    public class Telegram
    {
        private readonly string _token;
        private readonly string _chat;
        private static readonly HttpClient _http;

        static Telegram()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            _http = new HttpClient();
            _http.Timeout = TimeSpan.FromMinutes(5);
        }

        public Telegram(string token, string chat)
        {
            _token = token;
            _chat = chat;
        }

        private string Api(string method)
        {
            return "https://api.telegram.org/bot" + _token + "/" + method;
        }

        public bool SendMessage(string text)
        {
            try
            {
                var form = new MultipartFormDataContent();
                form.Add(new StringContent(_chat), "chat_id");
                form.Add(new StringContent(text), "text");
                var resp = _http.PostAsync(Api("sendMessage"), form).GetAwaiter().GetResult();
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public bool SendPhoto(string file, string caption)
        {
            return SendFile("sendPhoto", "photo", file, caption);
        }

        public bool SendVideo(string file, string caption)
        {
            return SendFile("sendVideo", "video", file, caption);
        }

        private bool SendFile(string method, string field, string file, string caption)
        {
            try
            {
                var form = new MultipartFormDataContent();
                form.Add(new StringContent(_chat), "chat_id");
                if (!string.IsNullOrEmpty(caption)) form.Add(new StringContent(caption), "caption");
                byte[] bytes = File.ReadAllBytes(file);
                form.Add(new ByteArrayContent(bytes), field, Path.GetFileName(file));
                var resp = _http.PostAsync(Api(method), form).GetAwaiter().GetResult();
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    }
}
