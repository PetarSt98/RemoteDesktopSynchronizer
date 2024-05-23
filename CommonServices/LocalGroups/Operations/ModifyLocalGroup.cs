using System.DirectoryServices;
using SynchronizerLibrary.Loggers;
using SynchronizerLibrary.Data;
using SynchronizerLibrary.CommonServices.LocalGroups.Components;
using SynchronizerLibrary.CommonServices.LAPS;
using System.Data.Entity;

namespace SynchronizerLibrary.CommonServices.LocalGroups.Operations
{
    public class ModifyLocalGroup
    {
        public ModifyLocalGroup()
        {

        }

        public async Task SyncModifiedGroups(string serverName, List<LocalGroup> modifiedGroups)
        {
            LoggerSingleton.General.Info(serverName, $"Synchronizing content of {modifiedGroups.Count} groups.");
            using (var db = new RapContext())
            {
                var tasks = modifiedGroups.Select(lg => SyncGroupContentAsync(lg, serverName, db));
                await Task.WhenAll(tasks);
                db.SaveChanges();
            }
        }

        private async Task SyncGroupContentAsync(LocalGroup lg, string server, RapContext db)
        {
            var success = true;
            var localGroup = GetLocalGroup(server, lg.Name);
            LocalGroupOrphanedSID localGroupOrphanedSID = new LocalGroupOrphanedSID();

            if (localGroupOrphanedSID.CleanFromOrphanedSids(localGroup, lg, server)) // ovo popraviti jer nesto nije ok
            {
                LocalGroupMembers localGroupMembers = new LocalGroupMembers();
                LocalGroupComputers localGroupComputers = new LocalGroupComputers();
                LAPSService lapsService = new LAPSService();

                if (!localGroupMembers.SyncMember(localGroup, lg, server))
                    success = false;

                if (!localGroupComputers.SyncComputers(localGroup, lg, server))
                    success = false;
                if (!await lapsService.SyncLAPS(server, lg))
                    Console.WriteLine($"LAPS FAILED");
                    //success = false;
            }
            else
            {
                LoggerSingleton.SynchronizedLocalGroups.Error($"Error while cleaning group: '{lg.Name}' from orphaned SIDs, further synchronization on this group is skipped.");
                success = false;
            }
            if (success)
            {
                LoggerSingleton.SynchronizedLocalGroups.Info($"Synchronized Local Group: {lg.Name} successfuly!");
                foreach (var member in lg.MembersObj.Names.Zip(lg.MembersObj.Flags, (name, flag) => new { Name = name, Flag = flag }))
                {
                    if (member.Flag == LocalGroupFlag.Delete) continue;

                    var matchingRaps = await db.raps
                        .Where(r => (r.login == member.Name && r.resourceGroupName == lg.Name))
                        .ToListAsync();

                    foreach (var rap in matchingRaps)
                    {
                        rap.synchronized = true;
                    }

                }
            }
            else
            {
                LoggerSingleton.SynchronizedLocalGroups.Info($"Unsuccessful synchronization og Local Group: {lg.Name}!");
            }
        }

        private DirectoryEntry GetLocalGroup(string server, string groupName)
        {
            //string username = "";
            //string password = "";
            //var ad = new DirectoryEntry($"WinNT://{server},computer", username, password);
            var ad = new DirectoryEntry($"WinNT://{server},computer");
            try
            {
                foreach (DirectoryEntry childEntry in ad.Children)
                {
                    if (childEntry.SchemaClassName == "Group" && childEntry.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase))
                    {
                        return childEntry;
                    }
                }

                // If no matching group is found, throw an exception or return null based on your requirement
                throw new Exception($"Local group '{groupName}' not found on server '{server}'.");
                // return null; // Uncomment this line to return null if the group is not found
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}
