using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/tests/lib/helpers/hasPlaintextSecrets.test.js. Upstream's `hasPlaintextSecrets(src)` is
/// `!sealed(src)` (here <c>!Primitives.Sealed</c>); its protectStdin `check()` rejects a file when `plaintextKeys` is not
/// empty (here <see cref="Protect.PlaintextKeys" />).
/// </remarks>
public class HasPlaintextSecretsTest
{
    private static bool HasPlaintextSecrets(string src)
    {
        return !global::Dotenvx.Primitives.Primitives.Sealed(src);
    }

    /// <remarks>Refer to "protection allows secret references without resolving them"</remarks>
    [Fact]
    public void TestProtectionAllowsSecretReferencesWithoutResolvingThem()
    {
        const string src = "PASSWORD=op://vault/item/password\nLOGIN=\"bw://My GitHub Account/username\"\n";

        HasPlaintextSecrets(src).ShouldBeFalse();
        Protect.PlaintextKeys(src).ShouldBeEmpty();
    }

    /// <remarks>Refer to "references do not hide plaintext assignments"</remarks>
    [Theory]
    [InlineData("op://vault/item/password")]
    [InlineData("bw://item/password")]
    public void TestReferencesDoNotHidePlaintextAssignments(string reference)
    {
        HasPlaintextSecrets($"SECRET={reference}\nOTHER=plaintext").ShouldBeTrue();
        HasPlaintextSecrets($"SECRET=plaintext\nSECRET={reference}").ShouldBeTrue();
        HasPlaintextSecrets($"SECRET={reference}\nSECRET=plaintext").ShouldBeTrue();
    }

    /// <remarks>Refer to "only supported literal prefixes are exempt"</remarks>
    [Theory]
    [InlineData(@"op\://vault/item/password")]
    [InlineData("OP://vault/item/password")]
    [InlineData("BW://item/password")]
    [InlineData("https://example.com")]
    [InlineData("prefix-op://vault/item/password")]
    [InlineData("$(some-command)")]
    [InlineData("${OTHER}")]
    public void TestOnlySupportedLiteralPrefixesAreExempt(string value)
    {
        HasPlaintextSecrets($"SECRET={value}").ShouldBeTrue(value);
    }

    /// <remarks>Refer to "existing protection exemptions still apply"</remarks>
    [Fact]
    public void TestExistingProtectionExemptionsStillApply()
    {
        HasPlaintextSecrets("EMPTY=\nWHITESPACE=\" \"\nSECRET=encrypted:abc\nDOTENV_PUBLIC_KEY=public\nVISIBLE_PLAIN=value")
            .ShouldBeFalse();
        HasPlaintextSecrets("SECRET=plaintext").ShouldBeTrue();
    }
}
