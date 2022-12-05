using System.Numerics;
using System.Runtime.CompilerServices;
using ArakCoin.Data;
using ArakCoin.Transactions;

namespace ArakCoin;

/**
 * The Blockchain object is simply a linked list of Block objects, along with some state fields & helper methods
 */
public class Blockchain
{
	//global blockchain state
	public LinkedList<Block> blockchain = new LinkedList<Block>();
	public int currentDifficulty = Protocol.INITIALIZED_DIFFICULTY;
	public UTxOut[] uTxOuts = new UTxOut[]{}; //list of unspent tx outputs for this blockchain
	
	//local temporary state
	public List<Transaction> mempool = new List<Transaction>();
	public readonly object blockChainLock = new object(); //lock for critical sections on this blockchain

	/**
	 * Iteratively searches the blockchain in O(n) for the block node with the given index. If not found, returns null
	 * The iterative search begins from the last node, and moves toward the first node, since we are more likely to
	 * access nodes toward the tail than the head of the blockchain (such as for prior block difficulty checks)
	 */
	private LinkedListNode<Block>? getBlockNodeByIndex(int index)
	{
		lock (blockchain)
		{
			LinkedListNode<Block>? node = blockchain.Last;
			while (node is not null && node.Value.index != index)
				node = node.Previous;

			return node;
		}
	}

	/**
	 * Retrieves the length of this blockchain
	 */
	public int getLength()
	{
		return blockchain.Count();
	}

	/**
	 * Replace the current blockchain with the input one. This does not test to see if it's valid
	 */
	public void replaceBlockchain(Blockchain newChain, bool overwriteMempool = true)
	{
		lock (blockChainLock)
		{
			this.blockchain = newChain.blockchain;
			this.currentDifficulty = newChain.currentDifficulty;
			this.uTxOuts = newChain.uTxOuts;

			if (overwriteMempool)
				this.mempool = newChain.mempool;
			
			sanitizeMempool(); //mutate current mempool so that it's valid with the replaced chain
		}
	}

	/**
	 * Adds the given block to the end of the blockchain. Note this method does *not* check if such an operation is
	 * legal within the blockchain's protocol - it will add any block object regardless of its validity. This is not
	 * the recommended way to add blocks as it allows invalid blocks to be added. Instead use addValidBlock
	 */
	public void forceAddBlock(Block block)
	{
		blockchain.AddLast(block);
	}

	/**
	 * Adds the given block to the end of the blockchain only if the operation is valid. Will return a boolean as to
	 * whether or not this operation was successful. This is the recommended way to append new blocks
	 *
	 * Will also optionally update the mempool to remove any now invalid transactions due to the block add (default
	 * value is true)
	 */
	public bool addValidBlock(Block block, bool sanitizeMempoolIfBlockAdded = true)
	{
		lock(blockChainLock)
		{
			if (!isNewBlockValid(block))
				return false;

			if (!updateUTxOuts(block))
				return false;

			forceAddBlock(block);
			updateDifficulty();

			if (sanitizeMempoolIfBlockAdded)
				sanitizeMempool();

			return true;
		}
	}

	//update the unspent txouts within the given transactions
	public bool updateUTxOuts(Block block)
	{
		UTxOut[] updatedUTxOuts = Transaction.getUpdatedUTxOuts(block.transactions.ToArray(), uTxOuts);
		uTxOuts = updatedUTxOuts;

		return true;
	}

