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
	 * Sign the input data with the given private key. Returns a string representing the signed data
	 */
	public static string signData(string data, string privateKey)
	{
		using Key key = convertPrivateKeyStringToKey(privateKey);
		
		byte[] dataBytes = Encoding.UTF8.GetBytes(data);
		byte[] sig = algorithm.Sign(key, dataBytes);

		return Convert.ToHexString(sig).ToLower();
	}

	/**
	 * Verify whether the signature, data and public key match. Returns true if a match, false if not
	 */
	public static bool verifySignedData(string signature, string data, string publicKey)
	{
		PublicKey pkey = convertPublicKeyStringToPublicKey(publicKey);
		byte[] dataBytes = Encoding.UTF8.GetBytes(data);
		byte[] signatureBytes = Convert.FromHexString(signature);

		return algorithm.Verify(pkey, dataBytes, signatureBytes);
	}
}