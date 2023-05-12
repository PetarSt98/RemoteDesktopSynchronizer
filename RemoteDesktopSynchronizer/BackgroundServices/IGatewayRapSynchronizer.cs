namespace RemoteDesktopCleaner.BackgroundServices
{
    public interface IGatewayRapSynchronizer
    {
        void SynchronizeRaps(string serverName, List<string> allGatewayGroups);
    }
}