	/**
	 * Checks whether the most recent block in this blockchain will trigger a difficulty update, based upon the
	 * DIFFICULTY_INTERVAL_BLOCKS variable in Settings - if so it will calculate and update this blockchain
	 * to a new difficulty if applicable
	 */
	public void updateDifficulty()
	{
		lock (blockChainLock)
		{
			Block? lastBlock = getLastBlock();
			if (lastBlock is null)
				return;

			if (lastBlock.index % Protocol.DIFFICULTY_INTERVAL_BLOCKS == 0) // difficulty update interval reached
			{
				int lastDifficultyIndex = lastBlock.index - (Protocol.DIFFICULTY_INTERVAL_BLOCKS - 1);
				Block? lastDifficultyUpdateBlock = getBlockByIndex(lastDifficultyIndex);
				
				// The blockchain's length must be at least as long as DIFFICULTY_INTERVAL_BLOCKS for this conditional to have been
				// entered into. If it's not, the blockchain is in an invalid state. We will log this, and exit this function
				// Alternatively, if lastDifficultyUpdateBlock is null, this is also an unexpected event
				if (getLength() < Protocol.DIFFICULTY_INTERVAL_BLOCKS)
				{
					string exceptionStr =
						$"Expected blockchain length of at least: {Protocol.DIFFICULTY_INTERVAL_BLOCKS} but instead" +
						$"the length is: {getLength()}. Occurrence: updateDifficulty() inner conditional, with" +
						$"lastBlock.index value of {lastBlock.index}";
					Utilities.exceptionLog(exceptionStr);

					return;
				}
				else if (lastDifficultyUpdateBlock is null)
				{
					string exceptionStr =
						$"Expected block at index {lastDifficultyIndex}, but received null. Occurrence: updateDifficulty() inner " +
						$"conditional, with lastBlock.index value of {lastBlock.index}, and DIFFICULTY_INTERVAL_BLOCKS value of" +
						$"{Protocol.DIFFICULTY_INTERVAL_BLOCKS}";
					Utilities.exceptionLog(exceptionStr);
					
					return;
				}

				long actualTimeTakenForDifficultyInterval = lastBlock.timestamp - lastDifficultyUpdateBlock.timestamp;
				//set time taken to be at least 1 second if it's less
				actualTimeTakenForDifficultyInterval = Math.Max(1, actualTimeTakenForDifficultyInterval);
				
				int expectedTimeTakenForDifficultyInterval =
					Protocol.BLOCK_INTERVAL_SECONDS * Protocol.DIFFICULTY_INTERVAL_BLOCKS;

				int lowerTimeBound = expectedTimeTakenForDifficultyInterval /
				                     Protocol.DIFFICULTY_ADJUSTMENT_MULTIPLICATIVE_ALLOWANCE;
				int higherTimeBound = expectedTimeTakenForDifficultyInterval *
				                      Protocol.DIFFICULTY_ADJUSTMENT_MULTIPLICATIVE_ALLOWANCE;
				
				if (actualTimeTakenForDifficultyInterval < lowerTimeBound)
				{
					// Time taken was shorter than expected, so we increase the blockchain difficulty
					int difficultyChange = 1;

					// change difficulty with proportion to how far off actual and expected time taken are
					while (actualTimeTakenForDifficultyInterval < expectedTimeTakenForDifficultyInterval / 
					       Math.Pow(Protocol.DIFFICULTY_BASE, difficultyChange + 1))
					{
						difficultyChange++;
					}
					
					currentDifficulty += difficultyChange;
					Utilities.log($"Difficulty increased to {currentDifficulty}");
				}
				else if (actualTimeTakenForDifficultyInterval > higherTimeBound)
				{
					// Time taken was greater than expected, so we decrease the blockchain difficulty
					int difficultyChange = 1;

					// change difficulty with proportion to how far off actual and expected time taken are
					while (actualTimeTakenForDifficultyInterval > expectedTimeTakenForDifficultyInterval * 
					       Math.Pow(Protocol.DIFFICULTY_BASE, difficultyChange + 1))
					{
						difficultyChange++;
					}
					
					currentDifficulty -= difficultyChange;
					Utilities.log($"Difficulty decreased to {currentDifficulty}");
				}
			}
		}
	}

	/**
	 * Iteratively searches the blockchain for the block with the given index. If not found, returns null
	 */
	public Block? getBlockByIndex(int index)
	{
		LinkedListNode<Block>? node = getBlockNodeByIndex(index);
		if (node is not null)
			return node.Value;

		return null;
	}

	/**
	 * Returns the first block of this blockchain in O(1) time. If the chain is empty, return null
	 */
	public Block? getFirstBlock()
	{
		return blockchain.First?.Value;
	}
	
	/**
	 * Returns the last block of this blockchain in O(1) time. If the chain is empty, returns null
	 */
	public Block? getLastBlock()
	{
		return blockchain.Last?.Value;
	}

