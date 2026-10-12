using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/cli/help.js (tests/cli/examples.test.js) - generated from the upstream test's own literals.</remarks>
public class HelpExamplesTest
{
    /// <remarks>Refer to "run function returns expected string"</remarks>
    [Fact]
    public void TestRunFunctionReturnsExpectedString()
    {
        HelpExamples.Run().ShouldBe("\nExamples:\n\n  ```\n  $ dotenvx run -- npm run dev\n  $ dotenvx run -- flask --app index run\n  $ dotenvx run -- php artisan serve\n  $ dotenvx run -- bin/rails s\n  ```\n\nTry it:\n\n  ```\n  $ echo \"HELLO=World\" > .env\n  $ echo \"console.log('Hello ' + process.env.HELLO)\" > index.js\n\n  $ dotenvx run -f .env -- node index.js\n  [dotenvx] injected env (1) from .env\n  Hello World\n  ```\n  ");
    }

    /// <remarks>Refer to "precommit function returns expected string"</remarks>
    [Fact]
    public void TestPrecommitFunctionReturnsExpectedString()
    {
        HelpExamples.Precommit().ShouldBe("\nExamples:\n\n  ```\n  $ dotenvx precommit\n  $ dotenvx precommit --install\n  ```\n\nTry it:\n\n  ```\n  $ dotenvx precommit\n  [dotenvx@0.45.0][precommit] success\n  ```\n  ");
    }

    /// <remarks>Refer to "prebuild function returns expected string"</remarks>
    [Fact]
    public void TestPrebuildFunctionReturnsExpectedString()
    {
        HelpExamples.Prebuild().ShouldBe("\nExamples:\n\n  ```\n  $ dotenvx prebuild\n  ```\n\nTry it:\n\n  ```\n  $ dotenvx prebuild\n  [dotenvx@0.10.0][prebuild] success\n  ```\n  ");
    }

    /// <remarks>Refer to "gitignore function returns expected string"</remarks>
    [Fact]
    public void TestGitignoreFunctionReturnsExpectedString()
    {
        HelpExamples.Gitignore().ShouldBe("\nExamples:\n\n  ```\n  $ dotenvx gitignore\n  $ dotenvx gitignore --pattern .env.keys\n  ```\n\n  Try it:\n\n  ```\n  $ dotenvx gitignore\n  ▣ ignored .env* (.gitignore)\n  ```\n  ");
    }

    /// <remarks>Refer to "set function returns expected string"</remarks>
    [Fact]
    public void TestSetFunctionReturnsExpectedString()
    {
        HelpExamples.Set().ShouldBe("\nExamples:\n\n  ```\n  $ dotenvx set KEY value\n  $ dotenvx set KEY \"value with spaces\"\n  $ dotenvx set KEY -- \"---value with a dash---\"\n  $ dotenvx set KEY -- \"-----BEGIN OPENSSH PRIVATE KEY-----\n                        b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAAMwAAAAtzc2gtZW\n                        -----END OPENSSH PRIVATE KEY-----\"\n  ```\n  ");
    }

