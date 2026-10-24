using System.Text.Json.Nodes;

using Dotenvx.Interop.DotenvxJs.Custodians;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.EnquirerJs;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Custodians;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/{selectKeyStorage,storeKeyStorage,selectConnection,selectConnectionResource}.js.
/// Upstream has no unit tests for these menus (they are covered by the e2e suite); here the prompts are scripted and the HTTP
/// seam stands in for Armor, so each test follows one path through the menus and checks the requests that result.
/// </remarks>
public class KeyStorageTest : EnvironmentTestBase
{
    private const string Hostname = "https://armor.example.com";
    private readonly List<(string Url, string Method, string? Body)> _requests = [];
    private readonly Queue<string> _answers = new();
    private readonly List<Prompts.SelectRequest> _asked = [];
    private readonly CapturedOutput _output = new();

    public KeyStorageTest()
    {
        string config = Environment.GetEnvironmentVariable("DOTENVX_CONFIG")!;
        Directory.CreateDirectory(config);
        File.WriteAllText(Path.Combine(config, ".env"), string.Join("\n",
            $"DOTENVX_ARMOR_HOSTNAME=\"{Hostname}\"", "DOTENVX_ARMOR_USERNAME=\"scott\"", "DOTENVX_ARMOR_TOKEN=\"token-123\"",
            "DOTENVX_ARMOR_ON=\"true\"", "DOTENVX_VERSION_LAST_CHECK=\"99999999999999\""));

        KeyStorage.IsInteractive = () => true;
        KeyStorage.Delay = _ => { };
        Prompts.PromptOutput = _output;
        Prompts.Select = request =>
        {
            _asked.Add(request);

            return _answers.Dequeue();
        };
        OpenUrl.OpenAction = _ => { };
    }

    private sealed class CapturedOutput : IPromptOutput
    {
        public string Text { get; private set; } = "";
        public int? Columns => 80;
        public int? Rows => 25;

        public void Write(string text)
        {
            Text += text;
        }
    }

    private void Respond(Func<string, string, (int Status, string Body)> handler)
    {
        ArmorHttp.Request = (url, method, _, body) =>
        {
            _requests.Add((url, method, body));
            (int status, string text) = handler(new Uri(url).PathAndQuery, method);

            return new HttpResult(status, text);
        };
    }

    private static (int, string) Catalog(string path, string method)
    {
        return (path, method) switch
        {
            ("/api/teams?page=1", "GET") => (200, """{"teams":[{"slug":"acme"}],"meta":{"next_page":null}}"""),
            ("/api/teams/acme/services", "GET") => (200, """{"services":[{"name":"GitHub","slug":"github"}]}"""),
            ("/api/teams/acme/connections?service=github&page=1", "GET") =>
                (200, """{"connections":[{"id":7,"name":"org","service":"github"}],"meta":{"next_page":null}}"""),
            ("/api/teams/acme/connections/7/resources?q=", "GET") =>
                (200, """{"resources":[{"id":99,"name":"repo"},{"id":100,"name":"/path"}],"meta":{"has_more":true}}"""),
            ("/api/teams/acme/connections/8/resources?q=", "GET") => (200, """{"resources":[],"meta":{}}"""),
            _ => (404, """{"error":{"message":"unexpected"}}""")
        };
    }

    private static KeyStorageOptions Options()
    {
        return new KeyStorageOptions { Access = new KeyAccessOptions() };
    }

    /// <remarks>Not from a JS test - selectKeyStorage returns 'file' without a terminal (since 2.38.0 it no longer prefers the native store).</remarks>
    [Fact]
    public void TestSelectDefaultsToFileWithoutATerminal()
    {
        KeyStorage.IsInteractive = () => false;

        KeyStorage.Select(Options()).Id.ShouldBe("file");
        _asked.ShouldBeEmpty();
    }

    /// <remarks>Not from a JS test - selectKeyStorage returns 'file' in CI and when no key may be created.</remarks>
    [Fact]
    public void TestSelectDefaultsToFileInCiOrWithNoCreate()
    {
        Environment.SetEnvironmentVariable("CI", "true");
        KeyStorage.Select(Options()).Id.ShouldBe("file");

        Environment.SetEnvironmentVariable("CI", null);
        KeyStorage.Select(new KeyStorageOptions { Access = new KeyAccessOptions(), NoCreate = true }).Id.ShouldBe("file");
        _asked.ShouldBeEmpty();
    }

