using ArakCoin.Transactions;
using Newtonsoft.Json;

namespace ArakCoin;

/**
 * A wrapper class for serializing and deserializing data. Also performs some sanitization checking, but no validation.
 * Method signatures should remain unchanged regardless of underlying serialization implementation (currently Newtonsoft)
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
        //first ensure block has a valid hash (does not validate the block)
        if (block.calculateBlockHash() is null)
            return null;

        return JsonConvert.SerializeObject(block);
    }

    //attempts to deserialize the input string to a block object. If this fails, returns null
    public static Block? deserializeJsonToBlock(string jsonBlock)
    {
        Block? deserializedBlock = JsonConvert.DeserializeObject<Block>(jsonBlock);
        if (deserializedBlock is null)
            return null;
        if (deserializedBlock.calculateBlockHash() is null)
            return null;
        
        return deserializedBlock;
    }
    
    //atempts to serialize the input blockchain into a json string. If this fails, returns null
    public static string? serializeBlockchainToJson(Blockchain bchain)
    {
        return JsonConvert.SerializeObject(bchain);
    }
    
    //attempts to deserialize the input string to a blockchain object. If this fails, returns null
    public static Blockchain? deserializeJsonToBlockchain(string jsonBlockchain)
    {
        return JsonConvert.DeserializeObject<Blockchain>(jsonBlockchain);
    }
    
    //attempts to serialize a mempool. If this fails, returns null
    public static string? serializeMempoolToJson(List<Transaction> container)
    {
        return JsonConvert.SerializeObject(container);
    }
    
    //attempts to deserialize the input string to a mempool. If this fails, returns null
    public static List<Transaction>? deserializeMempoolToJson(string jsonContainer)
    {
        return JsonConvert.DeserializeObject<List<Transaction>>(jsonContainer);
    }
}