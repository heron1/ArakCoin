using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;

namespace ArakCoin;

public static class Utilities
{
	
	/**
	 * Given any string, will convert it to another string representing its hexadecimal sha256 hash
	 */
	public static string calculateSHA256Hash(string input)
	{
		byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
		return Convert.ToHexString(hashBytes).ToLower();
	}
	
	/**
	 * Returns the current UTC time since epoch (in seconds)
	 */
	public static long getTimestamp()
	{
		return (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
	}

	/**
	 * Given an input difficulty, which represents the leading number of 0s to find in a SHA256 hash, convert this to
	 * the estimated number of hashes that need to be tried to reach said difficulty on average
	 */
	public static BigInteger convertDifficultyToHashAttempts(int difficulty)
	{
		if (difficulty == 0)
			return 0;
		
		return BigInteger.Pow(2, 4 * difficulty);
	}

	/**
	 * Sleep the calling thread for the given number of milliseconds
	 */
	public static void sleep(int milliseconds)
	{
		Thread.Sleep(milliseconds);
	}

	/**
	 * Attempts to automatically find the local ipv4 address of this host from the first found NIC, and return it.
	 */
	public static string getLocalIpAddress()
	{
		return Dns.GetHostEntry(Dns.GetHostName())
			.AddressList
			.First(x => x.AddressFamily == AddressFamily.InterNetwork)
			.ToString();
	}

	/**
	 * Gets a truly random number
	 */
	public static int getTrulyRandomNumber()
	{
		using (RNGCryptoServiceProvider rg = new RNGCryptoServiceProvider())
		{ 
			byte[] rno = new byte[5];    
			rg.GetBytes(rno);    
			return BitConverter.ToInt32(rno, 0); 
		}
	}

	/**
	 * Allows the slicing of a List with the given start (inclusive) and end (exclusive ) index. Does not mutate the
	 * input list, but instead returns a new list. Implementation may change over time to be more efficient. The
	 * generic parameter should be the type of object contained in the list
	 */
	public static List<T> sliceList<T>(List<T> list, int startIndex, int endIndex)
	{
		return list.ToArray()[startIndex..endIndex].ToList();
	}
	
	/**
	 * For general logging, such as for warnings and info, we will call this function to log the message somewhere.
	 * This implementation may change over time, but the same function can still be called.
	 */
	public static void log(string logMsg)
	{
		StringQueue.addToQueue(logMsg);
		if (Settings.displayLogMessages)
		{
			Console.WriteLine(logMsg);
			GlobalHandler.OnLogUpdate(logMsg);
		}
	}

	/**
	 * If we encounter an invalid program state, we will call this function to
	 * log a message and alert the client/node of what's occurred. We may also optionally throw an exception
	 * depending upon the value of terminateProgramOnExceptionLog in setting.json
	 * This implementation may change over time, but the same function can still be called.
	 */
	public static void exceptionLog(string exceptionLog)
	{
		Console.WriteLine($"Exception log message: {exceptionLog}");
		Debug.WriteLine($"Exception log message: {exceptionLog}");
		Console.Error.WriteLine($"Exception log message: {exceptionLog}");
		StringQueue.addToQueue("Exception log message: " + exceptionLog);

		if (Settings.terminateProgramOnExceptionLog)
			throw new Exception($"exceptionLog triggered and terminateProgramOnExceptionLog was set to true. " +
			                    $"exceptionLog message: {exceptionLog}");
	}
}