    /// <remarks>Not from a JS test - the key type menu offers Armored, Enclaved and back after choosing Armor.</remarks>
    [Fact]
    public void TestSelectOffersKeyTypesAfterArmor()
    {
        _answers.Enqueue("armored");
        _answers.Enqueue("armored");

        KeyStorage.Select(Options()).Id.ShouldBe("armored");

        Prompts.SelectRequest keyType = _asked[1];
        keyType.Message.ShouldBe("Choose key type");
        keyType.Prefix.ShouldBe("⛨");
        keyType.Initial.ShouldBe("armored");
        keyType.Choices.Select(choice => choice.Name).ShouldBe(["⛨ Armored", "◊ Enclaved", "← back"]);
    }

    /// <remarks>Not from a JS test - going back from the key type menu returns to the custody menu.</remarks>
    [Fact]
    public void TestSelectBackFromKeyTypeReturnsToCustodyMenu()
    {
        _answers.Enqueue("armored");
        _answers.Enqueue("__back");
        _answers.Enqueue("local");
        _answers.Enqueue("file");

        KeyStorage.Select(Options()).Id.ShouldBe("file");

        _asked[2].Message.ShouldBe("Choose private key custody");
        _asked[2].Initial.ShouldBe("armored");
    }

    /// <remarks>Not from a JS test - the enclaved path walks team, service, connection and resource and returns the selection.</remarks>
    [Fact]
    public void TestSelectEnclavedWalksTeamServiceConnectionAndResource()
    {
        Respond(Catalog);
        foreach (string answer in new[] { "armored", "enclaved", "acme", "github", "7", "99" })
        {
            _answers.Enqueue(answer);
        }

        StorageChoice choice = KeyStorage.Select(Options());

        choice.Id.ShouldBe("enclaved");
        choice.Enclave!.Team.ShouldBe("acme");
        choice.Enclave.Service.ShouldBe("github");
        choice.Enclave.ConnectionId.ShouldBe("7");
        choice.Enclave.ResourceId.ToJsonString().ShouldBe("99");

        Prompts.SelectRequest resources = _asked[^1];
        resources.Message.ShouldBe("Select resource");
        resources.Footer.ShouldBe("Showing 5 resources. Search to narrow the results.");
        resources.Choices.Select(item => item.Name).ShouldBe(["org / repo", "org /path", "Search…", "← back"]);
    }

    /// <remarks>Not from a JS test - an account with no teams cannot use enclave keys.</remarks>
    [Fact]
    public void TestSelectEnclavedWithoutTeamsFails()
    {
        Respond((_, _) => (200, """{"teams":[],"meta":{"next_page":null}}"""));
        _answers.Enqueue("armored");
        _answers.Enqueue("enclaved");

        Should.Throw<Exception>(() => KeyStorage.Select(Options())).Message.ShouldBe("No teams available for enclave keys");
    }

    /// <remarks>Not from a JS test - connecting a service opens the setup URL and polls until the connection is made.</remarks>
    [Fact]
    public void TestSelectEnclavedConnectsAServiceInTheBrowser()
    {
        string opened = "";
        OpenUrl.OpenAction = url => opened = url;
        int polls = 0;
        string expires = DateTimeOffset.UtcNow.AddMinutes(5).ToString("O");
        Respond((path, method) =>
        {
            if (path == "/api/teams/acme/connections?service=github&page=1")
            {
                return (200, """{"connections":[],"meta":{"next_page":null}}""");
            }

            if (path == "/api/teams/acme/connections/setup" && method == "POST")
            {
                return (200, $$"""{"id":"s1","setup_url":"/setup/s1","expires_at":"{{expires}}","interval":1}""");
            }

            if (path == "/api/teams/acme/connections/setup/s1")
            {
                polls++;

                return polls == 1
                    ? (200, """{"status":"pending"}""")
                    : (200, """{"status":"connected","connection":{"id":8,"name":"org","service":"github"}}""");
            }

            return Catalog(path, method);
        });
        foreach (string answer in new[] { "armored", "enclaved", "acme", "github", "__connect" })
        {
            _answers.Enqueue(answer);
        }

        _answers.Enqueue("__back"); // resource menu of the new connection -> back to connections
        _answers.Enqueue("__back"); // connections -> back to services
        _answers.Enqueue("__back"); // services -> teams
        _answers.Enqueue("__back"); // teams -> key types
        _answers.Enqueue("__back"); // key types -> custody
        _answers.Enqueue("local");
        _answers.Enqueue("file");

        KeyStorage.Select(Options()).Id.ShouldBe("file");

        polls.ShouldBe(2);
        opened.ShouldBe($"{Hostname}/setup/s1");
        _output.Text.ShouldContain($"◇ waiting for GitHub connection…\n{Hostname}/setup/s1\n");
        _requests.Single(request => request.Url.EndsWith("/connections/setup")).Body.ShouldBe("""{"service":"github"}""");
    }

