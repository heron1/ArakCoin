using System.Diagnostics;
using ArakCoin;

namespace TestSuite.FunctionalTests;

/*
 * Note these functional tests appear within the context of the current protocol settings, which are not modified
 * to low values like they are for the unit tests. They may take a while to complete depending upon the system
 */
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
		// for this test we temporarily set the Settings difficulties to much lower values
		int blocksToAdd = Settings.DIFFICULTY_INTERVAL_BLOCKS * 2 + Settings.DIFFICULTY_INTERVAL_BLOCKS / 2;
		for (int i = 0; i < blocksToAdd; i++)
		{
			bchain.addValidBlock(Factory.createAndMineEmptyBlock(bchain));
			Debug.WriteLine($"Tstamp diff: " +
			                $"{Utilities.getTimestampDifferenceToPredecessorBlock(bchain.getLastBlock(), bchain)}, " +
			                $"Block: {bchain.getLength()}, Difficulty: {bchain.currentDifficulty}");
		}

		Assert.IsTrue(bchain.isBlockchainValid());
	}
}