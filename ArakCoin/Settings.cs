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
	
	// Estimate the value of work required for one higher difficulty as this number being raised to one higher power
	public static int DIFFICULTY_BASE = 10;

	// how large the threshold can be before a difficulty adjustment is required
	public static int DIFFICULTY_ADJUSTMENT_MULTIPLICATIVE_ALLOWANCE = 2;

	// starting difficulty of the blockchain
	public static int INITIALIZED_DIFFICULTY = 1;

	#endregion

	
	#region Regular Settings
	/**
	 * These settings need not be fixed and may be adjustable without affecting the blockchain protocol
	 */
	
	// decide whether to throw an exception to terminate the program after Utilities.exceptionLog has been called
	public static bool terminateProgramOnExceptionLog = false;
	#endregion

}