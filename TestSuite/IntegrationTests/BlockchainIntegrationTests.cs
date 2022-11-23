using System.Diagnostics;
using System.Numerics;
using ArakCoin;
using ArakCoin.Transactions;

namespace TestSuite.IntegrationTests;

[TestFixture]
[Category("IntegrationTests")]
public class BlockchainIntegration
{
	private Transaction tx;	
	
	private Blockchain bchain;

	[SetUp]
	public void Setup()
	{
		// put blockchain protocol settings to low values integration tests so they don't take too long
		Protocol.DIFFICULTY_INTERVAL_BLOCKS = 50;
		Protocol.BLOCK_INTERVAL_SECONDS = 1;
		Protocol.INITIALIZED_DIFFICULTY = 1;
		
		Protocol.BLOCK_REWARD = 20;
		Protocol.MAX_TRANSACTIONS_PER_BLOCK = 10;
		Settings.nodePublicKey = testPublicKey;
		Settings.nodePrivateKey = testPrivateKey;
		
		bchain = new Blockchain();
		//mine a normal block to create some coins for the tests
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //genesis
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //normal

		tx = TransactionFactory.createTransaction(new TxOut[]
		{
			new TxOut(testPublicKey2, 2),
			new TxOut(testPrivateKey3, 3)
		}, testPrivateKey, bchain.uTxOuts, bchain.mempool, 0, false)!;
		Assert.IsNotNull(tx);
	}

	[Test]
	public void TestBlockEquality()
	{
		LogTestMsg("Testing TestBlockEquality..");
		// Note that blocks may mutate, meaning that two equal blocks now may not be equal in the future. This tests that
		
		Block b1 = BlockFactory.createNewBlock(bchain, new Transaction[] {tx});
		Block b2 = BlockFactory.createNewBlock(bchain, new Transaction[] {tx});
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
		//modify a tx
		tx = TransactionFactory.createTransaction(new TxOut[]
		{
			new TxOut(testPublicKey2, 3), //moved from 2 to 3
			new TxOut(testPrivateKey3, 3)
		}, testPrivateKey, bchain.uTxOuts, bchain.mempool, 0, false)!;
		b1.transactions = new Transaction[] {tx};
		
		// blocks should be different
		Assert.IsFalse(b1 == b2);
		Assert.IsFalse(b1.Equals(b2));
	
		//change tx back to unmodified state
		tx = TransactionFactory.createTransaction(new TxOut[]
		{
			new TxOut(testPublicKey2, 2), //2 is original amount
			new TxOut(testPrivateKey3, 3)
		}, testPrivateKey, bchain.uTxOuts, bchain.mempool, 0, false)!;
		b1.transactions = new Transaction[] {tx};
		
		// blocks should still have different references
		Assert.IsFalse(ReferenceEquals(b1, b2));
		
		// but should now be equal again
		Assert.IsTrue(b1 == b2);
		Assert.IsTrue(b1.Equals(b2));
	}

	[Test]
	public void TestValidBlocksAdded()
	{
		LogTestMsg("Testing TestValidBlocksAdded..");

		Assert.IsTrue(bchain.getLength() == 2); // 2 blocks already exist from setup

		// add normal block 
		Block nextBlock = BlockFactory.createAndMineNewBlock(bchain, null);
		bchain.addValidBlock(nextBlock);
		Assert.IsTrue(bchain.getLength() == 3);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());

		// normal block but with a timestamp in the future equal to half of the time variance allowance
		nextBlock = BlockFactory.createNewBlock(bchain, null);
		nextBlock.timestamp += Protocol.DIFFERING_TIME_ALLOWANCE / 2;

		//manual mine here, as the .mineBlock method will modify the timestamp we're trying to test
		Transaction? coinbaseTx = TransactionFactory.createCoinbaseTransaction(Settings.nodePublicKey,
			bchain.getLength() + 1, null);
		Assert.IsNotNull(coinbaseTx);
		nextBlock.transactions = new Transaction[] { coinbaseTx! };
		while (!nextBlock.hashDifficultyMatch())
			nextBlock.nonce++;
		
