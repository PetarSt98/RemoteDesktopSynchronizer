using Microsoft.Extensions.DependencyInjection;
using RemoteDesktopCleaner.BackgroundServices;
using SynchronizerLibrary.Loggers;
using SynchronizerLibrary.CommonServices;
using Microsoft.Extensions.Hosting;
using System.Security;

namespace RemoteDesktopCleaner
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                CheckCleanerStatus();
                EnsureDirectoriesExist();

                LoggerSingleton.General.Info($"Starting RemoteDesktopClearner console app");
                Console.WriteLine("Starting sychronizer");
                LoggerSingleton.General.Info("Starting sychronizer");

                // Create and configure the host
                var host = CreateHostBuilder(args).Build();

                // Start the host
                await host.RunAsync();

                LoggerSingleton.General.Info("Finished synchronizing");
                Console.WriteLine("Finished synchronizing");
            }
            catch (Exception ex)
            {
                LoggerSingleton.General.Fatal(ex.Message);
                Console.Error.WriteLine(ex.Message);
            }
        }
        public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureServices((hostContext, services) =>
        {
                    // Register your services and background tasks
                    ConfigureServices(services);
            services.AddHostedService<SynchronizationWorker>();
        });

        private static void EnsureDirectoriesExist()
        {
            string[] directories = { "Logs", "Info", "Cache" };

            foreach (var directory in directories)
            {
                string directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, directory);

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
            }
        }

        private static void CheckCleanerStatus()
        {
            try
            {
                string variableName = "CLEANER_STATUS";

                string value = Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.Machine);

                if (value != null)
                {
                    Console.WriteLine($"Cleaner is {value}");

                    if (value == "ON")
                    {
                        Console.WriteLine("Warning: Cleaner is ON.");
                        Environment.Exit(1);
                    }

                }

                Console.WriteLine($"Continue with Synchronization.");
            }
            catch (SecurityException)
            {
                Console.WriteLine("Error: Administrative privileges are required to modify system environment variables.");
                Environment.Exit(1);
            }
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            LoggerSingleton.General.Info("Configuring services");

            // Register services and dependencies
            services.AddSingleton<IGatewayRapSynchronizer, GatewayRapSynchronizer>();
            services.AddSingleton<ISynchronizer, Synchronizer>();
            services.AddSingleton<IGatewayLocalGroupSynchronizer, GatewayLocalGroupSynchronizer>();
            services.AddSingleton<SynchronizationWorker>();

            // No need to build and return the service provider here
            // var serviceProvider = services.BuildServiceProvider();
            // return serviceProvider;
        }
    }
}
