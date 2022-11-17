namespace ArakCoin.Networking;

/**
 * Class to provide helper methods to create, manage and cancel async tasks for special program operations
 */
public static class AsyncTasks
{
    /**
     * Asynchronously mine blocks until the returned cancellation token is cancelled, and broadcasts them to the
     * network. Automatically updates to the latest valid chain if local chain is changed.
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
                        Utilities.log($"Mining block #{Global.masterChain.getLength() + 1}..");
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
}