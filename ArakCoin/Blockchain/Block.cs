﻿using ArakCoin.Transactions;

namespace ArakCoin;

/**
 * An individual Block within a Blockchain object
 */
public class Block
{
	//block data fields that contribute to its hash
	public readonly int index;
	public Transaction[] transactions;
	public long timestamp;
	public string prevBlockHash;
	public int difficulty;
	public long nonce;

	//below fields are not part of the block's data and don't contribute to its hash
	public bool cancelMining = false; //allow a thread to cancel the mining of this block
	
	public Block(int index, Transaction[]? transactions, long timestamp, string prevBlockHash, int difficulty, 
		int nonce)
	{
		if (transactions is null)
			transactions = new Transaction[] { };
		
		this.index = index;
		this.transactions = transactions;
		this.timestamp = timestamp;
		this.prevBlockHash = prevBlockHash;
		this.difficulty = difficulty;
		this.nonce = nonce;
	}
	
	/**
	 * Calculate the block hash for any given block. Returns null if the block has an invalid format
	 */
	public static string? calculateBlockHash(Block block)
	{
		if (block.transactions is null || block.prevBlockHash is null || block.index < 0)
			return null;
		
		string? transactionsString = Transaction.convertTxArrayToString(block.transactions.ToArray());
		if (transactionsString is null)
			return null;
		string inputStr =
			$"{block.index.ToString()},{transactionsString}," +
			$"{block.timestamp.ToString()}," +
			$"{block.prevBlockHash.ToString()},{block.difficulty.ToString()},{block.nonce.ToString()}";

		return Utilities.calculateSHA256Hash(inputStr);
	}

	/**
	 * Calculate block hash for this block. Returns null if the block is invalid
	 */
	public string? calculateBlockHash()
	{
		return calculateBlockHash(this);
	}

	/**
	 * Returns true if the hash of this block matches its difficulty or greater, otherwise false
	 */
	public bool hashDifficultyMatch()
	{
		string? hash = calculateBlockHash();
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

		string? startingHash = calculateBlockHash(this);
		
		//assert block has valid data, if it doesn't, return false
		if (startingHash is null)
			return false;

		Utilities.log($"Mining block #{index} with starting hash {startingHash.Substring(0, 4)}...");

		//now we begin the mining process
		while (!hashDifficultyMatch())
		{
			if (cancelMining) //block mining has been cancelled by another thread, we terminate this method
				return hashDifficultyMatch(); 

			long newTimestamp = Utilities.getTimestamp();
			if (timestamp != newTimestamp)
			{
				timestamp = newTimestamp;
				nonce = 1;
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