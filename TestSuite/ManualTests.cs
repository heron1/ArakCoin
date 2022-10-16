using System.Diagnostics;
using ArakCoin;
using ArakCoin.Transactions;

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
		Settings.nodePrivateKey = testPrivateKey;
		Settings.nodePublicKey = testPublicKey;
	}
	
	[Test]
	public void TestBlockchainMining()
	{
		// Tests the blockchain mining process until manually terminated
		while (true)
		{
			BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
			LogTestMsg($"Tstamp diff: " +
			           $"{Blockchain.getTimestampDifferenceToPredecessorBlock(bchain.getLastBlock(), bchain)}, " +
			           $"Block: {bchain.getLength()}, Difficulty: {bchain.currentDifficulty}");
		}
	}

	// [Test]
	// public void TestBlockchainMiningWithSimulatedTransactions()
	// {
	// 	//note: this shouldn't just do valid txes, but invalid ones too. (eg: unavailable balances, etc). Periodic assertions
	// 	//should ensure total coin supply = current supply. Randomness should create many possible scenarios to observe,
	// 	//potentially revealing unexpected bugs
	// 	Random random = new Random();
	// 	List <TxRecord> txRecords = new List<TxRecord>();
	// 	double r, d;
	// 	int p;
	// 	
	// 	BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //genesis block
	//
	// 	while (true)
	// 	{
	// 		//change miner to random one
	// 		p = random.Next(0, 3);
	// 		switch (p)
	// 		{
	// 			case (0):
	// 				Settings.nodePublicKey = testPublicKey;
	// 				break;
	// 			case(1):
	// 				Settings.nodePublicKey = testPublicKey2;
	// 				break;
	// 			default:
	// 				Settings.nodePublicKey = testPublicKey3;
	// 				break;
	// 		}
	//
	//
	// 		while (random.NextDouble() < 0.2) //keep making txes 20% of the time
	// 		{
	// 			d = random.NextDouble();
	// 			string sender_priv = d < 0.16 ? testPrivateKey2 : (d < 0.32 ? testPrivateKey3 : testPrivateKey);
	// 			string sender_pub = Cryptography.getPublicKeyFromPrivateKey(sender_priv);
	// 			
	// 			List<TxOut> txouts = new List<TxOut>();
	// 			while ((r = random.NextDouble()) < 0.5) //keep adding tx outs to tx 50% of the time
	// 			{
	// 				string recipient = r < 0.16 ? testPublicKey2 : (r < 0.32 ? testPublicKey3 : testPublicKey);
	//
	// 				//txouts will mostly be invalid but transaction should be rejected anyway
	// 				txouts.Add(new TxOut(recipient, random.Next(0,
	// 					(int)Wallet.getAddressBalance(sender_pub, bchain.uTxOuts)/2)));
	// 			}
	//
	// 			//tx creation may or may not succeed, but this doesn't matter
	// 			//(we test both valid and invalid transaction creation)
	// 			Transaction? tx = TransactionFactory.createNewTransactionForBlockchain(txouts.ToArray(), 
	// 				sender_priv, bchain);
	// 			if (tx is not null)
	// 			{
	// 				foreach (var txOut in tx.txOuts)
	// 					txRecords.Add(new TxRecord(sender_pub, txOut.address, txOut.amount, tx.id));
	// 			}
	// 		}
	//
	// 		if (txRecords.Count > 0)
	// 		{
	// 			LogTestMsg($"\nBalances before block mine (with transactions): " +
	// 			           $"\n\t{testPublicKey}:{Wallet.getAddressBalance(testPublicKey, bchain.uTxOuts)}" +
	// 			           $"\n\t{testPublicKey2}:{Wallet.getAddressBalance(testPublicKey2, bchain.uTxOuts)}" +
	// 			           $"\n\t{testPublicKey3}:{Wallet.getAddressBalance(testPublicKey3, bchain.uTxOuts)}");
	// 		}
	//
	// 		foreach (var txRecord in txRecords)
	// 		{
	// 			LogTestMsg($"\n{txRecord.sender} sending {txRecord.amount} to {txRecord.receiver}");
	// 		}
	//
	// 		Block nextBlock = BlockFactory.createAndMineNewBlock(bchain); //mine the block
	// 		bchain.addValidBlock(nextBlock);
	// 		LogTestMsg($"\nBLOCK MINE: Block id: {nextBlock.index} mined with difficulty {nextBlock.difficulty}" +
	// 		           $"\n\thash: {nextBlock.calculateBlockHash()}, " +
	// 		           $"\b\tmined by: {Settings.nodePublicKey} ({nextBlock.transactions[0].txOuts[0].amount} coin reward");
	// 		
	// 		if (txRecords.Count > 0)
	// 		{
	// 			LogTestMsg($"\nBalances after block mine (with transactions): " +
	// 			           $"\n\t{testPublicKey}:{Wallet.getAddressBalance(testPublicKey, bchain.uTxOuts)}" +
	// 			           $"\n\t{testPublicKey2}:{Wallet.getAddressBalance(testPublicKey2, bchain.uTxOuts)}" +
	// 			           $"\n\t{testPublicKey3}:{Wallet.getAddressBalance(testPublicKey3, bchain.uTxOuts)}");
	// 		}
	// 	}
	// }
}