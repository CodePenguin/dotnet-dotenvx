using Dotenvx.Interop.DotenvxJs.Api;
using Dotenvx.Interop.DotenvxJs.Db;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;

namespace Dotenvx.Interop.DotenvxJs.Custodians;

/// <summary>The user chose "back" in the team prompt to return to the custody menu (`KEY_CUSTODY_BACK`).</summary>
internal sealed class KeyCustodyBackException() : Exception("Return to private key custody")
{
    public string Code => "KEY_CUSTODY_BACK";
}

/// <remarks>
/// Reference source: dotenvx/src/lib/custodians/managed/armor/{index,store}.js. Armor holds the key on the server: storing
/// posts the keypair (asking for a team when the account has several) and writes nothing local.
/// </remarks>
internal sealed class ArmorCustodian : ICustodian
{
    public const string BackValue = "__back";

    public string Id => "armored";
    public string Name => "⛨ Armor";
    public string Custody => "managed";

    public bool Enabled(KeyAccessOptions? options = null)
    {
        return options?.NoArmor != true && Environment.GetEnvironmentVariable("DOTENVX_NO_ARMOR") != "true";
    }

    public bool Available()
    {
        return true;
    }

    public bool Configured(KeyAccessOptions? options = null)
    {
        return !string.IsNullOrEmpty(options?.Token) || !new Session().NoArmor();
    }

    public IDictionary<string, string> Get(string publicKey)
    {
        return new Dictionary<string, string>();
    }

    public StoreResult Store(string publicKey, string privateKey, StoreContext context)
    {
        var session = new Session();
        string hostname = session.Hostname();
        string? token = !string.IsNullOrEmpty(context.Token) ? context.Token : session.Token();
        string devicePublicKey = session.DevicePublicKey();

        try
        {
            ArmorApi.ArmorUp(hostname, token, devicePublicKey, publicKey, privateKey, null);
        }
        catch (Exception error) when (ArmorTeam.IsTeamRequiredError(error))
        {
            Prompts.Choice[] choices = ArmorTeam.ChoicesFromMeta(((ApiException)error).Meta);
            string team = choices[0].Value;
            if (choices.Length > 1)
            {
                IReadOnlyList<Prompts.Choice> offered = context.AllowCustodyBack
                    ? [.. choices, new Prompts.Choice("← back", BackValue)]
                    : choices;
                team = Prompts.Select(new Prompts.SelectRequest("Select team", offered) { Prefix = "⛨", Navigation = context.AllowCustodyBack, BackValue = context.AllowCustodyBack ? BackValue : null }) ?? throw new PromptCancelledException();
            }

            if (team == BackValue)
            {
                throw new KeyCustodyBackException();
            }

            ArmorApi.ArmorUp(hostname, token, devicePublicKey, publicKey, privateKey, team);
        }

        return new StoreResult();
    }
}
