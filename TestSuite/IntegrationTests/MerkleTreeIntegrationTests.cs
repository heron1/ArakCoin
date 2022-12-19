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
        Protocol.BLOCK_REWARD = 20;
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
        Assert.IsTrue(bchain.getBlockFromTxId(txFee2.id) == preMineHeight + 1);
        Assert.IsTrue(bchain.getBlockFromTxId(txFee3.id) == preMineHeight + 1);
        Assert.IsTrue(bchain.getBlockFromTxId(txFee4.id) == preMineHeight + 1);
        Assert.IsTrue(bchain.getBlockFromTxId(txFee5.id) == preMineHeight + 2);
        
        //assert retrieving an invalid tx returns null
        Assert.IsNull(bchain.getBlockFromTxId("abc4"));
        
        //we will now save this chain to disk, reload it, and assert the same tests all pass
        ArakCoin.Globals.masterChain = bchain; //set the test chain as the master chain for easy saving/loading
        Blockchain.saveMasterChainToDisk("merkle_chain_test1");
        ArakCoin.Globals.masterChain = new Blockchain(); //ensure memory here is cleared
        Blockchain.loadMasterChainFromDisk("merkle_chain_test1");
        var reloadedChain = ArakCoin.Globals.masterChain;
        
        //run previous tests again
        Assert.IsTrue(reloadedChain.getBlockFromTxId(txFee2.id) == preMineHeight + 1);
        Assert.IsTrue(reloadedChain.getBlockFromTxId(txFee3.id) == preMineHeight + 1);
        Assert.IsTrue(reloadedChain.getBlockFromTxId(txFee4.id) == preMineHeight + 1);
        Assert.IsTrue(reloadedChain.getBlockFromTxId(txFee5.id) == preMineHeight + 2);
        Assert.IsNull(reloadedChain.getBlockFromTxId("abc4"));
        
        //we will mine a new chain, and replace it with the reloaded chain. The replacement operation should also
        //update the txToBlockMap. We will again run the same tests after this is done
        var newChain = new Blockchain();
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain); //mine a couple of blocks

        //the newChain of course shouldn't contain any transactions from the reloadedChain
        Assert.IsFalse(newChain.getBlockFromTxId(txFee2.id) == preMineHeight + 1);
        
        //replace it now with the reloadedChain
        newChain.replaceBlockchain(reloadedChain);
        
        //run previous tests again
        Assert.IsTrue(reloadedChain.getBlockFromTxId(txFee2.id) == preMineHeight + 1);
        Assert.IsTrue(reloadedChain.getBlockFromTxId(txFee3.id) == preMineHeight + 1);
        Assert.IsTrue(reloadedChain.getBlockFromTxId(txFee4.id) == preMineHeight + 1);
        Assert.IsTrue(reloadedChain.getBlockFromTxId(txFee5.id) == preMineHeight + 2);
        Assert.IsNull(reloadedChain.getBlockFromTxId("abc4"));

        //lastly we do a manual rebuild of the txToBlockMap hashmap. Same tests should pass
        reloadedChain.rebuildTxBlockMap();
        Assert.IsTrue(reloadedChain.getBlockFromTxId(txFee2.id) == preMineHeight + 1);
        Assert.IsTrue(reloadedChain.getBlockFromTxId(txFee3.id) == preMineHeight + 1);
        Assert.IsTrue(reloadedChain.getBlockFromTxId(txFee4.id) == preMineHeight + 1);
        Assert.IsTrue(reloadedChain.getBlockFromTxId(txFee5.id) == preMineHeight + 2);
        Assert.IsNull(reloadedChain.getBlockFromTxId("abc4"));
    }

    [Test]
    public void TestBinaryTreeHelpersClass()
    {
        string[] sortedBinaryTreeEven = new [] { "1", "2", "3", "4", "5", "6", "7", "8"};
        string[] sortedBinaryTreeOdd = new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9"};

        //test getLeftChild
        Assert.IsTrue(BTArrayHelpers.getLeftChild<string>("1", sortedBinaryTreeEven) == "2");
        Assert.IsTrue(BTArrayHelpers.getLeftChild<string>("2", sortedBinaryTreeOdd) == "4");
        Assert.IsTrue(BTArrayHelpers.getLeftChild<string>("3", sortedBinaryTreeEven) == "6");
        Assert.IsTrue(BTArrayHelpers.getLeftChild<string>("4", sortedBinaryTreeOdd) == "8");
        Assert.IsTrue(BTArrayHelpers.getLeftChild<string>("5", sortedBinaryTreeOdd) is null);
        Assert.IsTrue(BTArrayHelpers.getLeftChild<string>("6", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BTArrayHelpers.getLeftChild<string>("7", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BTArrayHelpers.getLeftChild<string>("8", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BTArrayHelpers.getLeftChild<string>("9", sortedBinaryTreeOdd) is null);
        Assert.IsTrue(BTArrayHelpers.getLeftChild<string>("100000000", sortedBinaryTreeOdd) is null);
        
        //test getRightChild
        Assert.IsTrue(BTArrayHelpers.getRightChild<string>("1", sortedBinaryTreeEven) == "3");
        Assert.IsTrue(BTArrayHelpers.getRightChild<string>("2", sortedBinaryTreeOdd) == "5");
        Assert.IsTrue(BTArrayHelpers.getRightChild<string>("3", sortedBinaryTreeEven) == "7");
        Assert.IsTrue(BTArrayHelpers.getRightChild<string>("4", sortedBinaryTreeOdd) == "9");
        Assert.IsTrue(BTArrayHelpers.getRightChild<string>("5", sortedBinaryTreeOdd) is null);
        Assert.IsTrue(BTArrayHelpers.getRightChild<string>("6", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BTArrayHelpers.getRightChild<string>("7", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BTArrayHelpers.getRightChild<string>("8", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BTArrayHelpers.getRightChild<string>("9", sortedBinaryTreeOdd) is null);
        Assert.IsTrue(BTArrayHelpers.getRightChild<string>("100000000", sortedBinaryTreeOdd) is null);
        
        //test getLeftSibling
        Assert.IsTrue(BTArrayHelpers.getLeftSibling<string>("1", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BTArrayHelpers.getLeftSibling<string>("2", sortedBinaryTreeOdd) is null);
        Assert.IsTrue(BTArrayHelpers.getLeftSibling<string>("3", sortedBinaryTreeEven) == "2");
        Assert.IsTrue(BTArrayHelpers.getLeftSibling<string>("4", sortedBinaryTreeOdd) is null);
        Assert.IsTrue(BTArrayHelpers.getLeftSibling<string>("5", sortedBinaryTreeOdd) == "4");
        Assert.IsTrue(BTArrayHelpers.getLeftSibling<string>("6", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BTArrayHelpers.getLeftSibling<string>("7", sortedBinaryTreeEven) == "6");
        Assert.IsTrue(BTArrayHelpers.getLeftSibling<string>("8", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BTArrayHelpers.getLeftSibling<string>("9", sortedBinaryTreeOdd) == "8");
        Assert.IsTrue(BTArrayHelpers.getLeftSibling<string>("100000000", sortedBinaryTreeOdd) is null);
        
        //test getRightSibling
        Assert.IsTrue(BTArrayHelpers.getRightSibling<string>("1", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BTArrayHelpers.getRightSibling<string>("2", sortedBinaryTreeOdd) == "3");
        Assert.IsTrue(BTArrayHelpers.getRightSibling<string>("3", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BTArrayHelpers.getRightSibling<string>("4", sortedBinaryTreeOdd) == "5");
        Assert.IsTrue(BTArrayHelpers.getRightSibling<string>("5", sortedBinaryTreeOdd) is null);
        Assert.IsTrue(BTArrayHelpers.getRightSibling<string>("6", sortedBinaryTreeEven) == "7");
        Assert.IsTrue(BTArrayHelpers.getRightSibling<string>("7", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BTArrayHelpers.getRightSibling<string>("8", sortedBinaryTreeOdd) == "9");
        Assert.IsTrue(BTArrayHelpers.getRightSibling<string>("8", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BTArrayHelpers.getRightSibling<string>("9", sortedBinaryTreeOdd) is null);
        Assert.IsTrue(BTArrayHelpers.getRightSibling<string>("100000000", sortedBinaryTreeOdd) is null);
        
        //test getSibling
        Assert.IsTrue(BTArrayHelpers.getSibling<string>("1", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BTArrayHelpers.getSibling<string>("2", sortedBinaryTreeOdd) == "3");
        Assert.IsTrue(BTArrayHelpers.getSibling<string>("3", sortedBinaryTreeEven) == "2");
        Assert.IsTrue(BTArrayHelpers.getSibling<string>("4", sortedBinaryTreeOdd) == "5");
        Assert.IsTrue(BTArrayHelpers.getSibling<string>("5", sortedBinaryTreeOdd) == "4");
        Assert.IsTrue(BTArrayHelpers.getSibling<string>("6", sortedBinaryTreeEven) == "7");
        Assert.IsTrue(BTArrayHelpers.getSibling<string>("7", sortedBinaryTreeEven) == "6");
        Assert.IsTrue(BTArrayHelpers.getSibling<string>("8", sortedBinaryTreeOdd) == "9");
        Assert.IsTrue(BTArrayHelpers.getSibling<string>("8", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BTArrayHelpers.getSibling<string>("9", sortedBinaryTreeOdd) == "8");
        Assert.IsTrue(BTArrayHelpers.getSibling<string>("100000000", sortedBinaryTreeOdd) is null);
        
        //test getParent
        Assert.IsTrue(BTArrayHelpers.getParent<string>("1", sortedBinaryTreeEven) is null);
        Assert.IsTrue(BTArrayHelpers.getParent<string>("2", sortedBinaryTreeOdd) == "1");
        Assert.IsTrue(BTArrayHelpers.getParent<string>("3", sortedBinaryTreeEven) == "1");
        Assert.IsTrue(BTArrayHelpers.getParent<string>("4", sortedBinaryTreeOdd) == "2");
        Assert.IsTrue(BTArrayHelpers.getParent<string>("5", sortedBinaryTreeOdd) == "2");
        Assert.IsTrue(BTArrayHelpers.getParent<string>("6", sortedBinaryTreeEven) == "3");
        Assert.IsTrue(BTArrayHelpers.getParent<string>("7", sortedBinaryTreeEven) == "3");
        Assert.IsTrue(BTArrayHelpers.getParent<string>("8", sortedBinaryTreeOdd) == "4");
        Assert.IsTrue(BTArrayHelpers.getParent<string>("8", sortedBinaryTreeEven) == "4");
        Assert.IsTrue(BTArrayHelpers.getParent<string>("9", sortedBinaryTreeOdd) == "4");
        Assert.IsTrue(BTArrayHelpers.getParent<string>("100000000", sortedBinaryTreeOdd) is null);
    }
    
    [Test]
    public void Temp()
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
        // var txFee3 = TransactionFactory.createNewTransactionForBlockchain(
        //     new TxOut[] { new TxOut(testPublicKey2, 1), new TxOut(testPublicKey3, 2)},
        //     testPrivateKey, bchain, 3);
        
        //mine the block with the transactions
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain);

        string root = Block.calculateMerkleRoot(bchain.getLastBlock().transactions);
        int b = 3;


    }

    
  
}