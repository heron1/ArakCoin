using ArakCoin.Transactions;

namespace ArakCoin.Networking;

/**
 * Class to provide helper methods to create, manage and cancel async tasks for special program operations
 */
public static class AsyncTasks
{
    /**
     * Asynchronously mine blocks until the returned cancellation token is cancelled, and broadcasts them to the
     * network. Automatically updates to the latest valid chain if local chain is changed, cancelling current
     * block mine and proceeding to the next block.
     */
    public static CancellationTokenSource mineBlocksAsync()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancelToken = cancellationTokenSource.Token;
        Task.Run(() =>
        {
            while (true)
            {
                lock (Globals.asyncMiningLock)
                {
                    if (!cancelToken.IsCancellationRequested)
                    {
                        //create and begin mining the next block on this node's own local master chain
                        Transaction[] toBeMinedTx = Globals.masterChain.getTxesFromMempoolForBlockMine();
                        Globals.nextBlock = BlockFactory.createNewBlock(Globals.masterChain, toBeMinedTx);
                        if (!Blockchain.isGenesisBlock(Globals.nextBlock))
                            Globals.nextBlock.mineBlock();

                        //if the block was successfully mined and added to the local chain, broadcast it
                        if (Globals.masterChain.addValidBlock(Globals.nextBlock))
                        {
                            Utilities.log($"This node mined block #{Globals.nextBlock.index}, broadcasting it..");
                            NetworkingManager.broadcastNextValidBlock(Globals.nextBlock);
                            GlobalHandler.handleMasterBlockchainUpdate(); //handle the blockchain update
                        }
                        else
                        {
                            Utilities.log("Mining failed (chain most likely updated externally)");
                        }
                    }
                    else
                    {
                        Utilities.log("Mining stopped via token interrupt..");
                        break;
                    }
                }
            }
        }, cancelToken);

        return cancellationTokenSource;
    }

    /**
     * Cancel the mining for the async task with the associated cancellation token
     */
    public static void cancelMineBlocksAsync(CancellationTokenSource cancelTokenSource)
    {
        //we acquire a lock here not because the .Cancel() method isn't atomic, but because we should wait for
        //an async mining thread to release its lock (indicating it's done for that block mine) before cancelling.
        //This guarantees that after we call this method, no further mining takes place (no race condition)
        lock (Globals.asyncMiningLock)
        {
            cancelTokenSource.Cancel();
        }
    }

    /**
     * Asynchronously discover other nodes by requesting the hosts file from known nodes and register this node
     * with them. This is the essence behind the P2P discovery protocol for this blockchain. This action
     * occurs every number of seconds as given in the secondsDelay parameter.
     *
     * This task will continue indefinitely until the returned cancellation token is cancelled.
     */
    public static CancellationTokenSource nodeDiscoveryAsync(int secondsDelay)
    {
        secondsDelay *= 1000; //convert input seconds into milliseconds

        var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancelToken = cancellationTokenSource.Token;

        Task.Run(() =>
        {
            while (true)
            {
                if (!cancelToken.IsCancellationRequested)
                {
                    Utilities.log("Conducting new node discovery & re-registering this node with known nodes..");
                    NetworkingManager.updateHostsFileFromKnownNodes();
                    foreach (var node in HostsManager.getNodes())
                    {
                        NetworkingManager.registerThisNodeWithAnotherNode(node);
                    }

                    Utilities.sleep(secondsDelay);
                }
                else
                {
                    Utilities.log("P2P node discovery stopped via token interrupt..");
                    break;
                }
            }
        }, cancelToken);

        return cancellationTokenSource;
    }

    /**
     * Cancel the P2P discovery for the async task with the associated cancellation token
     */
    public static void cancelNodeDiscoveryAsync(CancellationTokenSource cancelTokenSource)
    {
        cancelTokenSource.Cancel();
    }

    /**
     * Asynchronously broadcast this node's mempool to known nodes periodically, but only if the mempool has changed
     * since the last broadcast. Nodes should not immediately broadcast every transaction they receive, as
     * this could very easily lead to spam. Instead, they should periodically broadcast their mempool.
     */
    public static CancellationTokenSource shareMempoolAsync(int secondsDelay)
    {
        secondsDelay *= 1000; //convert input seconds into milliseconds

        //store copy of the old mempool as a HashSet. We do this because a HashSet doesn't care about ordering ->
        //we're only interested if all the txes in the mempool are equal or not, not their order (nodes may have
        //their own way of prioritizing the same txes besides using tx fees which is the standard method)
        HashSet<Transaction> lastMempoolHashSet = new HashSet<Transaction>(); //initialize empty for 1st broadcast

        var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancelToken = cancellationTokenSource.Token;
        Task.Run(() =>
        {
            while (true)
            {
                if (!cancelToken.IsCancellationRequested)
                {
                    //if the members of the current mempool are not identical to the old stored mempool, we do
                    //a mempool broadcast. Otherwise, we don't.
                    if (!lastMempoolHashSet.SetEquals(Globals.masterChain.mempool))
                    {
                        Utilities.log("broadcasting updated mempool..");
                        NetworkingManager.broadcastMempool(Globals.masterChain.mempool);
                        
                        //re-set the last mempool hashset with the new mempool
                        lastMempoolHashSet = new HashSet<Transaction>(Globals.masterChain.mempool); 
                    }
                    
                    Utilities.sleep(secondsDelay);
                }
                else
                {
                    Utilities.log("mempool broadcasting stopped via token interrupt..");
                    break;
                }
            }
        }, cancelToken);

        return cancellationTokenSource;
    }
    
    /**
     * Cancel the P2P mempool broadcasting for the async task with the associated cancellation token
     */
    public static void cancelshareMempoolAsync(CancellationTokenSource cancelTokenSource)
    {
        cancelTokenSource.Cancel();
    }
    
}