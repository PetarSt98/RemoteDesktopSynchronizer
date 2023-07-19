using System.Text.Json;
using SynchronizerLibrary.Data;
using SynchronizerLibrary.Loggers;
using SynchronizerLibrary.CommonServices;


namespace RemoteDesktopCleaner.BackgroundServices
{
    public class Synchronizer : ISynchronizer
    {
        private readonly IGatewayRapSynchronizer _gatewayRapSynchronizer;
        private readonly IGatewayLocalGroupSynchronizer _gatewayLocalGroupSynchronizer;

        public Synchronizer(IGatewayRapSynchronizer gatewayRapSynchronizer, IGatewayLocalGroupSynchronizer gatewayLocalGroupSynchronizer)
        {
            _gatewayLocalGroupSynchronizer = gatewayLocalGroupSynchronizer;
            _gatewayRapSynchronizer = gatewayRapSynchronizer;
        }

        public async void SynchronizeAsync(string serverName, List<rap> unsynchronizedRap)
        {
            try
            {
                LoggerSingleton.General.Info($"Starting the synchronization of '{serverName}' gateway.");

                var cfgDiscrepancy = GetConfigDiscrepancy(serverName);
                var changedLocalGroups = FilterChangedLocalGroups(cfgDiscrepancy.LocalGroups);

                var addedGroups = _gatewayLocalGroupSynchronizer.SyncLocalGroups(changedLocalGroups, serverName);

                LoggerSingleton.General.Info($"Finished getting gateway RAP names for '{serverName}'.");

                _gatewayRapSynchronizer.SynchronizeRaps(serverName, changedLocalGroups.LocalGroupsToAdd.Where(lg => lg.Name.StartsWith("LG-")).Select(lg => lg.Name).ToList(), new List<string>(), new List<string>());

                LoggerSingleton.General.Info("Finished synchronization");
            }
            catch (Exception ex)
            {
                LoggerSingleton.General.Error(ex, $"Error while synchronizing gateway: '{serverName}'.");
            }
            Console.WriteLine($"Finished synchronization for gateway '{serverName}'.");
            LoggerSingleton.General.Info($"Finished synchronization for gateway '{serverName}'.");
        }

        private GatewayConfig GetConfigDiscrepancy(string serverName)
        {
            LoggerSingleton.General.Info("Started comparing Local Groups and members from database and server");
            GatewayConfig modelCfgUnsychronized = ReadUnsychronizedConfigDbModel();
            var result = new List<LocalGroup>();
            foreach (var modelLocalGroup in modelCfgUnsychronized.LocalGroups)
            {
                var lg = new LocalGroup(modelLocalGroup.Name, LocalGroupFlag.Add);
                lg.ComputersObj.AddRange(GetListDiscrepancyTest(modelLocalGroup.Computers, new List<string>()));
                lg.MembersObj.AddRange(GetListDiscrepancyTest(modelLocalGroup.Members, new List<string>()));
                result.Add(lg);
            }
            var diff = new GatewayConfig("cerngt01");
            diff.Add(result);
            return diff;
        }

        private LocalGroupsChanges FilterChangedLocalGroups(List<LocalGroup> allGroups)
        {
            var groupsToDelete = allGroups.Where(lg => lg.Flag == LocalGroupFlag.Delete).ToList();
            var groupsToAdd = allGroups.Where(lg => lg.Flag == LocalGroupFlag.Add).ToList();
            var changedContent = allGroups.Where(lg => lg.Flag == LocalGroupFlag.CheckForUpdate && lg.MembersObj.Flags.Any(content => content != LocalGroupFlag.None)).ToList();
            var groupsToSync = new LocalGroupsChanges();
            groupsToSync.LocalGroupsToDelete = groupsToDelete;
            groupsToSync.LocalGroupsToAdd = groupsToAdd;
            groupsToSync.LocalGroupsToUpdate = changedContent;
            return groupsToSync;
        }

        private LocalGroupContent GetListDiscrepancyTest(ICollection<string> modelList, ICollection<string> otherList)
        {

            var flags = new List<LocalGroupFlag>();
            var names = new List<string>();
            flags.AddRange(from el in modelList select LocalGroupFlag.Add);
            names.AddRange(from el in modelList select el.ToLower());

            return new LocalGroupContent(names, flags);
        }

        public GatewayConfig ReadUnsychronizedConfigDbModel()
        {
            LoggerSingleton.General.Info("Getting valid config model.");
            var raps = GetRaps();
            var unsynchronizedRaps = raps
                                    .Where(r => r.synchronized == false || r.rap_resource.Any(rr => rr.synchronized == false))
                                    .ToList();

            var localGroups = new List<LocalGroup>();
            var validRaps = unsynchronizedRaps.Where(IsRapValid);
            foreach (var rap in validRaps)
            {
                var owner = rap.login;
                var resources = rap.rap_resource.Where(IsResourceValid)
                    .Select(resource => $"{resource.resourceName}$").ToList();
                resources.Add(owner);
                var lg = new LocalGroup(rap.resourceGroupName, resources);
                localGroups.Add(lg);
            }
            var gatewayModel = new GatewayConfig("MODEL", localGroups);
            return gatewayModel;
        }


        private void SaveToFile(GatewayConfig diff)
        {
            try
            {
                var path = AppConfig.GetInfoDir() + @"\" + diff.ServerName + "-diff.json";
                File.WriteAllText(path, JsonSerializer.Serialize(diff));
            }
            catch (Exception ex)
            {
                LoggerSingleton.General.Warn($"{ex.ToString()} Failed saving gateway: '{diff.ServerName}' discrepancy config to file.");
            }
        }

        public IEnumerable<rap> GetRaps()
        {
            var results = new List<rap>();
            try
            {
                using (var db = new RapContext())
                {
                    results.AddRange(db.raps.Include("rap_resource").ToList());
                }
            }
            catch (Exception ex)
            {
                LoggerSingleton.General.Fatal($"Failed query. {ex}");
                Console.WriteLine("Failed query.");
            }

            return results;
        }

        private bool IsRapValid(rap rap)
        {
            return !rap.toDelete;
        }

        private bool IsResourceValid(rap_resource resource)
        {
            return !resource.toDelete && resource.invalid.HasValue && !resource.invalid.Value;
        }

        private bool IsRapInvalid(rap rap)
        {
            return rap.toDelete;
        }

        private bool IsResourceInvalid(rap_resource resource)
        {
            return resource.toDelete || !resource.invalid.HasValue || resource.invalid.Value;
        }
    }
}
