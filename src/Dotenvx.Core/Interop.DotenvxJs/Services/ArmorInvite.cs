using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Dotenvx.Interop.DotenvxJs.Api;
using Dotenvx.Interop.DotenvxJs.Helpers;

using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <summary>The team and email an invitation went to.</summary>
internal sealed record ArmorInviteResult(string Team, string Email);

/// <remarks>
/// Reference source: dotenvx/src/lib/services/armorInvite.js. Invites a member to a team; the team (only those where the user
/// is an owner or admin) and the email are asked for when not given, which needs a terminal.
/// </remarks>
internal class ArmorInvite(string hostname, string? token, string? team, string? email)
{
    private static readonly Regex EmailPattern = new(@"^[^\s﻿@]+@[^\s﻿@]+\.[^\s﻿@]+$", RegexOptions.Compiled);

    private static readonly Func<bool> DefaultStdinIsTty = () => !Console.IsInputRedirected;

    public static Func<string, string?, string?, string?, ArmorInvite> Factory { get; set; } =
        (host, token, team, email) => new ArmorInvite(host, token, team, email);

    /// <summary>`process.stdin.isTTY`.</summary>
    public static Func<bool> StdinIsTty { get; internal set; } = DefaultStdinIsTty;

    internal static void ResetToDefaults()
    {
        StdinIsTty = DefaultStdinIsTty;
    }

    public virtual ArmorInviteResult Run()
    {
        if ((string.IsNullOrEmpty(team) || string.IsNullOrEmpty(email)) && !StdinIsTty())
        {
            throw new Exception("provide --team and --email when running non-interactively");
        }

        string? chosenTeam = team;
        string? chosenEmail = email;
        if (string.IsNullOrEmpty(chosenTeam))
        {
            Prompts.Choice[] choices =
            [
                .. ArmorApi.GetTeams(hostname, token)
                    .Where(item => item["current_user_role"]?.GetValueKind() == JsonValueKind.String
                        && item["current_user_role"]!.GetValue<string>() is "owner" or "admin")
                    .Select(item => new Prompts.Choice(item["slug"]!.ToString(), item["slug"]!.ToString()))
            ];
            if (choices.Length == 0)
            {
                throw new Exception("no teams available to invite members to (requires owner or admin role)");
            }

            chosenTeam = Prompts.Select(new Prompts.SelectRequest("Select team", choices) { Prefix = "⛨" })
                ?? throw new PromptCancelledException();
        }

        if (string.IsNullOrEmpty(chosenEmail))
        {
            chosenEmail = Prompts.InputText("Email address") ?? throw new PromptCancelledException();
        }

        chosenEmail = JsTrim(chosenEmail);
        if (!EmailPattern.IsMatch(chosenEmail))
        {
            throw new Exception("enter a valid email address");
        }

        ArmorApi.PostTeamInvitation(hostname, token, chosenTeam, chosenEmail);

        return new ArmorInviteResult(chosenTeam, chosenEmail);
    }
}
