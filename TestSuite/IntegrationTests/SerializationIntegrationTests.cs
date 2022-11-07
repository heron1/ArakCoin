using ArakCoin;
using ArakCoin.Transactions;

namespace TestSuite.IntegrationTests;

[TestFixture]
[Category("IntegrationTests")]
public class SerializationIntegrationTests
{
    [SetUp]
    public void Setup()
    {
        Settings.nodePublicKey = testPublicKey;
    }

    //todo - these unit tests should test for data corruption
    
    [Test]
    public void testBlockSerialization()
    {
        //Test 1: Create a block populated with transactions and serialize it. Test the deserialized block is equal
        //to it. Then append the deserialized block to the blockchain and assert its valid.
        
        //First mine some coins so we can add valid transactions to it
        var bchain = new Blockchain();
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
        TransactionFactory.createNewTransactionForBlockchain(new TxOut[]
        {
            new TxOut(testPublicKey2, 10),
            new TxOut(testPublicKey3, 3)
        }, testPrivateKey, bchain, 4);
        TransactionFactory.createNewTransactionForBlockchain(new TxOut[]
        {
            new TxOut(testPublicKey3, 1)}, testPrivateKey, bchain);
        Block block = BlockFactory.createAndMineNewBlock(bchain, bchain.mempool.ToArray());
        Assert.IsTrue(bchain.isNewBlockValid(block));

        string? serialized = Serialize.serializeBlockToJson(block);
        Assert.IsNotNull(serialized);
        Block deserialized = Serialize.deserializeJsonToBlock(serialized);
        Assert.IsTrue(block == deserialized); //original block and the deserialized one should be identical
        Assert.IsTrue(bchain.isNewBlockValid(deserialized));
        bool success = bchain.addValidBlock(deserialized);
        Assert.IsTrue(success);
        Assert.IsTrue(bchain.isBlockchainValid());
        
        //Test 2: Create a block with null transactions, and another with empty transactions. Assert both are equal.
        //Then serialize and deserialize both of them, assert both are equal
        Block emptyBlock = BlockFactory.createNewBlock(bchain);
        Block emptyBlock2 = BlockFactory.createNewBlock(bchain, new Transaction[] { });
        Assert.IsTrue(emptyBlock == emptyBlock2);
        string? serializedEmpty = Serialize.serializeBlockToJson(emptyBlock);
        string? serializedEmpty2 = Serialize.serializeBlockToJson(emptyBlock2);
        Block deserializedEmpty = Serialize.deserializeJsonToBlock(serializedEmpty);
        Block deserializedEmpty2 = Serialize.deserializeJsonToBlock(serializedEmpty2);
        Assert.IsTrue(deserializedEmpty == deserializedEmpty2);
        Assert.IsTrue(deserializedEmpty == emptyBlock);
        
        //Assert the deserialized blocks from Test 1 and Test 2 are not equal
        Assert.IsFalse(deserialized == deserializedEmpty);
    }

    [Test]
    public void testBlockchainSerialization()
    {
        //Create a blockchain, mine some blocks. Include txes in some of them
        var bchain = new Blockchain();
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
        TransactionFactory.createNewTransactionForBlockchain(new TxOut[]
        {
            new TxOut(testPublicKey2, 10),
            new TxOut(testPublicKey3, 3)
        }, testPrivateKey, bchain, 4);
        TransactionFactory.createNewTransactionForBlockchain(new TxOut[]
        {
            new TxOut(testPublicKey3, 1)}, testPrivateKey, bchain);
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
        
        //now serialize and deserialize the blockchain. Assert it's identical to the current one
        string? serialized = Serialize.serializeBlockchainToJson(bchain);
        Assert.IsNotNull(serialized);
        Blockchain deserialized = Serialize.deserializeJsonToBlockchain(serialized);
        Assert.IsTrue(bchain == deserialized);
        
        //modify the current bchain, assert it's different to the deserialized one
        bchain.getBlockByIndex(2).nonce++;
        Assert.IsFalse(bchain == deserialized);
    }

    [Test]
    public void testMempoolSerialization()
    {
        //Mine some coins and create some valid txes, which should populate the mempool
        var bchain = new Blockchain();
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
        TransactionFactory.createNewTransactionForBlockchain(new TxOut[]
        {
            new TxOut(testPublicKey2, 10),
            new TxOut(testPublicKey3, 3)
        }, testPrivateKey, bchain, 4);
        TransactionFactory.createNewTransactionForBlockchain(new TxOut[]
        {
            new TxOut(testPublicKey3, 1)}, testPrivateKey, bchain);
        Block block = BlockFactory.createAndMineNewBlock(bchain, bchain.mempool.ToArray());
        Assert.IsTrue(bchain.isNewBlockValid(block));
        Assert.IsTrue(bchain.mempool.Count == 2); //should contain 2 txes
        
        //now serialize and deserialize the mempool, assert it's identical to the local one
        string? serializedMempool = Serialize.serializeMempoolToJson(bchain.mempool);
        Assert.IsNotNull(serializedMempool);
        List<Transaction> deserializedMempool = Serialize.deserializeMempoolToJson(serializedMempool);
        Assert.IsTrue(deserializedMempool.Count == bchain.mempool.Count);
        for (int i = 0; i < deserializedMempool.Count; i++)
        {
            Assert.IsTrue(deserializedMempool[i] == bchain.mempool[i]);
        }
    }
    
    [Test]
    public void Temp()
    {
        
    }
}