    /// <remarks>Refer to "curl function returns API endpoints and examples"</remarks>
    [Fact]
    public void TestCurlFunctionReturnsAPIEndpointsAndExamples()
    {
        string output = HelpExamples.Curl();

        output.ShouldMatch("Endpoints:\\s+GET\\s+\\/api\\/account");
        output.ShouldMatch("GET\\s+\\/api\\/logs\\?team=:team");
        output.ShouldMatch("POST\\s+\\/api\\/armor\\/keypairs\\/:public_key\\/settings\\/guard");
        output.ShouldMatch("POST\\s+\\/api\\/teams\\/:team\\/invitations\\/:id\\/cancel");
        output.ShouldMatch("POST\\s+\\/api\\/teams\\/:team\\/members\\/:member_id\\/keypairs\\/:public_key\\/grant");
        output.ShouldMatch("POST\\s+\\/api\\/teams\\/:team\\/members\\/:member_id\\/keypairs\\/:public_key\\/revoke");
        output.ShouldMatch("POST\\s+\\/api\\/armor\\/keypairs\\/:public_key\\/members\\/:member_id\\/grant");
        output.ShouldMatch("POST\\s+\\/api\\/armor\\/keypairs\\/:public_key\\/members\\/:member_id\\/revoke");
        output.ShouldMatch("GET\\s+\\/api\\/join_requests");
        output.ShouldMatch("POST\\s+\\/api\\/join_requests");
        output.ShouldMatch("POST\\s+\\/api\\/join_requests\\/:id\\/cancel");
        output.ShouldMatch("GET\\s+\\/api\\/teams\\/:team\\/join_requests");
        output.ShouldMatch("POST\\s+\\/api\\/teams\\/:team\\/join_requests\\/:id\\/accept");
        output.ShouldMatch("POST\\s+\\/api\\/teams\\/:team\\/join_requests\\/:id\\/decline");
        output.ShouldMatch("POST\\s+\\/api\\/armor\\/:team\\/settings\\/join_requests");
        output.ShouldMatch("dotenvx curl \"https:\\/\\/armor\\.dotenvx\\.com\\/api\\/account\"");
        output.ShouldMatch("dotenvx curl \"https:\\/\\/armor\\.dotenvx\\.com\\/api\\/armor\\/keypairs\\?page=1&per=100\"");
        output.ShouldMatch("dotenvx curl \"https:\\/\\/armor\\.dotenvx\\.com\\/api\\/armor\\/keypairs\\?sort=name_asc\"");
        output.ShouldMatch("dotenvx curl \"https:\\/\\/armor\\.dotenvx\\.com\\/api\\/armor\\/keypairs\\?sort=public_key_desc\"");
        output.ShouldMatch("dotenvx curl \"https:\\/\\/armor\\.dotenvx\\.com\\/api\\/armor\\/keypairs\\/PUBLIC_KEY\\/name\" --data '\\{\"name\":\"Production\"\\}'");
        output.ShouldMatch("dotenvx curl \"https:\\/\\/armor\\.dotenvx\\.com\\/api\\/armor\\/keypairs\\/PUBLIC_KEY\\/name\" --data '\\{\"name\":null\\}'");
        output.ShouldMatch("dotenvx curl \"https:\\/\\/armor\\.dotenvx\\.com\\/api\\/join_requests\" --data '\\{\"team\":\"TEAM\"\\}'");
        output.ShouldMatch("dotenvx curl \"https:\\/\\/armor\\.dotenvx\\.com\\/api\\/teams\\/TEAM\\/invitations\\/123\\/cancel\" --request POST");
        output.ShouldMatch("dotenvx curl \"https:\\/\\/armor\\.dotenvx\\.com\\/api\\/join_requests\\/123\\/cancel\" --request POST");
        output.ShouldMatch("dotenvx curl \"https:\\/\\/armor\\.dotenvx\\.com\\/api\\/armor\\/keypairs\\/PUBLIC_KEY\\/members\\/123\\/grant\" --request POST");
        output.ShouldMatch("dotenvx curl \"https:\\/\\/armor\\.dotenvx\\.com\\/api\\/teams\\/TEAM\\/members\\/123\\/keypairs\\/PUBLIC_KEY\\/revoke\" --request POST");
        output.ShouldMatch("dotenvx curl \"https:\\/\\/armor\\.dotenvx\\.com\\/api\\/teams\\/TEAM\\/join_requests\\/123\\/accept\" --data '\\{\"role\":\"member\"\\}'");
        output.ShouldMatch("dotenvx curl \"https:\\/\\/armor\\.dotenvx\\.com\\/api\\/armor\\/TEAM\\/settings\\/join_requests\" --data '\\{\"value\":true\\}'");
        output.ShouldMatch("dotenvx curl \"https:\\/\\/armor\\.dotenvx\\.com\\/api\\/logs\\?team=TEAM\"");
        output.ShouldMatch("dotenvx curl \"https:\\/\\/armor\\.dotenvx\\.com\\/api\\/logs\\?team=TEAM&action=keypair\\/access&actor=USERNAME&result=success&resource=keypair\\/KEYPAIR_ID&page=1&per=100\"");
        output.ShouldMatch("dotenvx curl \"https:\\/\\/armor\\.dotenvx\\.com\\/api\\/logs\\?team=TEAM&actor=missing&resource=missing\"");
        System.Text.RegularExpressions.Regex.IsMatch(output, "--request DELETE").ShouldBeFalse();
    }
}
