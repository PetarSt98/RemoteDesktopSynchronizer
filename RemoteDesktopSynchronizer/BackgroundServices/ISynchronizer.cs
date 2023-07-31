using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SynchronizerLibrary.Data;
using SynchronizerLibrary.CommonServices;


namespace RemoteDesktopCleaner.BackgroundServices
{
    public interface ISynchronizer
    {
        void SynchronizeAsync(string serverName);
    }
}
