namespace ArakCoin.Networking;

/**
 * This class should contain an easy way to read and write nodes to/from a hosts file, and store them in memory
 * for P2P node communication in this program.
 */
public static class HostsManager
{
    private static List<Host> nodes = new List<Host>();
    private static string hostsFilename = "hostsfile.json";
    public static readonly object hostsLock = new object(); //lock for critical sections on the hosts file


    static HostsManager()
    {
        //attempt to load nodes from the hostsfile on disk at program start. This will additionally ensure that the
        //starting nodes in the Settings.startingNodes field exist in the hosts file. Also, if this host is a
        //node, it will include itself as well in the hosts file. Note that if no nodes are successfully
        //loaded, the nodes list will be empty at program start.
        loadNodes();
        foreach (var host in Settings.startingNodes)
        {
            if (!nodes.Contains(host))
            {
                addNode(host);
            }
        }

        if (Settings.isNode && !nodes.Contains(new Host(Settings.nodeIp, Settings.nodePort)))
            addNode(new Host(Settings.nodeIp, Settings.nodePort));
    }

    public static List<Host> getNodes()
    {
        //make a copy of the nodes so that if the internal list mutates, the caller doesn't encounter a changing list
        lock (hostsLock)
        {
            List<Host> currentNodes;
            currentNodes = nodes.ToList();

            return currentNodes;
        }
    }

    /**
     * Adds the given node to memory if it doesn't yet exist, and writes it to the hostsfile on disk as well.
     * Returns whether the node was succesfully written to the hostsfile or not
     */
    public static bool addNode(Host node)
    {
        lock (hostsLock)
        {
            if (nodes.Contains(node))
                return false;
            nodes.Add(node);
            bool success = saveNodes();
            if (success)
                Utilities.log($"Successfully registered node {node} in the hosts file..");

            return success;
        }
    }

    /**
     * Attempts to remove the given node from memory, and remove it from the hostsfile on disk as well.
     * Returns true if the operation removed a node, false otherwise
     */
    public static bool removeNode(Host node)
    {
        lock (hostsLock)
        {
            if (!nodes.Remove(node))
                return false;

            bool success = saveNodes();
            if (success)
                Utilities.log($"Successfully removed node {node} in the hosts file..");

            return success;
        }
    }

    /**
     * Saves the nodes in memory to the hostsfile on disk. If the hostsfile dosen't exist, it will be created.
     * Returns whether the operation succeeded
     */
    public static bool saveNodes()
    {
        lock (hostsLock)
        {
            var jsonNodes = Serialize.serializeHostsToJson(nodes);
            if (jsonNodes is null)
                return false;

            if (!Storage.writeJsonToDisk(jsonNodes, hostsFilename))
                return false;

            return true;
        }
    }

    /*
     * Loads nodes from disk and stores them in the global nodes property. Returns whether the operation
     * succeeded
     */
    public static bool loadNodes()
    {
        lock (hostsLock)
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

    /**
     * Clear stored network nodes both from memory and the hosts file on disk. Returns whether operation succeeded.
     * Note this operation is atomic (either both operations happen, or neither do)
     */
    public static bool clearAllNodes()
    {
        lock (hostsLock)
        {
            var oldNodes = nodes.ToList();
            nodes = new List<Host>();
            if (!saveNodes())
            {
                nodes = oldNodes;
                return false;
            }

            Utilities.log("all nodes cleared..");

            return true;
        }
    }
}