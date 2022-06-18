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
		if (block.index == 1 && block.data == "" && block.prevBlockHash == "0" && block.difficulty == 0 && block.nonce == 1)
			return true;

		return false;
	}
}