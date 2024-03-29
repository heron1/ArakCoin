﻿using System.Collections.Generic;
using ArakCoin;
using ArakCoin.Data;
using ArakCoin.Transactions;

namespace TestSuite.IntegrationTests;

[TestFixture]
[Category("IntegrationTests")]
public class DataIntegrationTests
{
    [SetUp]
    public void Setup()
    {
        Settings.allowParallelCPUMining = true; //all tests should be tested with parallel mining enabled
        Settings.nodePublicKey = testPublicKey;
    }
    
    [Test]
    public void TestBlockSerialization()
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
    public void TestBlockchainSerialization()
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
        BlockFactory.mineNextBlockAndAddToBlockchain(bchain);
        Assert.IsFalse(bchain == deserialized);
    }

    [Test]
    public void TestMempoolSerialization()
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
        List<Transaction> deserializedMempool = Serialize.deserializeJsonToMempool(serializedMempool);
        Assert.IsTrue(deserializedMempool.Count == bchain.mempool.Count);
        for (int i = 0; i < deserializedMempool.Count; i++)
        {
            Assert.IsTrue(deserializedMempool[i] == bchain.mempool[i]);
        }
    }
    
    [Test]
    public void TestDataStorage()
    {
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
        
        //Test 1: store the mempool on disk, assert it can be retrieved and is identical to local copy in memory
        string? serializedMempool = Serialize.serializeMempoolToJson(bchain.mempool);
        Assert.IsNotNull(serializedMempool);
        bool success = Storage.writeJsonToDisk(serializedMempool, "mempool.json");
        Assert.IsTrue(success);
        string json = Storage.readJsonFromDisk("mempool.json");
        Assert.IsNotNull(json);
        List<Transaction> deserializedMempool = Serialize.deserializeJsonToMempool(json);
        Assert.IsNotNull(deserializedMempool);
        Assert.IsTrue(deserializedMempool.Count == bchain.mempool.Count);
        for (int i = 0; i < deserializedMempool.Count; i++)
        {
            Assert.IsTrue(deserializedMempool[i] == bchain.mempool[i]);
        }
        
        //Test 2: store the mined block on disk, assert it can be retrieved and is identical to local copy in memory
        string? serializedBlock = Serialize.serializeBlockToJson(block);
        Storage.writeJsonToDisk(serializedBlock, "block.json");
        json = Storage.readJsonFromDisk("block.json");
        Block deserializedBlock = Serialize.deserializeJsonToBlock(json);
        Assert.IsTrue(deserializedBlock == block);
        
        //Test 3: Store the entire blockchain on disk, assert it can be retrieved and is identical to local copy in
        //memory
        string? serializedBlockchain = Serialize.serializeBlockchainToJson(bchain);
        Storage.writeJsonToDisk(serializedBlockchain, "blockchain.json");
        json = Storage.readJsonFromDisk("blockchain.json");
        Blockchain deserializedBlockchain = Serialize.deserializeJsonToBlockchain(json);
        Assert.IsTrue(deserializedBlockchain == bchain);
        Assert.IsTrue(deserializedBlockchain.isBlockchainValid());
    }

    [Test]
    public void TestSettingsFile()
    {
        //set the settings filename to a test setting name, which won't interfere with any real settings file
        Settings.jsonFilename = "test_settings.json";
        
        //delete test files if they exist
        Storage.deleteFile(Settings.jsonFilename);
        Storage.deleteFile($"invalid_{Settings.jsonFilename}");
        
        //create the settings file as the test version
        Settings.loadSettingsFileAtRuntime();
        
        //keep track of the original port
        int originalPort = Settings.nodePort;
        
        //change the settings port number to a different value, save test settings file to disk
        int testPort = originalPort + 1;
        Settings.nodePort = testPort;
        Settings.saveRuntimeSettingsToSettingsFile();
        
        //change the port number again, reload the test settings file from disk, assert old value is loaded
        Settings.nodePort = testPort + 2;
        Assert.IsTrue(Settings.nodePort != testPort);
        Settings.loadSettingsFileAtRuntime();
        Assert.IsTrue(Settings.nodePort == testPort);
        
        //attempt to deserialize a junk settings file, assert at least the port isn't corrupted
        Storage.writeJsonToDisk("some bad data", Settings.jsonFilename);
        Settings.loadSettingsFileAtRuntime();
        Assert.IsTrue(Settings.nodePort == testPort);
        //in addition, a backup of the junk settings file should have been saved correctly
        string? junkData = Storage.readJsonFromDisk($"invalid_{Settings.jsonFilename}");
        Assert.IsNotNull(junkData);
        Assert.IsTrue(junkData == "some bad data");
        
        Settings.nodePort = originalPort; //set port back to original value
    }
    
    [Test]
    public void Temp()
    {
       


    }
}