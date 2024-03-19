using System.DirectoryServices;
using SynchronizerLibrary.Loggers;


namespace SynchronizerLibrary.CommonServices.LocalGroups.Components
{
    public class LocalGroupMembers
    {
        public LocalGroupMembers()
        {

        }

        public bool SyncMember(DirectoryEntry newGroup, LocalGroup lg, string server)
        {
            var success = true;
            var general_success = true;

            var membersData = lg.MembersObj.Names.Zip(lg.MembersObj.Flags, (i, j) => new { Name = i, Flag = j });

            foreach (var member in membersData.Where(m => !m.Name.StartsWith(Constants.OrphanedSid)))
            {
                if (member.Flag == LocalGroupFlag.Delete)
                    success = DeleteMember(server, lg.Name, member.Name, newGroup);
                else if (member.Flag == LocalGroupFlag.Add)
                    success = AddMember(server, lg.Name, member.Name, newGroup);
                general_success = general_success && success;
            }
            if (!general_success) LoggerSingleton.SynchronizedLocalGroups.Error(server, $"Failed synchronizing members for group '{ lg.Name}'.");
            return general_success;
        }

        private bool DeleteMember(string server, string groupName, string memberName, DirectoryEntry newGroup)
        {
            if (RemoveMemberFromLocalGroup(memberName, groupName, server, newGroup)) return true;
            LoggerSingleton.SynchronizedLocalGroups.Error($"Failed removing member: '{memberName}' from group: '{groupName}' on gateway: '{server}'.");
            return false;
        }

        private bool AddMember(string server, string groupName, string memberName, DirectoryEntry newGroup)
        {
            if (AddMemberToLocalGroup(memberName, groupName, server, newGroup)) return true;
            LoggerSingleton.SynchronizedLocalGroups.Error($"Failed adding new member: '{memberName}' to group: '{groupName}' on gateway: '{server}'.");
            return false;
        }

        public bool RemoveMemberFromLocalGroup(string memberName, string groupName, string serverName, DirectoryEntry groupEntry)
        {

            bool success;
            LoggerSingleton.SynchronizedLocalGroups.Info($"Removing member: '{memberName}' from group: '{groupName}' on gateway: '{serverName}'.");
            Console.WriteLine($"Removing member: '{memberName}' from group: '{groupName}' on gateway: '{serverName}'.");
            try
            {
                try
                {
                    groupEntry.Invoke("Remove", $"WinNT://{memberName}");
                    groupEntry.CommitChanges();
                    LoggerSingleton.Reports.Info($"{serverName}: Removed memeber {memberName} from Local Group {groupName}");
                }
                catch (System.Reflection.TargetInvocationException ex)
                {
                    try
                    {
                        groupEntry.Invoke("Remove", $"WinNT://CERN/{memberName},user");
                        groupEntry.CommitChanges();
                        LoggerSingleton.Reports.Info($"{serverName}: Removed memeber {memberName} from Local Group {groupName}");
                    }
                    catch (System.Reflection.TargetInvocationException ex2)
                    {
                        LoggerSingleton.SynchronizedLocalGroups.Warn($"Member already deleted '{memberName}' in the group '{groupName}' in gateway '{serverName}'.");
                        Console.WriteLine(ex2.Message);
                    }
                    catch (Exception ex2)
                    {
                        throw ex2;
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                success = true;

            }
            catch (Exception ex)
            {
                LoggerSingleton.SynchronizedLocalGroups.Error(ex, $"Error while adding member '{memberName}' to group '{groupName}' on gateway '{serverName}'.");
                LoggerSingleton.Errors.Error($"{serverName}: Failed to remove memeber {memberName} from Local Group {groupName}");
                success = false;
            }

            return success;
        }

        public bool AddMemberToLocalGroup(string memberName, string groupName, string serverName, DirectoryEntry groupEntry)
        {
            bool success;
            //string username = "";
            //string password = "";
            LoggerSingleton.SynchronizedLocalGroups.Info($"Adding new member '{memberName}' to the group '{groupName}' on gateway '{serverName}'.");
            Console.WriteLine($"Adding new member '{memberName}' to the group '{groupName}' on gateway '{serverName}'.");
            try
            {
                try
                {
                    groupEntry.Invoke("Add", $"WinNT://{memberName}");
                    groupEntry.CommitChanges();
                    LoggerSingleton.Reports.Info($"{serverName}: Added memeber {memberName} to Local Group: {groupName}");
                }
                catch (System.Reflection.TargetInvocationException ex)
                {
                    try
                    {
                        groupEntry.Invoke("Add", $"WinNT://CERN/{memberName},user");
                        groupEntry.CommitChanges();
                        LoggerSingleton.Reports.Info($"{serverName}: Added memeber {memberName} to Local Group {groupName}");
                    }
                    catch (System.Reflection.TargetInvocationException ex2)
                    {
                        LoggerSingleton.SynchronizedLocalGroups.Warn($"Member already exists '{memberName}' to the group '{groupName}' on gateway '{serverName}'.");
                        Console.WriteLine(ex.Message);
                    }
                    catch (Exception ex2)
                    {
                        throw ex2;
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                success = true;

            }
            catch (Exception ex)
            {
                LoggerSingleton.SynchronizedLocalGroups.Error(ex, $"Error while adding member '{memberName}' to group '{groupName}' on gateway '{serverName}'.");
                LoggerSingleton.Errors.Error($"{serverName}: Failed to add memeber {memberName} to Local Group: {groupName}");
                success = false;
            }

            return success;
        }
    }
}
