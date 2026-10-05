using Dotenvx.Interop.DotenvxJs.Db;

using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - proves the config stores (conf@10 as bundled in @dotenvx/tooling) are byte-compatible between the
/// two CLIs: a `.device-key` written by either side is read by the other. The IV is random and becomes the pbkdf2 salt
/// through `iv.toString()`, so many rounds exercise the UTF-8 replacement-character handling.
/// </remarks>
public class ArmorStoreParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private const int Rounds = 40;

    private string Dir(int index) => Path.Combine(WorkDir, "cfg" + index);

    [Fact]
    public async Task DeviceKeysWrittenByNodeAreReadByDotnet()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        string script = $$"""
            const Device = require('./src/db/device');
            const out = [];
            for (let i = 0; i < {{Rounds}}; i++) {
              process.env.DOTENVX_CONFIG = process.argv[1] + require('path').sep + 'cfg' + i;
              out.push(new Device().publicKey());
            }
            console.log(JSON.stringify(out));
            """;
        CliResult node = await Fixture.RunNodeScriptAsync(script.Replace("process.argv[1]", $"'{WorkDir.Replace("\\", "\\\\")}'"));
        node.ExitCode.ShouldBe(0, node.StandardError);
        string[] publicKeys = System.Text.Json.JsonSerializer.Deserialize<string[]>(node.StandardOutput)!;

        for (int i = 0; i < Rounds; i++)
        {
            new Device(Dir(i)).PublicKey().ShouldBe(publicKeys[i], $"round {i}");
        }
    }

    [Fact]
    public async Task DeviceKeysWrittenByDotnetAreReadByNode()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        string[] expected = [.. Enumerable.Range(0, Rounds).Select(i => new Device(Dir(i)).PublicKey())];

        string script = $$"""
            const Device = require('./src/db/device');
            const out = [];
            for (let i = 0; i < {{Rounds}}; i++) {
              process.env.DOTENVX_CONFIG = '{{WorkDir.Replace("\\", "\\\\")}}' + require('path').sep + 'cfg' + i;
              out.push(new Device().publicKey());
            }
            console.log(JSON.stringify(out));
            """;
        CliResult node = await Fixture.RunNodeScriptAsync(script);

        node.ExitCode.ShouldBe(0, node.StandardError);
        System.Text.Json.JsonSerializer.Deserialize<string[]>(node.StandardOutput).ShouldBe(expected);
    }

    [Fact]
    public async Task DeviceValuesEncryptedByOneSideDecryptOnTheOther()
    {
        if (!EnsureAvailable())
        {
            return;
        }

        var device = new Device(Dir(0));
        string cipher = device.Encrypt("secret value");
        device.Decrypt(cipher).ShouldBe("secret value");

        string script = $$"""
            const Device = require('./src/db/device');
            process.env.DOTENVX_CONFIG = '{{WorkDir.Replace("\\", "\\\\")}}' + require('path').sep + 'cfg0';
            const d = new Device();
            console.log(JSON.stringify({ plain: d.decrypt('{{cipher}}'), cipher: d.encrypt('from node') }));
            """;
        CliResult node = await Fixture.RunNodeScriptAsync(script);

        node.ExitCode.ShouldBe(0, node.StandardError);
        using var json = System.Text.Json.JsonDocument.Parse(node.StandardOutput);
        json.RootElement.GetProperty("plain").GetString().ShouldBe("secret value");
        device.Decrypt(json.RootElement.GetProperty("cipher").GetString()!).ShouldBe("from node");
    }
}
