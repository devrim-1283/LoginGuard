using System;
using System.Diagnostics.Eventing.Reader;
using System.Xml;

namespace LoginGuard
{
    // Security günlüğünden 4625 (basarisiz giris) olayini okur ve alanlarini cozer.
    // Security günlüğünü okumak icin SYSTEM/yonetici gerekir (yakalama gorevi SYSTEM olarak calisir).
    public class FailedLogon
    {
        public DateTime Time = DateTime.Now;
        public string User = "";
        public string Domain = "";
        public string Workstation = "";
        public string IpAddress = "";
        public string LogonType = "";
        public string SubStatus = "";

        public static FailedLogon Read(string recordId)
        {
            try
            {
                string xpath = string.IsNullOrEmpty(recordId)
                    ? "*[System[EventID=4625]]"
                    : "*[System[EventRecordID=" + recordId + "]]";
                var query = new EventLogQuery("Security", PathType.LogName, xpath);
                query.ReverseDirection = true;
                using (var reader = new EventLogReader(query))
                {
                    EventRecord rec = reader.ReadEvent();
                    if (rec == null) return null;
                    using (rec) { return Parse(rec); }
                }
            }
            catch { return null; }
        }

        private static FailedLogon Parse(EventRecord rec)
        {
            var f = new FailedLogon();
            try { if (rec.TimeCreated.HasValue) f.Time = rec.TimeCreated.Value; } catch { }

            var doc = new XmlDocument();
            doc.LoadXml(rec.ToXml());
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("e", "http://schemas.microsoft.com/win/2004/08/events/event");

            foreach (XmlNode n in doc.SelectNodes("//e:EventData/e:Data", ns))
            {
                var nameAttr = n.Attributes != null ? n.Attributes["Name"] : null;
                if (nameAttr == null) continue;
                string val = n.InnerText;
                switch (nameAttr.Value)
                {
                    case "TargetUserName": f.User = val; break;
                    case "TargetDomainName": f.Domain = val; break;
                    case "WorkstationName": f.Workstation = val; break;
                    case "IpAddress": f.IpAddress = val; break;
                    case "LogonType": f.LogonType = val; break;
                    case "SubStatus": f.SubStatus = val; break;
                }
            }
            return f;
        }

        public static string LogonTypeText(string lt)
        {
            switch (lt)
            {
                case "2": return "Interaktif (klavye/konsol)";
                case "3": return "Ag";
                case "4": return "Batch";
                case "5": return "Servis";
                case "7": return "Kilit acma (Unlock)";
                case "8": return "NetworkCleartext";
                case "9": return "NewCredentials";
                case "10": return "Uzak masaustu (RDP)";
                case "11": return "Onbellekli interaktif";
                default: return string.IsNullOrEmpty(lt) ? "-" : ("Tip " + lt);
            }
        }

        public static string SubStatusText(string ss)
        {
            switch ((ss ?? "").ToUpperInvariant())
            {
                case "0XC000006A": return "Yanlis parola";
                case "0XC0000064": return "Kullanici mevcut degil";
                case "0XC0000234": return "Hesap kilitli";
                case "0XC0000072": return "Hesap devre disi";
                case "0XC0000070": return "Is istasyonu kisitlamasi";
                case "0XC0000193": return "Hesap suresi doldu";
                case "0XC0000071": return "Parola suresi doldu";
                case "0XC000015B": return "Bu oturum turune izin yok";
                default: return string.IsNullOrEmpty(ss) ? "-" : ("Kod: " + ss);
            }
        }
    }
}