		bchain.addValidBlock(nextBlock);
		Assert.IsTrue(bchain.getLength() == 4);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
	
		// normal block but with a timestamp in the past equal to half of the time variance allowance
		nextBlock = BlockFactory.createNewBlock(bchain, null);
		nextBlock.timestamp -= Protocol.DIFFERING_TIME_ALLOWANCE / 2;
		
		//manual mine here, as the .mineBlock method will modify the timestamp we're trying to test
		coinbaseTx = TransactionFactory.createCoinbaseTransaction(Settings.nodePublicKey,
			bchain.getLength() + 1, null);
		Assert.IsNotNull(coinbaseTx);
		nextBlock.transactions = new Transaction[] { coinbaseTx! };
		while (!nextBlock.hashDifficultyMatch())
			nextBlock.nonce++;
		
		bchain.addValidBlock(nextBlock);
		Assert.IsTrue(bchain.getLength() == 5);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		
		// create a new blockchain with an old head block, and test that it allows a much newer new block timestamp,
		// but not a much older one than the head block
		Blockchain oldChain = new Blockchain();
		Block oldBlockNext = BlockFactory.createNewBlock(oldChain, null);
		oldBlockNext.timestamp = 10000; // a very old timestamp
		
		//manual mine here, as the .mineBlock method will modify the timestamp we're trying to test
		//note: there can be no coinbase tx (or any other tx) for the 1st/genesis block
		while (!oldBlockNext.hashDifficultyMatch())
			oldBlockNext.nonce++;
		
		oldChain.addValidBlock(oldBlockNext); // old genesis block should be accepted
		Assert.IsTrue(oldChain.getLength() == 1);
	
		oldBlockNext = BlockFactory.createNewBlock(oldChain, null);
		oldBlockNext.timestamp = Utilities.getTimestamp(); // our current timestamp is much later than the last block
		
		//manual mine here, as the .mineBlock method will modify the timestamp we're trying to test
		coinbaseTx = TransactionFactory.createCoinbaseTransaction(Settings.nodePublicKey,
			oldChain.getLength() + 1, null);
		Assert.IsNotNull(coinbaseTx);
		oldBlockNext.transactions = new Transaction[] { coinbaseTx! };
		while (!oldBlockNext.hashDifficultyMatch())
			oldBlockNext.nonce++;
		
		oldChain.addValidBlock(oldBlockNext); // much newer block should be accepted
		Assert.IsTrue(oldChain.getLength() == 2);
		
		oldBlockNext = BlockFactory.createNewBlock(oldChain, null);
		oldBlockNext.timestamp = 10001; // a much older timestamp than the last block
		
		//manual mine here, as the .mineBlock method will modify the timestamp we're trying to test
		coinbaseTx = TransactionFactory.createCoinbaseTransaction(Settings.nodePublicKey,
			oldChain.getLength() + 1, null);
		Assert.IsNotNull(coinbaseTx);
		oldBlockNext.transactions = new Transaction[] { coinbaseTx! };
		while (!oldBlockNext.hashDifficultyMatch())
			oldBlockNext.nonce++;
		
		oldChain.addValidBlock(oldBlockNext); // much older block should be rejected
		Assert.IsFalse(oldChain.getLength() == 3);
		Assert.IsTrue(oldChain.isBlockchainValid()); // our oldChain should still be valid
		
