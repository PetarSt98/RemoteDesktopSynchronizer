﻿using Unity;
using RemoteDesktopCleaner.BackgroundServices;
using Unity.Lifetime;
using RemoteDesktopCleaner.Exceptions;
using RemoteDesktopCleaner.Loggers;

namespace RemoteDesktopCleaner
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                //LoggerSingleton.General.Info($"Starting RemoteDesktopClearner console app");

                UnityContainer container = new UnityContainer();
                ConfigureServices(container);

                SynchronizationWorker cw = container.Resolve<SynchronizationWorker>();
                cw.StartAsync(new CancellationToken());
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                //LoggerSingleton.General.Fatal(ex.Message);
            }
        }

        private static void ConfigureServices(UnityContainer container)
        {
            //LoggerSingleton.General.Info($"Configuring services");
            //container.RegisterType<IConfigValidator, ConfigValidator>(new HierarchicalLifetimeManager());
            container.RegisterType<IGatewayRapSynchronizer, GatewayRapSynchronizer>(new HierarchicalLifetimeManager());
            container.RegisterType<ISynchronizer, Synchronizer>(new HierarchicalLifetimeManager());
            container.RegisterType<IGatewayLocalGroupSynchronizer, GatewayLocalGroupSynchronizer>(new HierarchicalLifetimeManager());
        }
    }
}
