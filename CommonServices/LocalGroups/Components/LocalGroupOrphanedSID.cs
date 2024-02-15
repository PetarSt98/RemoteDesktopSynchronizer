using System.DirectoryServices;
using SynchronizerLibrary.Loggers;


namespace SynchronizerLibrary.CommonServices.LocalGroups.Components
{
    public class LocalGroupOrphanedSID
    {
        public LocalGroupOrphanedSID()
        {

        }

        public bool CleanFromOrphanedSids(DirectoryEntry localGroup, LocalGroup lg, string serverName)
        {
            try
            {
                bool success;
                success = RemoveOrphanedSids(localGroup, lg);
                return success;
            }
            catch (Exception ex)
            {
                LoggerSingleton.SynchronizedLocalGroups.Error(ex, $"Error while removing orphaned SIDs from group: '{lg.Name}' on gateway: '{serverName}'.");
                return false;
            }
        }

        public static bool RemoveOrphanedSids(DirectoryEntry groupPrincipal, LocalGroup lg)
        {
            var success = true;
            var globalSuccess = true;
            var membersData = lg.MembersObj.Names.Zip(lg.MembersObj.Flags, (i, j) => new { Name = i, Flag = j });

            foreach (var member in membersData)
            {
                if (!member.Name.StartsWith(Constants.OrphanedSid)) continue;
                try
                {
                    LoggerSingleton.SynchronizedLocalGroups.Info($"Removing SID: '{member.Name}'.");
                    groupPrincipal.Invoke("Remove", $"WinNT://{member.Name}");
                }
                catch (Exception ex)
                {
                    LoggerSingleton.SynchronizedLocalGroups.Error(ex, $"Failed removing SID: '{member.Name}'.");
                    success = false;
                }

                globalSuccess = globalSuccess && success;
            }

            return globalSuccess;
        }
    }
}
