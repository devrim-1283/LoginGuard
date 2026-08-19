using System;
using System.Diagnostics.Eventing.Reader;
using System.IO;

namespace LoginGuard
{
    // PC baslangic/durma olaylarini System gunlugunden okuyup loga dusurur.
    // 6005 = Olay Gunlugu servisi basladi (~acilis), 6006 = temiz kapanma,
    // 6008 = beklenmedik kapanma, 1074 = kapatma/yeniden baslatma baslatildi.
    // Tray her oturum acilisinda calisir; son islenen kaydin id'sini pcstate.txt'de tutar.
    public static class PcEventLogger
    {
        private static readonly string StatePath = Path.Combine(Config.DataDir, "pcstate.txt");

        public static void Backfill()
        {
            try
            {
                long last = ReadState();
                long newest = last;

                string xpath = "*[System[(EventID=6005 or EventID=6006 or EventID=6008 or EventID=1074)]]";
                var query = new EventLogQuery("System", PathType.LogName, xpath);
                query.ReverseDirection = true; // en yeniden eskiye

                var pending = new System.Collections.Generic.List<string>();
                using (var reader = new EventLogReader(query))
                {
                    EventRecord rec;
                    int guard = 0;
                    while ((rec = reader.ReadEvent()) != null && guard++ < 50)
                    {
                        using (rec)
                        {
                            long rid = rec.RecordId ?? 0;
                            if (rid <= last) break; // daha eskilere gerek yok
                            if (rid > newest) newest = rid;
                            DateTime t = rec.TimeCreated ?? DateTime.Now;
                            string msg = Describe(rec.Id) + " (" + t.ToString("yyyy-MM-dd HH:mm:ss") + ")";
                            pending.Add(msg);
                        }
                    }
                }
                // kronolojik sirayla yaz (eskiden yeniye)
                pending.Reverse();
                foreach (var m in pending) Logger.Log(m);

                if (newest != last) WriteState(newest);
            }
            catch { /* System gunlugu okunamazsa sessiz gec */ }
        }

        private static string Describe(int eventId)
        {
            switch (eventId)
            {
                case 6005: return "PC BASLADI (sistem acildi)";
                case 6006: return "PC DURDU (temiz kapanma)";
                case 6008: return "PC DURDU (beklenmedik kapanma!)";
                case 1074: return "Kapatma/yeniden baslatma baslatildi";
                default: return "Sistem olayi " + eventId;
            }
        }

        private static long ReadState()
        {
            try { if (File.Exists(StatePath)) return long.Parse(File.ReadAllText(StatePath).Trim()); }
            catch { }
            return 0;
        }

        private static void WriteState(long id)
        {
            try { Directory.CreateDirectory(Config.DataDir); File.WriteAllText(StatePath, id.ToString()); }
            catch { }
        }
    }
}
