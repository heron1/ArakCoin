using System.Diagnostics;
using System.Numerics;
using System.Text;

namespace ArakCoin;

public class Utilities
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
	 * For general logging, such as for warnings and info, we will call this function to log the message somewhere. This
	 * implementation may change over time, but the same function can still be called.
	 */
	public static void log(string logMsg)
	{
		Console.WriteLine($"Log message: {logMsg}");
		Debug.WriteLine($"Log message: {logMsg}");
	}

	/**
	 * If we encounter an invalid program state, we will call this function to
	 * log a message and alert the client/node of what's occurred. We may also optionally throw an exception
	 * depending upon the value of terminateProgramOnExceptionLog in Setting.cs
	 * This implementation may change over time, but the same function can still be called.
	 */
	public static void exceptionLog(string exceptionLog)
	{
		Console.WriteLine($"Exception log message: {exceptionLog}");
		Debug.WriteLine($"Exception log message: {exceptionLog}");

		if (Settings.terminateProgramOnExceptionLog)
			throw new Exception($"exceptionLog triggered and terminateProgramOnExceptionLog was set to true. " +
			                    $"exceptionLog message: {exceptionLog}");
	}
}