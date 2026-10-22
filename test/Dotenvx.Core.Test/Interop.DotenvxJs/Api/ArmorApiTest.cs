using System.Text.Json.Nodes;

using Dotenvx.Interop.DotenvxJs.Api;
using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Api;

/// <remarks>
/// Reference source: dotenvx/src/lib/api/*.js. Upstream stubs `http` and `buildApiError`; here the HTTP seam
/// (<see cref="ArmorHttp.Request" />) is stubbed, and the API error is asserted through the exception it raises
/// ("builds api error once with the status code and json" becomes "throws the error buildApiError would").
/// </remarks>
public class ArmorApiTest : EnvironmentTestBase
{
    private readonly List<(string Url, string Method, IReadOnlyDictionary<string, string>? Headers, string? Body)> _requests = [];

    public ArmorApiTest()
    {
        PackageJson.Version = "0.0.0-test";
    }

    private void Respond(int status, string json)
    {
        ArmorHttp.Request = (url, method, headers, body) =>
        {
            _requests.Add((url, method, headers, body));

            return new HttpResult(status, json);
        };
    }

    private JsonNode Body()
    {
        return JsonNode.Parse(_requests[0].Body!)!;
    }

    private static void ShouldBeJson(JsonNode actual, string expected)
    {
        actual.ToJsonString().ShouldBe(JsonNode.Parse(expected)!.ToJsonString());
    }

    private const string Unauthorized = """{"error":{"code":"UNAUTHORIZED","message":"bad token","help":"login again"}}""";

