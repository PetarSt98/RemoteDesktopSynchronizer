using Unity;
using RemoteDesktopCleaner.BackgroundServices;
using Unity.Lifetime;
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
                UnityContainer container = new UnityContainer();

                LoggerSingleton.General.Info("Configuring services");
                Console.WriteLine("Configuring services");
                ConfigureServices(container);

                LoggerSingleton.General.Info("Setting up workers");
                Console.WriteLine("Setting up workers");
                SynchronizationWorker cw = container.Resolve<SynchronizationWorker>();


                LoggerSingleton.General.Info("Starting initial synchronization");
                Console.WriteLine("Starting initial synchronization");
                cw.StartAsync(new CancellationToken());
                Console.WriteLine("Finished sychronizing");
                LoggerSingleton.General.Info("Finished sychronizing");

                Console.WriteLine("Starting scheduled sychronizer");
                LoggerSingleton.General.Info("Starting scheduled sychronizer");
                System.Timers.Timer timer = new System.Timers.Timer(5 * 60 * 1000);
                bool isFirstTime = true;

                timer.Elapsed += (sender, e) =>
                {
                    if (isFirstTime)
                    {
                        isFirstTime = false;
                        return; // Skip running StartAsync on the first elapsed event
                    }
                    Console.WriteLine("Starting sychronizer");
                    LoggerSingleton.General.Info("Starting sychronizer");
                    cw.StartAsync(new CancellationToken());
                    Console.WriteLine("Finished sychronizing");
                    LoggerSingleton.General.Info("Finished sychronizing");
                };

                timer.Start();
            }
            catch (Exception ex)
            {
                LoggerSingleton.General.Fatal(ex.Message);
                Console.Error.WriteLine(ex.Message);
            }
        }

        private static void ConfigureServices(UnityContainer container)
        {
            LoggerSingleton.General.Info($"Configuring services");
            container.RegisterType<IGatewayRapSynchronizer, GatewayRapSynchronizer>(new HierarchicalLifetimeManager());
            container.RegisterType<ISynchronizer, Synchronizer>(new HierarchicalLifetimeManager());
            container.RegisterType<IGatewayLocalGroupSynchronizer, GatewayLocalGroupSynchronizer>(new HierarchicalLifetimeManager());
            container.RegisterType<SynchronizationWorker>(new HierarchicalLifetimeManager());
        }
    }
}
