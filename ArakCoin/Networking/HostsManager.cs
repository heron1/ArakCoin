using ArakCoin.Data;

namespace ArakCoin.Networking;

/**
 * This class should contain an easy way to read and write nodes to/from a hosts file, and store them in memory
 * for P2P node communication in this program.
 */
public static class HostsManager
{
    private static List<Host> nodes = new List<Host>();
    private static string hostsFilename = "hostsfile.json";
    private static string blacklistedHostsFilename = "hostsblacklist.json";
    public static readonly object hostsLock = new object(); //lock for critical sections on the hosts file


    static HostsManager()
    {
        //attempt to load nodes from the hostsfile on disk at program start. This will additionally ensure that the
        //starting nodes in the Settings.startingNodes field exist in the hosts file. Also, if this host is a
        //node, it will include itself as well in the hosts file. Note that if no nodes are successfully
        //loaded, the nodes list will be empty at program start. Will also remove any blacklisted nodes
        
        loadNodes(); //load the nodes from disk into memory

        //add any hard coded blacklisted nodes to the blacklist
        List<Host>? blacklistedNodesLoaded = loadBlacklistedNodesFromDisk();
        var blacklistedNodes = new List<Host>();
        if (blacklistedNodesLoaded is not null)
        {
            blacklistedNodes = blacklistedNodesLoaded;
        }
        foreach (var blacklistedNode in Settings.manuallyBlacklistedNodes)
        {
            if (!blacklistedNodes.Contains(blacklistedNode))
                addNodeToBlacklist(blacklistedNode);
        }
        
        //add any hard coded starting nodes
        foreach (var host in Settings.startingNodes)
        {
            if (!nodes.Contains(host))
            {
                addNode(host);
            }
        }

        //add this host to the hosts file if it is set to be a node 
        if (Settings.isNode && !nodes.Contains(new Host(Settings.nodeIp, Settings.nodePort)))
            addNode(new Host(Settings.nodeIp, Settings.nodePort));
    }
    
     /**
     * Saves the nodes in memory to the hostsfile on disk. If the hostsfile dosen't exist, it will be created.
     * Returns whether the operation succeeded
     */
    private static bool saveNodes()
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
     * succeeded. Will filter out any blacklisted nodes if a blacklist exists.
     */
    private static bool loadNodes()
    {
        lock (hostsLock)
        {
            //load nodes
            string? jsonNodes = Storage.readJsonFromDisk(hostsFilename);
            if (jsonNodes is null)
                return false;

            List<Host>? deserializedNodes = Serialize.deserializeJsonToHosts(jsonNodes);
            if (deserializedNodes is null)
                return false;

            nodes = deserializedNodes;
            
            //load blacklist, remove any nodes in it from the loaded nodes
            var blacklistedNodes = loadBlacklistedNodesFromDisk();
            if (blacklistedNodes is null)
                return true; //the blacklist doesn't exist, so stop here and return true 

            foreach (var blacklistedNode in blacklistedNodes)
            {
                nodes.Remove(blacklistedNode);
            }
            
            return true;
        }
    }
    
    /** 
     * Loads blacklisted nodes from disk and returns them as a list of Hosts. Returns null if this fails
     */
    private static List<Host>? loadBlacklistedNodesFromDisk()
    {
        lock (hostsLock)
        {
            var jsonNodes = Storage.readJsonFromDisk(blacklistedHostsFilename);
            if (jsonNodes is null)
                return null;

            var blacklistedDeserializedNodes = Serialize.deserializeJsonToHosts(jsonNodes);
            if (blacklistedDeserializedNodes is null)
                return null;

            return blacklistedDeserializedNodes;
        }
    }

    public static List<Host> getNodes()
    {
        //make a copy of the nodes so that if the internal list mutates from another thread,
        //the caller doesn't encounter a changing list
        lock (hostsLock)
        {
            loadNodes(); //reload nodes from disk
            return nodes.ToList();
        }
    }

    public static List<Host>? getBlacklistedNodes()
    {
        lock (hostsLock)
        {
            return loadBlacklistedNodesFromDisk();
        }
    }

    /**
     * Adds the given node to memory if it doesn't yet exist, and writes it to the hostsfile on disk as well.
     * Returns whether the node was succesfully written to the hostsfile or not. Does not add a node if it's
     * blacklisted
     */
    public static bool addNode(Host node)
    {
        lock (hostsLock)
        {
            if (nodes.Contains(node)) //node already exists
                return false;

            var blacklistedNodes = loadBlacklistedNodesFromDisk();
            if (blacklistedNodes is not null)
            {
                //blacklist exists, so see if the proposed node to be added resides within it
                if (blacklistedNodes.Contains(node))
                    return false; //node is blacklisted, do not add it
            }
            
            nodes.Add(node);
            bool success = saveNodes();
            if (success)
                Utilities.log($"Successfully registered node {node} in the hosts file..");

            return success;
        }
    }

    /**
     * Adds the given node to the blacklisted nodes on disk, and also removes it if it exists within the current nodes
     */
    public static bool addNodeToBlacklist(Host blacklistedNode)
    {
        lock (hostsLock)
        {
            var blacklistedNodes = new List<Host>();
            var blackListedNodesLoaded = loadBlacklistedNodesFromDisk();
            if (blackListedNodesLoaded is not null)
                blacklistedNodes = blackListedNodesLoaded;

            if (blacklistedNodes.Contains(blacklistedNode)) //node already added to blacklist
                return false;
            
            blacklistedNodes.Add(blacklistedNode);
            var jsonNodes = Serialize.serializeHostsToJson(blacklistedNodes);
            if (jsonNodes is null)
                return false;

            if (!Storage.writeJsonToDisk(jsonNodes, blacklistedHostsFilename))
                return false;

            //remove the blacklisted node from the current nodes if it exists there
            removeNode(blacklistedNode);

            return true;
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
     * Attempts to remove the given node the blacklist.
     * Returns true if the operation removed a node, false otherwise
     */
    public static bool removeNodeFromBlacklist(Host node)
    {
        lock (hostsLock)
        {
            var blacklistedNodes = loadBlacklistedNodesFromDisk();
            if (blacklistedNodes is null)
                return false; //no blacklist exists, so no node can be removed from it

            if (!blacklistedNodes.Remove(node))
                return false; //node is not blacklisted
            
            var jsonNodes = Serialize.serializeHostsToJson(blacklistedNodes);
            if (jsonNodes is null)
                return false; //serialization failure

            if (!Storage.writeJsonToDisk(jsonNodes, blacklistedHostsFilename))
                return false;
            
            Utilities.log($"Successfully removed node {node} from the node blacklist file..");
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