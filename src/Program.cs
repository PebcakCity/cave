using System;
using Gtk;
using NLog;

namespace cave
{
    class Program
    {
        private static readonly Logger logger = LogManager.GetLogger("Program");

        [STAThread]
        public static void Main(string[] args)
        {
            Application.Init();

            var app = new Application("org.uca.avs.cave", GLib.ApplicationFlags.None);
            app.Register(GLib.Cancellable.Current);

            var win = new MainWindow();
            app.AddWindow(win);

            win.Show();
            Application.Run();
        }
    }
}