	/**
	 * Checks whether the data portion of the proposed block is valid to be appended to this blockchain.
	 * Here we check for main two things -
	 * 1) Test the format of the data portion of the block is legal given the general protocol of the blockchain
	 * 2) Test the data contains only valid transactions within the context of the blockchain
	 */
	public bool validateNextBlockData(Block block)
	{
		Transaction[] transactions = block.transactions.ToArray();

		//block data must contain at least one transaction
		if (block.transactions is null || transactions.Length == 0)
			return false;
		
		//however it cannot exceed the tx limit per block as per the protocol
		if (block.transactions.Length > Protocol.MAX_TRANSACTIONS_PER_BLOCK)
			return false;

		//first ensure the first block transaction is a valid coinbase tx for this block
		if (!Transaction.isValidCoinbaseTransaction(transactions[0], block))
			return false;
		
		//ensure no duplicate txIns
		if (Transaction.doesTxArrayContainDuplicateTxIn(block.transactions))
			return false;

		//now validate every normal transaction
		for (var i = 1; i < transactions.Length; i++)
		{
			var tx = transactions[i];
			
			//normal transactions cannot be coinbase
			if (tx.isCoinbaseTx)
				return false;

			//check the transaction is valid with respect to the current blockchain snapshot
			if (!Transaction.isValidTransaction(tx, uTxOuts))
				return false;
		}

		return true;
	}

	/**
	 * Tests whether the input block can be legally appended to the end of the chain
	 */
	public bool isNewBlockValid(Block block)
	{
		// *Tests that apply to both genesis and ordinary blocks*
		if (block.timestamp <= 0)
		{
			return false;
		}
		if (block.index != getLength() + 1)
			return false; 
	
		// *Tests that apply only to the genesis block*
		if (getLength() == 0)
		{
			if (!isGenesisBlock(block))
				return false;
	
			return true; // if blockchain has 0 length and this block is a valid genesis block, it's valid
		}
		
		// *Tests that apply only to non-genesis blocks*
		// Retrieve the last block node, ensure the new block can be legally appended to it
		Block lastBlock = getLastBlock();

		if (lastBlock.index != block.index - 1)
			return false;
		if (!block.hashDifficultyMatch() || block.difficulty != currentDifficulty)
			return false;
		if (block.prevBlockHash != Block.calculateBlockHash(lastBlock))
			return false;
		if (!isNewBlockTimestampValid(block))
			return false;
		if (!validateNextBlockData(block))
			return false;
		
		return true;
	}

	/**
	 * We check whether the timestamp of the input block is valid with respect to whether or not it can be appended as
	 * the new block to this blockchain, based upon this hosts own local time. If it is, we return true, otherwise
	 * false. Note that if the blockchain is empty, this always returns true, provided the timestamp is legal
	 * 
	 * Note the DIFFERING_TIME_ALLOWANCE variable in Settings determines how much leeway (in seconds) is allowed
	 */
	public bool isNewBlockTimestampValid(Block block)
	{
		if (block.timestamp <= 0)
			return false; // illegal timestamp
		
		Block? lastBlock = getLastBlock();
		if (lastBlock is null)
			return true;
		
		// ensure new block is not too far into the past with respect to the last block
		if (lastBlock.timestamp - Protocol.DIFFERING_TIME_ALLOWANCE > block.timestamp)
			return false;
		
		// ensure new block is not too far into the future from this hosts own local time
		if (block.timestamp - Protocol.DIFFERING_TIME_ALLOWANCE > Utilities.getTimestamp())
			return false;

		return true; 
	}
	
	/**
	 * Tests whether the given blockchain is valid
	 */
	public static bool isBlockchainValid(Blockchain chain)
	{
		lock (chain.blockChainLock)
		{
			bool originalMessageDisplay = Settings.displayLogMessages;

			try
			{
				//disable logging for the validation if applicable
				Settings.displayLogMessages = false;

				LinkedListNode<Block>? blockNode = chain.blockchain.First;

				// empty chain must have a null first element
				if (chain.getLength() == 0)
				{
					if (blockNode is not null)
						return false;

					return true;
				}

				//rebuild the chain, ensure each added block is permissible
				Blockchain rebuildChain = new Blockchain();

				while (blockNode is not null)
				{
					bool success = rebuildChain.addValidBlock(blockNode.Value);
					if (!success)
						return false;

					blockNode = blockNode.Next;
				}

				//assert difficulty is correct
				if (rebuildChain.currentDifficulty != chain.currentDifficulty)
					return false;

				//assert the uTxOuts in the rebuild chain are identical to input chain
				if (rebuildChain.uTxOuts.Length != chain.uTxOuts.Length)
					return false;
				for (int i = 0; i < rebuildChain.uTxOuts.Length; i++)
				{
					if (rebuildChain.uTxOuts[i] != chain.uTxOuts[i])
						return false;
				}

				//assert total circulating coin supply is valid
				long expectedSupply = (rebuildChain.getLength() - 1) * Protocol.BLOCK_REWARD;
				long actualSupply = Wallet.getCurrentCirculatingCoinSupply(rebuildChain);
				if (expectedSupply != actualSupply)
					return false;

				return true;
			}
			finally
			{
				//re-enable log messages if applicable
				Settings.displayLogMessages = originalMessageDisplay;
			}
		}
	}
	
