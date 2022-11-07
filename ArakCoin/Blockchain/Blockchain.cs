using System.Numerics;
using System.Runtime.CompilerServices;
using ArakCoin.Transactions;

namespace ArakCoin;

/**
 * The Blockchain object is simply a linked list of Block objects, along with some state fields & helper methods
 */
public class Blockchain
{
	//global blockchain state
	public LinkedList<Block> blockchain = new LinkedList<Block>();
	public int currentDifficulty = Settings.INITIALIZED_DIFFICULTY;
	public UTxOut[] uTxOuts = new UTxOut[]{}; //list of unspent tx outputs for this blockchain
	
	//local temporary state
	public List<Transaction> mempool = new List<Transaction>();

	
	/**
	 * Iteratively searches the blockchain in O(n) for the block node with the given index. If not found, returns null
	 * The iterative search begins from the last node, and moves toward the first node, since we are more likely to
	 * access nodes toward the tail than the head of the blockchain (such as for prior block difficulty checks)
	 */
	private LinkedListNode<Block>? getBlockNodeByIndex(int index)
	{
		LinkedListNode<Block>? node = blockchain.Last;
		while (node is not null && node.Value.index != index)
			node = node.Previous;

		return node;
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
	public void replaceBlockchain(Blockchain newChain, bool replaceMemPool = true)
	{
		this.blockchain = newChain.blockchain;
		this.currentDifficulty = newChain.currentDifficulty;
		this.uTxOuts = newChain.uTxOuts;
		
		if (replaceMemPool)
			this.mempool = newChain.mempool;
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
	 */
	public bool addValidBlock(Block block)
	{
		if (!isNewBlockValid(block))
			return false;

		if (!updateUTxOuts(block))
			return false;
		
		forceAddBlock(block);
		updateDifficulty();

		return true;
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
		Block? lastBlock = getLastBlock();
		if (lastBlock is null)
			return;

		if (lastBlock.index % Settings.DIFFICULTY_INTERVAL_BLOCKS == 0) // difficulty update interval reached
		{
			int lastDifficultyIndex = lastBlock.index - (Settings.DIFFICULTY_INTERVAL_BLOCKS - 1);
			Block? lastDifficultyUpdateBlock = getBlockByIndex(lastDifficultyIndex);
			
			// The blockchain's length must be at least as long as DIFFICULTY_INTERVAL_BLOCKS for this conditional to have been
			// entered into. If it's not, the blockchain is in an invalid state. We will log this, and exit this function
			// Alternatively, if lastDifficultyUpdateBlock is null, this is also an unexpected event
			if (getLength() < Settings.DIFFICULTY_INTERVAL_BLOCKS)
			{
				string exceptionStr =
					$"Expected blockchain length of at least: {Settings.DIFFICULTY_INTERVAL_BLOCKS} but instead" +
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
					$"{Settings.DIFFICULTY_INTERVAL_BLOCKS}";
				Utilities.exceptionLog(exceptionStr);
				
				return;
			}

			long actualTimeTakenForDifficultyInterval = lastBlock.timestamp - lastDifficultyUpdateBlock.timestamp;
			//set time taken to be at least 1 second if it's less
			actualTimeTakenForDifficultyInterval = Math.Max(1, actualTimeTakenForDifficultyInterval);
			
			int expectedTimeTakenForDifficultyInterval =
				Settings.BLOCK_INTERVAL_SECONDS * Settings.DIFFICULTY_INTERVAL_BLOCKS;

			int lowerTimeBound = expectedTimeTakenForDifficultyInterval /
			                     Settings.DIFFICULTY_ADJUSTMENT_MULTIPLICATIVE_ALLOWANCE;
			int higherTimeBound = expectedTimeTakenForDifficultyInterval *
			                      Settings.DIFFICULTY_ADJUSTMENT_MULTIPLICATIVE_ALLOWANCE;
			
			if (actualTimeTakenForDifficultyInterval < lowerTimeBound)
			{
				// Time taken was shorter than expected, so we increase the blockchain difficulty
				int difficultyChange = 1;

				// change difficulty with proportion to how far off actual and expected time taken are
				while (actualTimeTakenForDifficultyInterval < expectedTimeTakenForDifficultyInterval / 
				       Math.Pow(Settings.DIFFICULTY_BASE, difficultyChange + 1))
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
				       Math.Pow(Settings.DIFFICULTY_BASE, difficultyChange + 1))
				{
					difficultyChange++;
				}
				
				currentDifficulty -= difficultyChange;
				Utilities.log($"Difficulty decreased to {currentDifficulty}");
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
		if (block.transactions.Length > Settings.MAX_TRANSACTIONS_PER_BLOCK)
			return false;

		var tempPool = new List<Transaction>(); //temp new tx pool for block validation
		
		//first ensure the first block transaction is a valid coinbase tx for this block
		if (!Transaction.isValidCoinbaseTransaction(transactions[0], block))
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

			//if tx is valid, add it to the temporary tx pool in this function's scope to ensure
			//a subsequent transaction doesn't re-use any of the uTxOuts within this block
			if (!Transaction.isValidTransactionWithinPool(tx, tempPool, uTxOuts))
				return false;

			tempPool.Add(tx);
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
		if (!validateNextBlockData(block))
			return false;
		if (!isNewBlockTimestampValid(block))
			return false;
		
		return true;
	}

	/**
	 * We check whether the timestamp of the input block is valid with respect to whether or not it can be appended as the
	 * new block to this blockchain, based upon this hosts own local time. If it is, we return true, otherwise false.
	 * Note that if the blockchain is empty, this always returns true, provided the timestamp is legal
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
		if (lastBlock.timestamp - Settings.DIFFERING_TIME_ALLOWANCE > block.timestamp)
			return false;
		
		// ensure new block is not too far into the future from this hosts own local time
		if (block.timestamp - Settings.DIFFERING_TIME_ALLOWANCE > Utilities.getTimestamp())
			return false;

		return true; 
	}
	
	/**
	 * Tests whether the given blockchain is valid
	 */
	public static bool isBlockchainValid(Blockchain chain)
	{
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
		long expectedSupply = (rebuildChain.getLength() - 1) * Settings.BLOCK_REWARD;
		long actualSupply = Wallet.getCurrentCirculatingCoinSupply(rebuildChain);
		if (expectedSupply != actualSupply)
			return false;

		return true;
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

	//adds the given transaction to the mempool, only if it meets the requirements for this node
	//note this may fail whilst protocol requirements may pass (use Transaction.isValidTransactionWithinPool to see
	//if the tx passes protocol requirements). Returns true if success, otherwise false
	public bool addTransactionToMempoolGivenNodeRequirements(Transaction tx)
	{
		if (!Transaction.doesTransactionMeetMemPoolAddRequirements(tx, mempool, uTxOuts))
			return false;

		mempool.Add(tx);
		return true;
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
		return validateMemPool(this.mempool, this.uTxOuts);
	}

	//TODO - this is proposed advanced functionality. If time permits, then given a list of input mempools, retrieve the txes
	//that fill a new mempool up to the allowed protocol size, with the maximum miner fees, whilst ensuring all
	//transactions are valid. This would allow miners to communicate mempools with one another whilst retaining an
	//optimal local mempool which may be different to other nodes, whilst still using some received
	//mempool transactions (if miner fees in such txes are higher than locally
	//pooled txes and such txes don't yet exist in the local pool, they can be added/replaced within the local pool).
	public List<Transaction>? createOptimalMempool(List<List<Transaction>> mempools)
	{
		return null;
	}
	
	//TODO - Given a list of mempools, return the valid one with the greatest accumulative miner fees. Useful for
	//inter-node communication to get the best mempool
	public List<Transaction>? getBestMempool(List<List<Transaction>> mempools)
	{
		return null;
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
	
	#region Blockchain Transaction Methods
	
	
	#endregion
	
	
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