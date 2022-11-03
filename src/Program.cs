using System;
using Gtk;
using Microsoft.Extensions.Logging;

namespace cave
{
    class Program
    {
        private static ILogger logger;
        public static ILoggerFactory LogFactory { get; private set; }

        [STAThread]
        public static void Main(string[] args)
        {
            Application.Init();

            var app = new Application("org.uca.avs.cave", GLib.ApplicationFlags.None);
            app.Register(GLib.Cancellable.Current);

            LogFactory = LoggerFactory.Create( builder => {
                builder
                    .AddFilter("MainWindow", LogLevel.Warning)
                    .AddFilter("NEC", LogLevel.Information)
                    .AddFilter("NEC.Client", LogLevel.Information)
                    .AddSimpleConsole( options => {
                        options.IncludeScopes = true;
                        options.SingleLine = true;
                        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
                    } );
            } );
            logger = LogFactory.CreateLogger("Program");

            var win = new MainWindow();
            app.AddWindow(win);

            win.Show();
            Application.Run();
        }
    }
}
