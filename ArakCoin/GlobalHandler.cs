using ArakCoin.Networking;
using ArakCoin.Transactions;

namespace ArakCoin;

/**
 * Handle events that occur within the program centrally here. This class is highly coupled to the entire codebase,
 * so should serve as the central handler for inter-component interaction based upon events occurring
 */
public static class GlobalHandler
{
    /**
     * In the event the master blockchain has been updated from *any* source, we should call this handler method to
     * do whatever it is we should based upon that occurrence
     */
    public static void handleMasterBlockchainUpdate()
    {
        Blockchain.saveMasterChainToDisk(); //if the chain is updated, we should save it to disk
    }
    
    /**
     * In the event an incoming client message causes the local blockchain to be changed (such as receiving a next
     * valid block, or a replacement consensus chain), this method will ensure that the local state in this program
     * responds appropriately (eg: if mining, the local next block should stop being mined, and mining should be
     * resumed on the new updated chain, etc)
     */
    public static void handleExternalBlockchainUpdate(Host? externalNode = null)
    {
        //log the chain update
        string receivingNode = externalNode is null ? "unknown" : $"{externalNode.ToString()}";
        Utilities.log($"Node received updated block/chain with length {Global.masterChain.getLength()} from node" +
                      $" {receivingNode}");
        
        //log the chain content
        Utilities.log($"The latest block contains the following transactions:");
        foreach (var tx in Global.masterChain.getLastBlock().transactions)
        {
            Utilities.log($"\tTx id: {tx.id} (fee: {Transaction.getMinerFeeFromTransaction(tx)}). Confirmed TxOuts:");
            foreach (var txout in tx.txOuts)
            {
                Utilities.log($"\t\t{txout.address.Substring(0, 3)}.. received {txout.amount} coins");
            }
        }
        
        //stop current mining if it's occuring on a now outdated block
        if (Global.nextBlock is not null)
            Global.nextBlock.cancelMining = true;
        
        handleMasterBlockchainUpdate();
    }
}