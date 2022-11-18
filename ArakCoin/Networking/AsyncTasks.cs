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
                lock (Global.asyncMiningLock)
                {
                    if (!cancelToken.IsCancellationRequested)
                    {
                        //create and begin mining the next block on this node's own local master chain
                        Global.nextBlock = BlockFactory.createNewBlock(
                            Global.masterChain, Global.masterChain.mempool.ToArray());
                        if (!Blockchain.isGenesisBlock(Global.nextBlock))
                            Global.nextBlock.mineBlock();
                        
                        //if the block was successfully mined and added to the local chain, broadcast it
                        if (Global.masterChain.addValidBlock(Global.nextBlock))
                        {
                            Utilities.log($"This node mined block #{Global.nextBlock.index}, broadcasting it..");
                            NetworkingManager.broadcastNextValidBlock(Global.nextBlock);
                        }
                        else
                        {
                            Utilities.log("Mining failed (chain updated externally)");
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
        lock (Global.asyncMiningLock)
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
}