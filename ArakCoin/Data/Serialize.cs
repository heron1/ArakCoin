using ArakCoin.Networking;
using ArakCoin.Transactions;

namespace ArakCoin;

/**
 * A wrapper class for serializing and deserializing data. Also performs format checking, but no
 * validation. Method signatures should remain unchanged regardless of underlying implementation (currently Newtonsoft)
 */
public static class Serialize
{
    //todo sanity checks - all these methods should assert the types are correctly formatted (not valid)
    //both for serialization and deserialization
    //todo remaining - blockchain formatting
    //todo remaining - mempool formatting
    
    //attempts to serialize the input block into a json string. If this fails, returns null
    public static string? serializeBlockToJson(Block block)
    {
        //first ensure block has a valid hash (this does not validate the block)
        if (block.calculateBlockHash() is null)
            return null;

        return JsonConvert.SerializeObject(block);
    }

    //attempts to deserialize the input string to a block object. If this fails, returns null
    public static Block? deserializeJsonToBlock(string jsonBlock)
    {
        try
        {
            Block? deserializedBlock = JsonConvert.DeserializeObject<Block>(jsonBlock);
            if (deserializedBlock is null)
                return null;
            if (deserializedBlock.calculateBlockHash() is null)
                return null;

            return deserializedBlock;
        }
        catch
        {
            return null;
        }
    }
    
    //attempts to serialize the input blockchain into a json string. If this fails, returns null
    public static string? serializeBlockchainToJson(Blockchain bchain)
    {
        return JsonConvert.SerializeObject(bchain);
    }
    
    //attempts to deserialize the input string to a blockchain object. If this fails, returns null
    public static Blockchain? deserializeJsonToBlockchain(string jsonBlockchain)
    {
        try
        {
            return JsonConvert.DeserializeObject<Blockchain>(jsonBlockchain);
        }
        catch
        {
            return null;
        }
    }
    
    //attempts to serialize a mempool. If this fails, returns null
    public static string? serializeMempoolToJson(List<Transaction> container)
    {
        return JsonConvert.SerializeObject(container);
    }
    
    //attempts to deserialize the input string to a mempool. If this fails, returns null
    public static List<Transaction>? deserializeJsonToMempool(string jsonContainer)
    {
        try
        {
            return JsonConvert.DeserializeObject<List<Transaction>>(jsonContainer);
        }
        catch
        {
            return null;
        }
    }
    
    public static string? serializeNetworkMessageToJson(NetworkMessage networkMessage)
    {
        try
        {
            return JsonConvert.SerializeObject(networkMessage);
        }
        catch
        {
            return null;
        }
    }
    
    public static NetworkMessage? deserializeJsonToNetworkMessage(string jsonNetworkMessage)
    {
        try
        {
            return JsonConvert.DeserializeObject<NetworkMessage>(jsonNetworkMessage);
        }
        catch
        {
            return null;
        }
    }
}