	/**
	 * Tests whether this blockchain is valid
	 */
	public bool isBlockchainValid()
	{
		return isBlockchainValid(this);
	}

	/**
	 * Returns the accumulative difficulty of a chain, in terms of an estimate of the number of hash attempts required
	 * to mine all its blocks
	 */
	public static BigInteger calculateAccumulativeChainDifficulty(Blockchain chain)
	{
		BigInteger accumulatedDifficulty = 0;
		LinkedListNode<Block>? blockNode = chain.blockchain.First;
		
		while (blockNode is not null)
		{
			accumulatedDifficulty += Utilities.convertDifficultyToHashAttempts(blockNode.Value.difficulty);
			blockNode = blockNode.Next;
		}

		return accumulatedDifficulty;
	}

	/**
	 * Returns the accumulative difficulty of this chain
	 */
	public BigInteger calculateAccumulativeChainDifficulty()
	{
		return calculateAccumulativeChainDifficulty(this);
	}

	/**
	 * There can only be one genesis block which is fixed. This function will generate it.
	 */
	public static Block createGenesisBlock()
	{
		Block genesisBlock = new Block(1, new Transaction[]{}, 
			Utilities.getTimestamp(), "0", 0, 1);
		
		return genesisBlock;
	}

	/**
	 * Returns whether the input block is the genesis block or not. The genesis block can only vary in its timestamp
	 */
	public static bool isGenesisBlock(Block block)
	{
		if (block.index == 1 && block.prevBlockHash == "0" && block.difficulty == 0 && 
		    block.nonce == 1 && block.transactions.Length == 0)
			return true;

		return false;
	}

	/**
	 * Adds the given transaction to the mempool in a position corresponding to its miner fee, but only if it meets the
	 * add requirements for this node. Note this may fail whilst protocol requirements may pass
	 * (use Transaction.isValidTransactionWithinPool to see if the tx passes protocol requirements).
	 *
	 * In the event the mempool is already full, the input transaction will replace the one at the end of the existing
	 * mempool if it has a higher fee, provided the allowOverride parameter is set to true (default), otherwise it
	 * won't be added. Note that the *doesTransactionMeetMemPoolAddRequirements* function won't check for this by
	 * default and would return the transaction as not being valid within the mempool if it's already full. The method
	 * here is special in that it can delete an existing valid transaction to create room for a higher paying one,
	 * if required.
	 * 
	 * Returns true if transaction was successfully added to the mempool, otherwise false
	 */
	public static bool addTransactionToMempoolGivenNodeRequirements(Transaction tx, List<Transaction> mempool,
		UTxOut[] uTxOuts, bool allowOverride = true)
	{
		if (!Transaction.doesTransactionMeetMemPoolAddRequirements(tx, mempool, uTxOuts, allowOverride))
			return false;

		//the miner fee that this transaction is paying
		long txFee = Transaction.getMinerFeeFromTransaction(tx);

		//keeps track of whether this transaction was inserted into the mempool & pushed another one down the mempool
		bool wasTxInserted = false; 
		
		//the ordering within the mempool should be based upon miner fee
		for (int i = 0; i < mempool.Count; i++)
		{
			if (Transaction.getMinerFeeFromTransaction(mempool[i]) < txFee)
			{
				mempool.Insert(i, tx);
				wasTxInserted = true;
				break;
			}
		}

		if (!wasTxInserted && mempool.Count >= Settings.maxMempoolSize)
		{
			//mempool is full and this transaction hasn't paid a higher fee than any other transaction in the pool,
			//therefore the add operation fails
			return false;
		}
		else if (!wasTxInserted)
		{
			//mempool isn't full, so we add this transaction to the end of the pool successfully
			mempool.Add(tx);
		}
		else if (wasTxInserted && mempool.Count > Settings.maxMempoolSize)
		{
			//this transaction was added to the mempool, however it caused another transaction paying the lowest fee to
			//be pushed beyond the legally allowed mempool size. We will remove this last tx
			mempool.RemoveAt(mempool.Count - 1);
			
			//if somehow the mempool is still too large, then this should be logged and investigated, as the logical
			//flow of the program should not allow this
			if (mempool.Count > Settings.maxMempoolSize)
			{
				Utilities.exceptionLog($"Mempool somehow illegaly acquired length of {mempool.Count} after removing" +
				                       $" last element");
			}
		}
		
		//transaction was successfully added
		Utilities.log($"Added tx with id {tx.id.ToString().Substring(0, 3)}.. " +
		              $"(paying {Transaction.getMinerFeeFromTransaction(tx)} fee) " +
		              $"to local mempool (size now {mempool.Count})");
		return true;
	}
	
