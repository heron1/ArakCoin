using System.Diagnostics;
using ArakCoin;

namespace TestSuite.FunctionalTests;

[TestFixture]
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
		// pick a number of blocks to add that will test 2 difficulty intervals, along with some extra blocks
		int blocksToAdd = Settings.DIFFICULTY_INTERVAL_BLOCKS * 2 + Settings.DIFFICULTY_INTERVAL_BLOCKS / 2;
		for (int i = 0; i < blocksToAdd; i++)
		{
			bchain.addValidBlock(Factory.createAndMineEmptyBlock(bchain));
			Debug.WriteLine($"Tstamp diff: " +
			                $"{Blockchain.getTimestampDifferenceToPredecessorBlock(bchain.getLastBlock(), bchain)}, " +
			                $"Block: {bchain.getLength()}, Difficulty: {bchain.currentDifficulty}");
		}

		Assert.IsTrue(bchain.isBlockchainValid());
	}
}