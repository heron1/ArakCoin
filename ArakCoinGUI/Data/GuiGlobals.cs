using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArakCoin.Transactions;

namespace ArakCoin_GUI.Data
{
    static internal class GuiGlobals
    {
        public static bool startupFinished; //has the startup routine finished?
        public static long lastBalance; //last known address balance for this host
        public static long chainHeight; //last known height of the network chain

        public static long successfulTxBlock; //the block a successfully created transaction was broadcast in
        
        //the local transactions to monitor
        public static readonly List<Transaction> localTransactions = new List<Transaction>();
        
        //lock for the localTransactions list
        public static readonly object txLock = new object(); 
    }
}
