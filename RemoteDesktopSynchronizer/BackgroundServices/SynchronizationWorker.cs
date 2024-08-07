﻿using Microsoft.Extensions.Hosting;
using RemoteDesktopCleaner.Exceptions;
using System.Diagnostics;
using SynchronizerLibrary.Loggers;
using SynchronizerLibrary.Data;
using SynchronizerLibrary.CommonServices;
using SynchronizerLibrary.DataBuffer;
using System.DirectoryServices;
using System.Collections.Concurrent;


namespace RemoteDesktopCleaner.BackgroundServices
{


    public enum ObjectClass
    {
        User,
        Group,
        Computer,
        All,
        Sid
    }
    public sealed class SynchronizationWorker : BackgroundService
    {
        //private readonly IConfigValidator _configValidator;
        private readonly ISynchronizer _synchronizer;
        private readonly IHostApplicationLifetime _appLifetime;

        public SynchronizationWorker(ISynchronizer synchronizer, IHostApplicationLifetime appLifetime)
        {
            _synchronizer = synchronizer;
            _appLifetime = appLifetime;
            //_configValidator = configValidator;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            LoggerSingleton.General.Info("Cleaner Worker is starting.");
            var gatewaysToSynchronize = AppConfig.GetGatewaysInUse();
            stoppingToken.Register(() => LoggerSingleton.General.Info("CleanerWorker background task is stopping."));
            //while (!stoppingToken.IsCancellationRequested) 
            //{
            try
            {
                var raps = new List<rap>();


                //var gatewaysToSynchronize = new List<string> { "cerngt01", "cerngt05", "cerngt06", "cerngt07" };
                //var gatewaysToSynchronize = new List<string> { "cerngt01"};
                var tasks = new List<Task>();
                 
                foreach (var gatewayName in gatewaysToSynchronize)
                {
                    Console.WriteLine($"Starting the synchronization on gateway: {gatewayName}");
                    GlobalInstance.Instance.Names.Add(gatewayName);
                    GlobalInstance.Instance.ObjectLists[gatewayName] = new ConcurrentDictionary<string, RAP_ResourceStatus>();
                    tasks.Add(Task.Run(() => _synchronizer.SynchronizeAsync(gatewayName)));
                }

                // Asynchronously wait for all tasks to complete
                await Task.WhenAll(tasks);

                //foreach (var gatewayName in gatewaysToSynchronize)
                //{

                //    GlobalInstance.Instance.Names.Add(gatewayName);
                //    GlobalInstance.Instance.ObjectLists[gatewayName] = new Dictionary<string, RAP_ResourceStatus>();
                //    _synchronizer.SynchronizeAsync(gatewayName);
                //}

                DatabaseSynchronizator databaseSynchronizator = new DatabaseSynchronizator();
                databaseSynchronizator.AverageGatewayReults();
                databaseSynchronizator.UpdateDatabase();

                using (var db = new RapContext())
                {
                    UpdateDatabase(db);
                }

                _appLifetime.StopApplication();
                //break;
            }
            catch (OperationCanceledException)
            {
                LoggerSingleton.General.Info("Program canceled.");
                //break;
            }
            catch (CloningException)
            {
                //break;
            }
            catch (Exception ex)
            {
                LoggerSingleton.General.Fatal(ex.ToString());
                Console.WriteLine(ex.ToString());
                //break;
            }
            //}
        }
        static public void UpdateDatabase(RapContext db)
        {
            LoggerSingleton.General.Info("Saving changes into database (marked raps/rap_resources to be deleted)");
            LoggerSingleton.Raps.Info("Saving changes into database (marked raps/rap_resources to be deleted)");

            db.SaveChanges();

            var rapResourcesToDelete = db.rap_resource.Where(rr => rr.toDelete == true).ToList();
            db.rap_resource.RemoveRange(rapResourcesToDelete);

            LoggerSingleton.General.Info("Deleting obsolete RAPs and RAP_Resources from MySQL database");
            LoggerSingleton.Raps.Info("Deleting obsolete RAPs and RAP_Resources from MySQL database");

            db.SaveChanges();
        }

        private IEnumerable<rap> GetRaps(RapContext db)
        {
            var results = new List<rap>();
            try
            {
                results.AddRange(db.raps.Include("rap_resource").ToList());
            }
            catch (Exception)
            {
                LoggerSingleton.General.Fatal("Failed query.");
                Console.WriteLine("Failed query.");
            }
            return results;
        }
    }
}
