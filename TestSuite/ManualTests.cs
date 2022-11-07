using System.Diagnostics;
using System.Security.Cryptography;
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

	[Test]
	public void TestBlockchainMiningWithSimulatedTransactions()
	{
		//note: this shouldn't just do valid txes, but invalid ones too. (eg: unavailable balances, etc). Periodic assertions
		//should ensure total coin supply = current supply. Randomness should create many possible scenarios to observe,
		//potentially revealing unexpected bugs
		
		//initialize the RNG
		int seed;
		using (RNGCryptoServiceProvider rg = new RNGCryptoServiceProvider()) //get truly random seed for pseudo-rng
		{ 
			byte[] rno = new byte[5];    
			rg.GetBytes(rno);    
			seed = BitConverter.ToInt32(rno, 0); 
		}
		Random random = new Random(seed);
		
		List <TxRecord> txRecords = new List<TxRecord>();
		double r, d;
		int p;
		
		BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //genesis block
	
		while (true)
		{
			//change miner to random one
			p = random.Next(0, 3);
			switch (p)
			{
				case (0):
					Settings.nodePublicKey = testPublicKey;
					break;
				case(1):
					Settings.nodePublicKey = testPublicKey2;
					break;
				default:
					Settings.nodePublicKey = testPublicKey3;
					break;
			}
	
			while (random.NextDouble() < 0.7) //keep making txes 70% of the time
			{
				d = random.NextDouble();
				string sender_priv = d < 0.16 ? testPrivateKey2 : (d < 0.32 ? testPrivateKey3 : testPrivateKey);
				string sender_pub = Cryptography.getPublicKeyFromPrivateKey(sender_priv);
				
				List<TxOut> txouts = new List<TxOut>();
				while ((r = random.NextDouble()) < 0.5) //keep adding tx outs to tx 50% of the time
				{
					//change recipient randomly, assert different to sender
					string recipient;
					do
					{
						recipient = r < 0.16 ? testPublicKey2 : (r < 0.32 ? testPublicKey3 : testPublicKey);
						r = random.NextDouble();
					} while (recipient == sender_pub);

					//random txouts will mostly be invalid but transaction should be rejected anyway
					int balance = (int)Wallet.getAddressBalance(sender_pub, bchain.uTxOuts);
					if (balance == 0)
						break;
					txouts.Add(new TxOut(recipient, random.Next(1,balance)));
				}

				if (txouts.Count == 0)
					continue;
	
				//tx creation may or may not succeed, but this doesn't matter
				//(we test both valid and invalid transaction creation)
				long minerFee = random.Next(0, (int)Wallet.getAddressBalance(sender_pub, bchain.uTxOuts) / 4);
				Transaction? tx = TransactionFactory.createNewTransactionForBlockchain(txouts.ToArray(), 
					sender_priv, bchain, minerFee);
				if (tx is not null)
				{
					foreach (var txOut in tx.txOuts)
					{
						if (txOut.address != Settings.FEE_ADDRESS)
							txRecords.Add(new TxRecord(sender_pub, txOut.address, txOut.amount,
								tx.id, minerFee));
					}
				}
			}
	
			LogTestMsg("");
			
			foreach (var txRecord in txRecords)
			{
				LogTestMsg($"{txRecord.sender.Substring(0, 3)}... sending " +
				           $"{txRecord.amount} coins to {txRecord.receiver.Substring(0, 3)}, " +
				           $"(from tx: {txRecord.transactionId.Substring(0, 3)}, fee: {txRecord.minerFee})...");
			}

			LogTestMsg("\nMining next block..");
			Block nextBlock = BlockFactory.createAndMineNewBlock(bchain, bchain.mempool.ToArray()); //mine the block
			bool success = bchain.addValidBlock(nextBlock);
			Assert.IsTrue(success);
			bchain.clearMempool();
			txRecords.Clear();
			LogTestMsg($"\nBLOCK MINE: Block id: {nextBlock.index} mined with difficulty {nextBlock.difficulty}" +
			           $"\n\thash: {nextBlock.calculateBlockHash()}, " +
			           $"\n\tmined by: {Settings.nodePublicKey} " +
			           $"\n\treward: {nextBlock.transactions[0].txOuts[0].amount} coins " +
			           $"({Settings.BLOCK_REWARD} block reward, " +
			           $"{nextBlock.transactions[0].txOuts[0].amount - Settings.BLOCK_REWARD} fees)");

			LogTestMsg($"\nBalances after block mine: " +
			           $"\n\t{testPublicKey.Substring(0, 3)}..: " +
			           $"{Wallet.getAddressBalance(testPublicKey, bchain.uTxOuts)} coins" +
			           $"\n\t{testPublicKey2.Substring(0, 3)}..: " +
			           $"{Wallet.getAddressBalance(testPublicKey2, bchain.uTxOuts)} coins" +
			           $"\n\t{testPublicKey3.Substring(0, 3)}..: " +
			           $"{Wallet.getAddressBalance(testPublicKey3, bchain.uTxOuts)} coins\n");
			
			//every 10 blocks, validate the blockchain
			if (bchain.getLength() % 10 == 0)
			{
				Assert.IsTrue(bchain.isBlockchainValid());
				LogTestMsg($"\nValidated blockchain successfully. Total coin supply: " +
				           $"{Wallet.getCurrentCirculatingCoinSupply(bchain)} from {bchain.getLength()} blocks\n");
			}
		}
	}
}