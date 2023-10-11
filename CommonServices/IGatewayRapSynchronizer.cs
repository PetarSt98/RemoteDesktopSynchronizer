﻿namespace SynchronizerLibrary.CommonServices
{
    public interface IGatewayRapSynchronizer
    {
        List<string> GetGatewaysRapNamesAsync(string serverName, bool cacheFlag = false);
        void SynchronizeRaps(string serverName, List<string> allGatewayGroups, List<string> toDeleteGatweayGroups, List<string> gatewayRaps);
    }
}
