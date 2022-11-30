using ArakCoin.Networking;

namespace ArakCoin;

/**
 * The default user settings values which will be overriden on program startup from the settings.json file in the
 * application folder. Note if a settings.json file doesn't exist, it will be automatically generated within the
 * associated application folder for the OS this program is installed on (see README.md), with the default values
 * listed here. Once the settings file has been generated, it can be edited (don't edit the settings from this class,
 * edit them from the generated settings.json file as that is where the runtime settings values are read from).
 *
 * The default values here are examples only - end users are encouraged to change them to their liking within the
 * generated settings.json file (not here), particularly the public and private keys, and ensure there is at least one
 * valid startingNode that is online. If the user wishes to be a node, then the nodeIp must be set to the user's
 * publicly accessible IP address, along with the nodePort, for TCP sockets connections.
 */
public class Settings
{
	public static string jsonFilename = "settings.json";

	/**
	 * Returns true if a settings file was loaded, false if a default settings file had to be created instead (with
	 * the default values from Settings.cs). If no default settings file was able to be created, this method will
	 * throw an unconditional exception
	 */
	public static bool loadSettingsFileAtRuntime()
	{
		//attempt to load settings file from the application folder and populate the settings values here at runtime
		
		var jsonSettings = Storage.readJsonFromDisk(jsonFilename); //read the serialized settings file from disk
		if (jsonSettings is null)
		{
			//file doesn't exist, create it and populate it with default values within this class.
			//Exception thrown if this fails
			generateNewSettingsFileOnDisk();
			
			return false;
		}

		//prevent default lists from being read before deserializing the file to disk
		Settings.startingNodes = new List<Host>();
		Settings.manuallyBlacklistedNodes = new List<Host>();
		
		bool success = Serialize.deserializeJsonToSettings(jsonSettings); //deserialize it and put values in memory
		if (!success)
		{
			//file format is invalid, back it up and replace with a new settings file that is valid with default values
			Storage.writeJsonToDisk(jsonSettings, $"invalid_{jsonFilename}");
			generateNewSettingsFileOnDisk(); //throws exception if generating new settings file fails
			
			return false;
		}
		
		//if none of the above conditionals were triggered, we should have successfully loaded the user settings here
		Utilities.log($"Successfully loaded settings file {jsonFilename} from disk..");
		return true;
	}

	/**
	 * Attempt to save the settings in memory to settings file on disk. Returns whether this succeeded or not
	 */
	public static bool saveRuntimeSettingsToSettingsFile()
	{
		var serializedSettings = Serialize.serializeSettingsToJson();
		if (serializedSettings is null)
			return false;

		if (!Storage.writeJsonToDisk(serializedSettings, jsonFilename))
			return false;

		return true;
	}

	/**
	 * Attempts to generate a new settings file on disk. If this fails an unconditional exception is thrown, regardless
	 * of the terminateProgramOnExceptionLog value, as the program cannot operate without a user settings file.
	 */
	private static void generateNewSettingsFileOnDisk()
	{
		var serializedSettings = Serialize.serializeSettingsToJson();
		if (serializedSettings is null)
		{
			string errorMsg = "Could not serialize Settings.cs file, program cannot operate, exiting..";
			Utilities.exceptionLog(errorMsg);
			
			throw new Exception(errorMsg); 
		}

		if (!Storage.writeJsonToDisk(serializedSettings, jsonFilename))
		{
			string errorMsg = $"Could not write {jsonFilename} to disk, program cannot operate, exiting..";
			Utilities.exceptionLog(errorMsg);
			
			throw new Exception(errorMsg); 
		}
		
		Utilities.log($"Successfully generated new settings file {jsonFilename}..");
	}
	
	/**
	 * Decide whether to throw an exception to terminate the program after Utilities.exceptionLog has been called
	 */
	[JsonProperty]
	public static bool terminateProgramOnExceptionLog = false;

	/**
	 * Decide whether standard log (but not ExceptionLog) messages are displayed or not
	 */
	[JsonProperty]
	public static bool displayLogMessages = true;

	/**
	 * Sets whether this host is a node or not. If set to true, it will undergo network operations such as block
	 * validation and communicating with other nodes as per the blockchain's P2P networking protocol. Note that
	 * this host can still be a miner without being a node (ie: it will communicate mined blocks to the network but
	 * will not partake in block validation or any other network operations) - however this is not recommended as the
	 * miner will be lacking updated transactions and blocks that are being continouously shared throughought the
	 * network among nodes. Other nodes may also choose to give this host lower priority in the event it does
	 * mine a block, but it isn't a recognized node.
	 */
	[JsonProperty]
	public static bool isNode = true;

