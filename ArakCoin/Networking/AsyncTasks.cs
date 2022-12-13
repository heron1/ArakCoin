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
        Globals.miningIsBeingCancelled = false;
        bool blockMined = false; //for parallel operations, keeps track of whether a block has been mined
        
        Utilities.log("Mining service started..");

        var cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancelToken = cancellationTokenSource.Token;
        Task.Run(() =>
        {
            while (true)
            {
                if (!cancelToken.IsCancellationRequested && !Globals.miningIsBeingCancelled)
                {
                    //clear any currently mining blocks
                    lock (Globals.nextBlocksLock)
                    {
                        Globals.nextBlocks.Clear();
                    }
                    
                    //initialize values
                    int threads = Settings.maxParallelMiningCPUThreadCount;
                    long N = long.MaxValue;
                    
                    /*
                     * create and begin mining the next block on this node's own local master chain
                     */
                    
                    //retrieve alloweable transactions from the mempool
                    Transaction[] toBeMinedTx = Globals.masterChain.getTxesFromMempoolForBlockMine();
                    
                    if (!Settings.allowParallelCPUMining) //handle logic for non-parallel mining
                    {
                        Block nextBlock = BlockFactory.createNewBlock(Globals.masterChain, toBeMinedTx);
                        lock (Globals.nextBlocksLock)
                        {
                            Globals.nextBlocks.Add(nextBlock);
                        }
                        if (!Blockchain.isGenesisBlock(nextBlock))
                            nextBlock.mineBlock();
                        Globals.nextMinedBlock = nextBlock;
                    }
                    else //handle logic for parallel mining
                    {
                        //first test the next block isn't the genesis block, otherwise we stop at it
                        Block nextBlock = BlockFactory.createNewBlock(Globals.masterChain, toBeMinedTx);
                        lock (Globals.nextBlocksLock)
                        {
                            Globals.nextBlocks.Add(nextBlock);
                        }
                        if (!Blockchain.isGenesisBlock(nextBlock))
                        {
                            //block isn't the genesis block. Clear the nextBlocks and begin parallel mining
                            lock (Globals.nextBlocksLock)
                            {
                                Globals.nextBlocks.Clear();
                            }

                            //parallelism occurs within the lambda expression of this function
                            Parallel.For(0, Settings.maxParallelMiningCPUThreadCount, (long i) =>
                            {
                                //if block is already mined, exit this thread
                                if (blockMined) 
                                    return;
                                
                                //set up the parallel block and its nonce range
                                long startNonce = (N / threads * i);
                                long endNonce = (N / threads * (i + 1));
                                Block parallelBlock = BlockFactory.createNewBlock(Globals.masterChain, toBeMinedTx,
                                    startNonce, endNonce, true);
                                
                                lock (Globals.nextBlocksLock)
                                {
                                    Globals.nextBlocks.Add(parallelBlock);
                                }
                                parallelBlock.mineBlock();
                                
                                //the first thread to reach here must have been the one to mine its block first,
                                //it can cancel the mining for all other threads
                                lock (Globals.asyncMiningLock)
                                {
                                    if (blockMined)
                                        return; //this wasn't the first thread

                                    blockMined = true;

                                    lock (Globals.nextBlocksLock)
                                    {
                                        foreach (var block in Globals.nextBlocks)
                                            block.cancelMining = true;
                                    }
                                    
                                    Globals.nextMinedBlock = parallelBlock;
                                }
                            });
                        }
                        else
                        {
                            //block was the genesis block. It's the next valid block
                            Globals.nextMinedBlock = nextBlock;
                        }
                    }

                    blockMined = false; //reset the block mined tracker for parallelism

                    //if the block was successfully mined and added to the local chain, broadcast it
                    if (Globals.nextMinedBlock is not null && 
                        Globals.masterChain.addValidBlock(Globals.nextMinedBlock))
                    {
                        Utilities.log($"This node mined block #{Globals.nextMinedBlock.index}, broadcasting it..");
                        NetworkingManager.broadcastNextValidBlock(Globals.nextMinedBlock);
                        GlobalHandler.handleMasterBlockchainUpdate(); //handle the blockchain update
                    }
                    else
                    {
                        Utilities.log("Mining failed (chain most likely updated externally)");
                    }
                }
                else
                {
                    Utilities.log("Mining stopped via interrupt..");
                    break;
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
        lock (Globals.nextBlocksLock)
        {
            Globals.miningIsBeingCancelled = true;
            foreach (var block in Globals.nextBlocks)
                    block.cancelMining = true;
        }
        
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
        Host self = new Host(Settings.nodeIp, Settings.nodePort);

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
                        if (node == self)
                            continue; //don't register with self
                        
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