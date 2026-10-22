namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/cli/help.js - generated from that file's output (do not edit by hand).</remarks>
internal static class HelpExamples
{
    /// <summary>`help.run()`: the "Examples:" / "Try it:" trailer of `dotenvx run --help`.</summary>
    public static string Run()
    {
        return "\nExamples:\n\n  ```\n  $ dotenvx run -- npm run dev\n  $ dotenvx run -- flask --app index run\n  $ dotenvx run -- php artisan serve\n  $ dotenvx run -- bin/rails s\n  ```\n\nTry it:\n\n  ```\n  $ echo \"HELLO=World\" > .env\n  $ echo \"console.log('Hello ' + process.env.HELLO)\" > index.js\n\n  $ dotenvx run -f .env -- node index.js\n  [dotenvx] injected env (1) from .env\n  Hello World\n  ```\n  ";
    }

    /// <summary>`help.precommit()`: the "Examples:" / "Try it:" trailer of `dotenvx precommit --help`.</summary>
    public static string Precommit()
    {
        return "\nExamples:\n\n  ```\n  $ dotenvx precommit\n  $ dotenvx precommit --install\n  ```\n\nTry it:\n\n  ```\n  $ dotenvx precommit\n  [dotenvx@0.45.0][precommit] success\n  ```\n  ";
    }

    /// <summary>`help.prebuild()`: the "Examples:" / "Try it:" trailer of `dotenvx prebuild --help`.</summary>
    public static string Prebuild()
    {
        return "\nExamples:\n\n  ```\n  $ dotenvx prebuild\n  ```\n\nTry it:\n\n  ```\n  $ dotenvx prebuild\n  [dotenvx@0.10.0][prebuild] success\n  ```\n  ";
    }

    /// <summary>`help.gitignore()`: the "Examples:" / "Try it:" trailer of `dotenvx gitignore --help`.</summary>
    public static string Gitignore()
    {
        return "\nExamples:\n\n  ```\n  $ dotenvx gitignore\n  $ dotenvx gitignore --pattern .env.keys\n  ```\n\n  Try it:\n\n  ```\n  $ dotenvx gitignore\n  ▣ ignored .env* (.gitignore)\n  ```\n  ";
    }

    /// <summary>`help.set()`: the "Examples:" / "Try it:" trailer of `dotenvx set --help`.</summary>
    public static string Set()
    {
        return "\nExamples:\n\n  ```\n  $ dotenvx set KEY value\n  $ dotenvx set KEY \"value with spaces\"\n  $ dotenvx set KEY -- \"---value with a dash---\"\n  $ dotenvx set KEY -- \"-----BEGIN OPENSSH PRIVATE KEY-----\n                        b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAAMwAAAAtzc2gtZW\n                        -----END OPENSSH PRIVATE KEY-----\"\n  ```\n  ";
    }

    /// <summary>`help.del()`: the "Examples:" / "Try it:" trailer of `dotenvx del --help`.</summary>
    public static string Del()
    {
        return "\nExamples:\n\n  ```\n  $ dotenvx del KEY\n  $ dotenvx del KEY -f .env.production\n  ```\n  ";
    }

