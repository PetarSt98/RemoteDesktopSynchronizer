using System.DirectoryServices;
using SynchronizerLibrary.Loggers;
using SynchronizerLibrary.CommonServices.LAPS;


namespace SynchronizerLibrary.CommonServices.LocalGroups.Operations
{
    public class DeleteLocalGroup
    {
        public DeleteLocalGroup()
        {

        }

        public async Task DeleteGroups(string serverName, List<LocalGroup> groupsToDelete)
        {
            LoggerSingleton.General.Info($"Started deleting {groupsToDelete.Count} groups on gateway '{serverName}'.");
            //_reporter.Info(serverName, $"Deleting {groupsToDelete.Count} groups.");
            var deleteTasks = groupsToDelete.Select(lg => DeleteServerAndLocalGroup(serverName, lg));
            await Task.WhenAll(deleteTasks);
            LoggerSingleton.General.Info(serverName, "Finished deleting groups.");
        }

        private async Task DeleteServerAndLocalGroup(string server, LocalGroup localGroup)
        {
            LoggerSingleton.SynchronizedLocalGroups.Info(server, $"Removing group '{localGroup}'.");
            LAPSService lapsService = new LAPSService();

            if (DeleteLocalGroups(localGroup.Name, server))
                LoggerSingleton.SynchronizedLocalGroups.Info($"Local Group successfully deleted {localGroup} on server {server}");
            else
                LoggerSingleton.SynchronizedLocalGroups.Error($"Local Group unsuccessfully deleted {localGroup} on server {server}");
            if (!await lapsService.SyncLAPS(server, localGroup))
                LoggerSingleton.SynchronizedLocalGroups.Info($"Local Group successfully deleted {localGroup} on device");
            else
                LoggerSingleton.SynchronizedLocalGroups.Error($"Local Group unsuccessfully deleted {localGroup} on device");
        }

        public bool DeleteLocalGroups(string groupName, string server)
        {
            var success = true;
            //string username = "";
            //string password = "";
            bool groupExists = false;
            DirectoryEntry newGroup = null;

            LoggerSingleton.SynchronizedLocalGroups.Info($"Removing group '{groupName}' from gateway '{server}'.");
            try
            {
                //var ad = new DirectoryEntry($"WinNT://CERN,computer", username, password);
                var ad = new DirectoryEntry($"WinNT://{server},computer");
                try
                {

                    newGroup = ad.Children.Find(groupName, "group");
                    groupExists = true;
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    if (ex.ErrorCode == -2147022675 || ex.Message.Contains("The group name could not be found")) // Group not found.
                    {
                        groupExists = false;
                    }
                    else
                    {
                        throw;
                    }
                }
                if (groupExists && newGroup is not null)
                {
                    ad.Children.Remove(newGroup);
                    success = true;
                    LoggerSingleton.Reports.Info($"{server}: Removed Local Group: {groupName}");
                }
            }
            catch (Exception ex)
            {
                success = false;
                LoggerSingleton.SynchronizedLocalGroups.Error(ex, $"Error while deleting group: '{groupName}' from gateway: '{server}'.");
                LoggerSingleton.Errors.Error($"{server}: Failed to remove Local Group: {groupName}");
            }

            return success;
        }
    }
}
