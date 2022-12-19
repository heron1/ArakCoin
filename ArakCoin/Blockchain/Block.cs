using ArakCoin.Transactions;

namespace ArakCoin;

/**
 * An individual Block within a Blockchain object
 */
public class Block
{
	//block header data fields that contribute to its hash
	public readonly int index;
	public string? merkleRoot = null; //block has an invalid hash until its merkleRoot is calculated
	public long timestamp;
	public string prevBlockHash;
	public int difficulty;
	public long nonce;
	
	//only full nodes need contain the actual block transactions (only the merkleRoot is used in the block hash)
	public Transaction[] transactions;

	//below fields are not part of the block's data and don't contribute to its hash
	public bool cancelMining = false; //allow a thread to cancel the mining of this block
	private long startingMineNonce = 0; //for mining operations, always begin at this nonce. Useful for mining
									   //parallelism where different threads may begin at different nonces
									   
	/**
	 * If the isParallelBlock parameter is set to true (default false), then if this block is being mined, it will stop
	 * mining and return once endingNonceIfParallel equals the nonce. This is only useful for parallel mining
	 */
	public Block(int index, Transaction[]? transactions, long timestamp, string prevBlockHash, int difficulty, 
		long nonce)
	{
		if (transactions is null)
			transactions = new Transaction[] { };

		startingMineNonce = nonce; //keep track of the input nonce for mining operations, to always start from it
		
		this.index = index;
		this.transactions = transactions;
		this.timestamp = timestamp;
		this.prevBlockHash = prevBlockHash;
		this.difficulty = difficulty;
		this.nonce = nonce;
	}
	
	/**
	 * Calculate the block hash for any given block. Returns null if the block has an invalid format.
	 * Note that if the merkleRoot parameter is not null, it's assumed to represent the merkle root transactions of the
	 * block in string format so that this need not undergo re-calculation every function call. This is useful only for
	 * mining optimization to make it an O(1) operation. For validation, the merkle root should always be calculated
	 * from the current block transactions (ie: the merkleRoot should be left at the default null value).
	 */
	public static string? calculateBlockHash(Block block, string? merkleRoot = null)
	{
		if (block.transactions is null || block.prevBlockHash is null || block.index < 0)
			return null;
		
		if (merkleRoot is null)
		{
			//first ensure every transaction in the block is valid
			foreach (var tx in block.transactions)
			{
				
			}
			
			merkleRoot = Transaction.convertTxArrayToString(block.transactions.ToArray());
			if (merkleRoot is null)
				return null;
		}
		
		string inputStr =
			$"{block.index.ToString()},{merkleRoot}," +
			$"{block.timestamp.ToString()}," +
			$"{block.prevBlockHash.ToString()},{block.difficulty.ToString()},{block.nonce.ToString()}";

		return Utilities.calculateSHA256Hash(inputStr);
	}

	/**
	 * Calculate block hash for this block. Returns null if the block is invalid
	 */
	public string? calculateBlockHash(string? transactionsString = null)
	{
		return calculateBlockHash(this, transactionsString);
	}

	/**
	 * Returns true if the hash of this block matches its difficulty or greater, otherwise false. An input transactions
	 * string argument can be passed to pre-calculate the transactions as a string for the block. This should only
	 * be used for mining optimization to make it an O(1) operation - it should not be used for block validation.
	 */
	public bool hashDifficultyMatch(string? transactionsString = null)
	{
		string? hash = calculateBlockHash(transactionsString);
		if (hash is null)
			return false;
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
	 * Mine this block until its hash matches its difficulty. A block mine can be cancelled by setting the block's
	 * cancelMining property to true from another thread
	 *	 
	 * Returns true once the block is mined, false otherwise. Coinbase transaction rewards are set to this node's
	 * public key within the Settings file
	 */
	public bool mineBlock()
	{
		//first set the cancelMining property to false in case it was previously set to true
		cancelMining = false;
		
		//we create the coinbase transaction for this block using this node's public key,
		//and prepend it to the list of transactions. If the first transaction is already a coinbase
		//transaction, we override it
		if (transactions.Length != 0 && transactions[0].isCoinbaseTx)
			transactions = transactions.Skip(1).ToArray(); //remove the current coinbase tx

		Transaction? coinbaseTx = TransactionFactory.createCoinbaseTransaction(Settings.nodePublicKey,
			index, transactions);
		if (coinbaseTx is null)
		{
			Utilities.exceptionLog($"Attempted to create coinbaseTx in block {index} but failed");
			return false;
		}

		transactions = ((new[] { coinbaseTx }).Concat(transactions)).ToArray();
		if (!Transaction.isValidCoinbaseTransaction(coinbaseTx, this))
		{
			Utilities.exceptionLog($"Created coinbase tx in block {index} but it failed validation");
			return false;
		}

		string? startingHash = calculateBlockHash();
		
		//assert block has valid data, if it doesn't, return false
		if (startingHash is null)
		{
			Utilities.log("Attempted to mine block with invalid hash, cancelling..");
			return false;
		}
		
		//assert the transactions in the block don't exceed protocol limit. If they do, return false
		if (transactions.Length > Protocol.MAX_TRANSACTIONS_PER_BLOCK)
		{
			Utilities.log($"Attempted to mine blocks with {transactions.Length} transactions however the protocol " +
			              $"has a limit of {Protocol.MAX_TRANSACTIONS_PER_BLOCK}, cancelling..");
			return false;
		}
		
		//pre-calculate the transaction string so that mining becomes an n + O(m) instead of an O(m*n) operation, where
		//m represents the number of iterations required to find a hash difficulty match, and n the number of
		//transactions in the block.
		string? transactionsString = Transaction.convertTxArrayToString(this.transactions.ToArray());
		if (transactionsString is null)
			return false;

		Utilities.log($"Mining block #{index} with difficulty {difficulty} and " +
		              $"starting hash {startingHash.Substring(0, 4)}...");

		//now we begin the mining process
		while (!hashDifficultyMatch(transactionsString))
		{
			if (cancelMining) //block mining has been cancelled by another thread, we terminate this method
				return hashDifficultyMatch(transactionsString); 

			long newTimestamp = Utilities.getTimestamp();
			if (timestamp != newTimestamp)
			{
				timestamp = newTimestamp;
				nonce = startingMineNonce;
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

		if (this.index == other.index && Transaction.convertTxArrayToString(this.transactions) == 
		    Transaction.convertTxArrayToString(other.transactions) && this.timestamp == other.timestamp &&
		    this.prevBlockHash == other.prevBlockHash && this.difficulty == other.difficulty && 
		    this.nonce == other.nonce)
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