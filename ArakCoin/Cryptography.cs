using System.Text;
using NSec.Cryptography;

namespace ArakCoin;

/**
 * This class will use Elliptic Curve Ed25519 for public key cryptography via the NSec package.
 * This class serves as a wrapper such that it should be interchangeable with any other public key cryptography
 * solution in the future without changing the public method signatures
 */
public static class Cryptography
{
	static readonly Ed25519 algorithm = SignatureAlgorithm.Ed25519;

	private static Key convertPrivateKeyStringToKey(string privateKey)
	{
		byte[] privateKeyBytes = Convert.FromHexString(privateKey);
		return Key.Import(algorithm, privateKeyBytes, KeyBlobFormat.RawPrivateKey);
	}

	private static PublicKey convertPublicKeyStringToPublicKey(string publicKey)
	{
		byte[] publicKeyBytes = Convert.FromHexString(publicKey);
		return PublicKey.Import(algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey);
	}

	/**
	 * Generate a new public/private keypair in string format
	 */
	public static (string publicKey, string privateKey) generatePublicPrivateKeyPair()
	{
		var creationParameters = new KeyCreationParameters
		{
			ExportPolicy = KeyExportPolicies.AllowPlaintextArchiving
		};

		using Key key = new Key(algorithm, creationParameters); // allow key export

		byte[] publickey = key.Export(KeyBlobFormat.RawPublicKey);
		byte[] privateKey = key.Export(KeyBlobFormat.RawPrivateKey);

		string publicKeyString = Convert.ToHexString(publickey).ToLower();
		string privateKeyString = Convert.ToHexString(privateKey).ToLower();

		return (publicKeyString, privateKeyString);
	}

	/**
	 * Verify the input private and public keys are paired
	 */
	public static bool testKeypair(string publicKey, string privateKey)
	{
		//test data can be signed correctly
		string data = "dataTest";
		string? signature = Cryptography.signData(data, privateKey);
		if (signature is null)
			return false;

		if (!Cryptography.verifySignedData(signature, data, publicKey))
			return false;
		
		//test the public key can be derived from the private key
		return (getPublicKeyFromPrivateKey(privateKey) == publicKey);
	}

	/**
	 * Sign the input data with the given private key. Returns a string representing the signed data. If the operation fails,
	 * will return null
	 */
	public static string? signData(string data, string privateKey)
	{
		try
		{
			using Key key = convertPrivateKeyStringToKey(privateKey);

			byte[] dataBytes = Encoding.UTF8.GetBytes(data);
			byte[] sig = algorithm.Sign(key, dataBytes);

			return Convert.ToHexString(sig).ToLower();
		}
		catch (FormatException e)
		{
			return null;
		}
	}

	/**
	 * Returns the public key from the given private key. If private key is in invalid format, returns null
	 */
	public static string? getPublicKeyFromPrivateKey(string privateKey)
	{
		try
		{
			using Key key = convertPrivateKeyStringToKey(privateKey);
			byte[] publickey = key.Export(KeyBlobFormat.RawPublicKey);

			return Convert.ToHexString(publickey).ToLower();
		}
		catch (FormatException e)
		{
			return null;
		}
	}

	/**
	 * Verify whether the input data was signed by the private key belonging to the corresponding public key, based
	 * upon the input signature.
	 * Returns true if so, false if not. If the operation fails, will also return false
	 */
	public static bool verifySignedData(string signature, string data, string publicKey)
	{
		try
		{
			PublicKey pkey = convertPublicKeyStringToPublicKey(publicKey);
			byte[] dataBytes = Encoding.UTF8.GetBytes(data);
			byte[] signatureBytes = Convert.FromHexString(signature);

			return algorithm.Verify(pkey, dataBytes, signatureBytes);
		}
		catch (FormatException e)
		{
			return false;
		}
	}
}