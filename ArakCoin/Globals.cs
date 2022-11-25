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
    
    //global async mining cancellation token
    public static CancellationTokenSource miningCancelToken = new CancellationTokenSource(); 
    
    //global P2P discovery & node registration cancellation token
    public static CancellationTokenSource nodeDiscoveryCancelToken = new CancellationTokenSource(); 
    
    //global async mempool sharing cancellation token
    public static CancellationTokenSource mempoolCancelToken = new CancellationTokenSource();

    //the block that is currently being mined locally for the main blockchain at this node (if applicable)
    public static Block? nextBlock = null;
}