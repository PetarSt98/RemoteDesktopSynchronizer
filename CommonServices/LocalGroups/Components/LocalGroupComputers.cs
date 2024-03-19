using System.DirectoryServices;
using System.Collections;
using SynchronizerLibrary.Loggers;
using SynchronizerLibrary.DataBuffer;


namespace SynchronizerLibrary.CommonServices.LocalGroups.Components
{
    public class LocalGroupComputers
    {
        public LocalGroupComputers()
        {

        }

        private bool DeleteComputer(string server, string groupName, string computerName, DirectoryEntry newGroup)
        {
            if (RemoveComputerFromLocalGroup(computerName, groupName, server, newGroup)) return true;
            LoggerSingleton.SynchronizedLocalGroups.Error($"Failed removing computer: '{computerName}' from group: '{groupName}' on gateway: '{server}'.");
            return false;
        }

        private bool AddComputer(string server, string groupName, string computerName, DirectoryEntry newGroup)
        {
            if (AddComputerToLocalGroup(computerName, groupName, server, newGroup)) return true;
            LoggerSingleton.SynchronizedLocalGroups.Error($"Failed adding new computer: '{computerName}' to group: '{groupName}' on gateway: '{server}'.");
            return false;
        }

        public bool AddComputerToLocalGroup(string computerName, string groupName, string serverName, DirectoryEntry groupEntry)
        {
            bool success;
            LoggerSingleton.SynchronizedLocalGroups.Info($"Adding new computer '{computerName}' to the group '{groupName}' on gateway '{serverName}'.");
            Console.WriteLine($"Adding new computer '{computerName}' to the group '{groupName}' on gateway '{serverName}'.");
            try
            {
                var i = 0;
                while (i < 1)
                {
                    try
                    {


                        if (!ExistsInGroup(groupEntry, computerName))
                        {
                            groupEntry.Invoke("Add", $"WinNT://CERN/{computerName},computer");
                            groupEntry.CommitChanges();
                            LoggerSingleton.Reports.Info($"{serverName}: Added device {computerName} to Local Group {groupName}");
                            break;
                        }
                        else
                        {
                            //GlobalInstance.Instance.ObjectLists[serverName].Add(new RAP_ResourceStatus
                            //{
                            //    ComputerName = computerName.Substring(0, computerName.Length - 1),
                            //    GroupName = groupName,
                            //    Status = true
                            //});
                            GlobalInstance.Instance.AddToObjectsList(serverName, computerName, groupName, true);
                            return true;
                        }
                    }
                    catch (System.Reflection.TargetInvocationException ex)
                    {
                        i++;
                        Console.WriteLine($"Failed attempt:{i}");
                        LoggerSingleton.SynchronizedLocalGroups.Warn($"Failed attempt:{i}");
                        Console.WriteLine(ex.InnerException != null ? ex.InnerException.Message : ex.Message);
                        Console.WriteLine($"Local Group name: {groupName}, unsuccessful adding computer: {computerName}");
                        LoggerSingleton.SynchronizedLocalGroups.Warn(ex.InnerException != null ? ex.InnerException.Message : ex.Message);
                        LoggerSingleton.SynchronizedLocalGroups.Warn($"Local Group name: {groupName}, unsuccessful adding computer: {computerName}");
                        LoggerSingleton.SynchronizedLocalGroups.Warn($"Computer '{computerName}' does not exist or is not accessible on gateway '{serverName}'.");
                    }
                }
                //GlobalInstance.Instance.ObjectLists[serverName].Add(new RAP_ResourceStatus
                //{
                //    ComputerName = computerName.Substring(0, computerName.Length - 1),
                //    GroupName = groupName,
                //    Status = true
                //});
                GlobalInstance.Instance.AddToObjectsList(serverName, computerName, groupName, true);
                success = true;

            }
            catch (Exception ex)
            {
                LoggerSingleton.SynchronizedLocalGroups.Error(ex, $"Error while adding member '{computerName}' to group '{groupName}' on gateway '{serverName}'.");
                //GlobalInstance.Instance.ObjectLists[serverName].Add(new RAP_ResourceStatus
                //{
                //    ComputerName = computerName.Substring(0, computerName.Length - 1),
                //    GroupName = groupName,
                //    Status = false
                //});
                GlobalInstance.Instance.AddToObjectsList(serverName, computerName, groupName, false);
                success = false;
            }
            return success;
        }


        public bool RemoveComputerFromLocalGroup(string computerName, string groupName, string serverName, DirectoryEntry groupEntry)
        {
            var success = true;
            //string username = "";
            //string password = "";
            LoggerSingleton.SynchronizedLocalGroups.Info($"Deleting computer '{computerName}' from the group '{groupName}' on gateway '{serverName}'.");
            Console.WriteLine($"Deleting computer '{computerName}' from the group '{groupName}' on gateway '{serverName}'.");
            try
            {
                try
                {
                    groupEntry.Invoke("Remove", $"WinNT://CERN/{computerName},computer");
                    groupEntry.CommitChanges();
                    LoggerSingleton.Reports.Info($"{serverName}: Removed device {computerName} from Local Group {groupName}");
                }
                catch (System.Reflection.TargetInvocationException ex)
                {
                    LoggerSingleton.SynchronizedLocalGroups.Warn($"Device '{computerName}' already removed from the group '{groupName}' on gateway '{serverName}'.");
                    Console.WriteLine(ex.Message);
                }
                success = true;

            }
            catch (Exception ex)
            {
                LoggerSingleton.SynchronizedLocalGroups.Error(ex, $"Error while adding member '{computerName}' to group '{groupName}' on gateway '{serverName}'.");
                success = false;
            }

            return success;
        }


        public bool SyncComputers(DirectoryEntry newGroup, LocalGroup lg, string server)
        {
            //string groupName = FormatModifiedValue(lg.Name);
            var success = true;
            var generalSuccess = true;
            var computersData = lg.ComputersObj.Names.Zip(lg.ComputersObj.Flags, (i, j) => new { Name = i, Flag = j });
            foreach (var computer in computersData)
            {
                if (computer.Flag == LocalGroupFlag.Delete)
                    success = success && DeleteComputer(server, lg.Name, computer.Name, newGroup);
                else if (computer.Flag == LocalGroupFlag.Add)
                    success = success && AddComputer(server, lg.Name, computer.Name, newGroup);
                generalSuccess = generalSuccess && success;
            }
            if (!generalSuccess) LoggerSingleton.SynchronizedLocalGroups.Error(server, $"Failed synchronizing computers for group: '{lg.Name}'.");
            return generalSuccess;
        }

        private bool ExistsInGroup(DirectoryEntry groupEntry, string computerName)
        {
            var members = groupEntry.Invoke("Members") as IEnumerable;

            if (members != null)
            {
                foreach (var member in members)
                {
                    var directoryEntryMember = new DirectoryEntry(member);
                    if (directoryEntryMember.Name.Equals(computerName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
