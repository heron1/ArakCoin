namespace ArakCoin;

public static class Factory
{
	/**
	 * Create an empty block for the given blockchain, which isn't yet mined. If the blockchain is empty, return
	 * the genesis block.
	 */
	public static Block createEmptyBlock(Blockchain blockchain)
	{
		if (blockchain.getLength() == 0)
			return Utilities.createGenesisBlock();
		
		return new Block(blockchain.getLength() + 1, "", Utilities.getTimestamp(),
			blockchain.getLastBlock().calculateBlockHash(), blockchain.currentDifficulty, 1);
	}

	/**
	 * Create an empty block for the given blockchain, and also mine it. If the blockchain is empty, return
	 * the genesis block instead without any mining.
	 */
	public static Block createAndMineEmptyBlock(Blockchain blockchain)
	{
		Block block = createEmptyBlock(blockchain);
		
		if (!Utilities.isGenesisBlock(block))
			block.mineBlock();

		return block;
	}
}