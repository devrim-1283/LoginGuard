using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace LoginGuard
{
    // Tray ikonunu calisma zamaninda cizer (harici .ico dosyasi gerekmez).
    // Kalkan + kamera lensi: guvenlik izleme sembolu.
    public static class IconFactory
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static Icon CreateTrayIcon(bool enabled)
        {
            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                Color shield = enabled ? Color.FromArgb(0, 120, 215) : Color.FromArgb(120, 120, 120);

                // Kalkan govdesi
                using (var path = new GraphicsPath())
                {
                    path.AddLine(16, 2, 29, 7);
                    path.AddLine(29, 7, 29, 17);
                    path.AddBezier(29, 17, 29, 25, 22, 29, 16, 31);
                    path.AddBezier(16, 31, 10, 29, 3, 25, 3, 17);
                    path.AddLine(3, 17, 3, 7);
                    path.CloseFigure();
                    using (var b = new SolidBrush(shield)) g.FillPath(b, path);
                    using (var pen = new Pen(Color.White, 1.5f)) g.DrawPath(pen, path);
                }

                // Kamera lensi (beyaz halka + koyu merkez)
                using (var wb = new SolidBrush(Color.White)) g.FillEllipse(wb, 10, 11, 12, 12);
                using (var db = new SolidBrush(Color.FromArgb(30, 30, 30))) g.FillEllipse(db, 13, 14, 6, 6);
                using (var hb = new SolidBrush(Color.White)) g.FillEllipse(hb, 14, 15, 2, 2);

                IntPtr hIcon = bmp.GetHicon();
                try { return (Icon)Icon.FromHandle(hIcon).Clone(); }
                finally { DestroyIcon(hIcon); }
            }
        }
    }
}
