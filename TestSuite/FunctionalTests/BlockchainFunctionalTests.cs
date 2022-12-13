using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using ArakCoin;
using ArakCoin.Data;
using ArakCoin.Transactions;

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
		Settings.allowParallelCPUMining = true; //all tests should be tested with parallel mining enabled

		bchain = new Blockchain();
	}
	
	[Test]
	public void TestDifficultyScaling()
	{
		LogTestMsg("Testing TestDifficultyScaling..");
	
		// pick a number of blocks to add that will test 2 difficulty intervals, along with some extra blocks
		int blocksToAdd = Protocol.DIFFICULTY_INTERVAL_BLOCKS * 2 + Protocol.DIFFICULTY_INTERVAL_BLOCKS / 2;
		for (int i = 0; i < blocksToAdd; i++)
		{
			bchain.addValidBlock(BlockFactory.createAndMineNewBlock(bchain));
			LogTestMsg($"\tTstamp diff: " +
			                $"{Blockchain.getTimestampDifferenceToPredecessorBlock(bchain.getLastBlock(), bchain)}, " +
			                $"Block: {bchain.getLength()}, Difficulty: {bchain.currentDifficulty}");
		}
	
		Assert.IsTrue(bchain.isBlockchainValid());
	}

	/*
	 * Randomly mine three separate blockchains, each with different random transactions and save them locally
	 * on hard disk as separate files (in JSON format). Keep track of the longest one.
	 *
	 * Now randomly mine a fourth chain with invalid protocol settings but that has the greatest accumulative
	 * hashpower, and that is the longest. Also save this chain to disk.
	 *
	 * Retrieve all four chains from disk, and assert that the chain that correctly adheres to the protocol (one of
	 * the first three chains) and that is the longest is the winning chain (both in terms of length & accumulative
	 * hashpower).
	 *
	 * This functional test aims to test the Blockchain.establishWinningChain in a simulated real environment of
	 * competing chains where the chains are retrieved from serialized data, but done locally only
	 */
	[Test]
	public void TestWinningChainComparisonFunctionally()
	{
		LogTestMsg("Testing TestWinningChainComparisonFunctionally..");

		//initialize the RNG
		int seed;
		using (RNGCryptoServiceProvider rg = new RNGCryptoServiceProvider()) //get truly random seed for pseudo-rng
		{ 
			byte[] rno = new byte[5];    
			rg.GetBytes(rno);    
			seed = BitConverter.ToInt32(rno, 0); 
		}
		Random random = new Random(seed);

		//the first three blockchains are randomly created and populated with transactions and blocks
		//the fourth chain does the same, but is created with a different protocol
		Blockchain actualWinner = null; //determines the actual longest chain of the three local valid ones
		List<Blockchain> bchains = new List<Blockchain>(4);
		for (int i = 0; i < 4; i++)
		{
			if (i == 3)
			{
				//we now change protocol settings, and mine a fourth chain with greater length & hashpower
				//than prior 3
				Protocol.DIFFICULTY_INTERVAL_BLOCKS--;
			}
			Blockchain newChain = new Blockchain();
			bchains.Add(newChain);
			int randBlocks = random.Next(0, 25); //limit upper block bound so mining doesn't take too long
			if (i == 3)
				randBlocks = 35; //the fourth chain will always have more hashpower than the others
			for (int j = 0; j < randBlocks; j++)
			{
				//choose a random miner "winner" for this block to receive coins
				string blockMiner;
				switch (random.Next(0, 3))
				{
					case (0):
						blockMiner = testPublicKey;
						break;
					case(1):
						blockMiner = testPublicKey2;
						break;
					default:
						blockMiner = testPublicKey3;
						break;
				}
				Settings.nodePublicKey = blockMiner;
				
				while (random.Next(0, 2) % 2 == 0) //attempt to create new txes until odd number found
				{
					var txOuts = new List<TxOut>();
					string sender;
					switch (random.Next(0, 3))
					{
						case (0):
							sender = testPrivateKey;
							break;
						case(1):
							sender = testPrivateKey2;
							break;
						default:
							sender = testPrivateKey3;
							break;
					}					
					while (random.Next(0, 2) % 2 == 0) //attempt to create new txOuts until odd number found
					{
						string recipient;
						switch (random.Next(0, 3))
						{
							case (0):
								recipient = testPublicKey;
								break;
							case(1):
								recipient = testPublicKey2;
								break;
							default:
								recipient = testPublicKey3;
								break;
						}

						txOuts.Add(new TxOut(recipient, random.Next(0, 10)));
					}

					TransactionFactory.createNewTransactionForBlockchain(txOuts.ToArray(), sender, newChain,
						random.Next(0, 2));
				}

				BlockFactory.mineNextBlockAndAddToBlockchain(newChain);
			}

			if (i < 3) //keep track of the winning chain only if it satisfies the original protocol (first 3 chains)
			{
				if (actualWinner is null)
					actualWinner = newChain;
				else if (actualWinner.getLength() < newChain.getLength())
					actualWinner = newChain;
				
				LogTestMsg($"\tChain {i + 1} (valid) has {newChain.getLength()} blocks");
			}
			else
			{
				LogTestMsg($"\tChain {i + 1} (invalid) has {newChain.getLength()} blocks");
			}
		}
		
		Protocol.DIFFICULTY_INTERVAL_BLOCKS++; //set protocol back to the original one
		Assert.IsTrue(actualWinner.isBlockchainValid());
		Assert.IsFalse(bchains.Last().isBlockchainValid());

		LogTestMsg($"\tSerializing the four chains and saving them to disk..");
		int k = 1;
		foreach (var chain in bchains)
		{
			//serialize and write to disk
			string? jsonChain = Serialize.serializeBlockchainToJson(chain);
			Assert.IsNotNull(jsonChain);
			bool write = Storage.writeJsonToDisk(jsonChain, $"test_chain{k++}");
			Assert.IsTrue(write);
		}

		var chainsReloaded = new List<Blockchain>();
		LogTestMsg($"\tLoading the four chains from disk and deserializing them back into memory..");
		for (k = 1; k < 5; k++)
		{
			//load from disk and deserialize
			string? jsonChain = Storage.readJsonFromDisk($"test_chain{k}");
			Assert.IsNotNull(jsonChain);
			Blockchain? loadedChain = Serialize.deserializeJsonToBlockchain(jsonChain);
			Assert.IsNotNull(loadedChain);
			chainsReloaded.Add(loadedChain);
		}

		var chainsReloadedArr = chainsReloaded.ToArray();
		
		//now we establish the winning chain from the four chains reloaded from disk..
		var winningChain = Blockchain.establishWinningChain(chainsReloaded);
		Assert.IsNotNull(winningChain);
		//assert that it is equal to the actualWinner in memory
		Assert.IsTrue(winningChain == actualWinner);
		//winning chain should have smaller length than the invalid chain
		Assert.IsTrue(winningChain.getLength() < chainsReloadedArr[3].getLength());
		//it should also have less hashpower
		Assert.IsTrue(Blockchain.calculateAccumulativeChainDifficulty(winningChain) <
		              Blockchain.calculateAccumulativeChainDifficulty(chainsReloadedArr[3]));
		//but it should have greater length than any of the other valid ones
		for (int p = 0; p < 3; p++)
		{
			Assert.IsTrue(chainsReloadedArr[p].getLength() <= winningChain.getLength());
		}
		LogTestMsg($"\tThe winning chain has been correctly asserted as the " +
		           $"one with: {winningChain.getLength()} blocks");
	}
}