    /// <remarks>Refer to "GetAccount.run fetches account with bearer token"</remarks>
    [Fact]
    public void TestGetAccountRunFetchesAccountWithBearerToken()
    {
        Respond(200, """{"organizations":[{"provider_slug":"dotenvx"}]}""");

        JsonNode result = ArmorApi.GetAccount("https://armor.example.com", "token-123");

        _requests[0].Url.ShouldBe("https://armor.example.com/api/account");
        _requests[0].Method.ShouldBe("GET");
        _requests[0].Headers.ShouldBe(new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer token-123",
            ["Content-Type"] = "application/json"
        });
        ShouldBeJson(result, """{"organizations":[{"provider_slug":"dotenvx"}]}""");
    }

    /// <remarks>Refer to "GetAccount.run throws buildApiError output on non-2xx status"</remarks>
    [Fact]
    public void TestGetAccountRunThrowsBuildApiErrorOutputOnNon2xxStatus()
    {
        Respond(401, Unauthorized);

        var error = Should.Throw<ApiException>(() => ArmorApi.GetAccount("https://armor.example.com", "token-123"));

        error.Message.ShouldBe("[UNAUTHORIZED] bad token");
        error.StatusCode.ShouldBe(401);
        ShouldBeJson(error.Json!, Unauthorized);
    }

    /// <remarks>Refer to "PostOauthDeviceCode posts device login payload"</remarks>
    [Fact]
    public void TestPostOauthDeviceCodePostsDeviceLoginPayload()
    {
        Respond(200, """{"device_code":"device-code","user_code":"user-code"}""");

        JsonNode result = ArmorApi.OauthDeviceCode("https://armor.example.com", "device-pub",
            new SystemInformation.Info("uuid", "darwin", "arm64"));

        ShouldBeJson(result, """{"device_code":"device-code","user_code":"user-code"}""");
        _requests[0].Url.ShouldBe("https://armor.example.com/oauth/device/code");
        ShouldBeJson(Body(), """
            {"client_id":"oac_dotenvxcli","device_public_key":"device-pub","system_information":{"system_uuid":"uuid","os_platform":"darwin","os_arch":"arm64"},"dotenvx_project_id":null}
            """);
    }

    /// <remarks>Refer to "PostOauthDeviceCode throws oauth errors"</remarks>
    [Fact]
    public void TestPostOauthDeviceCodeThrowsOauthErrors()
    {
        Respond(400, """{"error":"invalid_request","error_description":"bad request"}""");

        Should.Throw<ApiException>(() => ArmorApi.OauthDeviceCode("https://armor.example.com", "device-pub",
            new SystemInformation.Info("", "", ""))).Code.ShouldBe("invalid_request");
    }

    /// <remarks>Refer to "PostOauthToken posts oauth device grant"</remarks>
    [Fact]
    public void TestPostOauthTokenPostsOauthDeviceGrant()
    {
        Respond(200, """{"access_token":"token-123"}""");

        JsonNode result = ArmorApi.OauthToken("https://armor.example.com", "device-code");

        ShouldBeJson(result, """{"access_token":"token-123"}""");
        _requests[0].Url.ShouldBe("https://armor.example.com/oauth/token");
        ShouldBeJson(Body(), """
            {"client_id":"oac_dotenvxcli","device_code":"device-code","grant_type":"urn:ietf:params:oauth:grant-type:device_code"}
            """);
    }

    /// <remarks>Refer to "PostOauthToken throws oauth errors"</remarks>
    [Fact]
    public void TestPostOauthTokenThrowsOauthErrors()
    {
        Respond(400, """{"error":"authorization_pending","error_description":"still waiting"}""");

        Should.Throw<ApiException>(() => ArmorApi.OauthToken("https://armor.example.com", "device-code")).Code
            .ShouldBe("authorization_pending");
    }

    /// <remarks>Refer to "PostArmorDecrypt.run sends encrypted src for server-side decryption"</remarks>
    [Fact]
    public void TestPostArmorDecryptRunSendsEncryptedSrcForServerSideDecryption()
    {
        Respond(200, """{"src":"HELLO=World\n"}""");

        JsonNode result = ArmorApi.ArmorDecrypt("https://armor.dotenvx.com", "token-123", "device-public-key",
            "public-key", "HELLO=encrypted:ciphertext\n", null);

        _requests[0].Url.ShouldBe("https://armor.dotenvx.com/api/armor/decrypt");
        _requests[0].Method.ShouldBe("POST");
        _requests[0].Headers!["Authorization"].ShouldBe("Bearer token-123");
        ShouldBeJson(Body(), """
            {"device_public_key":"device-public-key","cli_version":"0.0.0-test","public_key":"public-key","src":"HELLO=encrypted:ciphertext\n"}
            """);
        ShouldBeJson(result, """{"src":"HELLO=World\n"}""");
    }

    /// <remarks>Refer to "PostArmorDecrypt.run includes grant token when supplied"</remarks>
    [Fact]
    public void TestPostArmorDecryptRunIncludesGrantTokenWhenSupplied()
    {
        Respond(200, """{"src":"HELLO=World\n"}""");

        ArmorApi.ArmorDecrypt("https://armor.dotenvx.com", "token-123", "device-public-key", "public-key",
            "HELLO=encrypted:ciphertext\n", "grant-token-123");

        Body()["grant_token"]!.ToString().ShouldBe("grant-token-123");
    }

    /// <remarks>Refer to "PostArmorDecrypt.run throws buildApiError output on non-2xx status"</remarks>
    [Fact]
    public void TestPostArmorDecryptRunThrowsBuildApiErrorOutputOnNon2xxStatus()
    {
        const string json = """{"error":{"code":"DECRYPTION_FAILED","message":"decryption failed"}}""";
        Respond(400, json);

        var error = Should.Throw<ApiException>(() => ArmorApi.ArmorDecrypt("https://armor.dotenvx.com", "token-123",
            "device-public-key", "public-key", "HELLO=encrypted:invalid\n", null));

        error.Message.ShouldBe("[DECRYPTION_FAILED] decryption failed");
        error.StatusCode.ShouldBe(400);
        ShouldBeJson(error.Json!, json);
    }

    /// <remarks>Refer to "PostArmorDown.run sends public key and team in request body"</remarks>
    [Fact]
    public void TestPostArmorDownRunSendsPublicKeyAndTeamInRequestBody()
    {
        Respond(200, """{"private_key":"private-key","changed":true}""");

        JsonNode result = ArmorApi.ArmorDown("https://armor.dotenvx.com", null, "device-public-key", "public-key", "dotenvx");

        _requests[0].Url.ShouldBe("https://armor.dotenvx.com/api/armor/down");
        _requests[0].Method.ShouldBe("POST");
        _requests[0].Headers!["Authorization"].ShouldBe("Bearer ");
        ShouldBeJson(Body(), """
            {"device_public_key":"device-public-key","cli_version":"0.0.0-test","public_key":"public-key","team":"dotenvx"}
            """);
        ShouldBeJson(result, """{"private_key":"private-key","changed":true}""");
    }

    /// <remarks>Refer to "PostArmorDown.run throws buildApiError output on non-2xx status"</remarks>
    [Fact]
    public void TestPostArmorDownRunThrowsBuildApiErrorOutputOnNon2xxStatus()
    {
        Respond(401, Unauthorized);

        var error = Should.Throw<ApiException>(() => ArmorApi.ArmorDown("https://armor.dotenvx.com", "token-123",
            "device-public-key", "public-key", "dotenvx"));

        error.StatusCode.ShouldBe(401);
        ShouldBeJson(error.Json!, Unauthorized);
    }

    /// <remarks>Refer to "PostArmorMove.run sends public key and team in request body"</remarks>
    [Fact]
    public void TestPostArmorMoveRunSendsPublicKeyAndTeamInRequestBody()
    {
        Respond(200, """{"changed":true,"team":"dotenvx"}""");

        JsonNode result = ArmorApi.ArmorMove("https://armor.dotenvx.com", "token-123", "device-public-key", "public-key", "dotenvx");

        _requests[0].Url.ShouldBe("https://armor.dotenvx.com/api/armor/move");
        _requests[0].Headers!["Authorization"].ShouldBe("Bearer token-123");
        ShouldBeJson(Body(), """
            {"device_public_key":"device-public-key","cli_version":"0.0.0-test","public_key":"public-key","team":"dotenvx"}
            """);
        ShouldBeJson(result, """{"changed":true,"team":"dotenvx"}""");
    }

    /// <remarks>Refer to "PostArmorMove.run throws buildApiError output on non-2xx status"</remarks>
    [Fact]
    public void TestPostArmorMoveRunThrowsBuildApiErrorOutputOnNon2xxStatus()
    {
        Respond(401, Unauthorized);

        var error = Should.Throw<ApiException>(() => ArmorApi.ArmorMove("https://armor.dotenvx.com", "token-123",
            "device-public-key", "public-key", "dotenvx"));

        error.StatusCode.ShouldBe(401);
        ShouldBeJson(error.Json!, Unauthorized);
    }

    /// <remarks>Refer to "PostArmorPull.run sends public key and team in request body"</remarks>
    [Fact]
    public void TestPostArmorPullRunSendsPublicKeyAndTeamInRequestBody()
    {
        Respond(200, """{"private_key":"private-key","changed":true}""");

        JsonNode result = ArmorApi.ArmorPull("https://armor.dotenvx.com", "token-123", "device-public-key", "public-key", "dotenvx");

        _requests[0].Url.ShouldBe("https://armor.dotenvx.com/api/armor/pull");
        ShouldBeJson(Body(), """
            {"device_public_key":"device-public-key","cli_version":"0.0.0-test","public_key":"public-key","team":"dotenvx"}
            """);
        ShouldBeJson(result, """{"private_key":"private-key","changed":true}""");
    }

    /// <remarks>Refer to "PostArmorPull.run throws buildApiError output on non-2xx status"</remarks>
    [Fact]
    public void TestPostArmorPullRunThrowsBuildApiErrorOutputOnNon2xxStatus()
    {
        Respond(401, Unauthorized);

        var error = Should.Throw<ApiException>(() => ArmorApi.ArmorPull("https://armor.dotenvx.com", "token-123",
            "device-public-key", "public-key", "dotenvx"));

        error.StatusCode.ShouldBe(401);
        ShouldBeJson(error.Json!, Unauthorized);
    }

    /// <remarks>Refer to "PostArmorPush.run sends private key and team in request body"</remarks>
    [Fact]
    public void TestPostArmorPushRunSendsPrivateKeyAndTeamInRequestBody()
    {
        Respond(200, """{"changed":true,"team":"dotenvx"}""");

        JsonNode result = ArmorApi.ArmorPush("https://armor.dotenvx.com", "token-123", "device-public-key", "private-key", "dotenvx");

        _requests[0].Url.ShouldBe("https://armor.dotenvx.com/api/armor/push");
        _requests[0].Headers!["Authorization"].ShouldBe("Bearer token-123");
        ShouldBeJson(Body(), """
            {"device_public_key":"device-public-key","cli_version":"0.0.0-test","private_key":"private-key","team":"dotenvx"}
            """);
        ShouldBeJson(result, """{"changed":true,"team":"dotenvx"}""");
    }

    /// <remarks>Refer to "PostArmorPush.run throws buildApiError output on non-2xx status"</remarks>
    [Fact]
    public void TestPostArmorPushRunThrowsBuildApiErrorOutputOnNon2xxStatus()
    {
        Respond(401, Unauthorized);

        var error = Should.Throw<ApiException>(() => ArmorApi.ArmorPush("https://armor.dotenvx.com", "token-123",
            "device-public-key", "private-key", "dotenvx"));

        error.StatusCode.ShouldBe(401);
        ShouldBeJson(error.Json!, Unauthorized);
    }

    /// <remarks>Refer to "PostArmorUp.run sends public and private keys when both are present"</remarks>
    [Fact]
    public void TestPostArmorUpRunSendsPublicAndPrivateKeysWhenBothArePresent()
    {
        Respond(200, """{"changed":true}""");

        JsonNode result = ArmorApi.ArmorUp("https://armor.dotenvx.com", null, "device-public-key", "public-key",
            "private-key", "dotenvx");

        _requests[0].Url.ShouldBe("https://armor.dotenvx.com/api/armor/up");
        _requests[0].Method.ShouldBe("POST");
        _requests[0].Headers!["Authorization"].ShouldBe("Bearer ");
        ShouldBeJson(Body(), """
            {"device_public_key":"device-public-key","cli_version":"0.0.0-test","public_key":"public-key","private_key":"private-key","team":"dotenvx"}
            """);
        ShouldBeJson(result, """{"changed":true}""");
    }

    /// <remarks>Refer to "PostArmorUp.run sends null private key when it is not present locally"</remarks>
    [Fact]
    public void TestPostArmorUpRunSendsNullPrivateKeyWhenItIsNotPresentLocally()
    {
        Respond(200, """{"changed":false}""");

        ArmorApi.ArmorUp("https://armor.dotenvx.com", null, "device-public-key", "public-key", null, "dotenvx");

        _requests.Count.ShouldBe(1);
        ShouldBeJson(Body(), """
            {"device_public_key":"device-public-key","cli_version":"0.0.0-test","public_key":"public-key","private_key":null,"team":"dotenvx"}
            """);
    }

    /// <remarks>Refer to "PostArmorUp.run throws buildApiError output on non-2xx status"</remarks>
    [Fact]
    public void TestPostArmorUpRunThrowsBuildApiErrorOutputOnNon2xxStatus()
    {
        Respond(401, Unauthorized);

        var error = Should.Throw<ApiException>(() => ArmorApi.ArmorUp("https://armor.dotenvx.com", "token-123",
            "device-public-key", "public-key", null, null));

        error.StatusCode.ShouldBe(401);
        ShouldBeJson(error.Json!, Unauthorized);
    }

    /// <remarks>Refer to "PostLogout posts logout with bearer token"</remarks>
    [Fact]
    public void TestPostLogoutPostsLogoutWithBearerToken()
    {
        Respond(200, """{"ok":true}""");

        JsonNode result = ArmorApi.Logout("https://armor.example.com", "token-123");

        ShouldBeJson(result, """{"ok":true}""");
        _requests[0].Url.ShouldBe("https://armor.example.com/api/logout");
        _requests[0].Method.ShouldBe("POST");
        _requests[0].Headers.ShouldBe(new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer token-123",
            ["Content-Type"] = "application/json"
        });
        ShouldBeJson(Body(), "{}");
    }

    /// <remarks>Refer to "PostLogout sends empty bearer token when token is missing"</remarks>
    [Fact]
    public void TestPostLogoutSendsEmptyBearerTokenWhenTokenIsMissing()
    {
        Respond(200, """{"ok":true}""");

        ArmorApi.Logout("https://armor.example.com", null);

        _requests[0].Headers!["Authorization"].ShouldBe("Bearer ");
    }

    /// <remarks>Refer to "PostLogout throws api errors"</remarks>
    [Fact]
    public void TestPostLogoutThrowsApiErrors()
    {
        Respond(401, Unauthorized);

        var error = Should.Throw<ApiException>(() => ArmorApi.Logout("https://armor.example.com", "token-123"));

        error.Code.ShouldBe("UNAUTHORIZED");
        error.ErrorHelp.ShouldBe("fix: [login again]");
        ShouldBeJson(error.Json!, Unauthorized);
    }

    /// <remarks>Refer to "PostLogout api error falls back to status and json help"</remarks>
    [Fact]
    public void TestPostLogoutApiErrorFallsBackToStatusAndJsonHelp()
    {
        const string json = """{"error":{"message":"bad token"}}""";
        Respond(401, json);

        var error = Should.Throw<ApiException>(() => ArmorApi.Logout("https://armor.example.com", "token-123"));

        error.Code.ShouldBe("401");
        error.ErrorHelp.ShouldBe($"[401] {json}");
    }
}
