using System.Numerics;
using System.Runtime.CompilerServices;

namespace ArakCoin;

/**
 * The Blockchain object is simply a linked list of Block objects, along with some helper methods
 */
public class Blockchain
{
	public LinkedList<Block> blockchain = new LinkedList<Block>();
	public int currentDifficulty = Settings.INITIALIZED_DIFFICULTY;

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
	public void replaceBlockchain(Blockchain newChain)
	{
		this.blockchain = newChain.blockchain;
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
		if (block.index != blockchain.Count() + 1)
			return false;
		if (!isNewBlockValid(block))
			return false;
		
		forceAddBlock(block);
		updateDifficulty();

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
	 * Mines the next block in this blockchain with the given data, and appends it to this blockchain
	 */
	public Block mineNextBlock(string data)
	{
		throw new ArgumentException();
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
	 * Checks whether the data portion of the block is valid with respect to its previous block. Here we run two tests:
	 * 1) Test the format of the data portion of the block is legal given the general protocol of the blockchain
	 * 2) Test the data contains only valid transactions within the context of its previous block
	 */
	public static bool validateBlockData(Block block, Block previousBlock)
	{
		return true;
		//TODO implement this
		throw new ArgumentException();
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
		if (block.index != blockchain.Count() + 1)
			return false; 
	
		// *Tests that apply only to the genesis block*
		if (blockchain.Count() == 0)
		{
			if (!isGenesisBlock(block))
				return false;
	
			return true; // if blockchain has 0 length and this block is a valid genesis block, it's valid
		}
		
		// *Tests that apply only to non-genesis blocks*
		// Retrieve the last block node, ensure the new block can be legally appended to it
		Block lastBlock = blockchain.Last.Value;

		if (lastBlock.index != block.index - 1)
			return false;
		if (!block.hashDifficultyMatch() || block.difficulty != currentDifficulty)
			return false;
		if (block.prevBlockHash != Block.calculateBlockHash(lastBlock))
			return false;
		if (!validateBlockData(block, lastBlock))
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
		if (chain.blockchain.Count() == 0)
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
		Block genesisBlock = new Block(1, "", Utilities.getTimestamp(), "0", 0, 1);
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
			if (winningChain == null)
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

}