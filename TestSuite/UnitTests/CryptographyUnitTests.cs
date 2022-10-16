using ArakCoin;

namespace TestSuite.UnitTests;

[TestFixture]
[Category("UnitTests")]
public class CryptographyUnitTests
{

	[Test]
	public void TestSignVerifyData()
	{
		LogTestMsg("Testing TestSignVerifyData..");

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

	[Test]
	public void TestErroneousInputs()
	{
		LogTestMsg("Testing TestErroneousInputs..");

		Assert.DoesNotThrow(() => Cryptography.signData("hi", "some 11 wrong key"));
		Assert.DoesNotThrow(() => Cryptography.verifySignedData("not really signed", "ok", 
			"another 22 wrong key"));
		
		Assert.IsNull(Cryptography.signData("hi", "some 11 wrong key"));
		Assert.IsFalse(Cryptography.verifySignedData("not really signed", "ok", 
			"another 22 wrong key"));
	}

	[Test]
	public void TestGenerateKeyPair()
	{
		LogTestMsg("Testing TestGenerateKeyPair..");
		(string publicKey, string privateKey) = Cryptography.generatePublicPrivateKeyPair();
		Assert.IsNotNull(publicKey);
		Assert.IsNotNull(privateKey);

		LogTestMsg($"\tSuccessfully generated\n\t\tpublic key: {publicKey}" +
		           $"\n\t\tprivate key: {privateKey}");
	}
}