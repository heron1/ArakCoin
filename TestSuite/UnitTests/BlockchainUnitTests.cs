using System.Diagnostics;
using ArakCoin;

namespace TestSuite.UnitTests;

[TestFixture]
public class Tests
{
	private Blockchain bchain;

	[SetUp]
	public void Setup()
	{
		// initialize fixed low values for blockchain protocol so unit tests don't take long
		Settings.BLOCK_INTERVAL_SECONDS = 10;
		Settings.DIFFICULTY_INTERVAL_BLOCKS = 20;
		Settings.DIFFICULTY_BASE = 10;
		Settings.DIFFICULTY_ADJUSTMENT_MULTIPLICATIVE_ALLOWANCE = 2;
		Settings.INITIALIZED_DIFFICULTY = 1;

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
		
		// test invalid timestamp (note we must do a manual mine to ensure timestamp doesn't change)
		nextBlock = Factory.createEmptyBlock(bchain);
		nextBlock.timestamp = -1;
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

	/**
	 * Access point for testing arbitrary things in the project
	 */
	[Test]
	public void Temp()
	{
		
	}

}