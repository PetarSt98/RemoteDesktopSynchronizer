using System.DirectoryServices;
using System.Collections;
using System.Text.Json;
using SynchronizerLibrary.Loggers;
using SynchronizerLibrary.CommonServices.LocalGroups;
using SynchronizerLibrary.CommonServices.LocalGroups.Operations;
using SynchronizerLibrary.Caching;


namespace SynchronizerLibrary.CommonServices
{
    public enum ObjectClass
    {
        User,
        Group,
        Computer,
        All,
        Sid
    }

    public class GatewayLocalGroupSynchronizer : IGatewayLocalGroupSynchronizer
    {
        private const string AdSearchGroupPath = "WinNT://{0}/{1},group";
        private readonly DirectoryEntry _rootDir = new DirectoryEntry("LDAP://DC=cern,DC=ch");
        public GatewayLocalGroupSynchronizer()
        {
        }
        public bool DownloadGatewayConfig(string serverName, bool cacheFlag = false)
        {
            if (cacheFlag)
            {
                Console.WriteLine("Using cached downloaded Gateway Config");
                return true;
            }
            LoggerSingleton.General.Info($"Started fetching Local Groups from the server {serverName}");
            return ReadRemoteGatewayConfig(serverName);
        }
        private bool ReadRemoteGatewayConfig(string serverName)
        {
            try
            {
                LoggerSingleton.General.Info(serverName, "Downloading the gateway config.");
                LoggerSingleton.SynchronizedLocalGroups.Info(serverName, "Downloading the gateway config.");
                var localGroups = new List<LocalGroup>();
                var server = $"{serverName}.cern.ch";
                var path = serverName + ".json";
                var localGroupNames = GetAllLocalGroups(server);
                var i = 1;
                foreach (var lg in localGroupNames)
                {
                    Console.Write($"\rDownloading {serverName} config - {i + 1}/{localGroupNames.Count} - {100 * (i + 1) / localGroupNames.Count}%"); //TODO delete
                    LoggerSingleton.SynchronizedLocalGroups.Debug($"\r {i} - Downloading {serverName} config - {i + 1}/{localGroupNames.Count} - {100 * (i + 1) / localGroupNames.Count}%");
                    var members = GetGroupMembers(lg, serverName + ".cern.ch");
                    localGroups.Add(new LocalGroup(lg, members));
                    i++;
                }

                Cacher.SaveLocalGroupCacheToFile(localGroups, serverName);

                //File.WriteAllText(newCacheFilePath, JsonSerializer.Serialize(localGroups));
                LoggerSingleton.General.Info(serverName, "Gateway config downloaded.");
                LoggerSingleton.SynchronizedLocalGroups.Info(serverName, "Gateway config downloaded.");
                return true;
            }
            catch (Exception ex)
            {
                LoggerSingleton.General.Fatal($"{ex.ToString()} Error while reading gateway: '{serverName}' config.");
                return false;
            }
        }

        public List<string> GetAllLocalGroups(string serverName)
        {
            var localGroups = new List<string>();
            try
            {
                //string username = "";
                //string password = "";
                //using (var groupEntry = new DirectoryEntry($"WinNT://{serverName},computer", username, password))
                using (var groupEntry = new DirectoryEntry($"WinNT://{serverName},computer"))
                {
                    foreach (DirectoryEntry child in (IEnumerable)groupEntry.Children)
                    {
                        if (child.SchemaClassName.Equals("group", StringComparison.OrdinalIgnoreCase) &&
                            child.Name.StartsWith("LG-", StringComparison.OrdinalIgnoreCase))
                        {
                            localGroups.Add(child.Name);
                        }
                    }
                }
            }

            catch (Exception ex)
            {
                LoggerSingleton.General.Fatal($"{ex.ToString()} Error getting all local groups on gateway: '{serverName}'.");
            }

            return localGroups;
        }