	public bool addTransactionToMempoolGivenNodeRequirements(Transaction tx, bool allowOverride = true)
	{
		lock (blockChainLock)
		{
			return addTransactionToMempoolGivenNodeRequirements(tx, mempool, uTxOuts, allowOverride);
		}
	}

	//clears the current mempool
	public void clearMempool()
	{
		this.mempool = new List<Transaction>();
	}
	
	//validates the given mempool - returns true if valid, false otherwise
	public static bool validateMemPool(List<Transaction> mempool, UTxOut[] uTxOuts)
	{
		var tempPool = new List<Transaction>();
		
		foreach (var tx in mempool)
		{
			if (!Transaction.doesTransactionMeetMemPoolAddRequirements(tx, tempPool, uTxOuts))
				return false;

			tempPool.Add(tx);
		}
		
		return true;
	}
	
	//validates this blockchain's mempool
	public bool validateMemPool()
	{
		lock (blockChainLock)
		{
			return validateMemPool(this.mempool, this.uTxOuts);
		}
	}

	/**
	 * Given the input mempool, remove any transactions in it that are no longer valid with respect to the given
	 * uTxOuts
	 */
	public static List<Transaction> sanitizeMempool(List<Transaction> mempool, UTxOut[] uTxOuts)
	{
		var newPool = new List<Transaction>();
		foreach (var tx in mempool)
			if (Transaction.isValidTransactionWithinPool(tx, newPool, uTxOuts))
				newPool.Add(tx);
		
		return newPool;
	}

	/**
	 * Sanitize the locally stored mempool - remove any transactions in it that are no longer valid with respect
	 * to this blockchain
	 */
	public void sanitizeMempool()
	{
		lock (blockChainLock)
		{
			this.mempool = sanitizeMempool(this.mempool, this.uTxOuts);
		}
	}

	/**
	 * Retrieve the highest priority transactions from the mempool for a block mine and return them as an array.
	 * Does not mutate the mempool
	 */
	public Transaction[] getTxesFromMempoolForBlockMine()
	{
		lock (blockChainLock) {
			int endIndex;
			if (mempool.Count < Protocol.MAX_TRANSACTIONS_PER_BLOCK)
				endIndex = mempool.Count;
			else
				endIndex = Protocol.MAX_TRANSACTIONS_PER_BLOCK - 1; //leave space for 1 extra tx for the coinbase tx

			return Utilities.sliceList(this.mempool, 0, endIndex).ToArray();
		}
	}

	/**
	 * Given a list of blockchains, return the valid blockchain with the greatest accumulative hashpower in its mined blocks
	 * For tiebreakers, the earlier chain in the list is chosen instead of later ones (First In winner)
	 * For this reason, it's recommended to make the current chain (if applicable) as the first element of the list
	 * 
	 * Will return null if there is no valid blockchain
	 */
	public static Blockchain? establishWinningChain(List<Blockchain> blockchains)
	{
		Blockchain? winningChain = null;
		BigInteger winnerDifficulty = 0;
		Blockchain chain;
		for (int i = 0; i < blockchains.Count; i++)
		{
			chain = blockchains[i];
			if (winningChain is null)
			{
				// we only need to assert this chain is valid to make it the new winning chain, since current winner is null
				if (chain.isBlockchainValid())
				{
					winningChain = chain;
					winnerDifficulty = chain.calculateAccumulativeChainDifficulty();
				}
			}
			else
			{
				if (chain.calculateAccumulativeChainDifficulty() > winnerDifficulty && chain.isBlockchainValid())
				{
					winningChain = chain;
					winnerDifficulty = chain.calculateAccumulativeChainDifficulty();
				}
			}
		}

		return winningChain;
	}

