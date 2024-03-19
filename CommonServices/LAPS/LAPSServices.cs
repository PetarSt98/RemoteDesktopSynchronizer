using System.DirectoryServices;
using SynchronizerLibrary.CommonServices.Exceptions;
using SynchronizerLibrary.SOAPservices;
using SynchronizerLibrary.CommonServices.LocalGroups;
using SynchronizerLibrary.Loggers;
using SynchronizerLibrary.DataBuffer;


namespace SynchronizerLibrary.CommonServices.LAPS
{
    public class LAPSService
    {
        public LAPSService()
        {

        }

        public async Task<bool> SyncLAPS(string serverName, LocalGroup lg)
        {
            bool status = true;
            string statusMessage;
            try
            {
                var computersData = lg.ComputersObj.Names.Zip(lg.ComputersObj.Flags, (i, j) => new { Name = i, Flag = j });
                var membersData = lg.MembersObj.Names.Zip(lg.MembersObj.Flags, (i, j) => new { Name = i, Flag = j });
                foreach (var computer in computersData)
                {
                    foreach (var member in membersData)
                    {
                        if (member.Flag == LocalGroupFlag.Add && computer.Flag == LocalGroupFlag.Add)
                        {
                            (status, statusMessage) = await UpdateLaps(computer.Name.Replace("$", ""), member.Name, "Add");
                            Console.WriteLine(statusMessage);
                            GlobalInstance.Instance.AddToObjectsList(serverName, computer.Name.Replace("$", ""), "LG-" + member.Name, status, statusMessage);
                        }
                        else if (member.Flag == LocalGroupFlag.None && computer.Flag == LocalGroupFlag.Delete)
                        {
                            (status, statusMessage) = await UpdateLaps(computer.Name.Replace("$", ""), member.Name, "Remove");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerSingleton.SynchronizedLocalGroups.Error(ex.Message);
                return false;
            }
            return status;
        }

        private (DirectoryEntry, string) GetRemoteDesktopUsersGroup(string machineName, string adminUsername, string adminPassword)
        {
            Console.WriteLine($"LAPS password: {adminPassword}");
            try
            {
                var ad = new DirectoryEntry($"WinNT://{machineName},computer", adminUsername, adminPassword);
                foreach (DirectoryEntry childEntry in ad.Children)
                {
                    if (childEntry.SchemaClassName == "Group" && childEntry.Name.Equals("Remote Desktop Users", StringComparison.OrdinalIgnoreCase))
                    {
                        return (childEntry, "All ok");
                    }
                }
                throw new Exception($"Local group 'Remote Desktop Users' not found on server '{machineName}'.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return (null, "Device is unreachable.");
            }
        }

        private bool MemberExists(DirectoryEntry groupEntry, string domain, string userName)
        {
            foreach (object member in (System.Collections.IEnumerable)groupEntry.Invoke("Members"))
            {
                using (DirectoryEntry memberEntry = new DirectoryEntry(member))
                {
                    if (memberEntry.Name.Equals(userName, StringComparison.OrdinalIgnoreCase)
                        && memberEntry.Path.Contains($"WinNT://{domain}/"))
                    {
                        return true;  // Member exists
                    }
                }
            }

            return false;  // Member does not exist
        }

        private void AddorRemoveUserToRemoteDesktopUsersGroup(DirectoryEntry groupEntry, string domain, string userName, string lapsOperator, string machineName)
        {
            try
            {
                if (lapsOperator == "Add")
                {
                    if (!MemberExists(groupEntry, domain, userName))
                    {
                        groupEntry.Invoke("Add", $"WinNT://{domain}/{userName}");
                        groupEntry.CommitChanges();
                        LoggerSingleton.Reports.Info($"Added user {userName} to Remote Desktop Users Group on {machineName}");
                    }
                }
                if (lapsOperator == "Remove")
                {
                    if (MemberExists(groupEntry, domain, userName))
                    {
                        groupEntry.Invoke("Remove", $"WinNT://{domain}/{userName}");
                        groupEntry.CommitChanges();
                        LoggerSingleton.Reports.Info($"Removed user {userName} from Remote Desktop Users Group on {machineName}");
                    }
                }
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                //if (ex.HResult == -2146232828)
                //{
                //    Loggers.LoggerSingleton.SynchronizedLocalGroups.Debug("User already deleted in LAPS");
                //}
                throw ex;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Loggers.LoggerSingleton.SynchronizedLocalGroups.Error(ex.Message);
                throw ex;
            }
            Console.WriteLine("Updated with LAPS");
        }

        public void DisplayRemoteDesktopUsersGroupMembers(string machineName, string adminUsername, string adminPassword)
        {
            try
            {
                (DirectoryEntry remoteDesktopGroup, string statusMessage) = GetRemoteDesktopUsersGroup(machineName, adminUsername, adminPassword);

                if (remoteDesktopGroup != null)
                {
                    Console.WriteLine($"Members of 'Remote Desktop Users' group on '{machineName}':");

                    foreach (object member in (System.Collections.IEnumerable)remoteDesktopGroup.Invoke("Members"))
                    {
                        using (DirectoryEntry memberEntry = new DirectoryEntry(member))
                        {
                            Console.WriteLine($"- {memberEntry.Name}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Failed to retrieve 'Remote Desktop Users' group on '{machineName}': {statusMessage}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while trying to display members of the 'Remote Desktop Users' group: {ex.Message}");
            }
        }

        public async Task<(bool, string)> UpdateLaps(string machineName, string newUser, string lapsOperator)
        {
            if (lapsOperator != "Add" && lapsOperator != "Remove")
            {
                throw new Exception($"Uknown LAPS operator: {lapsOperator} !");
            }

            string lapsAttribute = "ms-Mcs-AdmPwd";
            string ldapPath = $"LDAP://CN={machineName},OU=Computers,OU=Organic Units,DC=cern,DC=ch";
            //string username = "";
            //string password = "";
            string domain = "CERN";
            string statusMessage = "";

            try
            {
                var lapsFetch = await SOAPMethods.ExecutePowerShellLAPScript(machineName);
                
                if (!lapsFetch["os"].Contains("windows", StringComparison.OrdinalIgnoreCase))
                    return (true, "All ok");

                if (lapsFetch.ContainsKey("password"))
                {
                    var pass = lapsFetch["password"];
                    Console.WriteLine("LAPS got password");
                    if (string.IsNullOrEmpty(pass))
                    {
                        throw new RemoteDesktopUserGroupNotFound($"Device: {machineName} is unreachable!");
                    }
                    (DirectoryEntry remoteDesktopGroup, statusMessage) = GetRemoteDesktopUsersGroup(machineName, "Administrator", pass);
                    using (remoteDesktopGroup)
                    {
                        Console.WriteLine("Entered device");
                        if (remoteDesktopGroup == null)
                        {
                            Console.WriteLine("Entered device: unsuccessful");
                            Console.WriteLine(statusMessage);
                            return (false, statusMessage);
                        }
                        AddorRemoveUserToRemoteDesktopUsersGroup(remoteDesktopGroup, domain, newUser, lapsOperator, machineName);
                    }
                }
                else
                {
                    return (false, "There is no such object on the server.");
                }
                
                return (true, "All ok");
            }
            catch (RemoteDesktopUserGroupNotFound ex)
            {
                return (false, "Unreachable password");
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                Console.WriteLine(ex.Message);
                if (ex.InnerException != null)
                {
                    Console.WriteLine(ex.InnerException.Message);
                    return (false, ex.InnerException.Message);
                }
                else
                {
                    return (false, ex.Message);
                }
            }
            catch (System.DirectoryServices.DirectoryServicesCOMException ex)
            {
                //if (ex.ErrorCode == -2147016656)
                //{
                //    return (false, "");
                //}

                //throw ex;
                Console.WriteLine(ex.Message);
                if (ex.InnerException != null)
                {
                    Console.WriteLine(ex.InnerException.Message);
                    return (false, ex.InnerException.Message);
                }
                else
                {
                    return (false, ex.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                if (ex.InnerException != null)
                {
                    Console.WriteLine(ex.InnerException.Message);
                    return (false, ex.InnerException.Message);
                }
                else
                {
                    return (false, ex.Message);
                }
            }
        }

    }
}

namespace SynchronizerLibrary.CommonServices.Exceptions
{
    class RemoteDesktopUserGroupNotFound : Exception
    {
        public RemoteDesktopUserGroupNotFound() : base() { }
        public RemoteDesktopUserGroupNotFound(string message) : base(message) { }
    }
}