	/**
	 * Specify whether this host will attempt to mine the next block in the background and broadcast it. Note that
	 * if the isNode property is set to false and this is true, then there is no guarantee that the block being mined
	 * is still valid for the consensus chain, as the host won't be participating in node P2P block sharing. Note it
	 * usually only makes sense for a host to be a node and not a miner to be able to immediately verify transactions
	 * as valid for receiving payments, otherwise both properties are recommended to be set to true or false.
	 */
	[JsonProperty]
	public static bool isMiner = true;
	
	/**
	 * The ipv4 address of this host for network communication.
	 * Set this manually if the incorrect IP is being inferred from the Utilities.getLocalIpAddress() function
	 */
	[JsonProperty]
	public static string nodeIp = "192.168.1.19";

	/**
	 * The default port to use for network communication as a node
	 */
	[JsonProperty]
	public static int nodePort = 8000;

	/**
	 * The public key to receive coins if this host mines a block
	 */
	[JsonProperty]
	public static string nodePublicKey = "1f62745d8f64ac7c9e28a17ad113cb2e4d1bd85e6eb6896f58de3bf3cabcd1b9";

	/**
	 * The private key to sign transactions this host creates
	 */
	[JsonProperty]
	public static string nodePrivateKey = "125ddf4ff1dca068ff72ab0a9dafe54170c3b3315326a0f8945a33db77eefd6b";

	/**
	 * Reject mempool transactions without a threshold miner fee. Only useful for miners
	 */
	[JsonProperty]
	public static int minMinerFee = 0;

	/**
	 * Limits the size of the mempool.
	 * If the mempool is full, a received transaction will only override the lowest priority one if its fee is higher
	 */
	[JsonProperty] public static int maxMempoolSize = 25;

	/**
	 * Time out a network communication action after waiting this number of milliseconds
	 */
	[JsonProperty]
	public static int networkCommunicationTimeoutMs = 2000;

	/**
	 * This setting is only applicable if the isNode property is set to true.
	 * Node will attempt to acquire a list of hosts files from all known nodes in its own hosts file, and also
	 * register itself with them every this number of seconds. Setting this value too low may be considered spam
	 * and result in the node's IP being blacklisted by some nodes. Recommended to leave as default value.
	 */
	[JsonProperty]
	public static int nodeDiscoveryDelaySeconds = 10;
	
	/**
	 * This setting is only applicable if the isNode property is set to true.
	 * Node will broadcast its mempool to all other known nodes in its hosts file every this number of seconds, only
	 * if the mempool has changed since the last broadcast. Setting this value too low may be considered spam
	 * and result in the node's IP being blacklisted by some nodes, however setting it too high and the node may
	 * be viewed as not cooperating in mempool sharing. Recommended to leave as default value.
	 */
	[JsonProperty]
	public static int mempoolSharingDelaySeconds = 10;

	/**
	 * The number of characters this node will allow for a valid ECHO request/response. It's recommended to leave this
	 * at the default value
	 */
	[JsonProperty]
	public static int echoCharLimit = 1000;

	/**
	 * In order for P2P network discovery to take place, this client must know at least one node that it can
	 * communicate with. From there, if this client is also a node, it will automatically register itself as another
	 * node in the P2P network. Whilst only one online node is required to kickstart the node P2P discovery protocol,
	 * adding more than one here is possible.
	 *
	 * Note: It is *not* needed to store a list of all known nodes in the blockchain here, this process will happen
	 * automatically by the blockchain's P2P discovery protocol, and does not need to be done manually. Only a single
	 * known online node is required to "kickstart" this process. Once new nodes are automatically discovered, they
	 * will automatically be added to a separate hosts file which will be loaded every time this program is started.
	 *
	 * Note that whilst the hosts file will dynamically change as nodes enter and leave the network, the list of
	 * starting nodes entered here will always exist as part of the hosts file.
	 */
	[JsonProperty]
	public static List<Host> startingNodes = new List<Host>()
	{
		new Host("192.168.1.19", 8000),
		new Host("20.173.66.180", 8000)
	};

	/**
	 * Blacklisted nodes can always be added by calling the HostsManager.addNodeToBlacklist function. The protocol
	 * may also automatically add malicious nodes to the blacklists file via that method.
	 * Nevertheless, if some nodes should always be manually blacklisted without calling that method,
	 * they can be entered here
	 */
	[JsonProperty]
	public static List<Host> manuallyBlacklistedNodes = new List<Host>()
	{
		//these nodes are used in networking integration tests, so we blacklist them for the live chain
		new Host("1.1.1.1", 9000),
		new Host("2.2.2.2", 9000)
	};
}