    /// <summary>`help.curl()`: the "Examples:" / "Try it:" trailer of `dotenvx curl --help`.</summary>
    public static string Curl()
    {
        return "\nEndpoints:\n\n  GET    /api/account\n  GET    /api/armor/keypairs\n  GET    /api/armor/keypairs/:public_key\n  POST   /api/armor/keypairs/:public_key/name\n  POST   /api/armor/keypairs/:public_key/members/:member_id/grant\n  POST   /api/armor/keypairs/:public_key/members/:member_id/revoke\n  POST   /api/armor/keypairs/:public_key/settings/guard\n  POST   /api/armor/keypairs/:public_key/settings/enclave\n  GET    /api/teams\n  GET    /api/teams/:team\n  GET    /api/teams/:team/members\n  POST   /api/teams/:team/members/:member_id/keypairs/:public_key/grant\n  POST   /api/teams/:team/members/:member_id/keypairs/:public_key/revoke\n  GET    /api/teams/:team/invitations\n  POST   /api/teams/:team/invitations\n  POST   /api/teams/:team/invitations/:id/cancel\n  GET    /api/join_requests\n  POST   /api/join_requests\n  POST   /api/join_requests/:id/cancel\n  GET    /api/teams/:team/join_requests\n  POST   /api/teams/:team/join_requests/:id/accept\n  POST   /api/teams/:team/join_requests/:id/decline\n  POST   /api/armor/:team/settings/join_requests\n  GET    /api/logs?team=:team\n\nExamples:\n\n  dotenvx curl \"https://armor.dotenvx.com/api/account\"\n  dotenvx curl \"https://armor.dotenvx.com/api/armor/keypairs\"\n  dotenvx curl \"https://armor.dotenvx.com/api/armor/keypairs?page=1&per=100\"\n  dotenvx curl \"https://armor.dotenvx.com/api/armor/keypairs?sort=name_asc\"\n  dotenvx curl \"https://armor.dotenvx.com/api/armor/keypairs?sort=public_key_desc\"\n  dotenvx curl \"https://armor.dotenvx.com/api/armor/keypairs/PUBLIC_KEY\"\n  dotenvx curl \"https://armor.dotenvx.com/api/armor/keypairs/PUBLIC_KEY/name\" --data '{\"name\":\"Production\"}'\n  dotenvx curl \"https://armor.dotenvx.com/api/armor/keypairs/PUBLIC_KEY/name\" --data '{\"name\":null}'\n  dotenvx curl \"https://armor.dotenvx.com/api/armor/keypairs/PUBLIC_KEY/members/123/grant\" --request POST\n  dotenvx curl \"https://armor.dotenvx.com/api/armor/keypairs/PUBLIC_KEY/members/123/revoke\" --request POST\n  dotenvx curl \"https://armor.dotenvx.com/api/armor/keypairs/PUBLIC_KEY/settings/guard\" --data '{\"value\":true}'\n  dotenvx curl \"https://armor.dotenvx.com/api/armor/keypairs/PUBLIC_KEY/settings/guard\" --data '{\"value\":false}'\n  dotenvx curl \"https://armor.dotenvx.com/api/armor/keypairs/PUBLIC_KEY/settings/enclave\" --data '{\"value\":true}'\n  dotenvx curl \"https://armor.dotenvx.com/api/armor/keypairs/PUBLIC_KEY/settings/enclave\" --data '{\"value\":false}'\n  dotenvx curl \"https://armor.dotenvx.com/api/teams\"\n  dotenvx curl \"https://armor.dotenvx.com/api/teams/TEAM\"\n  dotenvx curl \"https://armor.dotenvx.com/api/teams/TEAM/members\"\n  dotenvx curl \"https://armor.dotenvx.com/api/teams/TEAM/members/123/keypairs/PUBLIC_KEY/grant\" --request POST\n  dotenvx curl \"https://armor.dotenvx.com/api/teams/TEAM/members/123/keypairs/PUBLIC_KEY/revoke\" --request POST\n  dotenvx curl \"https://armor.dotenvx.com/api/teams/TEAM/invitations\"\n  dotenvx curl \"https://armor.dotenvx.com/api/teams/TEAM/invitations\" --data '{\"email\":\"person@example.com\",\"role\":\"member\"}'\n  dotenvx curl \"https://armor.dotenvx.com/api/teams/TEAM/invitations/123/cancel\" --request POST\n  dotenvx curl \"https://armor.dotenvx.com/api/join_requests\"\n  dotenvx curl \"https://armor.dotenvx.com/api/join_requests\" --data '{\"team\":\"TEAM\"}'\n  dotenvx curl \"https://armor.dotenvx.com/api/join_requests/123/cancel\" --request POST\n  dotenvx curl \"https://armor.dotenvx.com/api/teams/TEAM/join_requests\"\n  dotenvx curl \"https://armor.dotenvx.com/api/teams/TEAM/join_requests/123/accept\" --data '{\"role\":\"member\"}'\n  dotenvx curl \"https://armor.dotenvx.com/api/teams/TEAM/join_requests/123/decline\" --request POST\n  dotenvx curl \"https://armor.dotenvx.com/api/armor/TEAM/settings/join_requests\" --data '{\"value\":true}'\n  dotenvx curl \"https://armor.dotenvx.com/api/logs?team=TEAM\"\n  dotenvx curl \"https://armor.dotenvx.com/api/logs?team=TEAM&action=keypair/access&actor=USERNAME&result=success&resource=keypair/KEYPAIR_ID&page=1&per=100\"\n  dotenvx curl \"https://armor.dotenvx.com/api/logs?team=TEAM&actor=missing&resource=missing\"\n  ";
    }
}
