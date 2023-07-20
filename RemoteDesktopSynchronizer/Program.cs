using Microsoft.Extensions.DependencyInjection;
using RemoteDesktopCleaner.BackgroundServices;
using RemoteDesktopCleaner.Exceptions;
using SynchronizerLibrary.Loggers;
using SynchronizerLibrary.CommonServices;
using System;
using System.Threading;
using System.Timers;

namespace RemoteDesktopCleaner
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                LoggerSingleton.General.Info($"Starting RemoteDesktopClearner console app");
                Console.WriteLine("Starting sychronizer");
                LoggerSingleton.General.Info("Starting sychronizer");

                // Configure services
                var serviceProvider = ConfigureServices();

                LoggerSingleton.General.Info("Setting up workers");
                Console.WriteLine("Setting up workers");
                var cw = serviceProvider.GetService<SynchronizationWorker>();

                if (cw == null)
                {
                    LoggerSingleton.General.Error("Failed to resolve SynchronizationWorker from the DI container.");
                    Console.WriteLine("Failed to resolve SynchronizationWorker from the DI container.");
                    return;
                }

                LoggerSingleton.General.Info("Starting initial synchronization");
                Console.WriteLine("Starting initial synchronization");
                cw.StartAsync(CancellationToken.None).Wait();
                Console.WriteLine("Finished synchronizing");
                LoggerSingleton.General.Info("Finished synchronizing");

                Console.WriteLine("Starting scheduled synchronizer");
                LoggerSingleton.General.Info("Starting scheduled synchronizer");
                System.Timers.Timer timer = new System.Timers.Timer(5 * 60 * 1000);
                bool isFirstTime = true;

                timer.Elapsed += (sender, e) =>
                {
                    if (isFirstTime)
                    {
                        isFirstTime = false;
                        return; // Skip running StartAsync on the first elapsed event
                    }
                    Console.WriteLine("Starting synchronizer");
                    LoggerSingleton.General.Info("Starting synchronizer");
                    cw.StartAsync(CancellationToken.None).Wait();
                    Console.WriteLine("Finished synchronizing");
                    LoggerSingleton.General.Info("Finished synchronizing");
                };

                timer.Start();

                // Keep the application running
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                LoggerSingleton.General.Fatal(ex.Message);
                Console.Error.WriteLine(ex.Message);
            }
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            LoggerSingleton.General.Info("Configuring services");

            // Register services and dependencies
            services.AddSingleton<IGatewayRapSynchronizer, GatewayRapSynchronizer>();
            services.AddSingleton<ISynchronizer, Synchronizer>();
            services.AddSingleton<IGatewayLocalGroupSynchronizer, GatewayLocalGroupSynchronizer>();
            services.AddSingleton<SynchronizationWorker>();

            // Build the service provider
            var serviceProvider = services.BuildServiceProvider();

            return serviceProvider;
        }
    }
}
