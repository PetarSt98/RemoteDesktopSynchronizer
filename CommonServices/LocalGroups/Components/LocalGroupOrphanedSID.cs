using System.Collections;
using System.DirectoryServices;
using SynchronizerLibrary.Loggers;


namespace SynchronizerLibrary.CommonServices.LocalGroups.Components
{
    public class LocalGroupOrphanedSID
    {
        private const string AdSearchGroupPath = "WinNT://{0}/{1},group";
        private static readonly DirectoryEntry _rootDir = new DirectoryEntry("LDAP://DC=cern,DC=ch");
        public LocalGroupOrphanedSID()
        {

        }

        public bool CleanFromOrphanedSids(DirectoryEntry localGroup, LocalGroup lg, string serverName)
        {
            try
            {
                bool success;
                success = RemoveOrphanedSids(localGroup, lg, serverName);
                return success;
            }
            catch (Exception ex)
            {
                LoggerSingleton.SynchronizedLocalGroups.Error(ex, $"Error while removing orphaned SIDs from group: '{lg.Name}' on gateway: '{serverName}'.");
                return false;
            }
        }

        public static bool RemoveOrphanedSids(DirectoryEntry groupPrincipal, LocalGroup lg, string serverName)
        {
            var success = true;
            var globalSuccess = true;
            var membersData = lg.MembersObj.Names.Zip(lg.MembersObj.Flags, (i, j) => new { Name = i, Flag = j });

            foreach (var member in membersData)
            {
                Console.WriteLine(member.Name);
                if (!member.Name.StartsWith(Constants.OrphanedSid)) continue;
                try
                {
                    Console.WriteLine($"Removing SID: '{member.Name}'.");
                    LoggerSingleton.SynchronizedLocalGroups.Info($"Removing SID: '{member.Name}'.");
                    groupPrincipal.Invoke("Remove", $"WinNT://{member.Name}");
                    Console.WriteLine($"Successfully removed SID: '{member.Name}'.");
                    LoggerSingleton.Reports.Info($"{serverName}: Removed Orphaned SID {member.Name} from: {lg.Name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed removing SID: '{member.Name}'.");
                    LoggerSingleton.SynchronizedLocalGroups.Error(ex, $"Failed removing SID: '{member.Name}'.");
                    LoggerSingleton.Errors.Error($"{serverName}: Failed to remove Orphaned SID {member} from: {lg.Name}");
                    success = false;
                }

                globalSuccess = globalSuccess && success;
            }

            success = true;
            var members = GetGroupMembers(lg.Name, serverName + ".cern.ch");

            foreach(var member in members)
            {
                if (member.StartsWith(Constants.OrphanedSid))
                {
                    try
                    {
                        groupPrincipal.Invoke("Remove", $"WinNT://{member}");
                        LoggerSingleton.Reports.Info($"{serverName}: Removed Orphaned SID {member} from: {lg.Name}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed removing SID: '{member}'.");
                        LoggerSingleton.SynchronizedLocalGroups.Error(ex, $"Failed removing SID: '{member}'.");
                        LoggerSingleton.Errors.Error($"{serverName}: Failed to remove Orphaned SID {member} from: {lg.Name}");
                        success = false;
                    }
                }
                globalSuccess = globalSuccess && success;
            }

            return globalSuccess;
        }

        public static List<string> GetGroupMembers(string groupName, string serverName, ObjectClass memberType = ObjectClass.All)
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

        private static string GetGroupMember(object member, ObjectClass memberType)
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
    }
}
