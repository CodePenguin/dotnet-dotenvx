using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference sources: dotenvx/src/lib/helpers/protect.js (plaintextKeys) and src/lib/envfile/protection/protectEnvfile.js. Not from a
/// JS test (upstream covers them through the protect action and the Git filter); the Git-facing parts are compared with the real
/// Node CLI by PtyParityTest.
/// </remarks>
[Collection("Fsx")]
public class ProtectTest : EnvironmentTestBase
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dotenvx-protect-" + Guid.NewGuid().ToString("N"));

    public ProtectTest()
    {
        Directory.CreateDirectory(_dir);
    }

    ~ProtectTest()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }

    private string P(string name) => Path.Combine(_dir, name);

    [Fact]
    public void PlaintextKeysListsOnlyKeysWithAPlainValue()
    {
        const string content = "PLAIN=hello\nSEALED=\"encrypted:abc\"\nEMPTY=\nOP=op://vault/item\nBW=bw://item\nDOTENV_PUBLIC_KEY=\"02abc\"\n" +
                               "DOTENV_PRIVATE_KEY=1fc1cafa\n";

        Protect.PlaintextKeys(content).ShouldBe(["PLAIN", "DOTENV_PRIVATE_KEY"]);
    }

    [Fact]
    public void PlaintextKeysCountsAKeyWithAnyPlainValue()
    {
        Protect.PlaintextKeys("A=\"encrypted:abc\"\nA=plain\n").ShouldBe(["A"]);
    }

    [Fact]
    public void ProtectEnvfileHasNoOpinionWithoutAnEnvfile()
    {
        File.WriteAllText(P(".env"), "A=1\n");

        Protect.ProtectEnvfile(P(".env"), "A=1\n").ShouldBeNull();
    }

    [Fact]
    public void ProtectEnvfileReportsPlaintextKeysTheEnvfileRequiresEncrypted()
    {
        File.WriteAllText(P("Envfile"), "strict true\n\nenv \"A\"\nenv \"B\", encrypted: false\n");
        File.WriteAllText(P(".env"), "A=1\nB=2\nC=3\n");

        // A is declared (encrypted by default) and C is not declared at all; B is allowed to stay plain
        Protect.ProtectEnvfile(P(".env"), "A=1\nB=2\nC=3\n").ShouldBe(["A", "C"]);
    }

    [Fact]
    public void ProtectEnvfileAlwaysProtectsPrivateKeys()
    {
        File.WriteAllText(P("Envfile"), "strict true\n\nenv \"DOTENV_PRIVATE_KEY\", encrypted: false\n");

        Protect.ProtectEnvfile(P(".env"), "DOTENV_PRIVATE_KEY=1fc1cafa\n").ShouldBe(["DOTENV_PRIVATE_KEY"]);
    }

    [Fact]
    public void ProtectEnvfileRefusesADanglingEnvfileLink()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.CreateSymbolicLink(P("Envfile"), P("missing"));

        Should.Throw<Exception>(() => Protect.ProtectEnvfile(P(".env"), "A=1\n")).Message
            .ShouldBe($"Cannot read Envfile: {P("Envfile")}");
    }
}
