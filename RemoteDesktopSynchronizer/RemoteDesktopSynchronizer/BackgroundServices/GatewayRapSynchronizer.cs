using System.DirectoryServices;
using System.Management;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Options;
using System.Security;
using System.Text.Json;
using System.Text;
using RemoteDesktopCleaner.Loggers;
using RemoteDesktopCleaner.Caching;


namespace RemoteDesktopCleaner.BackgroundServices
{
    public class GatewayRapSynchronizer : IGatewayRapSynchronizer
    {
        private const string AdSearchGroupPath = "WinNT://{0}/{1},group";
        private const string NamespacePath = @"\root\CIMV2\TerminalServices";
        private readonly DirectoryEntry _rootDir = new DirectoryEntry("LDAP://DC=cern,DC=ch");

        public GatewayRapSynchronizer() { }

        public void SynchronizeRaps(string serverName, List<string> allGatewayGroups)
        {
            LoggerSingleton.General.Info($"Starting Policy synchronisation of server: '{serverName}'.");
            LoggerSingleton.SynchronizedRaps.Info($"Starting Policy synchronisation of server: '{serverName}'.");
            var modelRapNamesAdd = allGatewayGroups.Select(LgNameToRapName).ToList();
            AddMissingRaps(serverName, modelRapNamesAdd);
            LoggerSingleton.General.Info($"Finished Policy synchronisation of server: '{serverName}'.");
            LoggerSingleton.SynchronizedRaps.Info($"Finished Policy synchronisation of server: '{serverName}'.");
        }
        private static string LgNameToRapName(string lgName)
        {
            return lgName.Replace("LG-", "RAP_");
        }

        private void AddMissingRaps(string serverName, List<string> modelRapNames, List<string> gatewayRapNames)
        {
            var missingRapNames = modelRapNames.Except(gatewayRapNames).ToList();
            //_reporter.SetShouldAddRaps(serverName, missingRapNames.Count);
            //_reporter.Info(serverName, $"Adding {missingRapNames.Count} RAPs to the gateway.");
            LoggerSingleton.General.Info($"Adding {missingRapNames.Count} RAPs to the gateway '{serverName}'.");
            LoggerSingleton.SynchronizedRaps.Info($"Adding {missingRapNames.Count} RAPs to the gateway '{serverName}'.");
            AddMissingRaps(serverName, missingRapNames);
            //_reporter.Info(serverName, "Finished adding RAPs.");
            LoggerSingleton.General.Info($"Finished adding {missingRapNames.Count} RAPs to the gateway '{serverName}'.");
            LoggerSingleton.SynchronizedRaps.Info($"Finished adding {missingRapNames.Count} RAPs to the gateway '{serverName}'.");
        }

        public bool AddMissingRaps(string serverName, List<string> missingRapNames)
        {
            var sHost = $@"\\{serverName}";
            try
            {
                var oConn = new ConnectionOptions();
                oConn.Impersonation = ImpersonationLevel.Impersonate;
                oConn.Authentication = AuthenticationLevel.PacketPrivacy;
                oConn.Username = "svcgtw";
                oConn.Password = "7KJuswxQnLXwWM3znp";
                var oMScope = new ManagementScope(sHost + NamespacePath, oConn);
                oMScope.Options.Authentication = AuthenticationLevel.PacketPrivacy;
                oMScope.Options.Impersonation = ImpersonationLevel.Impersonate;

                var oMPath = new ManagementPath();
                oMPath.ClassName = "Win32_TSGatewayResourceAuthorizationPolicy";
                oMPath.NamespacePath = NamespacePath;

                oMScope.Connect();

                ManagementClass processClass = new ManagementClass(oMScope, oMPath, null);

                ManagementBaseObject inParameters = processClass.GetMethodParameters("Create");
                var mnvc = new ManagementNamedValueCollection();
                var imo = new InvokeMethodOptions();
                imo.Context = mnvc;
                processClass.Get();
                var i = 0;
                inParameters["Description"] = "";
                inParameters["Enabled"] = true;
                inParameters["ResourceGroupType"] = "CG";
                inParameters["ProtocolNames"] = "RDP";
                inParameters["PortNumbers"] = "3389";
                foreach (var rapName in missingRapNames)
                {
                    //_reporter.Info(serverName, $"Adding '{rapName}'.");
                    LoggerSingleton.SynchronizedRaps.Info($"Adding new RAP '{rapName}' to the gateway '{serverName}'.");
                    var groupName = ConvertToLgName(rapName);
                    inParameters["Name"] = "" + rapName;
                    inParameters["ResourceGroupName"] = groupName;
                    inParameters["UserGroupNames"] = groupName;

                    ManagementBaseObject outParameters = processClass.InvokeMethod("Create", inParameters, imo);

                    if ((uint)outParameters["ReturnValue"] == 0)
                    {
                        Console.WriteLine($"{rapName} created. {++i}/{missingRapNames.Count}"); //TODO delete
                        LoggerSingleton.SynchronizedRaps.Info($"RAP '{rapName}' added to the gateway '{serverName}'.");
                        //_reporter.IncrementAddedRaps(serverName);
                    }
                    else
                    {
                        if ((uint)outParameters["ReturnValue"] == 2147749913)
                            LoggerSingleton.SynchronizedRaps.Warn($"Error creating RAP: '{rapName}'. Reason: Already exists.");
                        else
                            LoggerSingleton.SynchronizedRaps.Error($"Error creating RAP: '{rapName}'. Reason: {(uint)outParameters["ReturnValue"]}.");
                    }
                }
                return true;
            }
            catch (System.Exception ex)
            {
                LoggerSingleton.SynchronizedRaps.Error(ex, $"Error when adding new RAPs to the gateway '{serverName}'.");
                //_reporter.Error(serverName, $"Exception when adding missing RAPs to the gateway. Details: {ex.Message}");
                return false;
            }
        }
        private string ConvertToLgName(string rapName)
        {
            return rapName.Replace("RAP_", "LG-");
        }
        
        private string CreateWhereClause(IEnumerable<string> names)
        {
            var enumerable = names.ToList();
            if (enumerable.Count == 0)
                return "";
            var sb = new StringBuilder();
            sb.Append("WHERE");
            for (var i = 0; i < enumerable.Count; i++)
            {
                var name = enumerable[i];
                sb.Append(" (Name=\"" + name + "\")");
                if (i != enumerable.Count - 1)
                    sb.Append(" or");
            }
            return sb.ToString();
        }
    }

}

