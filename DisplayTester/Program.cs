using NLog;
using Gtk;

// NiceLog
using Cave.Utils;

namespace Cave.DisplayTester
{
    internal class Program
    {
        [STAThread]
        public static void Main( string[] args )
        {
            NLog.LogManager.Setup().SetupExtensions(ext =>
            {
                ext.RegisterLayoutRenderer<NiceLog>();
            });

            Application.Init();

            var app = new Application("org.DisplayTester.DisplayTester", GLib.ApplicationFlags.None);
            app.Register(GLib.Cancellable.Current);

            var win = new MainWindow();
            app.AddWindow(win);

            win.Show();
            Application.Run();
        }
    }
}
