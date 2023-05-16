using Microsoft.Extensions.Hosting;
using RemoteDesktopCleaner.Exceptions;
using System.Diagnostics;
using SynchronizerLibrary.Loggers;
using SynchronizerLibrary.Data;
using SynchronizerLibrary.CommonServices;


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

        public SynchronizationWorker(ISynchronizer synchronizer)
        {
            _synchronizer = synchronizer;
            //_configValidator = configValidator;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            LoggerSingleton.General.Info("Cleaner Worker is starting.");
            var gateways = AppConfig.GetGatewaysInUse();
            stoppingToken.Register(() => LoggerSingleton.General.Info("CleanerWorker background task is stopping."));
            //while (!stoppingToken.IsCancellationRequested) 
            //{
                try
                {
                var raps = new List<rap>();
                using (var context = new RapContext())
                {
                    raps.AddRange(GetRaps(context));
                    var unsynchronizedRaps = context.raps
                                            .Where(r => r.synchronized == false || r.rap_resource.Any(rr => rr.synchronized == false))
                                            .ToList();
                    //var unsynchronizedRaps = context.raps.Where(r => r.synchronized == false).ToList();

                    //var synchronizedRapsunsynchronizedRapResources = context.raps.Where(r => r.synchronized == false).ToList();

                    //var unsynchronizedLogs = context.synchronized_rap
                    //                        .Where(r => r != null && r.success == false)
                    //                        .ToList();



                    _synchronizer.SynchronizeAsync("cerngt01", unsynchronizedRaps);

          
                    UpdateDatabase(context);
                }
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

            var rapsToDelete = db.raps.Where(r => r.toDelete == true).ToList();
            db.raps.RemoveRange(rapsToDelete);

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

        private readonly string source_username = @"CERN\pstojkov";
        private readonly string source_password = "GeForce9800GT.";
        private readonly string target_hostname = "dbod-remotedesktop.cern.ch";
        private readonly string target_username = "admin";
        private readonly string target_password = "oUgDdp5AnSzrvizXtN";
        private readonly string target_database = "RemoteDesktop";
        private readonly string target_port = "5500";

        private void CloneSQL2MySQLdb()
        {
            LoggerSingleton.General.Info("Started cloning RemoteDesktop database as MySQL database.");
            string scriptPath = $@"{Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName}\PowerShellScripts\database_copy_SQL2MySQL.ps1";
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" -source_username \"{source_username}\" -source_password \"{source_password}\" -target_hostname \"{target_hostname}\" -target_username \"{target_username}\" -target_password \"{target_password}\" -target_database \"{target_database}\" -target_port \"{target_port}\" -Add_toDelete_column",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process process = new Process { StartInfo = startInfo };
            process.Start();

            string errors = process.StandardError.ReadToEnd();
            if (errors.Length > 0)
            {
                LoggerSingleton.General.Fatal("Failed cloning RemoteDesktop database as MySQL database.");
                throw new CloningException();
            }
            process.WaitForExit();
            LoggerSingleton.General.Info("Successful cloning RemoteDesktop database as MySQL database.");
        }

    }
}