        public List<string> GetGroupMembers(string groupName, string serverName, ObjectClass memberType = ObjectClass.All)
        {
            var downloadedMembers = new List<string>();
            try
            {
                if (string.IsNullOrEmpty(groupName))
                {
                    LoggerSingleton.SynchronizedLocalGroups.Error("Group name not specified.");
                    throw new Exception("Group name not specified.");
                }
                    
                using (var groupEntry = new DirectoryEntry(string.Format(AdSearchGroupPath, serverName, groupName)))
                {
                    if (groupEntry == null) throw new Exception($"Group '{groupName}' not found on gateway: '{serverName}'.");

                    foreach (var member in (IEnumerable)groupEntry.Invoke("Members"))
                    {
                        string memberName = GetGroupMember(member, memberType);
                        if (memberName != null)
                        {
                            LoggerSingleton.SynchronizedLocalGroups.Debug($"Downloaded member: {memberName} from LG: {groupName} from server: {serverName}");
                            downloadedMembers.Add(memberName);
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                LoggerSingleton.General.Fatal($"{ex.ToString()} Error while getting members of group: '{groupName}' from gateway: '{serverName}'");
            }

            return downloadedMembers;
        }

        private string GetGroupMember(object member, ObjectClass memberType)
        {
            string result;
            using (var memberEntryNt = new DirectoryEntry(member))
            {
                string memberName = memberEntryNt.Name;
                using (var ds = new DirectorySearcher(_rootDir))
                {
                    switch (memberType)
                    {
                        case ObjectClass.User:
                            ds.Filter =
                                $"(&(objectCategory=CN=Person,CN=Schema,CN=Configuration,DC=cern,DC=ch)(samaccountname={memberName}))";
                            break;
                        case ObjectClass.Group:
                            ds.Filter =
                                $"(&(objectCategory=CN=Group,CN=Schema,CN=Configuration,DC=cern,DC=ch)(samaccountname={memberName}))";
                            break;
                        case ObjectClass.Computer:
                            ds.Filter =
                                $"(&(objectCategory=CN=Computer,CN=Schema,CN=Configuration,DC=cern,DC=ch)(samaccountname={memberName}))";
                            break;
                        case ObjectClass.Sid:
                            ds.Filter = $"(&(samaccountname={memberName}))";
                            //if (ds.FindOne() == null)
                            //    ret.Add(memberName.Trim());
                            break;
                    }

                    SearchResult res = ds.FindOne();
                    if (res == null)
                        return null;

                    if (memberType == ObjectClass.Computer)
                        memberName = memberName.Replace('$', ' ');
                    result = memberName.Trim();
                }
            }

            return result;
        }

        public async Task<List<string>> SyncLocalGroups(LocalGroupsChanges changedLocalGroups, string serverName)
        {
            LoggerSingleton.General.Info(serverName, $"There are {changedLocalGroups.LocalGroupsToAdd.Count + changedLocalGroups.LocalGroupsToDelete.Count + changedLocalGroups.LocalGroupsToUpdate.Count} groups to synchronize.");
            LoggerSingleton.General.Info(serverName, $"There are {changedLocalGroups.LocalGroupsToAdd.Count } groups to add to server {serverName}.");
            LoggerSingleton.General.Info(serverName, $"There are {changedLocalGroups.LocalGroupsToDelete.Count } groups to delete from server {serverName}.");
            LoggerSingleton.General.Info(serverName, $"There are {changedLocalGroups.LocalGroupsToUpdate.Count } groups to be updated by editing members/devices on server {serverName}.");

            LocalGroupOperations localGroupOperator = new LocalGroupOperations();

            Console.WriteLine($"Deleting LGs on {serverName}");
            await localGroupOperator.deleteLocalGroup.DeleteGroups(serverName, changedLocalGroups.LocalGroupsToDelete);
            Console.WriteLine($"Adding LGs on {serverName}");
            var addedGroups = await localGroupOperator.addLocalGroup.AddNewGroups(serverName, changedLocalGroups.LocalGroupsToAdd);
            Console.WriteLine($"Updating LGs on {serverName}");
            await localGroupOperator.modifyLocalGroup.SyncModifiedGroups(serverName, changedLocalGroups.LocalGroupsToUpdate);

            LoggerSingleton.General.Info($"Finished synchronizing local groups on server {serverName}.");
            LoggerSingleton.SynchronizedLocalGroups.Info($"Finished synchronizing local groups on server {serverName}.");

            return addedGroups;
        }
    }
}