		Assert.IsTrue(bchain.isBlockchainValid()); //original chain should be valid
	}
	
	[Test]
	public void TestInvalidBlocksRejected()
	{
		LogTestMsg("Testing TestInvalidBlocksRejected..");
		Assert.IsTrue(bchain.getLength() == 2);
	
		Block lastBlock = bchain.getLastBlock();
		// test invalid index (over by 1)
		Block nextBlock = new Block(
			bchain.getLength() + 2, null, Utilities.getTimestamp(),
			bchain.getLastBlock().calculateBlockHash(), bchain.currentDifficulty, 1);
		nextBlock.mineBlock();
		bchain.addValidBlock(nextBlock);
		Assert.IsFalse(bchain.getLength() == 3);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		Assert.IsTrue(bchain.getLastBlock() == lastBlock);
		
		// test invalid index (under by 1)
		nextBlock = new Block(
			bchain.getLength(), null, Utilities.getTimestamp(),
			bchain.getLastBlock().calculateBlockHash(), bchain.currentDifficulty, 1);
		nextBlock.mineBlock();
		bchain.addValidBlock(nextBlock);
		Assert.IsFalse(bchain.getLength() == 3);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		Assert.IsTrue(bchain.getLastBlock() == lastBlock);
		
		// test illegal timestamp value (note we must do a manual mine to ensure timestamp doesn't change)
		nextBlock = BlockFactory.createNewBlock(bchain, null);
		nextBlock.timestamp = -1;
		
		//manual mine here, as the .mineBlock method will modify the timestamp we're trying to test
		Transaction? coinbaseTx = TransactionFactory.createCoinbaseTransaction(Settings.nodePublicKey,
			bchain.getLength() + 1, null);
		Assert.IsNotNull(coinbaseTx);
		nextBlock.transactions = new Transaction[] { coinbaseTx! };
		while (!nextBlock.hashDifficultyMatch())
			nextBlock.nonce++;
		
		bchain.addValidBlock(nextBlock);
		Assert.IsFalse(bchain.getLength() == 3);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		Assert.IsTrue(bchain.getLastBlock() == lastBlock);
		
		// test block with legal timestamp but which is invalid within the context of our test blockchain (over)
		nextBlock = BlockFactory.createNewBlock(bchain, null);
		nextBlock.timestamp += Protocol.DIFFERING_TIME_ALLOWANCE * 2;
		
		//manual mine here, as the .mineBlock method will modify the timestamp we're trying to test
		coinbaseTx = TransactionFactory.createCoinbaseTransaction(Settings.nodePublicKey,
			bchain.getLength() + 1, null);
		Assert.IsNotNull(coinbaseTx);
		nextBlock.transactions = new Transaction[] { coinbaseTx! };
		while (!nextBlock.hashDifficultyMatch())
			nextBlock.nonce++;
		
		Assert.IsFalse(bchain.getLength() == 3);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		Assert.IsTrue(bchain.getLastBlock() == lastBlock);
		
		// test block with legal timestamp but which is invalid within the context of our test blockchain (under)
		nextBlock = BlockFactory.createNewBlock(bchain, null);
		nextBlock.timestamp -= Protocol.DIFFERING_TIME_ALLOWANCE * 2;
		
		//manual mine here, as the .mineBlock method will modify the timestamp we're trying to test
		coinbaseTx = TransactionFactory.createCoinbaseTransaction(Settings.nodePublicKey,
			bchain.getLength() + 1, null);
		Assert.IsNotNull(coinbaseTx);
		nextBlock.transactions = new Transaction[] { coinbaseTx! };
		while (!nextBlock.hashDifficultyMatch())
			nextBlock.nonce++;
		
		bchain.addValidBlock(nextBlock);
		Assert.IsFalse(bchain.getLength() == 3);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		Assert.IsTrue(bchain.getLastBlock() == lastBlock);
		
		//add a valid block before the next hash test, first assert it's successfully added
		nextBlock = BlockFactory.createAndMineNewBlock(bchain, null);
		bchain.addValidBlock(nextBlock);
		Assert.IsTrue(bchain.getLength() == 3);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		lastBlock = nextBlock;
		Assert.IsTrue(bchain.getLastBlock() == lastBlock);
		
		// test invalid hash (from wrong block)
		nextBlock = BlockFactory.createNewBlock(bchain, null);
		nextBlock.prevBlockHash = bchain.getBlockByIndex(1).calculateBlockHash();
		nextBlock.mineBlock();
		bchain.addValidBlock(nextBlock);
		Assert.IsTrue(bchain.getLength() == 3);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		Assert.IsTrue(bchain.getLastBlock() == lastBlock);
		
		// test mining requirements for the given difficulty are not met
		nextBlock = BlockFactory.createNewBlock(bchain, null);
		while (nextBlock.hashDifficultyMatch()) //note we mine here for *no* difficulty match (invalid)
			nextBlock.nonce++;
		bchain.addValidBlock(nextBlock);
		Assert.IsTrue(bchain.getLength() == 3);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		Assert.IsTrue(bchain.getLastBlock() == lastBlock);
		
		// test mining requirements for the given difficulty are not met, after they are initially met
		nextBlock = BlockFactory.createAndMineNewBlock(bchain, null);
		while (nextBlock.hashDifficultyMatch()) //note we mine here for *no* difficulty match (invalid)
			nextBlock.nonce++;
		bchain.addValidBlock(nextBlock);
		Assert.IsTrue(bchain.getLength() == 3);
		Assert.IsTrue(bchain.getLastBlock().index == bchain.getLength());
		Assert.IsTrue(bchain.getLastBlock() == lastBlock);

		// test all invalid blocks were successfully rejected
		Assert.IsTrue(bchain.isBlockchainValid());
		
		// force add an invalid block (wrong hash), verify chain is now invalid
		bchain.forceAddBlock(nextBlock);
		Assert.IsFalse(bchain.isBlockchainValid());
	}

	[Test]
	public void TestInvalidBlockMineRejected()
	{
		LogTestMsg("Testing TestInvalidBlocksRejected..");
		
		Assert.IsTrue(Protocol.BLOCK_REWARD == 20);
		Assert.IsTrue(Protocol.MAX_TRANSACTIONS_PER_BLOCK == 10);
		
		//create chain, mine some blocks, get some coins to use in our tests
		Blockchain bchain = new Blockchain();
		for (int i = 0; i < Protocol.MAX_TRANSACTIONS_PER_BLOCK + 3; i++)
		{
			BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		}

		Assert.IsTrue(bchain.mempool.Count == 0);
		//create 1 more tx than the protocol allows per block and add it to the mempool
		for (int i = 0; i < Protocol.MAX_TRANSACTIONS_PER_BLOCK + 1; i++)
		{
			Transaction? validTx = TransactionFactory.createNewTransactionForBlockchain(new TxOut[]
			{
				new TxOut(
					testPublicKey2, 10)
			}, testPrivateKey, bchain, 0, true);
			Assert.IsNotNull(validTx);
			bchain.addTransactionToMempoolGivenNodeRequirements(validTx);
		}
		Assert.IsTrue(bchain.mempool.Count > Protocol.MAX_TRANSACTIONS_PER_BLOCK);

		//create a block with more transactions than allowed, and attempt to mine it.
		//Mining itself should immediately fail
		Block invalidBlock = BlockFactory.createNewBlock(bchain, bchain.mempool.ToArray());
		Assert.IsFalse(invalidBlock.mineBlock());
		
		//nevertheless, we will manually mine it here (bypassing the checks in the mineBlock method)
		while (!invalidBlock.hashDifficultyMatch())
			invalidBlock.nonce++;
		Assert.IsTrue(invalidBlock.hashDifficultyMatch()); //block should have a hash matching blockchain difficulty
		//should also contain a valid coinbase transaction
		Assert.IsTrue(Transaction.isValidCoinbaseTransaction(invalidBlock.transactions[0], invalidBlock));
		//however the block is not valid - it contains more txes than allowed per protocol settings
		Assert.IsFalse(bchain.isNewBlockValid(invalidBlock));
		Block lastBlock = bchain.getLastBlock();
		//also attempt to add it, verify this failed
		bchain.addValidBlock(invalidBlock);
		Assert.IsTrue(bchain.getLastBlock() == lastBlock);
		
		//lets now adjust protocol settings so that the tx limit isn't breached. Block should now be valid
		Protocol.MAX_TRANSACTIONS_PER_BLOCK *= 2;
		Assert.IsTrue(bchain.isNewBlockValid(invalidBlock));
		bchain.addValidBlock(invalidBlock);
		Assert.False(bchain.getLastBlock() == lastBlock);
		
		//For our last test, we will set transactions for a block to "null" and assert this isn't a valid block
		Block invalidBlock2 = BlockFactory.createNewBlock(bchain);
		invalidBlock2.transactions = null;
		Assert.IsFalse(bchain.isNewBlockValid(invalidBlock2));
	}
	
	[Test]
	public void TestInvalidGenesisBlockRejected()
	{
		LogTestMsg("Testing TestInvalidGenesisBlockRejected..");
		Blockchain newChain = new Blockchain();
		Assert.IsTrue(newChain.getLength() == 0);
	
		// create a real genesis block
		Block genesisSpoof = BlockFactory.createNewBlock(newChain, null);
		Assert.IsTrue(Blockchain.isGenesisBlock(genesisSpoof));
		
		// now invalidate it. Below actions should fail - an invalid genesis block cannot be added
		genesisSpoof.nonce = 2;
		Assert.IsFalse(Blockchain.isGenesisBlock(genesisSpoof));
		newChain.addValidBlock(genesisSpoof);
		Assert.IsTrue(newChain.getLength() == 0);
	
		genesisSpoof.nonce = 1;
		genesisSpoof.prevBlockHash = "";
		newChain.addValidBlock(genesisSpoof);
		Assert.IsTrue(newChain.getLength() == 0);
	
		// force add the invalid genesis block, now blockchain validation should fail
		genesisSpoof.prevBlockHash = "";
		newChain.forceAddBlock(genesisSpoof);
		Assert.IsFalse(newChain.isBlockchainValid());
		
		// replace blockchain, restore genesisSpoof back to real genesis, ensure it successfully adds as genesis block 
		newChain.replaceBlockchain(new Blockchain());
		genesisSpoof.prevBlockHash = "0";
		newChain.addValidBlock(genesisSpoof);
		Assert.IsTrue(newChain.getLength() == 1);
		Assert.IsTrue(newChain.getLastBlock() == genesisSpoof);
	}
	
	[Test]
	public void TestInvalidBlockchainDetected()
	{
		LogTestMsg("Testing TestInvalidBlockchainDetected..");
		Blockchain newChain = new Blockchain();
		
		Assert.IsTrue(newChain.getLength() == 0);
		
		newChain.addValidBlock(Blockchain.createGenesisBlock());
		Assert.IsTrue(newChain.getLength() == 1);
		Assert.IsTrue(newChain.getLastBlock().index == newChain.getLength());
	
		Block nextBlock = BlockFactory.createAndMineNewBlock(newChain);
		newChain.addValidBlock(nextBlock);
		Assert.IsTrue(newChain.getLength() == 2);
		Assert.IsTrue(newChain.getLastBlock().index == newChain.getLength());
		
		nextBlock = BlockFactory.createAndMineNewBlock(newChain);
		newChain.addValidBlock(nextBlock);
		Assert.IsTrue(newChain.getLength() == 3);
		Assert.IsTrue(newChain.getLastBlock().index == newChain.getLength());
		
		// blockchain is valid up until this point, but now we will change the nonce in block 2, making it invalid
		newChain.getBlockByIndex(2).nonce++;
		
		Assert.IsFalse(newChain.isBlockchainValid());
	}
	
	[Test]
	public void TestBlockchainAccumulativeDifficulty()
	{
		LogTestMsg("Testing TestBlockchainAccumulativeDifficulty..");
		Blockchain newChain = new Blockchain();
	
		BigInteger accumDifficulty;
		
		// first assert the convertDifficultyToHashAttempts function is working correctly
		Assert.IsTrue(Utilities.convertDifficultyToHashAttempts(0) == 0);
		Assert.IsTrue(Utilities.convertDifficultyToHashAttempts(1) == BigInteger.Pow(2, 4 * 1));
		Assert.IsTrue(Utilities.convertDifficultyToHashAttempts(17) == BigInteger.Pow(2, 4 * 17));
		Assert.IsTrue(Utilities.convertDifficultyToHashAttempts(1000) == BigInteger.Pow(2, 4 * 1000));
		
		// We now test the calculateAccumulativeChainDifficulty function
		// genesis block alone has 0 accumulative difficulty
		BlockFactory.mineNextBlockAndAddToBlockchain(newChain);
		Assert.IsTrue(newChain.calculateAccumulativeChainDifficulty() == 0); 
	
		// adding 1 block should give the chain an accumulative difficulty equal to the initialized difficulty
		BlockFactory.mineNextBlockAndAddToBlockchain(newChain);
		accumDifficulty = Utilities.convertDifficultyToHashAttempts(Protocol.INITIALIZED_DIFFICULTY);
		Assert.IsTrue(newChain.calculateAccumulativeChainDifficulty() == accumDifficulty);
		Assert.IsTrue(newChain.getLength() == 2);
		LogTestMsg($"\tAccum. difficulty of {accumDifficulty} at {newChain.getLength()} blocks with " +
		           $"current chain difficulty {newChain.currentDifficulty} asserted");
	
		while (newChain.getLength() < Protocol.DIFFICULTY_INTERVAL_BLOCKS)
		{
			BlockFactory.mineNextBlockAndAddToBlockchain(newChain);
		}
		// accumulative difficulty should be equal to the hash difficulty of chain length minus 1 before the first
		// update difficulty event (due to ignoring genesis block)
		accumDifficulty = Utilities.convertDifficultyToHashAttempts(Protocol.INITIALIZED_DIFFICULTY)
		                  * (newChain.getLength() - 1);
		Assert.IsTrue(newChain.calculateAccumulativeChainDifficulty() == accumDifficulty);
		LogTestMsg($"\tAccum. difficulty of {accumDifficulty} at {newChain.getLength()} blocks with " +
		           $"current chain difficulty {newChain.currentDifficulty} asserted");
	
		while (newChain.getLength() < Protocol.DIFFICULTY_INTERVAL_BLOCKS * 2)
		{
			BlockFactory.mineNextBlockAndAddToBlockchain(newChain);
		}
		// fully test accumulative difficulty behaves as expected after difficulty adjustment
		int lastMinedDifficulty = 
			newChain.getBlockByIndex(newChain.getLength() - 1).difficulty; // difficulty prior to 2nd difficulty adjustment
		BigInteger firstIntervalAccumulativeDifficulty =
			Utilities.convertDifficultyToHashAttempts(Protocol.INITIALIZED_DIFFICULTY) 
			* (Protocol.DIFFICULTY_INTERVAL_BLOCKS - 1); // - 1 to subtract genesis block from this interval
		BigInteger secondIntervalAccumulativeDifficulty = 
			Utilities.convertDifficultyToHashAttempts(lastMinedDifficulty) * Protocol.DIFFICULTY_INTERVAL_BLOCKS;
		accumDifficulty = firstIntervalAccumulativeDifficulty;
		accumDifficulty += secondIntervalAccumulativeDifficulty;
		Assert.IsTrue(accumDifficulty == newChain.calculateAccumulativeChainDifficulty());
		LogTestMsg($"\tAccum. difficulty of {accumDifficulty} at {newChain.getLength()} blocks with " +
		           $"current chain difficulty {newChain.currentDifficulty} asserted");
	}
	
	[Test]
	public void TestWinningChainComparison()
	{
		LogTestMsg("Testing TestWinningChainComparison..");
	
		// both chains to have a light difficulty
		Protocol.DIFFICULTY_INTERVAL_BLOCKS = 5;
		Protocol.BLOCK_INTERVAL_SECONDS = 1;
		Protocol.INITIALIZED_DIFFICULTY = 1;
		Blockchain chain1 = new Blockchain();
		Blockchain chain2 = new Blockchain();
		
		// populate first blockchain with 14 blocks
		for (int i = 0; i < 14; i++)
		{
			BlockFactory.mineNextBlockAndAddToBlockchain(chain1);
		}
		
		// populate second chain with only 11 blocks
		for (int i = 0; i < 11; i++)
		{
			BlockFactory.mineNextBlockAndAddToBlockchain(chain2);
		}
	
		// assert chain1 has greater accumulative difficulty than 2nd chain, and is also the winning chain
		BigInteger chain1Difficulty = chain1.calculateAccumulativeChainDifficulty();
		BigInteger chain2Difficulty = chain2.calculateAccumulativeChainDifficulty();
		Assert.IsTrue(chain1Difficulty > chain2Difficulty);
		LogTestMsg($"\tchain1 accumulative difficulty: {chain1Difficulty}. chain2 accumulative difficulty:" +
		                $"{chain2Difficulty}");
		Assert.IsTrue(Blockchain.establishWinningChain(new List<Blockchain>() {chain1, chain2}) == chain1);
		
		// now introduce a new blockchain with higher difficulty settings
		Protocol.DIFFICULTY_INTERVAL_BLOCKS = 5;
		Protocol.BLOCK_INTERVAL_SECONDS = 2;
		Protocol.INITIALIZED_DIFFICULTY = 2;
		Blockchain chain3 = new Blockchain();
		
		// populate it with 15 blocks. This chain should have both greater length and accumulative hashpower than other chains
		for (int i = 0; i < 15; i++)
		{
			BlockFactory.mineNextBlockAndAddToBlockchain(chain3);
		}
		
		// put settings back to the way they were previously for chain1 & chain 2
		// chain3 should now have the greatest accumulative hashpower, but be invalid according to the protocol settings
		Protocol.DIFFICULTY_INTERVAL_BLOCKS = 5;
		Protocol.BLOCK_INTERVAL_SECONDS = 1;
		Protocol.INITIALIZED_DIFFICULTY = 1;
		
		// assert both length and hashpower of chain3 is greater than chain1 or chain2
		Assert.IsTrue(chain3.getLength() > chain1.getLength() && chain3.getLength() > chain2.getLength());
		LogTestMsg($"\tchain1 length: {chain1.getLength()}. " +
		                $"chain2 length: {chain2.getLength()}. chain3 length: {chain3.getLength()}");
		BigInteger chain3Difficulty = chain3.calculateAccumulativeChainDifficulty();
		Assert.IsTrue(chain3Difficulty > chain1Difficulty && chain3Difficulty > chain2Difficulty);
		LogTestMsg($"\tchain3 accumulative difficulty: {chain3Difficulty}");
		
		// but chain3 is not the winning chain, since it doesn't satisfy the protocol settings which were changed back
		Assert.IsTrue(Blockchain.establishWinningChain(new List<Blockchain>() {chain1, chain2, chain3}) == chain1);
	
		// now mine chain2 to 15 blocks (4 more blocks). Compared to chain 1's 14 blocks, it should now be the winning chain
		for (int i = 0; i < 4; i++)
		{
			BlockFactory.mineNextBlockAndAddToBlockchain(chain2);
		}
		// for our last assertion we also change the ordering of the list to ensure this doesn't matter in this context
		Assert.IsTrue(Blockchain.establishWinningChain(new List<Blockchain>() {chain3, chain2, chain1}) == chain2);
		
		// mine one more block for chain 1, so that both chain 1 and chain 2 have equal difficulty
		BlockFactory.mineNextBlockAndAddToBlockchain(chain1);
		
		// assert chain1 and chain2 have equivalent accumulative difficulty
		chain1Difficulty = chain1.calculateAccumulativeChainDifficulty();
		chain2Difficulty = chain2.calculateAccumulativeChainDifficulty();
		Assert.IsTrue(chain1Difficulty == chain2Difficulty);
	
		// when comparing these chains with equivalent hashpower, the winning chain is chosen based upon the ordering input
		Assert.IsTrue(Blockchain.establishWinningChain(new List<Blockchain>() {chain2, chain1}) == chain2);
		Assert.IsTrue(Blockchain.establishWinningChain(new List<Blockchain>() {chain1, chain2}) == chain1);
		LogTestMsg($"\tchain1 accumulative difficulty: {chain1Difficulty}. chain2 accumulative difficulty:" +
		                $"{chain2Difficulty}");
	}

	[Test]
	public void TestTamperedBlockchainRejected()
	{
		//TODO continually update this as new ways to tamper the blockchain become available (eg: new features added)
		
		//Test 1 - tampered historical transaction
		//Setup: mine some blocks, do some transactions
		bchain = new Blockchain();
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //genesis
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //normal
		tx = TransactionFactory.createTransaction(new TxOut[]
		{
			new TxOut(testPublicKey2, 2),
			new TxOut(testPrivateKey3, 3)
		}, testPrivateKey, bchain.uTxOuts, bchain.mempool, 0, false)!;
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[]
			{
				new TxOut(testPublicKey2, 30), 
				new TxOut(testPublicKey3, 30),
			}, testPrivateKey, 
			bchain, 5);
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[]
			{
				new TxOut(testPublicKey2, 5), 
				new TxOut(testPublicKey, 2),
			}, testPrivateKey3, 
			bchain, 1);
		TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[]
			{
				new TxOut(testPublicKey3, 12), 
				new TxOut(testPublicKey, 10),
			}, testPrivateKey2, 
			bchain, 7);
		TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[]
			{
				new TxOut(testPublicKey2, 17), 
				new TxOut(testPublicKey3, 4),
			}, testPrivateKey, 
			bchain, 0);
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		//blockchain should be valid
		Assert.IsTrue(bchain.isBlockchainValid());
		//Test: We will now tamper with a tx in the 6th block
		bchain.getBlockByIndex(6).transactions[1].txOuts[1].amount--;
		//Assert chain is now invalid
		Assert.IsFalse(bchain.isBlockchainValid());
		//Put back to correct value, but now modify coinbase tx in the 3rd block
		bchain.getBlockByIndex(6).transactions[1].txOuts[1].amount++;
		Assert.IsTrue(bchain.isBlockchainValid());
		string origValue = bchain.getBlockByIndex(3).transactions[0].txIns[0].txOutId;
		bchain.getBlockByIndex(3).transactions[0].txIns[0].txOutId = "hi";
		Assert.IsFalse(bchain.isBlockchainValid());
		bchain.getBlockByIndex(3).transactions[0].txIns[0].txOutId = origValue;

		//Test 2 - Test tampered unspent utxout
		bchain.uTxOuts[0].amount++;
		Assert.IsFalse(bchain.isBlockchainValid());
		bchain.uTxOuts[0].amount--;
		
		//Test 3 - Test tampered difficulty
		bchain.currentDifficulty++;
		Assert.IsFalse(bchain.isBlockchainValid());
		bchain.currentDifficulty--;

		//after all tests, chain should be restored to a valid state. Assert it's valid
		Assert.IsTrue(bchain.isBlockchainValid());
	}

	[Test]
	public void TestCancelBlockMining()
	{
		//first ensure we can mine an easy block on a new thread to give confidence our test setup is correct
		Block easyBlock = new Block(1, null, 100, "0", 2, 1);
		Assert.IsFalse(easyBlock.hashDifficultyMatch());
		var easyBlockMineTask = Task.Run(() =>
		{
			easyBlock.mineBlock();
		});
		easyBlockMineTask.Wait(); //wait for block mine on the thread
		Assert.IsTrue(easyBlock.hashDifficultyMatch()); //block should be successfully mined
		
		//now we will create a block with a completely unreasonable difficulty, and attempt to mine it on a new thread
		Block veryDifficultBlock = new Block(1, null, 100, "0", 40, 1);
		Assert.IsFalse(veryDifficultBlock.hashDifficultyMatch());
		var unreasonableBlockMineTask = Task.Run(() =>
		{
			veryDifficultBlock.mineBlock();
		});
		while (veryDifficultBlock.nonce == 1)
			Utilities.sleep(10); //wait for at least some mining to occur from the other thread
		
		//now we test our ability to cancel the block mining in the other thread from this thread
		veryDifficultBlock.cancelMining = true;
		unreasonableBlockMineTask.Wait(); //wait for the block cancel to process on the other thread
		
		//Our main test assertion follows - We can successfully interrupt block mining before it's complete:
		Assert.IsFalse(veryDifficultBlock.hashDifficultyMatch());
	}

	

	/**
	 * Access point for testing arbitrary things in the project
	 */
	[Test]
	public void Temp()
	{
		
	}

}