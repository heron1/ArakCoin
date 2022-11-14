namespace ArakCoin.Networking;

/**
 * This class should contain an easy way to read and write nodes to/from a hosts file, and store them in memory
 * for P2P node communication in this program.
 */
public static class HostsManager
{
    private static List<Host> nodes = new List<Host>();
    private static string hostsFilename = "hostsfile.json";

    static HostsManager()
    {
        //attempt to load nodes from the hostsfile on disk at program start. If fails, nodes list will be empty
        loadNodes(); 
    }

    public static List<Host> getNodes()
    {
        return nodes;
    }

    /**
     * Adds the given node to memory if it doesn't yet exist, and writes it to the hostsfile on disk as well.
     * Returns whether the node was succesfully written to the hostsfile or not
     */
    public static bool addNode(Host node)
    {
        if (nodes.Contains(node))
            return false;
        nodes.Add(node);
        
        return saveNodes();
    }

    /**
     * Attempts to remove the given node from memory, and remove it from the hostsfile on disk as well.
     * Returns true if the operation removed a node, false otherwise
     */
    public static bool removeNode(Host node)
    {
        if (!nodes.Remove(node))
            return false;
        
        return saveNodes();
    }

    /**
     * Saves the nodes in memory to the hostsfile on disk. If the hostsfile dosen't exist, it will be created.
     * Returns whether the operation succeeded
     */
    public static bool saveNodes()
    {
        var jsonNodes = Serialize.serializeHostsToJson(nodes);
        if (jsonNodes is null)
            return false;

        if (!Storage.writeJsonToDisk(jsonNodes, hostsFilename))
            return false;

        return true;
    }

    /*
     * Loads nodes from disk and stores them in the global nodes property. Returns whether the operation
     * succeeded
     */
    public static bool loadNodes()
    {
        string? jsonNodes = Storage.readJsonFromDisk(hostsFilename);
        if (jsonNodes is null)
            return false;

        List<Host>? deserializedNodes = Serialize.deserializeJsonToHosts(jsonNodes);
        if (deserializedNodes is null)
            return false;

        nodes = deserializedNodes;
        return true;
    }
}