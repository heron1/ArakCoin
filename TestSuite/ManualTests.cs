using System.Diagnostics;
using ArakCoin;

namespace TestSuite;

/*
 * Non-automated functional tests. These tests may require manual intervention in the testing process, or may loop
 * forever until terminated (such as simulating blockchain mining without any end condition, as one test example)
 *
 * These tests should not be run as part of any automated testing process, but only for manual verificaiton
 */
[TestFixture]
[Category("ManualTests")]
public class ManualTests
{
	private Blockchain bchain;

	[SetUp]
	public void Setup()
	{
		bchain = new Blockchain();
	}
	
	[Test]
	public void TestBlockchainMining()
	{
		// Tests the blockchain mining process until terminated
		while (true)
		{
			bchain.addValidBlock(Factory.createAndMineEmptyBlock(bchain));
			Debug.WriteLine($"Tstamp diff: " +
			                     $"{Blockchain.getTimestampDifferenceToPredecessorBlock(bchain.getLastBlock(), bchain)}, " +
			                     $"Block: {bchain.getLength()}, Difficulty: {bchain.currentDifficulty}");
		}
	}
}