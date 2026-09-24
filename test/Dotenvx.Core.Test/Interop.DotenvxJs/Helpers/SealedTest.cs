using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: @dotenvx/primitives' `sealed()` export - no readable source, matrix verified
/// live via `node -e "..."` against the actual installed package (see Sealed.cs's doc comment).
/// </remarks>
public class SealedTest
{
    [Fact]
    public void TestSealedAllEncrypted()
    {
        Sealed("HELLO=\"encrypted:abc\"").ShouldBeTrue();
    }

    [Fact]
    public void TestSealedPlainUnencrypted()
    {
        Sealed("HELLO=world").ShouldBeFalse();
    }

    [Fact]
    public void TestSealedDotenvPublicKeyExempt()
    {
        Sealed("DOTENV_PUBLIC_KEY=\"03abc\"\nHELLO=\"encrypted:abc\"").ShouldBeTrue();
    }

    [Fact]
    public void TestSealedDotenvPublicKeyPrefixedNameExempt()
    {
        Sealed("DOTENV_PUBLIC_KEY_PRODUCTION=\"03abc\"\nHELLO=\"encrypted:abc\"").ShouldBeTrue();
    }

    [Fact]
    public void TestSealedPlainSuffixExempt()
    {
        Sealed("HELLO=\"encrypted:abc\"\nSOMETHING_PLAIN=\"safe\"").ShouldBeTrue();
    }

    [Fact]
    public void TestSealedMixedOnePlainUnencrypted()
    {
        Sealed("HELLO=\"encrypted:abc\"\nWORLD=notencrypted").ShouldBeFalse();
    }

    [Fact]
    public void TestSealedEmptyContentIsVacuouslySealed()
    {
        Sealed("").ShouldBeTrue();
    }

    [Fact]
    public void TestSealedDotenvPublicKeyUnencryptedValueIsExemptRegardless()
    {
        Sealed("DOTENV_PUBLIC_KEY=notencrypted").ShouldBeTrue();
    }

    /// <remarks>
    /// Confirms EVERY occurrence of a duplicate key must be encrypted, not just the last - real
    /// @dotenvx/primitives behavior, verified live, that DotenvParse's default last-value-wins
    /// (and even its own collectAllValues param, which keeps only the FIRST occurrence) can't
    /// replicate - see Sealed.cs's doc comment for why it has its own dedicated parsing instead.
    /// </remarks>
    [Fact]
    public void TestSealedDuplicateKeyRequiresEveryOccurrenceEncrypted()
    {
        Sealed("HELLO=notencrypted\nHELLO=\"encrypted:abc\"").ShouldBeFalse();
        Sealed("HELLO=\"encrypted:abc\"\nHELLO=notencrypted").ShouldBeFalse();
    }
}
