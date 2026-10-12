using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/canonicalEnvFilename.test.js</remarks>
public class CanonicalEnvFilenameTest
{
    /// <remarks>Refer to "#canonicalEnvFilename (.env)"</remarks>
    [Fact]
    public void TestCanonicalEnvFilenameDotEnv()
    {
        CanonicalEnvFilename(".env").ShouldBe(".env");
    }

    /// <remarks>Refer to "#canonicalEnvFilename (.env.txt)"</remarks>
    [Fact]
    public void TestCanonicalEnvFilenameDotEnvTxt()
    {
        CanonicalEnvFilename(".env.txt").ShouldBe(".env");
    }

    /// <remarks>Refer to "#canonicalEnvFilename (.env.production.txt)"</remarks>
    [Fact]
    public void TestCanonicalEnvFilenameDotEnvProductionTxt()
    {
        CanonicalEnvFilename(".env.production.txt").ShouldBe(".env.production");
    }

    /// <remarks>Refer to "#canonicalEnvFilename (.ENV.LOCAL.TXT)"</remarks>
    [Fact]
    public void TestCanonicalEnvFilenameUppercase()
    {
        CanonicalEnvFilename(".ENV.LOCAL.TXT").ShouldBe(".env.local");
    }

    /// <remarks>Refer to "#canonicalEnvFilename (secrets.txt)"</remarks>
    [Fact]
    public void TestCanonicalEnvFilenameNonEnvFileUnaffected()
    {
        CanonicalEnvFilename("secrets.txt").ShouldBe("secrets.txt");
    }
}
