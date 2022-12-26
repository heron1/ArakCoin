using ArakCoin;
using ArakCoin.Data;
using ArakCoin.Transactions;

namespace TestSuite.IntegrationTests;

[TestFixture]
[Category("IntegrationTests")]
public class MerkleTreeIntegrationTests
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
        Protocol.INITIALIZED_BLOCK_REWARD = 20;
        Settings.minMinerFee  = 0;
        Settings.maxMempoolSize = Protocol.MAX_TRANSACTIONS_PER_BLOCK * 2;
        Settings.nodePublicKey  = testPublicKey;
        Settings.nodePrivateKey  = testPrivateKey;
    }

    [Test]
    public void TestTxToBlockMapFunctionality()
    {
        //first create a blockchain and mine some coins
        Blockchain bchain = new Blockchain();
        for (int i = 0; i < 10; i++)
        {
            BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
        }
        
        int preMineHeight = bchain.getLength();

        //create some transactions with some random values
        var txFee2 = TransactionFactory.createNewTransactionForBlockchain(
            new TxOut[] { new TxOut(testPublicKey2, 1)},
            testPrivateKey, bchain, 2);
        var txFee4 = TransactionFactory.createNewTransactionForBlockchain(
            new TxOut[] { new TxOut(testPublicKey2, 1)},
            testPrivateKey, bchain, 4);
        var txFee3 = TransactionFactory.createNewTransactionForBlockchain(
            new TxOut[] { new TxOut(testPublicKey2, 1), new TxOut(testPublicKey3, 2)},
            testPrivateKey, bchain, 3);
        
        //mine the block with the transactions
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
        
        //add another tx
        var txFee5 = TransactionFactory.createNewTransactionForBlockchain(
            new TxOut[] { new TxOut(testPublicKey2, 1)},
            testPrivateKey, bchain, 2);
        
        //mine the block with it as well
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain);

        //now assert the TxToBlockMap has stored all the transactions correctly, with the correct corresponding blocks
        Assert.IsTrue(bchain.getBlockIdFromTxId(txFee2.id) == preMineHeight + 1);
        Assert.IsTrue(bchain.getBlockIdFromTxId(txFee3.id) == preMineHeight + 1);
        Assert.IsTrue(bchain.getBlockIdFromTxId(txFee4.id) == preMineHeight + 1);
        Assert.IsTrue(bchain.getBlockIdFromTxId(txFee5.id) == preMineHeight + 2);
        
        //assert retrieving an invalid tx returns null
        Assert.IsNull(bchain.getBlockIdFromTxId("abc4"));
        
        //we will now save this chain to disk, reload it, and assert the same tests all pass
        ArakCoin.Globals.masterChain = bchain; //set the test chain as the master chain for easy saving/loading
        Blockchain.saveMasterChainToDisk("merkle_chain_test1");
        ArakCoin.Globals.masterChain = new Blockchain(); //ensure memory here is cleared
        Blockchain.loadMasterChainFromDisk("merkle_chain_test1");
        var reloadedChain = ArakCoin.Globals.masterChain;
        
        //run previous tests again
        Assert.IsTrue(reloadedChain.getBlockIdFromTxId(txFee2.id) == preMineHeight + 1);
        Assert.IsTrue(reloadedChain.getBlockIdFromTxId(txFee3.id) == preMineHeight + 1);
        Assert.IsTrue(reloadedChain.getBlockIdFromTxId(txFee4.id) == preMineHeight + 1);
        Assert.IsTrue(reloadedChain.getBlockIdFromTxId(txFee5.id) == preMineHeight + 2);
        Assert.IsNull(reloadedChain.getBlockIdFromTxId("abc4"));
        
        //we will mine a new chain, and replace it with the reloaded chain. The replacement operation should also
        //update the txToBlockMap. We will again run the same tests after this is done
        var newChain = new Blockchain();
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //mine a couple of blocks

        //the newChain of course shouldn't contain any transactions from the reloadedChain
        Assert.IsFalse(newChain.getBlockIdFromTxId(txFee2.id) == preMineHeight + 1);
        
        //replace it now with the reloadedChain
        newChain.replaceBlockchain(reloadedChain);
        
        //run previous tests again
        Assert.IsTrue(reloadedChain.getBlockIdFromTxId(txFee2.id) == preMineHeight + 1);
        Assert.IsTrue(reloadedChain.getBlockIdFromTxId(txFee3.id) == preMineHeight + 1);
        Assert.IsTrue(reloadedChain.getBlockIdFromTxId(txFee4.id) == preMineHeight + 1);
        Assert.IsTrue(reloadedChain.getBlockIdFromTxId(txFee5.id) == preMineHeight + 2);
        Assert.IsNull(reloadedChain.getBlockIdFromTxId("abc4"));

        //lastly we do a manual rebuild of the txToBlockMap hashmap. Same tests should pass
        reloadedChain.rebuildTxBlockMap();
        Assert.IsTrue(reloadedChain.getBlockIdFromTxId(txFee2.id) == preMineHeight + 1);
        Assert.IsTrue(reloadedChain.getBlockIdFromTxId(txFee3.id) == preMineHeight + 1);
        Assert.IsTrue(reloadedChain.getBlockIdFromTxId(txFee4.id) == preMineHeight + 1);
        Assert.IsTrue(reloadedChain.getBlockIdFromTxId(txFee5.id) == preMineHeight + 2);
        Assert.IsNull(reloadedChain.getBlockIdFromTxId("abc4"));
    }

    [Test]
    public void TestBinaryTreeHelpersClass()
    {
        string[] sortedBinaryTreeEven = new [] { "1", "2", "3", "4", "5", "6", "7", "8"};
        string[] sortedBinaryTreeOdd = new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9"};

        //test getLeftChild
        Assert.IsTrue(BinaryTreeArrayHelpers.getLeftChild<string>("1", sortedBinaryTreeEven) == "2");
        Assert.IsTrue(BinaryTreeArrayHelpers.getLeftChild<string>("2", sortedBinaryTreeOdd) == "4");
        Assert.IsTrue(BinaryTreeArrayHelpers.getLeftChild<string>("3", sortedBinaryTreeEven) == "6");
        Assert.IsTrue(BinaryTreeArrayHelpers.getLeftChild<string>("4", sortedBinaryTreeOdd) == "8");
        Assert.IsTrue(BinaryTreeArrayHelpers.getLeftChild<string>("5", sortedBinaryTreeOdd) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getLeftChild<string>("6", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getLeftChild<string>("7", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getLeftChild<string>("8", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getLeftChild<string>("9", sortedBinaryTreeOdd) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getLeftChild<string>("100000000", sortedBinaryTreeOdd) is null);
        
        //test getRightChild
        Assert.IsTrue(BinaryTreeArrayHelpers.getRightChild<string>("1", sortedBinaryTreeEven) == "3");
        Assert.IsTrue(BinaryTreeArrayHelpers.getRightChild<string>("2", sortedBinaryTreeOdd) == "5");
        Assert.IsTrue(BinaryTreeArrayHelpers.getRightChild<string>("3", sortedBinaryTreeEven) == "7");
        Assert.IsTrue(BinaryTreeArrayHelpers.getRightChild<string>("4", sortedBinaryTreeOdd) == "9");
        Assert.IsTrue(BinaryTreeArrayHelpers.getRightChild<string>("5", sortedBinaryTreeOdd) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getRightChild<string>("6", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getRightChild<string>("7", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getRightChild<string>("8", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getRightChild<string>("9", sortedBinaryTreeOdd) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getRightChild<string>("100000000", sortedBinaryTreeOdd) is null);
        
        //test getLeftSibling
        Assert.IsTrue(BinaryTreeArrayHelpers.getLeftSibling<string>("1", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getLeftSibling<string>("2", sortedBinaryTreeOdd) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getLeftSibling<string>("3", sortedBinaryTreeEven) == "2");
        Assert.IsTrue(BinaryTreeArrayHelpers.getLeftSibling<string>("4", sortedBinaryTreeOdd) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getLeftSibling<string>("5", sortedBinaryTreeOdd) == "4");
        Assert.IsTrue(BinaryTreeArrayHelpers.getLeftSibling<string>("6", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getLeftSibling<string>("7", sortedBinaryTreeEven) == "6");
        Assert.IsTrue(BinaryTreeArrayHelpers.getLeftSibling<string>("8", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getLeftSibling<string>("9", sortedBinaryTreeOdd) == "8");
        Assert.IsTrue(BinaryTreeArrayHelpers.getLeftSibling<string>("100000000", sortedBinaryTreeOdd) is null);
        
        //test getRightSibling
        Assert.IsTrue(BinaryTreeArrayHelpers.getRightSibling<string>("1", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getRightSibling<string>("2", sortedBinaryTreeOdd) == "3");
        Assert.IsTrue(BinaryTreeArrayHelpers.getRightSibling<string>("3", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getRightSibling<string>("4", sortedBinaryTreeOdd) == "5");
        Assert.IsTrue(BinaryTreeArrayHelpers.getRightSibling<string>("5", sortedBinaryTreeOdd) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getRightSibling<string>("6", sortedBinaryTreeEven) == "7");
        Assert.IsTrue(BinaryTreeArrayHelpers.getRightSibling<string>("7", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getRightSibling<string>("8", sortedBinaryTreeOdd) == "9");
        Assert.IsTrue(BinaryTreeArrayHelpers.getRightSibling<string>("8", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getRightSibling<string>("9", sortedBinaryTreeOdd) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getRightSibling<string>("100000000", sortedBinaryTreeOdd) is null);
        
        //test getSibling
        Assert.IsTrue(BinaryTreeArrayHelpers.getSibling<string>("1", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getSibling<string>("2", sortedBinaryTreeOdd).Value.node == "3");
        Assert.IsTrue(BinaryTreeArrayHelpers.getSibling<string>("3", sortedBinaryTreeEven).Value.node == "2");
        Assert.IsTrue(BinaryTreeArrayHelpers.getSibling<string>("4", sortedBinaryTreeOdd).Value.node == "5");
        Assert.IsTrue(BinaryTreeArrayHelpers.getSibling<string>("5", sortedBinaryTreeOdd).Value.node == "4");
        Assert.IsTrue(BinaryTreeArrayHelpers.getSibling<string>("6", sortedBinaryTreeEven).Value.node == "7");
        Assert.IsTrue(BinaryTreeArrayHelpers.getSibling<string>("7", sortedBinaryTreeEven).Value.node == "6");
        Assert.IsTrue(BinaryTreeArrayHelpers.getSibling<string>("8", sortedBinaryTreeOdd).Value.node == "9");
        Assert.IsTrue(BinaryTreeArrayHelpers.getSibling<string>("8", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getSibling<string>("9", sortedBinaryTreeOdd).Value.node == "8");
        Assert.IsTrue(BinaryTreeArrayHelpers.getSibling<string>("100000000", sortedBinaryTreeOdd) is null);
        
        //test getSibling Positioning
        Assert.IsTrue(BinaryTreeArrayHelpers.getSibling<string>("1", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getSibling<string>("2", sortedBinaryTreeOdd).Value.position == 1);
        Assert.IsTrue(BinaryTreeArrayHelpers.getSibling<string>("3", sortedBinaryTreeEven).Value.position == 0);
        Assert.IsTrue(BinaryTreeArrayHelpers.getSibling<string>("4", sortedBinaryTreeOdd).Value.position == 1);
        Assert.IsTrue(BinaryTreeArrayHelpers.getSibling<string>("5", sortedBinaryTreeOdd).Value.position == 0);
        Assert.IsTrue(BinaryTreeArrayHelpers.getSibling<string>("6", sortedBinaryTreeEven).Value.position == 1);
        Assert.IsTrue(BinaryTreeArrayHelpers.getSibling<string>("7", sortedBinaryTreeEven).Value.position == 0);
        Assert.IsTrue(BinaryTreeArrayHelpers.getSibling<string>("8", sortedBinaryTreeOdd).Value.position == 1);
        Assert.IsTrue(BinaryTreeArrayHelpers.getSibling<string>("8", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getSibling<string>("9", sortedBinaryTreeOdd).Value.position == 0);
        Assert.IsTrue(BinaryTreeArrayHelpers.getSibling<string>("100000000", sortedBinaryTreeOdd) is null);
        
        //test getParent
        Assert.IsTrue(BinaryTreeArrayHelpers.getParent<string>("1", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BinaryTreeArrayHelpers.getParent<string>("2", sortedBinaryTreeOdd) == "1");
        Assert.IsTrue(BinaryTreeArrayHelpers.getParent<string>("3", sortedBinaryTreeEven) == "1");
        Assert.IsTrue(BinaryTreeArrayHelpers.getParent<string>("4", sortedBinaryTreeOdd) == "2");
        Assert.IsTrue(BinaryTreeArrayHelpers.getParent<string>("5", sortedBinaryTreeOdd) == "2");
        Assert.IsTrue(BinaryTreeArrayHelpers.getParent<string>("6", sortedBinaryTreeEven) == "3");
        Assert.IsTrue(BinaryTreeArrayHelpers.getParent<string>("7", sortedBinaryTreeEven) == "3");
        Assert.IsTrue(BinaryTreeArrayHelpers.getParent<string>("8", sortedBinaryTreeOdd) == "4");
        Assert.IsTrue(BinaryTreeArrayHelpers.getParent<string>("8", sortedBinaryTreeEven) == "4");
        Assert.IsTrue(BinaryTreeArrayHelpers.getParent<string>("9", sortedBinaryTreeOdd) == "4");
        Assert.IsTrue(BinaryTreeArrayHelpers.getParent<string>("100000000", sortedBinaryTreeOdd) is null);
    }

    [Test]
    public void testMerkleRootGeneration()
    {
        //TEST 1 - Odd transactions
        //we will create an array of 8 dummy transactions, and assert the merkle root generated is correct
        Transaction[] dummyTxes = new Transaction[8];
        //the tx id's will be "0","1",...,"7"
        for (int i = 0; i < 8; i++)
        {
            dummyTxes[i] = new Transaction(i.ToString(), new TxIn[] { }, new TxOut[] { });
        }

        //the automaticly built merkle root
        var automaticRoot = MerkleFunctions.getMerkleRoot(dummyTxes);
        
        //We now begin our own manual build.
        //First, combine the low level txes into a merkle level
        List<string> merkleLevel1 = new();
        merkleLevel1.Add(Utilities.calculateSHA256Hash(dummyTxes[0].id + dummyTxes[1].id));
        merkleLevel1.Add(Utilities.calculateSHA256Hash(dummyTxes[2].id + dummyTxes[3].id));
        merkleLevel1.Add(Utilities.calculateSHA256Hash(dummyTxes[4].id + dummyTxes[5].id));
        merkleLevel1.Add(Utilities.calculateSHA256Hash(dummyTxes[6].id + dummyTxes[7].id));
        List<string> merkleLevel2 = new();
        merkleLevel2.Add(Utilities.calculateSHA256Hash(merkleLevel1[0] + merkleLevel1[1]));
        merkleLevel2.Add(Utilities.calculateSHA256Hash(merkleLevel1[2] + merkleLevel1[3]));
        string merkleRoot = Utilities.calculateSHA256Hash(merkleLevel2[0] + merkleLevel2[1]);
        
        //now assert our manually generated merkle root is the same as the automatic one
        Assert.IsTrue(merkleRoot == automaticRoot);
    }

    [Test]
    public void TestMerkleSpoofFailure()
    {
        //TEST 1- We will legally mine some blocks, then modify the merkle root in one. The chain should become invalid
        ArakCoin.Globals.masterChain = new Blockchain();
        for (int i = 0; i < 10; i++)
        {
            BlockFactory.mineNextBlockAndAddToBlockchain(ArakCoin.Globals.masterChain);
        }

        Assert.IsTrue(Blockchain.isBlockchainValid(ArakCoin.Globals.masterChain));
        //do a "switcheroo"
        ArakCoin.Globals.masterChain.getBlockByIndex(4).merkleRoot =
            ArakCoin.Globals.masterChain.getBlockByIndex(3).merkleRoot;
        Assert.IsFalse(Blockchain.isBlockchainValid(ArakCoin.Globals.masterChain));

        //TEST 2- We will modify a single transaction id, and assert both the merkle root is different, and the chain
        //becomes invalid
        ArakCoin.Globals.masterChain = new Blockchain();
        for (int i = 0; i < 10; i++)
        {
            BlockFactory.mineNextBlockAndAddToBlockchain(ArakCoin.Globals.masterChain);
        }
        Assert.IsTrue(Blockchain.isBlockchainValid(ArakCoin.Globals.masterChain));
        Assert.IsTrue(MerkleFunctions.getMerkleRoot(
                    ArakCoin.Globals.masterChain.getBlockByIndex(5).transactions) ==
                    ArakCoin.Globals.masterChain.getBlockByIndex(5).merkleRoot);
        
        //modify the tx
        ArakCoin.Globals.masterChain.getBlockByIndex(5).transactions[0].id =
            ArakCoin.Globals.masterChain.getBlockByIndex(4).transactions[0].id;
        
        Assert.IsFalse(Blockchain.isBlockchainValid(ArakCoin.Globals.masterChain));
        Assert.IsFalse(MerkleFunctions.getMerkleRoot(
                          ArakCoin.Globals.masterChain.getBlockByIndex(5).transactions) ==
                      ArakCoin.Globals.masterChain.getBlockByIndex(5).merkleRoot);
    }

    [Test]
    public void TestSPV()
    {
        //Create a blockchain, mine some coins
        Blockchain bchain = new Blockchain();
        ArakCoin.Globals.masterChain = bchain;

        for (int i = 0; i < 20; i++)
        {
            BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
        }
        
        //create & mine a block with 9 txes (8 manual + coinbase)
        for (int i = 0; i < 8; i++)
        {
            TransactionFactory.createNewTransactionForBlockchain(
                new TxOut[] { new TxOut(testPublicKey2, 1)},
                testPrivateKey, bchain, 2);
        }
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
        Assert.IsTrue(bchain.getLastBlock().transactions.Length == 9); //should be 9 transactions we will test

        var merkleRoot = bchain.getLastBlock().merkleRoot; //this is the root our SPV's should derive
        
        //Let's say we wish to verify a transaction is included in the blockchain. We do this by deriving the merkle
        //root from the block contaning our tx. Simulate getting the minimal spv hashes from a
        //full node for this tx, along with the corresponding block. After that, ensure we can reconstruct the merkle
        //root for the matching block from the minimal spv hashes, and verify it matches the known merkleRoot
        
        Transaction desiredTxToVerify = bchain.getLastBlock().transactions[2]; //let' s say we want verify the 3rd tx
        
        //first retrieve the minimal hashes, and associated metadata, to allow the calculation
        var minimalHashes = MerkleFunctions.calculateMinimalVerificationHashesFromTx(desiredTxToVerify);
        
        //assert that the minimalHashes are smaller in number than the number of transactions (otherwise the entire
        //point of having merkle trees is non-existent)
        Assert.IsTrue(bchain.getLastBlock().transactions.Length > minimalHashes.Length);
        
        //now perform the merkle root calculation locally
        var calculatedRootFromMinimalHashes =
            MerkleFunctions.calculateMerkleRootFromMinimalVerificationHashes(desiredTxToVerify, minimalHashes);
        
        //Test 1 -
        //assert that the locally calculated merkle root equals the actual merkle root for the block containing the tx
        Assert.IsTrue(calculatedRootFromMinimalHashes == merkleRoot);

        //Test 2 -
        //Verify this doesn't work for another tx
        Transaction someOtherTx = bchain.getLastBlock().transactions[1];
        var incorrectMinimalHashes =
            MerkleFunctions.calculateMerkleRootFromMinimalVerificationHashes(someOtherTx, minimalHashes);
        Assert.False(incorrectMinimalHashes == merkleRoot);

        //Test 3 -
        //Create a transaction not mined, verify this fails at the calculateMinimalVerificationHashesFromTx
        //(ie: the node shouldn't be able to find any block containing this tx)
        var tx = TransactionFactory.createNewTransactionForBlockchain(
            new TxOut[] { new TxOut(testPublicKey2, 1)},
            testPrivateKey, bchain, 2);
        var invalidMerklePath = MerkleFunctions.calculateMinimalVerificationHashesFromTx(tx);
        Assert.IsNull(invalidMerklePath);
        
        //Test 4 -
        //We will repeat everything in test 1, but this time choose the odd numbered last tx, and assert everything
        //still works
        desiredTxToVerify = bchain.getLastBlock().transactions[8];
        minimalHashes = MerkleFunctions.calculateMinimalVerificationHashesFromTx(desiredTxToVerify);
        calculatedRootFromMinimalHashes =
            MerkleFunctions.calculateMerkleRootFromMinimalVerificationHashes(desiredTxToVerify, minimalHashes);
        Assert.IsTrue(calculatedRootFromMinimalHashes == merkleRoot);
        
        //Test 5 -
        //For a block with only 1tx (ie: the coinbase tx), everything should still work. We additionally assert that
        //this coinbase tx id is in fact equivalent to the block's merkle root
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //mine the previous tx
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //no new regular txes
        Assert.IsTrue(bchain.getLastBlock().transactions.Length == 1); //only 1 coinbase tx should be present
        merkleRoot = bchain.getLastBlock().merkleRoot;
        desiredTxToVerify = bchain.getLastBlock().transactions[0];
        minimalHashes = MerkleFunctions.calculateMinimalVerificationHashesFromTx(desiredTxToVerify);
        calculatedRootFromMinimalHashes =
            MerkleFunctions.calculateMerkleRootFromMinimalVerificationHashes(desiredTxToVerify, minimalHashes);
        Assert.IsTrue(calculatedRootFromMinimalHashes == merkleRoot);
        Assert.IsTrue(desiredTxToVerify.id == bchain.getLastBlock().merkleRoot);
    }
    
    [Test]
    public void Temp()
    {
        


    }

    
  
}