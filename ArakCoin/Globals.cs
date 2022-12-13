using ArakCoin.Networking;

namespace ArakCoin;

/**
 * All globally accessible objects should be available here
 */
public static class Globals
{
    //the main blockchain located at this node
    public static Blockchain masterChain = new Blockchain(); 
    
    //lock to synchronize mining between threads
    public static readonly object asyncMiningLock = new object(); 
    
    //whether the mining process is undergoing a cancellation process
    public static bool miningIsBeingCancelled = false;

    //the global node listening server
    public static NodeListenerServer nodeListener = new NodeListenerServer();
    
    //global async mining cancellation token
    public static CancellationTokenSource miningCancelToken = new CancellationTokenSource(); 
    
    //global P2P discovery & node registration cancellation token
    public static CancellationTokenSource nodeDiscoveryCancelToken = new CancellationTokenSource(); 
    
    //global async mempool sharing cancellation token
    public static CancellationTokenSource mempoolCancelToken = new CancellationTokenSource();

    //lock for accessing the nextBlocks container
    public static readonly object nextBlocksLock = new object(); 
    
    //the blocks that are currently being mined locally for the main blockchain at this node (if applicable). Note that
    //if parallel mining is disabled, then this list will only contain a single block.
    public static readonly List<Block> nextBlocks = new List<Block>();

    //keep track of the local next mined block
    public static Block? nextMinedBlock;
}