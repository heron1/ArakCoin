using ArakCoin.Transactions;

namespace ArakCoin;

public static class BlockFactory
{
	public static Block createNewBlock(Blockchain blockchain, Transaction[]? transactions = null, 
		long startingNonce = 1)
	{
		lock (blockchain.blockChainLock)
		{
			if (blockchain.getLength() == 0)
				return Blockchain.createGenesisBlock();

			if (transactions is null)
				transactions = new Transaction[] {};

			return new Block(blockchain.getLength() + 1, transactions.ToArray(), Utilities.getTimestamp(),
				blockchain.getLastBlock().calculateBlockHash(), blockchain.currentDifficulty, startingNonce);
		}
	}

	public static Block createAndMineNewBlock(Blockchain blockchain, Transaction[]? transactions = null)
	{
		Block block = createNewBlock(blockchain, transactions);

		if (!Blockchain.isGenesisBlock(block))
		{
			block.mineBlock();
		}

		return block;
	}

	/** Returns true if a new block is successfully mined and added to blockchain, false if not. Will automatically
	 * include transactions from the mempool (if any)
	 */
	public static bool mineNextBlockAndAddToBlockchain(Blockchain blockchain)
	{
		Transaction[] toBeMinedTx = blockchain.getTxesFromMempoolForBlockMine();
		Block minedBlock = createAndMineNewBlock(blockchain, toBeMinedTx);
		
		return blockchain.addValidBlock(minedBlock);
	}
}