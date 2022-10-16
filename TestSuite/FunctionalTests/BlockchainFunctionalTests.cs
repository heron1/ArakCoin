using System.Diagnostics;
using ArakCoin;

namespace TestSuite.FunctionalTests;

/*
 * These functional tests test the current actual protocol, not any adjusted protocol for testing purposes
 */
[TestFixture]
[Category("FunctionalTests")]
public class BlockchainFunctionalTests
{
	private Blockchain bchain;

	[SetUp]
	public void Setup()
	{
		bchain = new Blockchain();
	}
	
	[Test]
	public void TestDifficultyScaling()
	{
		LogTestMsg("Testing TestDifficultyScaling..");
	
		// pick a number of blocks to add that will test 2 difficulty intervals, along with some extra blocks
		int blocksToAdd = Settings.DIFFICULTY_INTERVAL_BLOCKS * 2 + Settings.DIFFICULTY_INTERVAL_BLOCKS / 2;
		for (int i = 0; i < blocksToAdd; i++)
		{
			bchain.addValidBlock(BlockFactory.createAndMineNewBlock(bchain));
			LogTestMsg($"\tTstamp diff: " +
			                $"{Blockchain.getTimestampDifferenceToPredecessorBlock(bchain.getLastBlock(), bchain)}, " +
			                $"Block: {bchain.getLength()}, Difficulty: {bchain.currentDifficulty}");
		}
	
		Assert.IsTrue(bchain.isBlockchainValid());
	}

	[Test]
	public void TestByzantineConsensus()
	{
		//TODO Three separate nodes communicate different blockchains. Assert all nodes converge to the correct chain
	}
}