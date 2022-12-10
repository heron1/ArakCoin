using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArakCoin_GUI.Data
{
    static internal class GuiGlobals
    {
        public static bool startupFinished; //has the startup routine finished?
        public static long lastBalance; //last known address balance for this host
        public static long chainHeight; //last known height of the network chain
    }
}
