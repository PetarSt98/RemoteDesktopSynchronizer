using SynchronizerLibrary.Data;
using SynchronizerLibrary.Loggers;
using SynchronizerLibrary.CommonServices;
using SynchronizerLibrary.DataBuffer;
using SynchronizerLibrary.CommonServices.LocalGroups;
using System.Data.Entity;

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

        public async Task SynchronizeAsync(string serverName)
        {
            try
            {
                LoggerSingleton.General.Info($"Starting the synchronization of '{serverName}' gateway.");
                Console.WriteLine($"Starting the synchronization of '{serverName}' gateway.\n");
                var cfgDiscrepancy = await GetConfigDiscrepancy(serverName);
                var changedLocalGroups = FilterChangedLocalGroups(cfgDiscrepancy.LocalGroups);
                Console.WriteLine($"Synchronizing LGs on {serverName}");
                var addedGroups = await _gatewayLocalGroupSynchronizer.SyncLocalGroups(changedLocalGroups, serverName);

                LoggerSingleton.General.Info($"Finished getting gateway RAP names for '{serverName}'.");

                Console.WriteLine($"Synchronizing RAPSs on {serverName}");
                await _gatewayRapSynchronizer.SynchronizeRaps(serverName, changedLocalGroups.LocalGroupsToAdd.Where(lg => lg.Name.StartsWith("LG-")).Select(lg => lg.Name).ToList(), new List<string>(), new List<string>());

                LoggerSingleton.General.Info($"Finished synchronization on {serverName}");
            }
            catch (Exception ex)
            {
                LoggerSingleton.General.Error(ex, $"Error while synchronizing gateway: '{serverName}'.");
            }
            Console.WriteLine($"Finished synchronization for gateway '{serverName}'.");
            LoggerSingleton.General.Info($"Finished synchronization for gateway '{serverName}'.");
        }

        private async Task<GatewayConfig> GetConfigDiscrepancy(string serverName)
        {
            LoggerSingleton.General.Info("Started comparing Local Groups and members from database and server");
            var result = new List<LocalGroup>();

            GatewayConfig modelCfgUnsychronizedAdd = await ReadUnsychronizedConfigDbModelAdd();
            foreach (var modelLocalGroup in modelCfgUnsychronizedAdd.LocalGroups)
            {
                var lg = new LocalGroup(modelLocalGroup.Name, LocalGroupFlag.Add);
                lg.ComputersObj.AddRange(GetListDiscrepancyTest(modelLocalGroup.Computers, "Add"));
                lg.MembersObj.AddRange(GetListDiscrepancyTest(modelLocalGroup.Members, "Add"));
                result.Add(lg);
            }
            GatewayConfig modelCfgUnsychronizedUpdate = await ReadUnsychronizedConfigDbModelUpdate();
            foreach (var modelLocalGroup in modelCfgUnsychronizedUpdate.LocalGroups)
            {
                var lg = new LocalGroup(modelLocalGroup.Name, LocalGroupFlag.Add); // It should be CheckUpdates
                lg.ComputersObj.AddRange(GetListDiscrepancyTest(modelLocalGroup.Computers, "Add"));
                lg.MembersObj.AddRange(GetListDiscrepancyTest(modelLocalGroup.Members, "Add"));
                result.Add(lg);
            }

            GatewayConfig modelCfgFailedSychronizedServers = await ReadFailedSychronizedServersConfigDbModel(serverName);
            foreach (var modelLocalGroup in modelCfgFailedSychronizedServers.LocalGroups)
            {
                var lg = new LocalGroup(modelLocalGroup.Name, LocalGroupFlag.Add); // It should be CheckUpdates
                lg.ComputersObj.AddRange(GetListDiscrepancyTest(modelLocalGroup.Computers, "Add"));
                lg.MembersObj.AddRange(GetListDiscrepancyTest(modelLocalGroup.Members, "Add"));
                result.Add(lg);
            }

            GatewayConfig modelCfgUnsychronizedDelete = await ReadUnsychronizedConfigDbModelDelete();
            foreach (var modelLocalGroup in modelCfgUnsychronizedDelete.LocalGroups)
            {
                var lg = new LocalGroup(modelLocalGroup.Name, LocalGroupFlag.CheckForUpdate);
                lg.ComputersObj.AddRange(GetListDiscrepancyTest(modelLocalGroup.Computers, "Delete"));
                lg.MembersObj.AddRange(GetListDiscrepancyTest(modelLocalGroup.Members, "None"));
                result.Add(lg);
            }

            var diff = new GatewayConfig(serverName);
            diff.Add(result);
            return diff;
        }

        private LocalGroupsChanges FilterChangedLocalGroups(List<LocalGroup> allGroups)
        {
            var groupsToDelete = allGroups.Where(lg => lg.Flag == LocalGroupFlag.Delete).ToList();
            var groupsToAdd = allGroups.Where(lg => lg.Flag == LocalGroupFlag.Add).ToList();
            var changedContent = allGroups.Where(lg => lg.Flag == LocalGroupFlag.CheckForUpdate && (lg.MembersObj.Flags.Any(content => content != LocalGroupFlag.None) || lg.ComputersObj.Flags.Any(content => content != LocalGroupFlag.None))).ToList();
            var groupsToSync = new LocalGroupsChanges();

            checkForSpam(groupsToDelete);
            checkForSpam(groupsToAdd);
            checkForSpam(changedContent);

            //markGroupsToDelete(changedContent, groupsToDelete);

            groupsToSync.LocalGroupsToDelete = groupsToDelete;
            groupsToSync.LocalGroupsToAdd = groupsToAdd;
            groupsToSync.LocalGroupsToUpdate = changedContent;
            return groupsToSync;
        }

        private async Task markGroupsToDelete(List<LocalGroup> changedContent, List<LocalGroup> groupsToDelete)
        {
            foreach (var lg in changedContent)
            {
                var validLgs = await GetValidLocalGroups(lg.Name);


                bool deleteGroup = true;

                foreach (var computerFlag in validLgs.ComputersObj.Flags)
                {
                    if (computerFlag != LocalGroupFlag.Delete)
                    {
                        deleteGroup = false;
                    }
                }

                foreach (var computerFlag in lg.ComputersObj.Flags)
                {
                    if (computerFlag != LocalGroupFlag.Delete)
                    {
                        deleteGroup = false;
                    }
                }

                if (deleteGroup)
                {
                    lg.Flag = LocalGroupFlag.Delete;
                    groupsToDelete.Add(lg);
                }
            }
            changedContent.RemoveAll(lg => lg.Flag == LocalGroupFlag.Delete);
        }

        private void checkForSpam(List<LocalGroup> localGroups)
        {
            bool stopSync = true;
            foreach (var lg in localGroups)
            {
                for (int i = 0; i < lg.ComputersObj.Flags.Count; i++)
                {
                    if (lg.ComputersObj.Flags[i] != LocalGroupFlag.None)
                    {
                        if(SpamFailureHandler.CheckStatus(lg.ComputersObj.Names[i].Replace("$", ""), lg.Name.Replace("LG-", "")))
                        {
                            lg.ComputersObj.Flags[i] = LocalGroupFlag.None;

                        }
                    }

                    if (lg.ComputersObj.Flags[i] != LocalGroupFlag.None)
                    {
                        stopSync = false;
                    }
                }
                if (stopSync)
                {
                    for (int i = 0; i < lg.ComputersObj.Flags.Count; i++)
                    {
                        lg.MembersObj.Flags[i] = LocalGroupFlag.None;
                    }
                    lg.Flag = LocalGroupFlag.None;
                }
            }
            localGroups.RemoveAll(lg => lg.Flag == LocalGroupFlag.None);
        }

        private LocalGroupContent GetListDiscrepancyTest(ICollection<string> modelList, string addOrDeleteFlag)
        {

            var flags = new List<LocalGroupFlag>();
            var names = new List<string>();
            if (addOrDeleteFlag == "None")
            {
                flags.AddRange(from el in modelList select LocalGroupFlag.None);
            }
            else if (addOrDeleteFlag == "Add")
                flags.AddRange(from el in modelList select LocalGroupFlag.Add);
            else if (addOrDeleteFlag == "Delete")
                flags.AddRange(from el in modelList select LocalGroupFlag.Delete);
            else
            {
                throw new Exception("Unknown operator!");
            }
            names.AddRange(from el in modelList select el.ToLower());

            return new LocalGroupContent(names, flags);
        }

        public async Task<GatewayConfig> ReadUnsychronizedConfigDbModelAdd()
        {
            LoggerSingleton.General.Info("Getting valid config model.");
            var raps = await GetRapsAsync();
            var unsynchronizedRaps = raps
                        .Where(r => !r.toDelete && (r.synchronized == false ))
                        .ToList();

            var localGroups = new List<LocalGroup>();
            var validRaps = unsynchronizedRaps.Where(IsRapValid);
            foreach (var rap in validRaps)
            {
                var owner = rap.login;
                var resources = rap.rap_resource.Where(IsResourceValid).Where(r => !r.synchronized)
                    .Select(resource => $"{resource.resourceName}$").ToList();
                resources.Add(owner);
                var lg = new LocalGroup(rap.resourceGroupName, resources);
                localGroups.Add(lg);
            }
            var gatewayModel = new GatewayConfig("MODEL", localGroups);
            return gatewayModel;
        }

        public async Task<GatewayConfig> ReadUnsychronizedConfigDbModelUpdate()
        {
            LoggerSingleton.General.Info("Getting valid config model.");
            var raps = await GetRapsAsync();
            var unsynchronizedRaps = raps
                        .Where(r => !r.toDelete && (r.synchronized == true && r.rap_resource.Any(rr => (rr.synchronized == false && !rr.toDelete))))
                        .ToList();

            var localGroups = new List<LocalGroup>();
            var validRaps = unsynchronizedRaps.Where(IsRapValid);
            foreach (var rap in validRaps)
            {
                var owner = rap.login;
                var resources = rap.rap_resource.Where(IsResourceValid).Where(r => !r.synchronized)
                    .Select(resource => $"{resource.resourceName}$").ToList();
                if (resources.Count == 0) continue;
                resources.Add(owner);

                var lg = new LocalGroup(rap.resourceGroupName, resources);
                localGroups.Add(lg);
            }
            var gatewayModel = new GatewayConfig("MODEL", localGroups);
            return gatewayModel;
        }

        public async Task<LocalGroup> GetValidLocalGroups(string lgName)
        {
            var raps = await GetRapsAsync();
            var unsynchronizedRaps = raps
                        .Where(r => r.resourceGroupName == lgName)
                        .ToList();

            var localGroup = new LocalGroup();
            var validRaps = unsynchronizedRaps.Where(IsRapValid);
            foreach (var rap in validRaps)
            {
                var owner = rap.login;
                var resources = rap.rap_resource.Where(IsResourceValid).Where(r => !r.toDelete)
                    .Select(resource => $"{resource.resourceName}$").ToList();
                resources.Add(owner);
                var lg = new LocalGroup(rap.resourceGroupName, resources);
                localGroup= lg;
            }
            return localGroup;
        }

        public async Task<GatewayConfig> ReadFailedSychronizedServersConfigDbModel(string serverName)
        {
            LoggerSingleton.General.Info("Getting failed sync server config model.");
            var raps = await GetRapsAsync();
            var unsynchronizedRaps = raps
                        .Where(r => !r.toDelete && (r.synchronized == true && r.rap_resource.Any(rr => (rr.synchronized == true && !rr.toDelete && rr.unsynchronizedGateways.Contains(serverName))))) 
                        .ToList();

            var localGroups = new List<LocalGroup>();
            var validRaps = unsynchronizedRaps.Where(IsRapValid);
            foreach (var rap in validRaps)
            {
                var owner = rap.login;
                var resources = rap.rap_resource.Where(IsResourceValid).Where(r => r.synchronized && !r.toDelete && r.unsynchronizedGateways.Contains(serverName))
                    .Select(resource => $"{resource.resourceName}$").ToList();
                if (resources.Count == 0) continue;
                resources.Add(owner);

                var lg = new LocalGroup(rap.resourceGroupName, resources);
                localGroups.Add(lg);
            }
            var gatewayModel = new GatewayConfig("MODEL", localGroups);
            return gatewayModel;
        }

        public async Task<GatewayConfig> ReadUnsychronizedConfigDbModelDelete()
        {
            LoggerSingleton.General.Info("Getting valid config model.");
            var raps = await GetRapsAsync();
            var unsynchronizedRaps = raps
                                    .Where(r => !r.toDelete && r.rap_resource.Any(rr => rr.toDelete))
                                    .ToList();

            var localGroups = new List<LocalGroup>();
            foreach (var rap in unsynchronizedRaps)
            {
                var owner = rap.login;
                var resources = rap.rap_resource.Where(IsResourceRemovable)
                    .Select(resource => $"{resource.resourceName}$").ToList();
                resources.Add(owner);
                var lg = new LocalGroup(rap.resourceGroupName, resources);
                localGroups.Add(lg);
            }
            var gatewayModel = new GatewayConfig("MODEL", localGroups);
            return gatewayModel;
        }

        public async Task<IEnumerable<rap>> GetRapsAsync()
        {
            var results = new List<rap>();
            try
            {
                using (var db = new RapContext())
                {
                    results.AddRange(await db.raps.Include("rap_resource").ToListAsync());
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
            return !resource.toDelete && resource.access && resource.invalid.HasValue && !resource.invalid.Value;
            //return !resource.toDelete && resource.invalid.HasValue && !resource.invalid.Value;
        }

        private bool IsResourceRemovable(rap_resource resource)
        {
            return resource.toDelete;
        }
        
    }
}
