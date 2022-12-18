using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using ArakCoin;
using ArakCoin.Networking;
using ArakCoin.Transactions;

namespace TestSuite.IntegrationTests;

[TestFixture]
[Category("IntegrationTests")]
public class TransactionIntegration
{
	[SetUp]
	public void Setup()
	{
		Settings.allowParallelCPUMining = true; //all tests should be tested with parallel mining enabled

		// put blockchain protocol settings to low values integration tests so they don't take too long
		Protocol.DIFFICULTY_INTERVAL_BLOCKS = 50;
		Protocol.BLOCK_INTERVAL_SECONDS = 1;
		Protocol.INITIALIZED_DIFFICULTY = 1;
		Protocol.MAX_TRANSACTIONS_PER_BLOCK = 10;
		
		//keep these protocol test parameters the same even if real protocol values change
		Protocol.BLOCK_REWARD = 20;
		Settings.minMinerFee  = 0;
		Settings.maxMempoolSize = Protocol.MAX_TRANSACTIONS_PER_BLOCK * 2;
		Settings.nodePublicKey  = testPublicKey;
		Settings.nodePrivateKey  = testPrivateKey;
	}

	[Test]
	public void TestBlockRewardAndMinerFees()
	{
		//Reminder: Every mined block will give a block reward to this node's public key. 
		//Don't forget this when validating balances
		Blockchain bchain = new Blockchain();
		Assert.IsTrue(Protocol.BLOCK_REWARD == 20);

		//create genesis (no block reward) - assert no reward
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		Assert.IsTrue(Wallet.getAddressBalance(testPublicKey, bchain.uTxOuts) == 0);

		//create second block (block reward) - assert correct reward
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		Assert.IsTrue(Wallet.getAddressBalance(testPublicKey, bchain.uTxOuts) ==
		              Protocol.BLOCK_REWARD);
		
		//add signed tx to mempool with no fee, mine - assert only block reward received
		//Setup - give 5 coins to 2nd address, signed by this node's private key
		TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[] { new TxOut(testPublicKey2, 5) }, testPrivateKey, bchain);
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		Assert.IsTrue(Wallet.getAddressBalance(testPublicKey, bchain.uTxOuts) == 35);
		Assert.IsTrue(Wallet.getAddressBalance(testPublicKey2, bchain.uTxOuts) == 5);
		//Test: testPublicKey2 to send 2 coins to testPublicKey3, paying no mining fee
		TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[] { new TxOut(testPublicKey3, 2) }, testPrivateKey2, bchain);
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		//Assert: No tx fees received for node (but mining reward should be), correct balances updated for key2 & key3
		Assert.IsTrue(Wallet.getAddressBalance(testPublicKey, bchain.uTxOuts) == 55);
		Assert.IsTrue(Wallet.getAddressBalance(testPublicKey2, bchain.uTxOuts) == 3);
		Assert.IsTrue(Wallet.getAddressBalance(testPublicKey3, bchain.uTxOuts) == 2);
		
		//testpublicKey2 will send 1 coin to testPublicKey3, but pay a mining fee of 2 coins
		TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[] { new TxOut(testPublicKey3, 1)}, testPrivateKey2, 
			bchain, 2);
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		//node should receive 20 coins block reward + 2 coins fee = 22 coins
		Assert.IsTrue(Wallet.getAddressBalance(testPublicKey, bchain.uTxOuts) == 77);
		//testPublickey2 has a balance of 0 (1 coin sent, 2 paid in mining fee)
		Assert.IsTrue(Wallet.getAddressBalance(testPublicKey2, bchain.uTxOuts) == 0);
		//testPublickey3 has a balance of 3 (2 existing, + 1 received from testPublickey2)
		Assert.IsTrue(Wallet.getAddressBalance(testPublicKey3, bchain.uTxOuts) == 3);
		
		//We will now test more complicated fee scenarios involving multiple transactions and txouts
		//in a single block.
		//Setup - node to give 30 coins each to testPublicKey2 and testPublicKey3. It will also pay
		//a fee of 5 coins but since it's the miner, it should receive this back, spending 60 coins total
		TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[]
			{
				new TxOut(testPublicKey2, 30), 
				new TxOut(testPublicKey3, 30),
			}, testPrivateKey, 
			bchain, 5);
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //remember node also gets the block reward
		Assert.IsTrue(Wallet.getAddressBalance(testPublicKey, bchain.uTxOuts) == 37);
		Assert.IsTrue(Wallet.getAddressBalance(testPublicKey2, bchain.uTxOuts) == 30);
		Assert.IsTrue(Wallet.getAddressBalance(testPublicKey3, bchain.uTxOuts) == 33);
		//Test - Multiple complex transactions in a single block.
		//	1) testPublicKey3 to send 5 coins to testPublicKey2, 2 coins to testPublicKey, and
		//	will also pay a mining fee of 1 coin
		//  2) testPublicKey2 to send 12 coins to testPublicKey3, 10 coins to testPublicKey, and
		//  will also pay a mining fee of 7 coins
		//  3) testPublicKey to send 17 coins to testPublicKey2, 4 coins to testPublicKey3, and
		// not pay any mining fee.
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
		//First assert the mempool contains these 3 transactions
		Assert.IsTrue(bchain.mempool.Count == 3);
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //now mine the block
		//Mempool should now be empty after successful block mine
		Assert.IsTrue(bchain.mempool.Count == 0);
		
		// Assert - We assert the final balances are all correct after these 3 transactions were mined
		
		//ADDRESS 1
		//Balance is 57 after block mine. Then +2 from testPublicKey3 and +1 mining reward
		//then +10 from testPublicKey2 and +7 mining reward
		//then -21 from coin sends = 56
		Assert.IsTrue(Wallet.getAddressBalance(testPublicKey, bchain.uTxOuts) == 56);
		
		//ADDRESS 2
		//Balance is 30 then -22 from transaction sends, and -7 mining fee = 1
		//then receives 22 coins from other transactions = 23
		Assert.IsTrue(Wallet.getAddressBalance(testPublicKey2, bchain.uTxOuts) == 23);
		
		//ADDRESS 3
		//Balance is 33 then -7 from transaction sends, and -1 mining fee = 25
		//then +16 from the two transaction receives = 41
		Assert.IsTrue(Wallet.getAddressBalance(testPublicKey3, bchain.uTxOuts) == 41);

		//We lastly must assert the total coin supply is equal to (blocks_mined - 1) * miner_reward
		//(remember genesis block gives no reward)
		long expectedSupply = (bchain.getLength() - 1) * Protocol.BLOCK_REWARD;
		long actualSupply = Wallet.getCurrentCirculatingCoinSupply(bchain);
		Assert.IsTrue(expectedSupply == actualSupply);
		
		LogTestMsg($"\tCoins generated: {expectedSupply} (from {bchain.getLength()} blocks). " +
		           $"Circulating coins remaining after fee & reward testing:" +
		           $" {actualSupply} (both correctly equal)");
	}

	[Test]
	public void TestValidAndInvalidTransactions()
	{
		Blockchain bchain = new Blockchain();
		Assert.IsTrue(Protocol.BLOCK_REWARD == 20);
		
		long add1Balance = Wallet.getAddressBalance(testPublicKey, bchain.uTxOuts);
		long add2Balance = Wallet.getAddressBalance(testPublicKey2, bchain.uTxOuts);
		long add3Balance = Wallet.getAddressBalance(testPublicKey3, bchain.uTxOuts);
		
		//mine 3 blocks to get 60 coins reward for this node
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //genesis
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //normal block mine
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //normal block mine
		bool success = BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //normal block mine
		Assert.IsTrue(success);
		add1Balance = Wallet.getAddressBalance(testPublicKey, bchain.uTxOuts);
		add2Balance = Wallet.getAddressBalance(testPublicKey2, bchain.uTxOuts);
		add3Balance = Wallet.getAddressBalance(testPublicKey3, bchain.uTxOuts);
		Assert.IsTrue(add1Balance == 60);
		Assert.IsTrue(add2Balance == 0);
		Assert.IsTrue(add3Balance == 0);

		//mine another block, distribute 30 coins each to testPublicKey2, testPublickey3 in a tx
		//from this node
		Transaction? txCreate = TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[]
			{
				new TxOut(testPublicKey2, 30), 
				new TxOut(testPublicKey3, 30)
			}, 
			testPrivateKey, bchain, 0);
		Assert.IsNotNull(txCreate);
		success = BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //normal block mine
		Assert.IsTrue(success);
		add1Balance = Wallet.getAddressBalance(testPublicKey, bchain.uTxOuts);
		add2Balance = Wallet.getAddressBalance(testPublicKey2, bchain.uTxOuts);
		add3Balance = Wallet.getAddressBalance(testPublicKey3, bchain.uTxOuts);
		Assert.IsTrue(add1Balance == 20);
		Assert.IsTrue(add2Balance == 30);
		Assert.IsTrue(add3Balance == 30);
		
		//Test a valid tx from testPublicKey2 to testPublickey3, sending 5 coins, paying 1 mining fee
		TxOut tx = new TxOut(testPublicKey3, 5);
		txCreate = TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[] {tx}, 
			testPrivateKey2, bchain, 1);
		Assert.IsNotNull(txCreate);
		success = BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		add1Balance = Wallet.getAddressBalance(testPublicKey, bchain.uTxOuts);
		add2Balance = Wallet.getAddressBalance(testPublicKey2, bchain.uTxOuts);
		add3Balance = Wallet.getAddressBalance(testPublicKey3, bchain.uTxOuts);
		Assert.IsTrue(add1Balance == 41);
		Assert.IsTrue(add2Balance == 24);
		Assert.IsTrue(add3Balance == 35);
		
		//Now we test 2 invalid transactions which should both fail
		//Test 1: testPublicKey2 to send 25 coins to testPublicKey3 - insufficient balance (24)
		tx = new TxOut(testPublicKey3, 25);
		int memPoolSize = bchain.mempool.Count;
		txCreate = TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[] {tx}, 
			testPrivateKey2, bchain, 0); //invalid transaction (txOut is above available balance)
		Assert.IsNull(txCreate);
		Assert.IsTrue(bchain.mempool.Count == memPoolSize); //mempool should not have been updated
		//We mine the block: balances & coin supply should remain unchanged, except for block reward.
		success = BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		Assert.IsTrue(success);
		add1Balance = Wallet.getAddressBalance(testPublicKey, bchain.uTxOuts);
		add2Balance = Wallet.getAddressBalance(testPublicKey2, bchain.uTxOuts);
		add3Balance = Wallet.getAddressBalance(testPublicKey3, bchain.uTxOuts);
		Assert.IsTrue(add1Balance == 61); //received block reward, but no normal txes took place
		Assert.IsTrue(add2Balance == 24);
		Assert.IsTrue(add3Balance == 35);
		
		//Test 2: testPublickey2 to send 20 coins to testPublicKey3 (allowed),
		//but also attempts to send a mining reward of 5 coins which exceeds the available balance (of 24).
		//This should thus also fail
		tx = new TxOut(testPublicKey3, 20);
		memPoolSize = bchain.mempool.Count;
		txCreate = TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[] {tx}, 
			testPrivateKey2, bchain, 5); //invalid transaction (txOut + miner fee is above available balance)
		Assert.IsNull(txCreate);
		Assert.IsTrue(bchain.mempool.Count == memPoolSize); //mempool should not have been updated
		//We mine the block: balances & coin supply should remain unchanged, except for block reward.
		success = BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		Assert.IsTrue(success);
		add1Balance = Wallet.getAddressBalance(testPublicKey, bchain.uTxOuts);
		add2Balance = Wallet.getAddressBalance(testPublicKey2, bchain.uTxOuts);
		add3Balance = Wallet.getAddressBalance(testPublicKey3, bchain.uTxOuts);
		Assert.IsTrue(add1Balance == 81); //block reward added for mine
		Assert.IsTrue(add2Balance == 24);
		Assert.IsTrue(add3Balance == 35);
		
		//Test 3: Run Test 2 logic again, except this time testPublicKey2 receives 15 coins from this node
		//as another transaction in the same mempool (giving it a sufficient balance).
		//The transaction made by testPublicKey2 should still fail however, as unspent txouts are
		//only counted within the consensus blockchain, not a local mempool
		//This node's transaction however should still go through, and then in the *next* block,
		//the attempted transaction by testPublicKey2 should now work (since the TxOut from the
		//successful transaction is now part of the main chain, not just its mempool)
		tx = new TxOut(testPublicKey3, 20);
		memPoolSize = bchain.mempool.Count;
		txCreate = TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[] {new TxOut(testPublicKey2, 15)}, 
			testPrivateKey, bchain, 0); //node's valid tx
		Assert.IsTrue(success);
		txCreate = TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[] {tx}, 
			testPrivateKey2, bchain, 5); //invalid transaction (txOut is above available balance)
		Assert.IsNull(txCreate);
		Assert.IsTrue(bchain.mempool.Count == memPoolSize + 1); //mempool should contain only 1 valid tx
		//We mine the block
		success = BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		Assert.IsTrue(success);
		add1Balance = Wallet.getAddressBalance(testPublicKey, bchain.uTxOuts);
		add2Balance = Wallet.getAddressBalance(testPublicKey2, bchain.uTxOuts);
		add3Balance = Wallet.getAddressBalance(testPublicKey3, bchain.uTxOuts);
		Assert.IsTrue(add1Balance == 86); //81 + 20 (block reward) - 15 (send) = 86
		Assert.IsTrue(add2Balance == 39); //24 + receive 15 coins from node = 39
		Assert.IsTrue(add3Balance == 35); //unchanged, tx by testPublicKey2 didn't go through
		
		//now we attempt Test 2 again which should pass (testPublicKey2 now has 39 coins)
		tx = new TxOut(testPublicKey3, 20);
		memPoolSize = bchain.mempool.Count;
		txCreate = TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[] {tx}, 
			testPrivateKey2, bchain, 5); //now valid transaction, fee + amount = 25 < 39
		Assert.IsNotNull(txCreate);
		Assert.IsFalse(bchain.mempool.Count == memPoolSize); //mempool should now be updated
		//We mine the block
		success = BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		Assert.IsTrue(success);
		add1Balance = Wallet.getAddressBalance(testPublicKey, bchain.uTxOuts);
		add2Balance = Wallet.getAddressBalance(testPublicKey2, bchain.uTxOuts);
		add3Balance = Wallet.getAddressBalance(testPublicKey3, bchain.uTxOuts);
		Assert.IsTrue(add1Balance == 111); //86 + 20 from block reward + 5 mining fee
		Assert.IsTrue(add2Balance == 14); //39 - 25 from tx (20 sent, 5 mining fee paid)
		Assert.IsTrue(add3Balance == 55); //finally receives the 20 coins from add2
		
		//attempt to create a transaction with no coins and add it to the blockchain, this should fail
		var output = TransactionFactory.createNewTransactionForBlockchain(new TxOut[] {
			new TxOut(testPublicKey2, 0)}, testPrivateKey, bchain);
		Assert.IsNull(output);

		//we have done a lot of txes: assert total balances of addresses used is equal to the
		//accumulative value of the UTxOuts for the blockchain, and also that this is equal
		//to the total block mining rewards (the only source of coin creation)
		long usedAddressBalance = add1Balance + add2Balance + add3Balance;
		Assert.IsTrue(usedAddressBalance == Wallet.getCurrentCirculatingCoinSupply(bchain));
		Assert.IsTrue(usedAddressBalance == (bchain.getLength() - 1) * Protocol.BLOCK_REWARD);
		
	}
	
	[Test]
	public void TestTransactionTampering()
	{
		Blockchain bchain = new Blockchain();
		Assert.IsTrue(Protocol.BLOCK_REWARD == 20);
		
		long add1Balance = Wallet.getAddressBalance(testPublicKey, bchain.uTxOuts);
		long add2Balance = Wallet.getAddressBalance(testPublicKey2, bchain.uTxOuts);
		long add3Balance = Wallet.getAddressBalance(testPublicKey3, bchain.uTxOuts);
		
		//mine 4 blocks to get 80 coins reward for this node, distribute 30 coins to 2 other addresses
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //genesis
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //normal block mine
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //normal block mine
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //normal block mine
		TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[]
			{
				new TxOut(testPublicKey2, 30), 
				new TxOut(testPublicKey3, 30)
			}, 
			testPrivateKey, bchain, 0);
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //normal block mine
		add1Balance = Wallet.getAddressBalance(testPublicKey, bchain.uTxOuts);
		add2Balance = Wallet.getAddressBalance(testPublicKey2, bchain.uTxOuts);
		add3Balance = Wallet.getAddressBalance(testPublicKey3, bchain.uTxOuts);
		Assert.IsTrue(add1Balance == 20);
		Assert.IsTrue(add2Balance == 30);
		Assert.IsTrue(add3Balance == 30);
		
		//now we can begin our tests of transaction tampering
		int bchainLength = bchain.getLength();

		//Test 1 fail
		Assert.IsTrue(bchain.mempool.Count == 0);
		//create a valid tx in the mempool. testPublicKey2 sends 10 coins to testPublicKey3, paying 1 mining fee
		TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[]
			{
				new TxOut(testPublicKey3, 10), 
			}, 
			testPrivateKey2, bchain, 1);
		Assert.IsTrue(bchain.mempool.Count == 1); //valid tx successfully added to mempool
		Transaction tx = bchain.mempool[0]; //retrieve a reference to the created tx
		//retrieve TxOut corresponding to the sending of the 10 coins
		TxOut? relatedTxOut = null;
		foreach (var txOut in tx.txOuts)
		{
			if (txOut.address == testPublicKey3 && txOut.amount == 10)
			{
				relatedTxOut = txOut;
				break;
			}
		}
		Assert.IsNotNull(relatedTxOut);
		//now tamper this txout to instead send 11 coins
		relatedTxOut.amount = 11;
		//now attempt to mine the block. This should fail due to a tampered tx being inside the mempool
		bool success = BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		Assert.IsFalse(success);
		
		//test 2 fail
		//set the tampered tx back to its correct amount, but change the destination address
		relatedTxOut.amount = 10;
		relatedTxOut.address = testPublicKey;
		success = BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		Assert.IsFalse(success);
		
		//test 3 fail
		//set the tampered tx back to correct destination address, but adjust miner fee
		relatedTxOut.address = testPublicKey3;
		TxOut minerFeeTxOut = null;
		foreach (var txOut in tx.txOuts) //first retrieve the miner fee TxOut
		{
			if (txOut.address == Protocol.FEE_ADDRESS && txOut.amount == 1)
			{
				minerFeeTxOut = txOut;
				break;
			}
		}
		Assert.IsNotNull(minerFeeTxOut);
		minerFeeTxOut.amount = 2;
		//now attempt to mine, which should fail
		success = BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		Assert.IsFalse(success);

		//test 4 fail
		//set fee amount back to correct amount, but make miner fee address different
		minerFeeTxOut.amount = 1;
		minerFeeTxOut.address = testPublicKey;
		//now attempt to mine, which should fail
		success = BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		Assert.IsFalse(success);
		
		//no mining should have taken place in any of the above tests. All balances should be the same
		Assert.IsTrue(bchain.getLength() == bchainLength);
		add1Balance = Wallet.getAddressBalance(testPublicKey, bchain.uTxOuts);
		add2Balance = Wallet.getAddressBalance(testPublicKey2, bchain.uTxOuts);
		add3Balance = Wallet.getAddressBalance(testPublicKey3, bchain.uTxOuts);
		Assert.IsTrue(add1Balance == 20);
		Assert.IsTrue(add2Balance == 30);
		Assert.IsTrue(add3Balance == 30);

		//additionally verify Tx id is incorrect
		Assert.IsTrue(tx.id != Transaction.getTxId(tx));
		
		//set the minerFeeTxOut back to correct miner burn address
		minerFeeTxOut.address = Protocol.FEE_ADDRESS;
		//now the transaction should be valid again
		Assert.IsTrue(tx.id == Transaction.getTxId(tx));
		
		//Next we will tamper with coinbase transactions
		//In the 1st block, change the miner address
		bchain.getBlockByIndex(2).transactions[0].txOuts[0].address = testPublicKey2;
		//Blockchain should now be invalid
		Assert.IsFalse(Blockchain.isBlockchainValid(bchain));
		//Now set back to correct address, but change the reward amount
		bchain.getBlockByIndex(2).transactions[0].txOuts[0].address = testPublicKey;
		bchain.getBlockByIndex(2).transactions[0].txOuts[0].amount--;
		//Blockchain should now be invalid
		Assert.IsFalse(Blockchain.isBlockchainValid(bchain));
		//Change back. Now tamper reward amount in another coinbase tx in another block
		bchain.getBlockByIndex(2).transactions[0].txOuts[0].amount++;
		bchain.getBlockByIndex(5).transactions[0].txOuts[0].amount -= 5; 
		//Blockchain should now be invalid
		Assert.IsFalse(Blockchain.isBlockchainValid(bchain));
		//change back, blockchain should now be valid
		bchain.getBlockByIndex(5).transactions[0].txOuts[0].amount += 5; 
		Assert.IsTrue(Blockchain.isBlockchainValid(bchain));

		//Attempt to mine next block, which should succeed - balances to be as expected
		success = BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		Assert.IsTrue(success);
		Assert.IsTrue(bchain.getLength() == bchainLength + 1);
		add1Balance = Wallet.getAddressBalance(testPublicKey, bchain.uTxOuts);
		add2Balance = Wallet.getAddressBalance(testPublicKey2, bchain.uTxOuts);
		add3Balance = Wallet.getAddressBalance(testPublicKey3, bchain.uTxOuts);
		Assert.IsTrue(add1Balance == 41); //+20 miner reward, +1 fee
		Assert.IsTrue(add2Balance == 19); //-10 tx send, -1 fee
		Assert.IsTrue(add3Balance == 40); //+10 receive
		
	}

	[Test]
	public void TestTransactionsPerBlockLimit()
	{
		Blockchain bchain = new Blockchain();
		Assert.IsTrue(Protocol.BLOCK_REWARD == 20);
		Protocol.BLOCK_INTERVAL_SECONDS = 1; //we'll mine a few blocks so set the block interval low
		Protocol.DIFFICULTY_INTERVAL_BLOCKS = Protocol.MAX_TRANSACTIONS_PER_BLOCK; //and prevent difficulty increases
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //genesis
		//mine some blocks to get some coins
		for (int i = 0; i < Protocol.MAX_TRANSACTIONS_PER_BLOCK; i++)
		{
			BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		}
		
		//temporarily increase the mempool allowance, outside the protocol settings
		Protocol.MAX_TRANSACTIONS_PER_BLOCK++;
		
		//create a block with 1 more transaction than the tx limit (we create 1 less normal transaction, for coinbase)
		List<Transaction> txes = new List<Transaction>(Protocol.MAX_TRANSACTIONS_PER_BLOCK);
		for (int i = 0; i < Protocol.MAX_TRANSACTIONS_PER_BLOCK - 1; i++)
		{
			txes.Add(TransactionFactory.createTransaction(new TxOut[] { new TxOut(testPublicKey2, 1) },
				testPrivateKey, bchain.uTxOuts, bchain.mempool));
		}
		//create new block with these transactions
		Block txOverBlock = BlockFactory.createAndMineNewBlock(bchain, txes.ToArray());
		Assert.IsTrue(txOverBlock.transactions.Length == Protocol.MAX_TRANSACTIONS_PER_BLOCK); //over
		//assert the block can be legally appended given our temporary protocol (tx 1 over real protocol size)
		Assert.IsTrue(bchain.isNewBlockValid(txOverBlock));
		//now change the protocol back to the correct one
		Protocol.MAX_TRANSACTIONS_PER_BLOCK--;
		//now assert the block is invalid
		Assert.IsFalse(bchain.isNewBlockValid(txOverBlock));

		//adding this block should fail since it has a block tx limit thats over by 1
		bool success = bchain.addValidBlock(txOverBlock);
		Assert.IsFalse(success);
		
		//we will force add the block, then validate the blockchain, which should fail
		//first assert blockchain is valid
		Assert.IsTrue(bchain.isBlockchainValid());
		bchain.forceAddBlock(txOverBlock);
		//now assert it's invalid after we've forcefully added the over block
		Assert.IsFalse(bchain.isBlockchainValid());
	}

	[Test]
	public void TestAddMemPool()
	{
		Assert.IsTrue(Protocol.BLOCK_REWARD == 20);
		Blockchain bchain = new Blockchain();
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //genesis
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //mine a block for a reward
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //mine a block for a reward
		
		//change our local miner settings to require a fee of 1 (doesn't affect blockchain protocol)
		Settings.minMinerFee  = 1;
		
		//create a valid tx satisfying our fee, but don't yet add to mempool
		Transaction txValid = TransactionFactory.createNewTransactionForBlockchain(new TxOut[] { new TxOut(testPublicKey2, 1)},
			testPrivateKey, bchain, 2, false);
		
		//create an two invalid txs not satisfying our fee, but don't yet attempt to add to mempool
		Transaction txinvalid = TransactionFactory.createNewTransactionForBlockchain(new TxOut[] { new TxOut(testPublicKey2, 1)},
			testPrivateKey, bchain, 0, false); //no miner fee
		
		Assert.IsTrue(bchain.mempool.Count == 0);
		
		//add a valid tx to mempool
		bool success = bchain.addTransactionToMempoolGivenNodeRequirements(txValid);
		Assert.IsTrue(success);
		Assert.IsTrue(bchain.mempool.Count == 1);
		
		//attempt to add invalid tx to mempool. This should fail (miner fee is too low)
		success = bchain.addTransactionToMempoolGivenNodeRequirements(txinvalid);
		Assert.IsFalse(success);
		Assert.IsTrue(bchain.mempool.Count == 1);
		
		//change our settings to now require a mining fee of 2. Create a tx with mining fee of 1, add should fail
		Settings.minMinerFee  = 2;
		Transaction txinvalid2 = TransactionFactory.createNewTransactionForBlockchain(new TxOut[] { new TxOut(testPublicKey2, 1)},
			testPrivateKey, bchain, 1, false); //1 miner fee (too low)
		success = bchain.addTransactionToMempoolGivenNodeRequirements(txinvalid2);
		Assert.IsFalse(success);
		Assert.IsTrue(bchain.mempool.Count == 1);
	}

	[Test]
	public void TestAddOverMempoolFails()
	{
		//for this test we set the node's max mempool size to 5, and assert that the 6th added transaction isn't added
		Settings.maxMempoolSize = 5;
		Assert.IsTrue(Protocol.BLOCK_REWARD == 20);
		Blockchain bchain = new Blockchain();
		//mine some blocks so we can create valid transactions from the rewards
		for (int i = 0; i < 10; i++)
		{
			BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		}

		//now add 5 txes to the mempool, which should all succeed
		Transaction txValid;
		for (int i = 0; i < 5; i++)
		{
			txValid = TransactionFactory.createNewTransactionForBlockchain(
				new TxOut[] { new TxOut(testPublicKey2, 1)},
				testPrivateKey, bchain, 2, false);
			Assert.IsTrue(bchain.addTransactionToMempoolGivenNodeRequirements(txValid));
		}
		//the 6th one is higher than our mempool add requirements, so should fail
		txValid = TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[] { new TxOut(testPublicKey2, 1)},
			testPrivateKey, bchain, 2, false);
		Assert.False(bchain.addTransactionToMempoolGivenNodeRequirements(txValid));
		Assert.IsTrue(bchain.mempool.Count == 5);
		
		//change our settings to now allow 6 txes. The add should now work
		Settings.maxMempoolSize = 6;
		Assert.True(bchain.addTransactionToMempoolGivenNodeRequirements(txValid));
		Assert.IsTrue(bchain.mempool.Count == 6);
	}

	[Test]
	public void TestValidateMempool()
	{
		//we will create an invalid mempool in different ways, which should fail validation
		Settings.minMinerFee  = 1; //as a mining node, we will require a miner fee of 1 for each transaction
		
		//create chain, mine some blocks, get some coins to use in our tests
		Blockchain bchain = new Blockchain();
		for (int i = 0; i < 10; i++)
		{
			BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		}

		Assert.IsTrue(bchain.mempool.Count == 0);
		
		//create a known valid transaction we can use for every test
		Transaction validTx = TransactionFactory.createNewTransactionForBlockchain(new TxOut[]
		{
			new TxOut(
				testPublicKey2, 10)
		}, testPrivateKey, bchain, 1, false);

		//Test 1 - Mempool containing a tampered transaction. Should fail
		Transaction invalidTx = TransactionFactory.createNewTransactionForBlockchain(new TxOut[]
		{
			new TxOut(
				testPublicKey2, 5)
		}, testPrivateKey, bchain, 1, false);
		invalidTx.txIns[0].txOutId="dummyValue"; //now tamper the transaction
		bchain.addTransactionToMempoolGivenNodeRequirements(validTx); //valid tx should be added
		bool success = bchain.addTransactionToMempoolGivenNodeRequirements(invalidTx); //this should fail
		Assert.IsFalse(success);
		Assert.IsTrue(bchain.mempool.Count == 1);
		//but now we forcefully add the invalid tx
		bchain.mempool.Add(invalidTx);
		Assert.IsTrue(bchain.mempool.Count == 2);
		//mempool validation should fail
		Assert.IsFalse(bchain.validateMemPool());
		//but of course the blockchain itself is valid (the local mempool shouldn't affect the consensus blockchain state)
		Assert.IsTrue(bchain.isBlockchainValid());

		//Test 2 - Mempool containing a coinbase transaction. Should fail
		bchain.clearMempool();
		Assert.IsTrue(bchain.mempool.Count == 0);
		Assert.IsTrue(bchain.validateMemPool());
		//create a new valid block, but put its coinbase transaction into the mempool
		Block nextValidBlock = BlockFactory.createAndMineNewBlock(bchain, new Transaction[] {validTx});
		Assert.IsTrue(bchain.isNewBlockValid(nextValidBlock)); //the block is valid to be appended to the chain
		bchain.mempool = nextValidBlock.transactions.ToList(); //however we add its coinbase tx to the mempool
		Assert.IsFalse(bchain.validateMemPool());

		//Test 3 - Mempool containing a tx which is below our required miner fee. Should fail
		bchain.clearMempool();
		Assert.IsTrue(bchain.mempool.Count == 0);
		Assert.IsTrue(bchain.validateMemPool());
		Transaction lowFeeTx = TransactionFactory.createNewTransactionForBlockchain(new TxOut[]
		{
			new TxOut(
				testPublicKey2, 10)
		}, testPrivateKey, bchain, 0, false);
		Assert.IsFalse(bchain.addTransactionToMempoolGivenNodeRequirements(lowFeeTx));
		Assert.IsTrue(bchain.mempool.Count == 0);
		Assert.IsTrue(bchain.validateMemPool());
	}
	
	[Test]
	public void TestGetTxesFromMempoolForBlockMine()
	{
		//This test should test the correct retrieval of txes from the mempool for a block mine based upon protocol
		//settings, and that the underlying mempool isn't mutated unless it's delibaretely updated
		
		Protocol.MAX_TRANSACTIONS_PER_BLOCK = 5; //set protocol to low tx limit for testing
		Settings.maxMempoolSize = 20; //ensure mempool can hold our txes
		
		//first create a blockchain and mine some coins
		Blockchain bchain = new Blockchain();
		for (int i = 0; i < 16; i++)
		{
			BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		}
		//add 15 valid txes to the mempool
		Transaction txValid;
		for (int i = 0; i < 15; i++)
		{
			txValid = TransactionFactory.createNewTransactionForBlockchain(
				new TxOut[] { new TxOut(testPublicKey2, 1)},
				testPrivateKey, bchain, 2, false);
			Assert.IsTrue(bchain.addTransactionToMempoolGivenNodeRequirements(txValid));
		}
		Assert.IsTrue(bchain.mempool.Count == 15);
		var originalPool = bchain.mempool.ToList(); //make a value copy of the mempool
		
		//now retrieve a chunk of txes from the mempool for the block mine
		var txArrayForBlockMine = bchain.getTxesFromMempoolForBlockMine();
		//the array should have 4 members - 1 less to allow for the coinbase tx (remember max tx per block is 5 here)
		Assert.IsTrue(txArrayForBlockMine.Length == 4);
		Block nextBlock = BlockFactory.createAndMineNewBlock(bchain, txArrayForBlockMine);
		nextBlock.mineBlock(); //mine the chunk of txes retrieved from the mempool
		Assert.IsTrue(nextBlock.transactions.Length == 5); //block should have 5 txes, which includes the coinbase tx
		//the original mempool should not have been mutated, test for this:
		Assert.IsTrue(bchain.mempool.Count == 15);
		for (int i = 0; i < 15; i++)
			Assert.IsTrue(originalPool[i] == bchain.mempool[i]);
		//do the block mine, mempool should undergo automatic sanitization to remove the mined txes
		Assert.IsTrue(bchain.addValidBlock(nextBlock));
		Assert.IsTrue(bchain.mempool.Count == 11); //we should have 11 txes left in the mempool
		Assert.IsTrue(bchain.validateMemPool()); //the mempool should also be valid
		
		//do the same thing with the next block, but we'll test the higher level mineNextBlockAndAddToBlockchain
		//function which should do all this automatically for us
		bool success = BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		Assert.IsTrue(success);
		Assert.IsTrue(bchain.mempool.Count == 7);
		Assert.IsTrue(bchain.validateMemPool());
		
		//and the next block
		success = BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		Assert.IsTrue(success);
		Assert.IsTrue(bchain.mempool.Count == 3);
		Assert.IsTrue(bchain.validateMemPool());
		
		//assert the getTxesFromMempoolForBlockMine will retrieve the 3 remaining txes (less than default max):
		txArrayForBlockMine = bchain.getTxesFromMempoolForBlockMine();
		Assert.IsTrue(txArrayForBlockMine.Length == 3);
		//but we'll use the mineNextBlockAndAddToBlockchain instead for the next block mine:
		success = BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		Assert.IsTrue(success);
		Assert.IsTrue(bchain.mempool.Count == 0);
		Assert.IsTrue(bchain.validateMemPool());
		Assert.IsTrue(bchain.getLastBlock().transactions.Length == 4); //3 normal txes from mempool, 1 coinbase tx
		
		//what if the mempool is empty? everything should still work
		txArrayForBlockMine = bchain.getTxesFromMempoolForBlockMine();
		Assert.IsTrue(txArrayForBlockMine.Length == 0);
		success = BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		Assert.IsTrue(success);
		Assert.IsTrue(bchain.mempool.Count == 0);
		Assert.IsTrue(bchain.validateMemPool());
		Assert.IsTrue(bchain.isBlockchainValid());
		//the last block should only have the coinbase tx
		Assert.IsTrue(bchain.getLastBlock().transactions.Length == 1);
		int bchainLength = bchain.getLength(); //get the bchain length for the next test
		
		//everything is fine until now. Let's set the mempool back to the original mempool. These txes should now
		//be spent and the block mine should fail. Assert this:
		bchain.mempool = originalPool;
		Assert.IsFalse(bchain.validateMemPool());
		success = BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		Assert.IsFalse(success);
		//although the blockchain shouldn't have been mutated from the above failed test:
		Assert.IsTrue(bchain.getLength() == bchainLength);
		Assert.IsTrue(bchain.isBlockchainValid());
		
		//but let's manually create a block with some of the already spent txes:
		Block invalidBlock = BlockFactory.createNewBlock(bchain, originalPool.ToArray()[..2]);
		//block should be invalid, and adding it should fail:
		Assert.IsFalse(bchain.isNewBlockValid(invalidBlock));
		Assert.IsFalse(bchain.addValidBlock(invalidBlock));
	}
	
	[Test]
	public void TestMempoolOrdering()
	{
		Settings.maxMempoolSize = 3; //we will use a small mempool for this test
		
		//first create a blockchain and mine some coins
		Blockchain bchain = new Blockchain();
		for (int i = 0; i < 10; i++)
		{
			BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		}
		
		//we now have the block rewards to create some transactions paying different miner fees
		//(note the variable names correspond to the exact fee being paid by that transaction)
		
		//let's add transactions to the mempool paying 2, 4 & 3 coins as a miner fee in that order
		var txFee2 = TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[] { new TxOut(testPublicKey2, 1)},
			testPrivateKey, bchain, 2);
		var txFee4 = TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[] { new TxOut(testPublicKey2, 1)},
			testPrivateKey, bchain, 4);
		var txFee3 = TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[] { new TxOut(testPublicKey2, 1)},
			testPrivateKey, bchain, 3);
		
		//we should now have a mempool of size 3 which is full
		Assert.IsTrue(bchain.mempool.Count == 3);
		Assert.IsTrue(bchain.validateMemPool());

		//if we don't allow mempool overriding, adding another tx even with a higher fee to the mempool should fail
		var txFee5 = TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[] { new TxOut(testPublicKey2, 1)},
			testPrivateKey, bchain, 5, false); //we don't do automatic mempool add
		Assert.IsNotNull(txFee5); 
		Assert.IsTrue(Transaction.isValidTransaction(txFee5, bchain.uTxOuts)); 
		//tx is valid, but without mempool overriding it will fail mempool add:
		Assert.IsFalse(bchain.addTransactionToMempoolGivenNodeRequirements(txFee5, false)); //no override
		
		//we must assert that the mempool was ordered correctly after each prior add -> txes paying the highest
		//miner fee should appear first within the pool, and txes with lowest paying fees last
		//(node the order we added the txes to the mempool is different to the order we're asserting now)
		Assert.IsTrue(bchain.mempool[0] == txFee4); //tx paying 4 coins should be first in the pool
		Assert.IsTrue(bchain.mempool[1] == txFee3); //tx paying 3 coins should be second in the pool
		Assert.IsTrue(bchain.mempool[2] == txFee2); //tx paying 2 coins should be third/last in the pool
		
		//we will now try adding a tx that paid 1 fee with mempool overriding (default). Since all the txes in the
		//pool paid a higher fee and the pool is full, this operation should fail
		var txFee1 = TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[] { new TxOut(testPublicKey2, 1)},
			testPrivateKey, bchain, 1);
		Assert.IsNull(txFee1); //no tx created since it couldn't be added to the pool
		
		//we'll also try adding another tx that is paying a fee of 2 (like the 3rd/last tx in the pool), however 
		//this should be rejected also as the override should only kick in if the fee is higher than an existing tx
		var txFee2_another = TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[] { new TxOut(testPublicKey2, 1)},
			testPrivateKey, bchain, 2, false); //let's add it manually this time
		Assert.IsNotNull(txFee2_another); 
		Assert.IsTrue(Transaction.isValidTransaction(txFee2_another, bchain.uTxOuts)); //tx is valid
		Assert.IsFalse(bchain.addTransactionToMempoolGivenNodeRequirements(txFee2_another)); //but pool is full
		Assert.IsTrue(bchain.mempool[2] == txFee2); //no overriding happened
		Assert.IsTrue(bchain.mempool.Count == 3);
		Assert.IsTrue(bchain.validateMemPool());

		//now we will try again to add the tx that is paying the highest fee of 5 coins, but this time we will allow
		//mempool transaction overriding. It should delete the tx paying 2 coins, and should also be positioned
		//as the 1st tx in the pool, with the other txes shifting down the pool
		Assert.IsTrue(bchain.addTransactionToMempoolGivenNodeRequirements(txFee5));
		Assert.IsTrue(bchain.mempool[0] == txFee5);
		Assert.IsTrue(bchain.mempool[1] == txFee4);
		Assert.IsTrue(bchain.mempool[2] == txFee3);
		Assert.IsFalse(bchain.mempool.Contains(txFee2)); //txFee2 is now deleted
		Assert.IsTrue(bchain.mempool.Count == 3);
		Assert.IsTrue(bchain.validateMemPool());

		//let's change the *protocol* to have a block tx limit of 3, then mine the next block.
		//This should pull the 2 highest transactions from the mempool (room is left for coinbase), leaving only txFee3
		Protocol.MAX_TRANSACTIONS_PER_BLOCK = 3;
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
		Assert.IsTrue(bchain.mempool.Count == 1);
		Assert.IsTrue(bchain.validateMemPool());
		Assert.IsTrue(bchain.mempool[0] == txFee3);
		
		//let's re-create a transaction with a fee of 1. It of course should now be addeable to the mempool given
		//that its length is only 1 now
		txFee1 = TransactionFactory.createNewTransactionForBlockchain(
			new TxOut[] { new TxOut(testPublicKey2, 1)},
			testPrivateKey, bchain, 1);
		Assert.IsTrue(bchain.mempool.Count == 2);
		Assert.IsTrue(bchain.validateMemPool());
		//assert ordering
		Assert.IsTrue(bchain.mempool[0] == txFee3);
		Assert.IsTrue(bchain.mempool[1] == txFee1);
		//do a block mine
		Assert.IsTrue(BlockFactory.mineNextBlockAndAddToBlockchain(bchain));
		//mempool should now be empty
		Assert.IsTrue(bchain.mempool.Count == 0);
		Assert.IsTrue(bchain.validateMemPool());

		//lastly assert the fee for the 2nd transaction (after coinbase) in the current blockchain's most recent block
		//is indeed 3 coins, from txFee3. This is a sanity check that everything is working as expected
		Assert.IsTrue(bchain.getLastBlock().transactions[1].txOuts[0].amount == 3);
	}
	
	[Test]
	public void Temp()
	{

	}

	
}