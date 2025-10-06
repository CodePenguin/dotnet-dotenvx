using System.Text;
using dotnet_dotenvx.Logic;
using Shouldly;

namespace dotnet_dotenvx.Test;

public class EciesJsTest
{
    [Theory]
    [InlineData(
        "bd7c50b352ce23973ec9db355d70212305a0baaade92f0165f02915b213bfbe2",
        "BGMyAFNH6UjetjWsYHUkbndQosw/barJwdynj9wXQmlucqsM2TxCAxCwXNKQfbA8axd2TCcUJaWVltrhcUZdtDo87caC7aN2G9D7m3/vQVpcCffdYeaKtlrGyGB9IHJzKOrXH3jEheKQBPLv1L6foObYREAeRzw=",
        "production (encrypted)"
        )]
    [InlineData(
        "1fc1cafa954a7a2bf0a6fbff46189c9e03e3a66b4d1133108ab9fcdb9e154b70",
        "BMVCQpz/+NYDcGZhbXyqbwP8IDJSTXl4xDQsgusQHEVFAWOXQnKRBTOzRiwuYIJzjuWnKkrQJEDEi8Av9xnfx61jVTJymVWLjVmFK7CM+6lmKOnIhPMzu0Mi0dH82P81bOXjkZTHIIcA",
        "expanded")]
    public void TestDecryptKnown(string privateKeyHex, string encryptedB64, string expected)
    {
        var encrypted = Convert.FromBase64String(encryptedB64);
        var decrypted = EciesJs.Decrypt(privateKeyHex, encrypted);
        var plainText = Encoding.ASCII.GetString(decrypted);
        plainText.ShouldBe(expected);
    }
}