using ArakCoin;

namespace TestSuite.UnitTests;

[TestFixture]
[Category("UnitTests")]
public class CryptographyUnitTests
{

	[Test]
	public void TestSignVerifyData()
	{
		(string pub, string priv) = Cryptography.generatePublicPrivateKeyPair();
		(string pub2, string priv2) = Cryptography.generatePublicPrivateKeyPair();

		string dataCorrect = "im the intended message";
		string dataIncorrect = "im not";

		string signedData = Cryptography.signData(dataCorrect, priv);
		string signedDataIncorrect = Cryptography.signData(dataIncorrect, priv);

		Assert.IsFalse(Cryptography.verifySignedData(signedDataIncorrect, dataCorrect, pub));
		Assert.IsFalse(Cryptography.verifySignedData(signedData, dataIncorrect, pub));
		Assert.IsFalse(Cryptography.verifySignedData(signedData, dataCorrect, pub2));
		Assert.IsFalse(Cryptography.verifySignedData(signedData, dataCorrect, priv));
		Assert.IsTrue(Cryptography.verifySignedData(signedData, dataCorrect, pub));
	}
}