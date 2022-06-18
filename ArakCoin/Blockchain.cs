using System.Runtime.CompilerServices;

namespace ArakCoin;

/**
 * The Blockchain object is simply a linked list of Block objects, along with some helper methods
 */
public class Blockchain
{
	public LinkedList<Block> blockchain = new LinkedList<Block>();
	public int currentDifficulty = 0;

	/**
	 * Iteratively searches the blockchain for the block node with the given index. If not found, returns null
	 */
	private LinkedListNode<Block>? getBlockNodeByIndex(int index)
	{
		LinkedListNode<Block>? node = blockchain.First;
		while (node is not null && node.Value.index != index)
			node = node.Next;

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

		return true;
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
	 * Returns the last block of this blockchain in O(1) time. If the chain is empty, returns null
	 */
	public Block? getLastBlock()
	{
		return blockchain.Last?.Value;
	}

	/**
	 * Checks whether the data portion of the block is valid with respect to its previous block. Here we run two tests:
	 * 1) Test the format of the block is valid given the general protocol of the blockchain
	 * 2) Test the data itself contains only legal transactions within the context of its previous block
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
		// Ensure this block has the corresponding index of the length of the chain + 1
		if (block.index != blockchain.Count() + 1)
			return false; 
		
		// *Tests that apply to both genesis and ordinary blocks*
		if (block.timestamp <= 0)
		{
			return false;
		}
	
		// *Tests that apply only to the genesis block*
		if (blockchain.Count() == 0)
		{
			// expand this with additional tests if the block object has future parameters added
			// TODO validate block data for the genesis block. Ensure it has exact expected value
			if (!Utilities.isGenesisBlock(block))
				return false;
	
			return true;
		}
		
		// *Tests that apply only to non-genesis blocks*
		// Retrieve the last block node, ensure the new block can be legally appended to it
		Block lastBlock = blockchain.Last.Value;

		if (lastBlock.index != block.index - 1)
			return false;
		if (block.prevBlockHash != Block.calculateBlockHash(lastBlock))
			return false;
		if (!validateBlockData(block, lastBlock))
			return false;
		if (!block.hashDifficultyMatch())
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

	
	

}