    /// <remarks>Not from a JS test - an expired setup raises the retry message.</remarks>
    [Fact]
    public void TestSelectEnclavedSetupExpiry()
    {
        string expires = DateTimeOffset.UtcNow.AddMinutes(5).ToString("O");
        Respond((path, method) =>
        {
            if (path == "/api/teams/acme/connections?service=github&page=1")
            {
                return (200, """{"connections":[],"meta":{"next_page":null}}""");
            }

            if (path == "/api/teams/acme/connections/setup" && method == "POST")
            {
                return (200, $$"""{"id":"s1","setup_url":"/setup/s1","expires_at":"{{expires}}"}""");
            }

            if (path == "/api/teams/acme/connections/setup/s1")
            {
                return (200, """{"status":"expired"}""");
            }

            return Catalog(path, method);
        });
        foreach (string answer in new[] { "armored", "enclaved", "acme", "github", "__connect" })
        {
            _answers.Enqueue(answer);
        }

        Should.Throw<Exception>(() => KeyStorage.Select(Options())).Message
            .ShouldBe("GitHub connection setup expired. Select Connect to GitHub to try again.");
    }

    /// <remarks>Not from a JS test - storing in an enclave asks for the key name and posts the resource.</remarks>
    [Fact]
    public void TestStoreEnclavedPostsTheKeypairRequest()
    {
        string publicKey = global::Dotenvx.Primitives.Primitives.Keypair().PublicKey;
        Respond((path, _) => path == "/api/teams/acme/connections/7/keypairs"
            ? (200, $$"""{"keypair":{"public_key":"{{publicKey}}"} }""")
            : (404, "{}"));
        string? initial = null;
        Prompts.InputWithInitial = (_, preset) =>
        {
            initial = preset;

            return "MY_KEY";
        };
        var choice = new StorageChoice("enclaved", new EnclaveSelection("acme", "github", "7", JsonNode.Parse("99")!));

        (StorageChoice storage, StoreResult stored, string resultKey) = KeyStorage.Store(choice, null, null, Options(),
            new StoreContext { PrivateKeyName = "DOTENV_PRIVATE_KEY" });

        resultKey.ShouldBe(publicKey);
        storage.ShouldBe(choice);
        stored.HasKeysSrc.ShouldBeFalse();
        initial.ShouldBe("DOTENV_PRIVATE_KEY");
        _requests.Single().Body.ShouldBe("""{"resource_id":99,"private_key_name":"MY_KEY"}""");
    }

    /// <remarks>Not from a JS test - a public key that is not a secp256k1 key is rejected.</remarks>
    [Fact]
    public void TestStoreEnclavedRejectsAnInvalidPublicKey()
    {
        Respond((_, _) => (200, """{"keypair":{"public_key":"nope"}}"""));
        Prompts.InputWithInitial = (_, _) => "MY_KEY";
        var choice = new StorageChoice("enclaved", new EnclaveSelection("acme", "github", "7", JsonNode.Parse("99")!));

        Should.Throw<Exception>(() => KeyStorage.Store(choice, null, null, Options(), new StoreContext())).Message
            .ShouldBe("Invalid public key returned by Armor");
    }

    /// <remarks>Not from a JS test - storing with no key given makes the keypair after the storage is chosen.</remarks>
    [Fact]
    public void TestStoreGeneratesTheKeypairAfterChoosingStorage()
    {
        string directory = Path.Combine(Path.GetTempPath(), "dotenvx-keystorage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Environment.CurrentDirectory = directory;
        Directory.CreateDirectory(Environment.CurrentDirectory);
        KeyStorage.IsInteractive = () => false;

        (StorageChoice storage, StoreResult stored, string publicKey) = KeyStorage.Store(null, null, null, Options(),
            new StoreContext { PrivateKeyName = "DOTENV_PRIVATE_KEY", Comment = ".env" });

        storage.Id.ShouldBe("file");
        stored.HasKeysSrc.ShouldBeTrue();
        stored.KeysSrc!.ShouldContain($"DOTENV_PRIVATE_KEY=");
    }
}