	/**
	 * Saves the main blockchain located on this host to disk. Returns whether this was successfully done
	 */
	public static bool saveMasterChainToDisk(string filename = "master_blockchain")
	{
		lock (Globals.masterChain.blockChainLock)
		{
			var serializedChain = Serialize.serializeBlockchainToJson(Globals.masterChain);
			if (serializedChain is null)
				return false;

			if (!Storage.writeJsonToDisk(serializedChain, filename))
				return false;

			return true;
		}
	}

	/**
	 * Loads the main blockchain located on this host from disk into memory, and validates it. Returns whether this was
	 * successfully done
	 */
	public static bool loadMasterChainFromDisk(string filename = "master_blockchain")
	{
		var serializedChain = Storage.readJsonFromDisk(filename);
		if (serializedChain is null)
			return false;

		var deserializedChain = Serialize.deserializeJsonToBlockchain(serializedChain);
		if (deserializedChain is null)
			return false;
		
		//validate the blockchain
		if (!deserializedChain.isBlockchainValid())
			return false;

		//sucessfully loaded and validated the masterchain. Set it as the global chain
		Globals.masterChain = deserializedChain;
		return true;
	}

	#region Blockchain Helper Methods
	/**
	 * Given the input block and blockchain, calculate the timestamp difference between that block and its predecessor.
	 * Note that due to timestamping variation allowances in terms of block acceptance, it could be possible a negative
	 * value is returned if the previous block has a later timestamp than its successor.
	 * 
	 * Returns 0 for the genesis block or a blockchain with <= 1 element, returns null if the block cannot be found
	 */
	public static long? getTimestampDifferenceToPredecessorBlock(Block block, Blockchain bchain)
	{
		if (bchain.getLength() <= 1 || isGenesisBlock(block))
			return 0;

		Block? previousBlock = bchain.getBlockByIndex(block.index - 1);
		if (previousBlock is null)
			return null;

		return block.timestamp - previousBlock.timestamp;
	}

	/**
	 * Given the input block, calculate the timestamp difference between that block and its predecessor in this blockchain.
	 * Note that due to timestamping variation allowances in terms of block acceptance, it could be possible a negative
	 * value is returned if the previous block has a later timestamp than its successor.
	 * 
	 * Returns 0 for the genesis block or a blockchain with <= 1 element, returns null if the block cannot be found
	 */
	public long? getTimestampDifferenceToPredecessorBlock(Block block)
	{
		return getTimestampDifferenceToPredecessorBlock(block, this);
	}
	
	#endregion
	
	#region equality override
	
	//determine if two blockchains are identical. Does not check local fields (eg: mempool)
	public override bool Equals(object? o)
	{
		if (o is null || o.GetType() != typeof(Blockchain))
			return false;
		Blockchain other = (Blockchain)o;

		//first assert difficulty is identical
		if (this.currentDifficulty != other.currentDifficulty)
			return false;
		
		//next assert the UTxOuts are identical
		if (this.uTxOuts.Length != other.uTxOuts.Length)
			return false;
		for (int i = 0; i < uTxOuts.Length; i++)
		{
			if (this.uTxOuts[i] != other.uTxOuts[i])
				return false;
		}
		
		//now check every block is identical
		if (this.blockchain.Count != other.blockchain.Count)
			return false;
		LinkedListNode<Block>? nodeThis = this.blockchain.First;
		LinkedListNode<Block>? nodeOther = other.blockchain.First;
		if (nodeThis is null && nodeOther is null)
			return true;
		if (nodeThis is null || nodeOther is null)
			return false;
		while (nodeThis is not null)
		{
			if (nodeThis.Value != nodeOther.Value)
				return false;

			nodeThis = nodeThis.Next;
			nodeOther = nodeOther.Next;
		}

		return true;
	}

	public static bool operator == (Blockchain b1, Blockchain b2)
	{
		return b1.Equals(b2);
	}

	public static bool operator != (Blockchain b1, Blockchain b2)
	{
		return !(b1 == b2);
	}
	
	#endregion equality override

}