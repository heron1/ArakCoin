namespace ArakCoin;

/**
 * An individual Block within a Blockchain object
 */
public class Block
{
	public readonly int index;
	public string data; //temp, replace with custom type later for the data portion
	public long timestamp;
	public string prevBlockHash;
	public int difficulty;
	public long nonce;

	public Block(int index, string data, int timestamp, string prevBlockHash, int difficulty, int nonce)
	{
		this.index = index;
		this.data = data;
		this.timestamp = timestamp;
		this.prevBlockHash = prevBlockHash;
		this.difficulty = difficulty;
		this.nonce = nonce;
	}
	
	/**
	 * Calculate the block hash for any given block
	 */
	public static string calculateBlockHash(Block block)
	{
		string inputStr =
			$"{block.index.ToString()},{block.data.ToString()},{block.timestamp.ToString()}," +
			$"{block.prevBlockHash.ToString()},{block.difficulty.ToString()},{block.nonce.ToString()}";

		return Utilities.calculateSHA256Hash(inputStr);
	}

	/**
	 * Calculate block hash for this block
	 */
	public string calculateBlockHash()
	{
		return calculateBlockHash(this);
	}

	/**
	 * Returns true if the hash of this block matches its difficulty or greater, otherwise false
	 */
	public bool hashDifficultyMatch()
	{
		string hash = calculateBlockHash();
		int hashDifficulty = 0;
		for (int i = 0; i < hash.Length; i++)
		{
			if (hash[i] == '0')
			{
				if (++hashDifficulty >= difficulty)
					break;
			}
			else
				break;
		}

		return hashDifficulty >= difficulty;
	}

	/**
	 * Mine this block until its hash matches its difficulty. By default this will continue without any timeout, however
	 * if a timeout is specified (in seconds) then the mining will stop once this is reached.
	 
	 * Returns true once the block is mined, false otherwise
	 */
	public bool mineBlock(int? timeoutSeconds = null)
	{
		int timestampStart = Utilities.getTimestamp();
		
		while (!hashDifficultyMatch())
		{
			int newTimestamp = Utilities.getTimestamp();
			if (timestamp != newTimestamp)
			{
				timestamp = newTimestamp;
				nonce = 1;

				if (timeoutSeconds is not null)
				{
					if (newTimestamp - timestampStart > timeoutSeconds)
						return hashDifficultyMatch(); // our optional timeout has been reached, we terminate the method
				}
			}

			nonce++;
		}

		return true; // block has been mined
	}
	
	
	#region equality override
	public override bool Equals(object? o)
	{
		if (o is null || o.GetType() != typeof(Block))
			return false;
		Block other = (Block)o;

		if (this.index == other.index && this.data == other.data && this.timestamp == other.timestamp &&
		    this.prevBlockHash == other.prevBlockHash && this.difficulty == other.difficulty && this.nonce == other.nonce)
			return true;

		return false;
	}

	public static bool operator == (Block b1, Block b2)
	{
		return b1.Equals(b2);
	}

	public static bool operator != (Block b1, Block b2)
	{
		return !(b1 == b2);
	}
	
	#endregion equality override
}