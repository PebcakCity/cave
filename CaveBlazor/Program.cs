using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using NLog;
using NLog.Web;

using Blazored.Toast;

using CaveBlazor.Data;

namespace CaveBlazor
{
    public class Program
    {
        private static Logger logger = NLog.LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();

        public static void Main( string[] args )
        {
            try
            {
                var builder = WebApplication.CreateBuilder(args);

                // Add services to the container.
                builder.Services.AddRazorPages();
                builder.Services.AddServerSideBlazor();
                builder.Services.AddSingleton<WeatherForecastService>();

                builder.Services.AddBlazoredToast();

                builder.Logging.ClearProviders();
                builder.Host.UseNLog();

                var app = builder.Build();

                // Configure the HTTP request pipeline.
                if ( !app.Environment.IsDevelopment() )
                {
                    app.UseExceptionHandler( "/Error" );
                }


                app.UseStaticFiles();

                app.UseRouting();

                app.MapBlazorHub();
                app.MapFallbackToPage( "/_Host" );

                app.Run();
            }
            catch ( Exception ex )
            {
                logger.Error( ex, "Stopped because of exception." );
                throw;
            }
            finally
            {
                NLog.LogManager.Shutdown();
            }
        }
    }
}
