using Dotenvx.Interop.DotenvxJs.Custodians;
using Dotenvx.Interop.DotenvxJs.Services;

using NSubstitute;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/lib/services/custodyTransfer.js. Not from a JS test (upstream covers it through the
/// custody action tests); these run the real service against files in a temporary directory and an in-memory custodian.
/// </remarks>
[Collection("Fsx")]
public class CustodyTransferTest : EnvironmentTestBase
{
    private const string PrivateKey = "a4547dcd9d3429615a3649bb79e87edb62ee6a74b007075e9141ae44f5fb412c";
    private const string OtherPrivateKey = "1fc1cafa954a7a2bf0a6fbff46189c9e03e3a66b4d1133108ab9fcdb9e154b70";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dotenvx-ct-" + Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string> _store = [];
    private readonly ILocalCustodian _provider = Substitute.For<ILocalCustodian>();

    public CustodyTransferTest()
    {
        Directory.CreateDirectory(_dir);
        Environment.CurrentDirectory = _dir;
        File.WriteAllText(".env", $"DOTENV_PUBLIC_KEY=\"{PublicKey}\"\n");
        _provider.Get(Arg.Any<string>()).Returns(_ => _store);
        _provider.When(provider => provider.Set(Arg.Any<string>(), Arg.Any<string>()))
            .Do(call => _store[call.ArgAt<string>(0)] = call.ArgAt<string>(1));
        _provider.When(provider => provider.Delete(Arg.Any<string>())).Do(call => _store.Remove(call.ArgAt<string>(0)));
    }

    ~CustodyTransferTest()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }

    private static string PublicKey => global::Dotenvx.Primitives.Primitives.Derive(PrivateKey);

    private static string Keys() => File.Exists(".env.keys") ? File.ReadAllText(".env.keys") : "";

    [Fact]
    public void UpMovesTheKeyFromTheKeysFileIntoTheCustodian()
    {
        File.WriteAllText(".env.keys", $"DOTENV_PRIVATE_KEY={PrivateKey}\n");

        CustodyTransferResult result = CustodyTransfer.Run(_provider, "native", "up");

        result.Changed.ShouldBeTrue();
        result.PrivateKeyName.ShouldBe("DOTENV_PRIVATE_KEY");
        result.PublicKeyValue.ShouldBe(PublicKey);
        _store[PublicKey].ShouldBe(PrivateKey);
        Keys().ShouldNotContain("DOTENV_PRIVATE_KEY");
    }

    [Fact]
    public void PushCopiesTheKeyAndKeepsTheKeysFile()
    {
        File.WriteAllText(".env.keys", $"DOTENV_PRIVATE_KEY={PrivateKey}\n");

        CustodyTransfer.Run(_provider, "native", "push").Changed.ShouldBeTrue();

        _store[PublicKey].ShouldBe(PrivateKey);
        Keys().ShouldContain(PrivateKey);
    }

    [Fact]
    public void PushReportsNoChangeWhenTheCustodianAlreadyHoldsTheKey()
    {
        File.WriteAllText(".env.keys", $"DOTENV_PRIVATE_KEY={PrivateKey}\n");
        _store[PublicKey] = PrivateKey;

        CustodyTransfer.Run(_provider, "native", "push").Changed.ShouldBeFalse();
    }

    [Fact]
    public void PushNeedsTheKeyInTheKeysFile()
    {
        Should.Throw<Exception>(() => CustodyTransfer.Run(_provider, "native", "push")).Message
            .ShouldBe("missing DOTENV_PRIVATE_KEY in .env.keys");
    }

    [Fact]
    public void UpNeedsAKeyFromSomewhere()
    {
        Should.Throw<Exception>(() => CustodyTransfer.Run(_provider, "native", "up")).Message
            .ShouldBe("missing DOTENV_PRIVATE_KEY in .env.keys");
    }

    [Fact]
    public void UpRefusesAKeysFileKeyThatDoesNotMatchThePublicKey()
    {
        File.WriteAllText(".env.keys", $"DOTENV_PRIVATE_KEY={OtherPrivateKey}\n");

        Should.Throw<Exception>(() => CustodyTransfer.Run(_provider, "native", "up")).Message
            .ShouldBe("private key does not match the .env public key");
        _store.ShouldBeEmpty();
    }

    [Fact]
    public void UpRefusesWhenTheCustodianCannotConfirmTheStoredKey()
    {
        File.WriteAllText(".env.keys", $"DOTENV_PRIVATE_KEY={PrivateKey}\n");
        ILocalCustodian forgetful = Substitute.For<ILocalCustodian>();
        forgetful.Get(Arg.Any<string>()).Returns(_ => _store);

        Should.Throw<Exception>(() => CustodyTransfer.Run(forgetful, "native", "up")).Message
            .ShouldBe("could not verify private key in native; .env.keys unchanged");
        Keys().ShouldContain(PrivateKey);
    }

    [Fact]
    public void DownMovesTheKeyBackAndRemovesItFromTheCustodian()
    {
        _store[PublicKey] = PrivateKey;

        CustodyTransfer.Run(_provider, "native", "down").Changed.ShouldBeTrue();

        Keys().ShouldContain($"DOTENV_PRIVATE_KEY={PrivateKey}");
        _store.ShouldBeEmpty();
    }

    [Fact]
    public void DownWithTheKeyAlreadyInTheKeysFileOnlyVerifiesIt()
    {
        File.WriteAllText(".env.keys", $"DOTENV_PRIVATE_KEY={PrivateKey}\n");

        CustodyTransfer.Run(_provider, "native", "down").Changed.ShouldBeFalse();
    }

    [Fact]
    public void PullCopiesTheKeyAndLeavesTheCustodianAlone()
    {
        _store[PublicKey] = PrivateKey;

        CustodyTransfer.Run(_provider, "native", "pull").Changed.ShouldBeTrue();

        Keys().ShouldContain(PrivateKey);
        _store[PublicKey].ShouldBe(PrivateKey);
    }

    [Fact]
    public void PullFailsWhenTheCustodianHasNoKey()
    {
        Should.Throw<Exception>(() => CustodyTransfer.Run(_provider, "native", "pull")).Message
            .ShouldBe("[NOT_FOUND] private key not found in native");
    }

    [Fact]
    public void RejectsAnUnknownOperation()
    {
        Should.Throw<Exception>(() => CustodyTransfer.Run(_provider, "native", "sideways")).Message
            .ShouldBe("unknown custody operation");
    }
}
