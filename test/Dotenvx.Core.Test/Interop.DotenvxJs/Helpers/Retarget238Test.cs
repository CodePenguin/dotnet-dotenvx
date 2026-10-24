using Dotenvx.Interop.DotenvxJs;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using NSubstitute;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/{enclavedHint,logSetSummary,statusReporter}.js and lib/main.js `getSync` (new
/// between 2.35.0 and 2.38.2). Upstream has no unit tests of its own for them, so each test says what it checks.
/// </remarks>
public class Retarget238Test : EnvironmentTestBase
{
    private static readonly string PublicKey = global::Dotenvx.Primitives.Primitives.Keypair().PublicKey;

    private static ProcessedSet Row(string filepath, Dictionary<string, string>? sources = null)
    {
        return new ProcessedSet
        {
            EnvFilePath = filepath,
            EnvSrc = $"DOTENV_PUBLIC_KEY=\"{PublicKey}\"\n",
            KeySources = sources
        };
    }

    /// <remarks>Not from a JS test - an ENCLAVED error gets the `dotenvx armor open` hint.</remarks>
    [Fact]
    public void TestEnclavedHintPointsToArmorOpen()
    {
        var error = new DotenvxException("ENCLAVED", "[ENCLAVED] private key enclaved", null);

        EnclavedHint.Apply(error, ".env.production");

        error.Help.ShouldBe("hint: [dotenvx armor open -f .env.production]");
        error.MessageWithHelp.ShouldBe("[ENCLAVED] private key enclaved. hint: [dotenvx armor open -f .env.production]");
    }

    /// <remarks>Not from a JS test - a file name with unusual characters is escaped in the hint.</remarks>
    [Fact]
    public void TestEnclavedHintEscapesUnusualFilenames()
    {
        var error = new DotenvxException("ENCLAVED", "[ENCLAVED] private key enclaved", null);

        EnclavedHint.Apply(error, "my env");

        error.Help.ShouldBe("hint: [dotenvx armor open -f 'my env']");
    }

    /// <remarks>Not from a JS test - other errors are left alone.</remarks>
    [Fact]
    public void TestEnclavedHintIgnoresOtherErrors()
    {
        var error = new DotenvxException("OTHER", "[OTHER] x", null);

        EnclavedHint.Apply(error, ".env");

        error.Help.ShouldBeNull();
    }

    /// <remarks>Not from a JS test - logSetSummary: a plain set that changed a file.</remarks>
    [Fact]
    public void TestLogSetSummaryPlainSet()
    {
        LogSetSummary.Log(MockLogger, "HELLO", false, [Row(".env")], [".env"], []);

        MockLogger.InfoMessages.ShouldBe(["◇ set HELLO (.env)"]);
    }

    /// <remarks>Not from a JS test - logSetSummary: an encrypt names the key and where its private key is kept.</remarks>
    [Fact]
    public void TestLogSetSummaryEncryptNamesTheKeyCustody()
    {
        LogSetSummary.Log(MockLogger, "HELLO", true, [Row(".env", new Dictionary<string, string> { [PublicKey] = "file" })], [".env"],
            []);

        string line = MockLogger.InfoMessages.Single();
        line.ShouldStartWith("◈ encrypted HELLO (.env) (");
        line.ShouldEndWith("via .env.keys ⛉");
    }

    /// <remarks>Not from a JS test - logSetSummary: nothing changed reports the unchanged files.</remarks>
    [Fact]
    public void TestLogSetSummaryNoChange()
    {
        LogSetSummary.Log(MockLogger, "HELLO", false, [Row(".env")], [], [".env"]);

        MockLogger.InfoMessages.ShouldBe(["○ no change (.env)"]);
    }

    /// <remarks>Not from a JS test - logSetSummary prints nothing when no file was involved.</remarks>
    [Fact]
    public void TestLogSetSummaryWithoutFilesPrintsNothing()
    {
        LogSetSummary.Log(MockLogger, "HELLO", true, [], [], []);

        MockLogger.InfoMessages.ShouldBeEmpty();
    }

    /// <remarks>Not from a JS test - getSync reads like get but never prompts.</remarks>
    [Fact]
    public void TestGetSyncDoesNotPrompt()
    {
        bool? prompted = null;
        Get mockGet = Substitute.For<Get>("KEY", Array.Empty<EnvItem>(), false, false, null!);
        mockGet.Run().Returns(_ =>
        {
            prompted = mockGet.PromptForSecrets;

            return new GetResult { Parsed = new Dictionary<string, string> { ["KEY"] = "value" }, Errors = [] };
        });
        Get.Factory = (_, _, _, _, _) => mockGet;

        Main.GetSync("KEY").ShouldBe("value");

        prompted.ShouldBe(false);
    }
}
