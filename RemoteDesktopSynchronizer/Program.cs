using Microsoft.Extensions.DependencyInjection;
using RemoteDesktopCleaner.BackgroundServices;
using SynchronizerLibrary.Loggers;
using SynchronizerLibrary.CommonServices;


namespace RemoteDesktopCleaner
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                EnsureDirectoriesExist();

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


            }
            catch (Exception ex)
            {
                LoggerSingleton.General.Fatal(ex.Message);
                Console.Error.WriteLine(ex.Message);
            }
        }

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
