using ArakCoin.Networking;
using ArakCoin.Transactions;

namespace ArakCoin;

/**
 * A wrapper class for serializing and deserializing data. Also performs format checking, but no validation.
 * Method signatures should remain unchanged regardless of underlying implementation (currently Newtonsoft)
 */
public static class Serialize
{
    //todo sanity checks - all these methods should assert the types are correctly formatted (not valid)
    //both for serialization and deserialization
    //todo remaining - blockchain formatting
    //todo remaining - mempool formatting

    static Serialize()
    {
        JsonConvert.DefaultSettings = () => new JsonSerializerSettings
        {
            MaxDepth = 128, //fix DOS exploit in Newtonsoft
            MissingMemberHandling = MissingMemberHandling.Error //only load complete JSON objects
        }; 
    }
    
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
    
    public static string? serializeHostToJson(Host host)
    {
        return serializeHostsToJson(new List<Host>(1) { host });
    }

    public static Host? deserializeJsonToHost(string jsonHost)
    {
        var hosts = deserializeJsonToHosts(jsonHost);
        if (hosts is null)
            return null;
        if (hosts.Count != 1)
            return null;

        return hosts[0];
    }

    public static string? serializeHostsToJson(List<Host> hosts)
    {
        try
        {
            return JsonConvert.SerializeObject(hosts);
        }
        catch
        {
            return null;
        }
    }
    
    public static List<Host>? deserializeJsonToHosts(string jsonHosts)
    {
        List<Host> hostList;
        try
        {
            hostList = JsonConvert.DeserializeObject<List<Host>>(jsonHosts);
        }
        catch
        {
            return null;
        }

        foreach (var host in hostList)
        {
            if (!host.validateHostFormatting())
                return null;
        }

        return hostList;
    }

    /**
     * This performs serializaion of the static Settings.cs members - no input argument is necessary. Since the
     * serialized settings file is intended to be edited by users, it is made human readable
     */
    public static string? serializeSettingsToJson()
    {
        try
        {
            return JsonConvert.SerializeObject(new Settings(), Formatting.Indented);
        }
        catch
        {
            return null;
        }
    }

    /**
     * This performs static deserialization into the Settings.cs file's static members from the input json - no type is
     * returned but instead a boolean as to whether the settings were successfully loaded or not
     */
    public static bool deserializeJsonToSettings(string jsonSettings)
    {
        try
        {
            JsonConvert.DeserializeObject<Settings>(jsonSettings);
        }
        catch
        {
            return false;
        }

        return true;
    }
}
