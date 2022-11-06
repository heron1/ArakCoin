﻿using ArakCoin.Transactions;

namespace ArakCoin;

public static class BlockFactory
{
	public static Block createNewBlock(Blockchain blockchain, Transaction[]? transactions = null)
	{
		if (blockchain.getLength() == 0)
			return Blockchain.createGenesisBlock();

		if (transactions is null)
			transactions = new Transaction[] {};

		return new Block(blockchain.getLength() + 1, transactions.ToArray(), Utilities.getTimestamp(),
			blockchain.getLastBlock().calculateBlockHash(), blockchain.currentDifficulty, 1);
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

	//returns true if a new block is successfully mined and added to blockchain, false if not. If no mempool
	//is given as an argument, it will use the input blockchain mempool. If the default blockchain mempool is
	//used, it will be cleared after the mine
	public static bool mineNextBlockAndAddToBlockchain(Blockchain blockchain, Transaction[]? mempool = null)
	{
		Block minedBlock;
		if (mempool is null)
		{
			minedBlock = createAndMineNewBlock(blockchain, blockchain.mempool.ToArray());
		}
		else
		{
			minedBlock = createAndMineNewBlock(blockchain, mempool);
		}
		
		bool success = blockchain.addValidBlock(minedBlock);
		if (success)
			blockchain.clearMempool();

		return success;
	}
}