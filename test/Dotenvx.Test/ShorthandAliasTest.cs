using Dotenvx.Actions;
using Dotenvx.Core.Test;

using Shouldly;

namespace dotnet_dotenvx.Test;

/// <remarks>
/// Reference source: dotenvx/tests/cli/dotenvx.test.js. Not ported, because they cover the Dotenvx Armor, login/logout and
/// root help layout features that are out of scope or rendered by a different help engine: "login and logout remain hidden from
/// default command list", "default help lists direct utility commands with ls first after keypair" (command order and sections
/// come from System.CommandLine's own help builder; the per-command descriptions and options are compared by
/// HelpTextParityTest instead), "login resolves through native action", "logout resolves through native action" and "armor
/// resolves through native command". "set exposes --plain without a -p shorthand" is covered by HelpTextParityTest's option
/// comparison for `set` (an extra `-p` alias would fail it).
/// </remarks>
public class ShorthandAliasTest : EnvironmentTestBase
{
    /// <remarks>
    /// Refer to "enc is a hidden shorthand for encrypt". The alias exists and resolves to the encrypt command; System.CommandLine
    /// cannot hide a single alias, so it is listed in help (trap #17).
    /// </remarks>
    [Fact]
    public void TestEncIsAShorthandForEncrypt()
    {
        EncryptAction encrypt = new(MockLogger);

        encrypt.Name.ShouldBe("encrypt");
        encrypt.Aliases.ShouldContain("enc");
    }

    /// <remarks>Refer to "dec is a hidden shorthand for decrypt". See the note on the encrypt test about hiding.</remarks>
    [Fact]
    public void TestDecIsAShorthandForDecrypt()
    {
        DecryptAction decrypt = new(MockLogger);

        decrypt.Name.ShouldBe("decrypt");
        decrypt.Aliases.ShouldContain("dec");
    }
}
