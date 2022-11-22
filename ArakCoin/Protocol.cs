namespace ArakCoin;

/**
 * These protocol settings are to be fixed. Changing them will invalidate the current protocol, thus creating a new
 * blockchain protocol.
 */
public static class Protocol
{
    // desired seconds between each block mine
    public static int BLOCK_INTERVAL_SECONDS = 10;
	
    // actual blocks to wait before next difficulty adjustment check takes place
    public static int DIFFICULTY_INTERVAL_BLOCKS = 20;
	
    // how large the variance in actual difficulty can be before a difficulty adjustment is required
    public static int DIFFICULTY_ADJUSTMENT_MULTIPLICATIVE_ALLOWANCE = 2;
	
    // how large the variance in UTC timestamps between different nodes can be (in seconds)
    public static int DIFFERING_TIME_ALLOWANCE = 120;
	
    // Estimate the value of work required for one higher difficulty as this number being raised to one higher power
    public static int DIFFICULTY_BASE = 10;

    // starting difficulty of the blockchain
    public static int INITIALIZED_DIFFICULTY = 1;
	
    // block reward for the coinbase transaction/mining a new block TODO Advanced: Change this to scale to limit supply
    public static int BLOCK_REWARD = 15;

    // the protocol public fee address where coins sent to are considered destroyed, but are permitted as miner reward
    public static string FEE_ADDRESS = "0";
	
    // max transactions per block will limit the block size
    public static int MAX_TRANSACTIONS_PER_BLOCK = 10;

    
}