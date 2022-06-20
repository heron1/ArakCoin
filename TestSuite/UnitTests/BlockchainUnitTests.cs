using System.Diagnostics;
using System.Numerics;
using ArakCoin;

namespace TestSuite.UnitTests;

[TestFixture]
public class Tests
{
	private Blockchain bchain;

	[SetUp]
	public void Setup()
	{
		bchain = new Blockchain();
	}

	[Test]
	public void TestBlockEquality()
	{
		// Note that blocks may mutate, meaning that two equal blocks now may not be equal in the future. This tests that
		
		Block b1 = Factory.createEmptyBlock(bchain);
		Block b2 = Factory.createEmptyBlock(bchain);
		b1.timestamp = b2.timestamp;
		
		// blocks should have different references
		Assert.IsFalse(ReferenceEquals(b1, b2));
		
		// but the blocks should be identical
		Assert.IsTrue(b1 == b2);
		Assert.IsTrue(b1.Equals(b2));

		b1.nonce++;
		
		// blocks should now be different
		Assert.IsFalse(b1 == b2);
		Assert.IsFalse(b1.Equals(b2));

		b1.nonce--;
		b1.timestamp++;
		
		// blocks should be different
		Assert.IsFalse(b1 == b2);
		Assert.IsFalse(b1.Equals(b2));

		b1.timestamp--;
		b1.difficulty++;
		
		// blocks should be different
		Assert.IsFalse(b1 == b2);
		Assert.IsFalse(b1.Equals(b2));

		b1.difficulty--;
		b1.prevBlockHash = "00";
		
		// blocks should be different
		Assert.IsFalse(b1 == b2);
		Assert.IsFalse(b1.Equals(b2));

		b1.prevBlockHash = b2.prevBlockHash;
		b1.data = "1";
		
		// blocks should be different
		Assert.IsFalse(b1 == b2);
		Assert.IsFalse(b1.Equals(b2));

		b1.data = b2.data;
		
		// blocks should still have different references
		Assert.IsFalse(ReferenceEquals(b1, b2));
		
		// but should now be equal again
		Assert.IsTrue(b1 == b2);
		Assert.IsTrue(b1.Equals(b2));
	}

	[Test]
	public void TestValidBlocksAdded()
	{
		Assert.IsTrue(bchain.getLength() == 0);
		
		// genesis block
		bchain.addValidBlock(Blockchain.createGenesisBlock());
		Assert.IsTrue(bchain.getLength() == 1);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());

		// normal block 1
		Block nextBlock = Factory.createAndMineEmptyBlock(bchain);
		bchain.addValidBlock(nextBlock);
		Assert.IsTrue(bchain.getLength() == 2);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		
		// normal block 2
		nextBlock = Factory.createAndMineEmptyBlock(bchain);
		bchain.addValidBlock(nextBlock);
		Assert.IsTrue(bchain.getLength() == 3);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		
		// normal block but with a timestamp in the future equal to half of the time variance allowance
		nextBlock = Factory.createEmptyBlock(bchain);
		nextBlock.timestamp += Settings.DIFFERING_TIME_ALLOWANCE / 2;
		while (!nextBlock.hashDifficultyMatch())
			nextBlock.nonce++;
		bchain.addValidBlock(nextBlock);
		Assert.IsTrue(bchain.getLength() == 4);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		
		// normal block but with a timestamp in the past equal to half of the time variance allowance
		nextBlock = Factory.createEmptyBlock(bchain);
		nextBlock.timestamp -= Settings.DIFFERING_TIME_ALLOWANCE / 2;
		while (!nextBlock.hashDifficultyMatch())
			nextBlock.nonce++;
		bchain.addValidBlock(nextBlock);
		Assert.IsTrue(bchain.getLength() == 5);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		
		// create a new blockchain with an old head block, and test that it allows a much newer new block timestamp,
		// but not a much older one than the head block
		Blockchain oldChain = new Blockchain();
		Block oldBlockNext = Factory.createEmptyBlock(oldChain);
		oldBlockNext.timestamp = 10000; // a very old timestamp
		while (!oldBlockNext.hashDifficultyMatch())
			oldBlockNext.nonce++;
		oldChain.addValidBlock(oldBlockNext); // old genesis block should be accepted
		Assert.IsTrue(oldChain.getLength() == 1);

