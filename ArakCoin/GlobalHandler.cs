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
        Blockchain.saveMasterChainToDisk(); //if the master chain is updated, we should save it to disk
        
        //log the latest block content
        Block lastBlock = Global.masterChain.getLastBlock();
        Utilities.log($"The latest mined block #{lastBlock.index} with difficulty {lastBlock.difficulty} details -");
        Utilities.log($"\tMined by: {Transaction.getMinerPublicKeyFromBlock(lastBlock)}");
        Utilities.log($"\tBlock hash: {lastBlock.calculateBlockHash()}");
        Utilities.log($"\tBlock transactions:");

        foreach (var tx in Global.masterChain.getLastBlock().transactions)
        {
            Utilities.log($"\t\tTx id: {tx.id.Substring(0, 3)} " +
                          $"(fee: {Transaction.getMinerFeeFromTransaction(tx)}). Confirmed TxOuts:");
            foreach (var txout in tx.txOuts)
            {
                string subaddr; //shrink the address logged
                if (txout.address.Length < 3) 
                    subaddr = txout.address;
                else
                    subaddr = txout.address.Substring(0, 3) + "..";
                Utilities.log($"\t\t\t{subaddr} received {txout.amount} coins");
                
            }
        }
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
        
        //stop current mining if it's occuring on a now outdated block
        if (Global.nextBlock is not null)
            Global.nextBlock.cancelMining = true;
        
        handleMasterBlockchainUpdate();
    }
}