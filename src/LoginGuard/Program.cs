using System;
using System.Threading;
using System.Windows.Forms;

namespace LoginGuard
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // --capture [--record <EventRecordID>] : SYSTEM gorevinin 4625'te cagirdigi yakalama modu
            if (args.Length > 0 && args[0] == "--capture")
            {
                string record = "";
                for (int i = 1; i < args.Length - 1; i++)
                    if (args[i] == "--record") record = args[i + 1];
                CaptureEngine.Run(record);
                return 0;
            }

            // --test : komut satirindan test yakalama
            if (args.Length > 0 && args[0] == "--test")
            {
                CaptureEngine.Run("", true);
                return 0;
            }

            // Argumansiz : tray uygulamasi (tek ornek)
            bool createdNew;
            using (var single = new Mutex(true, "Local\\LoginGuardTraySingleton", out createdNew))
            {
                if (!createdNew) return 0; // zaten calisiyor

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
            }
            return 0;
        }
    }
}