		oldBlockNext = Factory.createEmptyBlock(oldChain);
		oldBlockNext.timestamp = Utilities.getTimestamp(); // our current timestamp is much later than the last block
		while (!oldBlockNext.hashDifficultyMatch())
			oldBlockNext.nonce++;
		oldChain.addValidBlock(oldBlockNext); // much newer block should be accepted
		Assert.IsTrue(oldChain.getLength() == 2);
		
		oldBlockNext = Factory.createEmptyBlock(oldChain);
		oldBlockNext.timestamp = 10; // a much older timestamp than the last block
		while (!oldBlockNext.hashDifficultyMatch())
			oldBlockNext.nonce++;
		oldChain.addValidBlock(oldBlockNext); // much older block should be rejected
		Assert.IsFalse(oldChain.getLength() == 3);
		Assert.IsTrue(oldChain.isBlockchainValid()); // our oldChain should still be valid
		
		Assert.IsTrue(bchain.isBlockchainValid());
	}

	
	[Test]
	public void TestInvalidBlocksRejected()
	{
		//TODO keep adding more invalid tests until final block structure determined. This includes testing data element
		Assert.IsTrue(bchain.getLength() == 0);
		
		bchain.addValidBlock(Blockchain.createGenesisBlock());
		Assert.IsTrue(bchain.getLength() == 1);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());

		Block lastBlock = bchain.getLastBlock();
		// test invalid index (over by 1)
		Block nextBlock = new Block(
			bchain.getLength() + 2, "helloIndex2", Utilities.getTimestamp(),
			bchain.getLastBlock().calculateBlockHash(), bchain.currentDifficulty, 1);
		nextBlock.mineBlock();
		bchain.addValidBlock(nextBlock);
		Assert.IsFalse(bchain.getLength() == 2);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		Assert.IsTrue(bchain.getLastBlock() == lastBlock);
		
		// test invalid index (under by 1)
		nextBlock = new Block(
			bchain.getLength(), "helloIndex2", Utilities.getTimestamp(),
			bchain.getLastBlock().calculateBlockHash(), bchain.currentDifficulty, 1);
		nextBlock.mineBlock();
		bchain.addValidBlock(nextBlock);
		Assert.IsFalse(bchain.getLength() == 2);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		Assert.IsTrue(bchain.getLastBlock() == lastBlock);
		
		// test illegal timestamp value (note we must do a manual mine to ensure timestamp doesn't change)
		nextBlock = Factory.createEmptyBlock(bchain);
		nextBlock.timestamp = -1;
		while (!nextBlock.hashDifficultyMatch())
			nextBlock.nonce++;
		bchain.addValidBlock(nextBlock);
		Assert.IsFalse(bchain.getLength() == 2);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		Assert.IsTrue(bchain.getLastBlock() == lastBlock);
		
		// test block with legal timestamp but which is invalid within the context of our test blockchain (over)
		nextBlock = Factory.createEmptyBlock(bchain);
		nextBlock.timestamp += Settings.DIFFERING_TIME_ALLOWANCE * 2;
		while (!nextBlock.hashDifficultyMatch())
			nextBlock.nonce++;
		bchain.addValidBlock(nextBlock);
		Assert.IsFalse(bchain.getLength() == 2);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		Assert.IsTrue(bchain.getLastBlock() == lastBlock);
		
		// test block with legal timestamp but which is invalid within the context of our test blockchain (under)
		nextBlock = Factory.createEmptyBlock(bchain);
		nextBlock.timestamp -= Settings.DIFFERING_TIME_ALLOWANCE * 2;
		while (!nextBlock.hashDifficultyMatch())
			nextBlock.nonce++;
		bchain.addValidBlock(nextBlock);
		Assert.IsFalse(bchain.getLength() == 2);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		Assert.IsTrue(bchain.getLastBlock() == lastBlock);
		
		//add a valid block before the next hash test, first assert it's successfully added
		nextBlock = Factory.createAndMineEmptyBlock(bchain);
		bchain.addValidBlock(nextBlock);
		Assert.IsTrue(bchain.getLength() == 2);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		lastBlock = nextBlock;
		Assert.IsTrue(bchain.getLastBlock() == lastBlock);
		
		// test invalid hash (from wrong block)
		nextBlock = Factory.createEmptyBlock(bchain);
		nextBlock.prevBlockHash = bchain.getBlockByIndex(1).calculateBlockHash();
		nextBlock.mineBlock();
		bchain.addValidBlock(nextBlock);
		Assert.IsTrue(bchain.getLength() == 2);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		Assert.IsTrue(bchain.getLastBlock() == lastBlock);
		
		// test mining requirements for the given difficulty are not met
		nextBlock = Factory.createEmptyBlock(bchain);
		while (nextBlock.hashDifficultyMatch())
			nextBlock.nonce++;
		bchain.addValidBlock(nextBlock);
		Assert.IsTrue(bchain.getLength() == 2);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		Assert.IsTrue(bchain.getLastBlock() == lastBlock);
		
		// test mining requirements for the given difficulty are not met, after they are initially met
		nextBlock = Factory.createAndMineEmptyBlock(bchain);
		while (nextBlock.hashDifficultyMatch())
			nextBlock.nonce++;
		bchain.addValidBlock(nextBlock);
		Assert.IsTrue(bchain.getLength() == 2);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		Assert.IsTrue(bchain.getLastBlock() == lastBlock);
		
		// test all invalid blocks were successfully rejected
		Assert.IsTrue(bchain.isBlockchainValid());
		
		// force add an invalid block (wrong hash), verify chain is now invalid
		bchain.forceAddBlock(nextBlock);
		Assert.IsFalse(bchain.isBlockchainValid());
	}

	[Test]
	public void TestInvalidGenesisBlockRejected()
	{
		Assert.IsTrue(bchain.getLength() == 0);

		// below actions should fail - an invalid genesis block cannot be added
		Block genesisSpoof = Factory.createEmptyBlock(bchain);
		Assert.IsTrue(Blockchain.isGenesisBlock(genesisSpoof));
		
		genesisSpoof.nonce = 2;
		Assert.IsFalse(Blockchain.isGenesisBlock(genesisSpoof));
		bchain.addValidBlock(genesisSpoof);
		Assert.IsTrue(bchain.getLength() == 0);

		genesisSpoof.nonce = 1;
		genesisSpoof.prevBlockHash = "";
		bchain.addValidBlock(genesisSpoof);
		Assert.IsTrue(bchain.getLength() == 0);

		genesisSpoof.prevBlockHash = "";
		bchain.forceAddBlock(genesisSpoof);
		Assert.IsFalse(bchain.isBlockchainValid());
		
		// replace blockchain, restore genesisSpoof back to real genesis, ensure it successfully adds as genesis block 
		bchain.replaceBlockchain(new Blockchain());
		genesisSpoof.prevBlockHash = "0";
		bchain.addValidBlock(genesisSpoof);
		Assert.IsTrue(bchain.getLength() == 1);
		Assert.IsTrue(bchain.getLastBlock() == genesisSpoof);
	}

	[Test]
	public void TestInvalidBlockchainDetected()
	{
		Assert.IsTrue(bchain.getLength() == 0);
		
		bchain.addValidBlock(Blockchain.createGenesisBlock());
		Assert.IsTrue(bchain.getLength() == 1);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());

		Block nextBlock = Factory.createAndMineEmptyBlock(bchain);
		bchain.addValidBlock(nextBlock);
		Assert.IsTrue(bchain.getLength() == 2);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		
		nextBlock = Factory.createAndMineEmptyBlock(bchain);
		bchain.addValidBlock(nextBlock);
		Assert.IsTrue(bchain.getLength() == 3);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		
		// blockchain is valid up until this point, but now we will change the nonce in block 2, making it invalid
		bchain.getBlockByIndex(2).nonce++;
		
		Assert.IsFalse(bchain.isBlockchainValid());
	}

	[Test]
	public void TestBlockchainAccumulativeDifficulty()
	{
		BigInteger accumDifficulty;
		
		// put blockchain protocol settings to low values for this test so it doesn't take too long
		Settings.DIFFICULTY_INTERVAL_BLOCKS = 5;
		Settings.BLOCK_INTERVAL_SECONDS = 2;
		Settings.INITIALIZED_DIFFICULTY = 2;
		bchain = new Blockchain(); // re-initialize with these settings
		
		// first assert the convertDifficultyToHashAttempts function is working correctly
		Assert.IsTrue(Utilities.convertDifficultyToHashAttempts(0) == 0);
		Assert.IsTrue(Utilities.convertDifficultyToHashAttempts(1) == BigInteger.Pow(2, 4 * 1));
		Assert.IsTrue(Utilities.convertDifficultyToHashAttempts(17) == BigInteger.Pow(2, 4 * 17));
		Assert.IsTrue(Utilities.convertDifficultyToHashAttempts(1000) == BigInteger.Pow(2, 4 * 1000));
		
		// We now test the calculateAccumulativeChainDifficulty function
		// genesis block alone has 0 accumulative difficulty
		bchain.addValidBlock(Factory.createAndMineEmptyBlock(bchain));
		Assert.IsTrue(bchain.calculateAccumulativeChainDifficulty() == 0); 

		// adding 1 block should give the chain an accumulative difficulty equal to the initialized difficulty
		bchain.addValidBlock(Factory.createAndMineEmptyBlock(bchain));
		accumDifficulty = Utilities.convertDifficultyToHashAttempts(Settings.INITIALIZED_DIFFICULTY);
		Assert.IsTrue(bchain.calculateAccumulativeChainDifficulty() == accumDifficulty);
		Assert.IsTrue(bchain.getLength() == 2);
		Debug.WriteLine($"Accum. difficulty of {accumDifficulty} at {bchain.getLength()} blocks with " +
		                $"current chain difficulty {bchain.currentDifficulty} asserted");

		while (bchain.getLength() < Settings.DIFFICULTY_INTERVAL_BLOCKS)
		{
			bchain.addValidBlock(Factory.createAndMineEmptyBlock(bchain));
		}
		// accumulative difficulty should be equal to the hash difficulty of chain length minus 1 before the first
		// update difficulty event (due to ignoring genesis block)
		accumDifficulty = Utilities.convertDifficultyToHashAttempts(Settings.INITIALIZED_DIFFICULTY)
		                  * (bchain.getLength() - 1);
		Assert.IsTrue(bchain.calculateAccumulativeChainDifficulty() == accumDifficulty);
		Debug.WriteLine($"Accum. difficulty of {accumDifficulty} at {bchain.getLength()} blocks with " +
		                $"current chain difficulty {bchain.currentDifficulty} asserted");

		while (bchain.getLength() < Settings.DIFFICULTY_INTERVAL_BLOCKS * 2)
		{
			bchain.addValidBlock(Factory.createAndMineEmptyBlock(bchain));
		}
		// fully test accumulative difficulty behaves as expected after difficulty adjustment
		int lastMinedDifficulty = 
			bchain.getBlockByIndex(bchain.getLength() - 1).difficulty; // difficulty prior to 2nd difficulty adjustment
		BigInteger firstIntervalAccumulativeDifficulty =
			Utilities.convertDifficultyToHashAttempts(Settings.INITIALIZED_DIFFICULTY) 
			* (Settings.DIFFICULTY_INTERVAL_BLOCKS - 1); // - 1 to subtract genesis block from this interval
		BigInteger secondIntervalAccumulativeDifficulty = 
			Utilities.convertDifficultyToHashAttempts(lastMinedDifficulty) * Settings.DIFFICULTY_INTERVAL_BLOCKS;
		accumDifficulty = firstIntervalAccumulativeDifficulty;
		accumDifficulty += secondIntervalAccumulativeDifficulty;
		Assert.IsTrue(accumDifficulty == bchain.calculateAccumulativeChainDifficulty());
		Debug.WriteLine($"Accum. difficulty of {accumDifficulty} at {bchain.getLength()} blocks with " +
		                $"current chain difficulty {bchain.currentDifficulty} asserted");
	}

	/**
	 * Access point for testing arbitrary things in the project
	 */
	[Test]
	public void Temp()
	{
	}

}