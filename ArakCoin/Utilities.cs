using System.Diagnostics;
using System.Text;

namespace ArakCoin;

public class Utilities
{
	/**
	 * Given any string, will convert it to another string representing its hexadecimal sha256 hash
	 */
	public static string calculateSHA256Hash(string input)
	{
		byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
		var hashBuilder = new StringBuilder();
		foreach (byte b in hashBytes)
		{
			hashBuilder.Append(b.ToString("x2"));
		}

		return hashBuilder.ToString();
	}

	/**
	 * Returns the current UTC time since epoch (in seconds)
	 */
	public static int getTimestamp()
	{
		return (int)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
	}

	/**
	 * There can only be one genesis block which is fixed. This function will generate it.
	 */
	public static Block createGenesisBlock()
	{
		Block genesisBlock = new Block(1, "", getTimestamp(), "0", 0, 1);
		return genesisBlock;
	}

	/**
	 * Returns whether the input block is the genesis block or not. The genesis block can only vary in its timestamp
	 */
	public static bool isGenesisBlock(Block block)
	{
		// TODO validate block data for the genesis block. Ensure it has exact expected value
		if (block.index == 1 && block.data == "" && block.prevBlockHash == "0" && block.difficulty == 0 && block.nonce == 1)
			return true;

		return false;
	}

	/**
	 * Given the input block and blockchain, calculate the timestamp difference between that block and its predecessor.
	 * Note that due to timestamping variation allowances in terms of block acceptance, it could be possible a negative
	 * value is returned if the previous block has a later timestamp than its successor.
	 * 
	 * Returns 0 for the genesis block or a blockchain with <= 1 element, returns null if the block cannot be found
	 */
	public static int? getTimestampDifferenceToPredecessorBlock(Block block, Blockchain bchain)
	{
		if (bchain.getLength() <= 1 || isGenesisBlock(block))
			return 0;

		Block? previousBlock = bchain.getBlockByIndex(block.index - 1);
		if (previousBlock is null)
			return null;

		return block.timestamp - previousBlock.timestamp;
	}

	/**
	 * For general logging, such as for warnings and info, we will call this function to log the message somewhere. This
	 * implementation may change over time, but the same function can still be called.
	 */
	public static void log(string logMsg)
	{
		Console.WriteLine($"Log message: {logMsg}");
		Debug.WriteLine($"Log message: {logMsg}");
	}

	/**
	 * If we encounter an invalid program state, we will call this function to
	 * log a message and alert the client/node of what's occurred. We may also optionally throw an exception
	 * depending upon the value of terminateProgramOnExceptionLog in Setting.cs
	 * This implementation may change over time, but the same function can still be called.
	 */
	public static void exceptionLog(string exceptionLog)
	{
		Console.WriteLine($"Exception log message: {exceptionLog}");
		Debug.WriteLine($"Exception log message: {exceptionLog}");

		if (Settings.terminateProgramOnExceptionLog)
			throw new Exception($"exceptionLog triggered and terminateProgramOnExceptionLog was set to true. " +
			                    $"exceptionLog message: {exceptionLog}");
	}
}