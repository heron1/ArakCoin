using System.Text;
using ArakCoin.Networking;
using ArakCoin.Transactions;

namespace ArakCoin;

/**
 * Handle events that occur within the program centrally here. This class is highly coupled to the entire codebase,
 * so should serve as the central handler for inter-component interaction based upon events occurring
 */
public static class GlobalHandler
{
    public static event EventHandler<Block>? latestBlockUpdateEvent; //event associated with a blockchain update
    public static event EventHandler<string>? logUpdate; //event associated with a log update
    
    public static void OnLatestBlockUpdateEvent(Block lastBlock)
    {
        EventHandler<Block>? handler = latestBlockUpdateEvent;
        if (handler is not null)
            handler(null, lastBlock);
    }
    
    public static void OnLogUpdate(string newLogMsg)
    {
        EventHandler<string>? handler = logUpdate;
        if (handler is not null)
            handler(null, newLogMsg);
    }
    
    /**
     * In the event the master blockchain has been updated from *any* source, we should call this handler method to
     * do whatever it is we should based upon that occurrence
     */
    public static void handleMasterBlockchainUpdate()
    {
        Blockchain.saveMasterChainToDisk(); //if the master chain is updated, we should save it to disk
        
        //log the latest block content
        Block lastBlock = Globals.masterChain.getLastBlock();
        if (!Blockchain.isGenesisBlock(lastBlock))
        {
            var sb = new StringBuilder();
            sb.Append($"The latest mined block #{lastBlock.index} with difficulty {lastBlock.difficulty} details -\n");
            sb.Append($"\tMined by: {Transaction.getMinerPublicKeyFromBlock(lastBlock)}\n");
            sb.Append($"\tBlock hash: {lastBlock.calculateBlockHash()}\n");
            sb.Append($"\tBlock transactions:\n");
            foreach (var tx in lastBlock.transactions)
            {
                sb.Append($"\t\tTx id: {tx.id.Substring(0, 3)} " +
                          $"(fee: {Transaction.getMinerFeeFromTransaction(tx)}). Confirmed TxOuts:\n");
                foreach (var txout in tx.txOuts)
                {
                    string subaddr; //shrink the address logged
                    if (txout.address.Length < 3)
                        subaddr = txout.address;
                    else
                        subaddr = txout.address.Substring(0, 3) + "..";
                    sb.Append($"\t\t\t{subaddr} received {txout.amount} coins\n");
                }
            }
            Utilities.log(sb.ToString());
        }
        
        //trigger a blockchain update event globally to any listeners (that might be listening for their own reasons)
        OnLatestBlockUpdateEvent(lastBlock);
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
        Utilities.log($"Node received updated block/chain with length {Globals.masterChain.getLength()} from node" +
                      $" {receivingNode}");
        
        //stop current mining if it's occuring on a now outdated block
        if (Globals.nextBlock is not null)
            Globals.nextBlock.cancelMining = true;
        
        handleMasterBlockchainUpdate();
    }

    public static void enableNodeServices()
    {
        //begin the node listening server
        Globals.nodeListener.startListeningServer();

        //begin node discovery & registration as a new Task in the background
        Globals.nodeDiscoveryCancelToken =
            AsyncTasks.nodeDiscoveryAsync(Settings.nodeDiscoveryDelaySeconds);

        //begin periodic mempool broadcasting as a new Task in the background
        Globals.mempoolCancelToken =
            AsyncTasks.shareMempoolAsync(Settings.mempoolSharingDelaySeconds);

        Settings.isNode = true;
    }
    
    public static void disableNodeServices()
    {
        Globals.nodeListener.stopListeningServer();
        AsyncTasks.cancelNodeDiscoveryAsync(Globals.nodeDiscoveryCancelToken);
        AsyncTasks.cancelshareMempoolAsync(Globals.mempoolCancelToken);
        
        Settings.isNode = false;
    }

    public static void enableMining()
    {
        Globals.miningCancelToken = AsyncTasks.mineBlocksAsync();
        Settings.isMiner = true;
    }

    public static void disableMining()
    {
        AsyncTasks.cancelMineBlocksAsync(Globals.miningCancelToken);
        Settings.isMiner = false;
    }
    
    
}