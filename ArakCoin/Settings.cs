namespace ArakCoin;

public static class Settings
{
	#region Blockchain Protocol
	/**
	 * These settings are to be fixed. Changing them will invalidate the current protocol, thus creating a new
	 * blockchain protocol.
	 */
	
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

	#endregion

	
	#region Regular Settings
	/**
	 * These settings need not be fixed and may be adjustable without affecting the blockchain protocol
	 */
	
	// decide whether to throw an exception to terminate the program after Utilities.exceptionLog has been called
	public static bool terminateProgramOnExceptionLog = false;

	/**
	 * The public key to receive coins if this node mines a block
	 */
	public static string nodePublicKey = "1f62745d8f64ac7c9e28a17ad113cb2e4d1bd85e6eb6896f58de3bf3cabcd1b9";

	/**
	 * The private key to sign transactions this node creates
	 */
	public static string nodePrivateKey = "125ddf4ff1dca068ff72ab0a9dafe54170c3b3315326a0f8945a33db77eefd6b";

	/**
	 * Reject mempool transactions without a threshold miner fee. Only useful for miners
	 */
	public static int minMinerFee = 0;

	/**
	 * The ipv4 address of this host for network communication.
	 * Set this manually if the incorrect IP is being inferred from the Utilities.getLocalIpAddress() function
	 */
	public static string nodeIp = Utilities.getLocalIpAddress();

	/**
	 * The default port to use for network communication as a node
	 */
	public static int nodePort = 8000;

	/**
	 * Time out a network communication action after waiting this number of milliseconds
	 */
	public static int networkCommunicationTimeoutMs = 2000;

	/**
	 * The number of characters this node will allow for a valid ECHO request/response. It's recommended to leave this
	 * at the default value
	 */
	public static int echoCharLimit = 1000;

	#endregion

}