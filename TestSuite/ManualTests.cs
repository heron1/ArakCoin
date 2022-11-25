using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using ArakCoin;
using ArakCoin.Networking;
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
		Settings.nodePrivateKey  = testPrivateKey;
		Settings.nodePublicKey  = testPublicKey;
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
		int seed = Utilities.getTrulyRandomNumber();
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
					Settings.nodePublicKey  = testPublicKey;
					break;
				case(1):
					Settings.nodePublicKey  = testPublicKey2;
					break;
				default:
					Settings.nodePublicKey  = testPublicKey3;
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
						if (txOut.address != Protocol.FEE_ADDRESS)
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
			           $"({Protocol.BLOCK_REWARD} block reward, " +
			           $"{nextBlock.transactions[0].txOuts[0].amount - Protocol.BLOCK_REWARD} fees)");

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

	
	
	/**
	 * This is a non-trivial manual test which can be called from any number of separate nodes in
	 * distributed locations that can each separately take part in this same test, communicating via their own sockets
	 * connections following the blockchain's P2P protocol.
	 *
	 * In essence this will test the blockchain in action in a live network - as separate nodes run this test they will
	 * communicate their host details to the known nodes in their own local hosts file. Nodes should share on-going
	 * mined blocks, mempools, and the list of other known nodes that have registered with them. Nodes should also
	 * reach byzantine consensus via requesting blockchains from one another if communicated blocks differ to what's
	 * expected.
	 *
	 * Random mining will ensure that the communicated blocks will sometimes be only the next valid block, whilst
	 * other times may be many blocks ahead (ie: some nodes may mine the chain many blocks ahead in "stealth").
	 * This behaviour is programmed via nodes randomly sleeping instead of listening to the network or mining.
	 * Delibarately invalid blocks will also be communicated via the network by nodes, which should not corrupt the
	 * byzantine consensus chain.
	 *
	 * Periodic assertions will exist for things that are easy to test (such as chain validation), however manual
	 * log observation should be done to ensure the blockchain network is operating correctly and that all nodes are
	 * recognizing one another. It's assumed the tester will be able to access each node and view the log messages
	 * in the terminal.
	 *
	 * This is an on-going test which will be updated as the P2P protocol is expanded, and may also include
	 * simulated transactions in the future. This test should serve as a "sanity check" that the blockchain is working
	 * as intended on the network protocol level and below.
	 *
	 * todo: begin implementing this
	 * todo: implementation is on-going
	 */
	[Test]
	public async Task TestBlockhainOperationAndByzantineConsensusViaNetworkOfNodes()
	{
		//SETUP BEGINS
		
		//start this node's listening server to handle incoming connections from both other nodes and clients
		NodeListenerServer listeningServer = new NodeListenerServer();
		listeningServer.startListeningServer();
		
		//load this node's hosts file, and asynchronously register itself with every node in it.
		//Whilst every node need not contain the same starting node to kickstart the P2P discovery protocol,
		//it should be logically possible for every node to discover one another from each other - the
		//ordering this is done in however shouldn't matter.
		foreach (var node in HostsManager.getNodes())
		{
			NetworkingManager.registerThisNodeWithAnotherNode(node);
		}
		
		//update local hosts file from all known nodes - wait for this operation to complete
		NetworkingManager.updateHostsFileFromKnownNodes();

		var candidateChains = new List<Blockchain>();
		//retrieve each known node's blockchain and establish the consensus chain locally from the responses
		lock (HostsManager.hostsLock)
		{
			foreach (var node in HostsManager.getNodes())
			{
				var receivedChain = NetworkingManager.getBlockchainFromOtherNode(node);
				if (receivedChain is not null) 
					candidateChains.Add(receivedChain); //chain validation will happen later
			}
		}

		//validated winning chain is stored as the local consensus chain
		var winningChain = Blockchain.establishWinningChain(candidateChains);
		if (winningChain is not null)
			ArakCoin.Globals.masterChain = winningChain;
		
		
		//SIMULATION BEGINS (end of setup)
		//begin mining as a new Task in the background
		ArakCoin.Globals.miningCancelToken = AsyncTasks.mineBlocksAsync();
		
		//initialize the RNG
		int seed = Utilities.getTrulyRandomNumber();
		Random random = new Random(seed);

		int noSleepCounter = 10; //don't sleep for this number of iterations when no sleep mode is activated
		bool isNoSleepActive = false; //is the noSleep counter active?
		
		while (true)
		{
			if (!isNoSleepActive && random.Next(0, 3) == 0)
				isNoSleepActive = true; //go into "no sleep" mode 33% of the time if state isn't active
			
			if (!isNoSleepActive)
			{
				int sleepTime = random.Next(0, 10000);
				LogTestMsg($"sleeping {sleepTime}ms..");
				ArakCoin.Globals.miningCancelToken.Cancel(); //cancel local block mining
				Utilities.sleep(sleepTime);
				ArakCoin.Globals.miningCancelToken = AsyncTasks.mineBlocksAsync(); //resume local block mining
			}
			else
			{
				if (noSleepCounter-- == 0)
				{
					isNoSleepActive = false;
					noSleepCounter = 10;
				}
			}

			if (random.Next(0, 10) == 0) //10% of the time in the loop, re-register this node to known nodes
			//and also update hosts file
			{
				StringBuilder sb = new StringBuilder("Known nodes: \n");
				foreach (var node in HostsManager.getNodes())
				{
					NetworkingManager.registerThisNodeWithAnotherNode(node);
					sb.Append($"\t{node.ToString()}");
				}
				LogTestMsg(sb.ToString());
				NetworkingManager.updateHostsFileFromKnownNodes();
			}
			//todo - node check re-registration broadcast (every now and then)
			//todo - debug why other node not communicating (periodic re-registration broadcast should ensure it does,
			//or breakpoint being hit)
			//todo - many types of broadcasting (eg: mempools)
			
			//now sleep a bit to let the background threads do their work
			Utilities.sleep(random.Next(0, 1000));
			if (ArakCoin.Globals.masterChain.isBlockchainValid())
			{
				Utilities.log("Blockchain validated..");
			}
			else
			{
				throw new("Invalid blockchain encountered");
			}
		}

	}
}