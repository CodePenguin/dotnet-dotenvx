using System.Text;
using dotnet_dotenvx.Logic;
using Shouldly;

namespace dotnet_dotenvx.Test;

public class EciesJsTest
{
    [Fact]
    public void TestDecryptKnown()
    {
        const string privateKeyHex = "bd7c50b352ce23973ec9db355d70212305a0baaade92f0165f02915b213bfbe2";
        const string encryptedB64 = "BGMyAFNH6UjetjWsYHUkbndQosw/barJwdynj9wXQmlucqsM2TxCAxCwXNKQfbA8axd2TCcUJaWVltrhcUZdtDo87caC7aN2G9D7m3/vQVpcCffdYeaKtlrGyGB9IHJzKOrXH3jEheKQBPLv1L6foObYREAeRzw=";
        var encrypted = Convert.FromBase64String(encryptedB64);
        var decrypted = EciesJs.Decrypt(privateKeyHex, encrypted);
        var plainText = Encoding.ASCII.GetString(decrypted);
        plainText.ShouldBe("production (encrypted)");
    }
}