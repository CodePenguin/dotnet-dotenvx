using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/tests/lib/helpers/validate.test.js</remarks>
public class ValidateTest
{
    private static Dictionary<string, List<string>> Example(params (string Key, string[] Values)[] entries)
    {
        return entries.ToDictionary(e => e.Key, e => e.Values.ToList());
    }

    private static ValidationError MissingRequired(params string[] keys)
    {
        return new ValidationError("MISSING_REQUIRED", keys, $"missing required ({string.Join(", ", keys)})");
    }

    /// <remarks>Refer to "validate accepts an empty example"</remarks>
    [Fact]
    public void TestAcceptsAnEmptyExample()
    {
        ValidationResult result = Validate(Example(), new Dictionary<string, string>());

        result.Valid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    /// <remarks>Refer to "validate accepts required keys that exist"</remarks>
    [Fact]
    public void TestAcceptsRequiredKeysThatExist()
    {
        ValidationResult result = Validate(Example(("HELLO", [""])), new Dictionary<string, string> { ["HELLO"] = "World" });

        result.Valid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    /// <remarks>Refer to "validate rejects required keys with empty values"</remarks>
    [Fact]
    public void TestRejectsRequiredKeysWithEmptyValues()
    {
        ValidationResult result = Validate(Example(("HELLO", [""])), new Dictionary<string, string> { ["HELLO"] = "" });

        result.Valid.ShouldBeFalse();
        result.Errors.ShouldBe([MissingRequired("HELLO")]);
    }

    /// <remarks>Refer to "validate rejects required keys with whitespace-only values"</remarks>
    [Fact]
    public void TestRejectsRequiredKeysWithWhitespaceOnlyValues()
    {
        ValidationResult result = Validate(Example(("HELLO", [""])), new Dictionary<string, string> { ["HELLO"] = " \t" });

        result.Valid.ShouldBeFalse();
        result.Errors.ShouldBe([MissingRequired("HELLO")]);
    }

    /// <remarks>Refer to "validate collects all missing required keys"</remarks>
    [Fact]
    public void TestCollectsAllMissingRequiredKeys()
    {
        ValidationResult result = Validate(Example(("HELLO", [""]), ("DATABASE_URL", [""]), ("API_KEY", [""])),
            new Dictionary<string, string> { ["HELLO"] = "World" });

        result.Valid.ShouldBeFalse();
        result.Errors.ShouldBe([MissingRequired("DATABASE_URL", "API_KEY")]);
    }

    /// <remarks>Refer to "validate accepts keys marked optional by an inline comment"</remarks>
    [Fact]
    public void TestAcceptsKeysMarkedOptionalByAnInlineComment()
    {
        Dictionary<string, List<string>> example = Example(("REQUIRED", [""]), ("OPTIONAL_EMPTY", [""]),
            ("OPTIONAL_VALUE", ["fallback"]), ("OPTIONAL_UPPERCASE", [""]));
        Dictionary<string, List<string?>> comments = new()
        {
            ["REQUIRED"] = [null],
            ["OPTIONAL_EMPTY"] = ["optional"],
            ["OPTIONAL_VALUE"] = ["this is optional here"],
            ["OPTIONAL_UPPERCASE"] = ["OPTIONAL"]
        };

        ValidationResult result = Validate(example, new Dictionary<string, string>(), comments);

        result.Valid.ShouldBeFalse();
        result.Errors.ShouldBe([MissingRequired("REQUIRED")]);
    }

    /// <remarks>Refer to "validate accepts a key if any duplicate occurrence is optional"</remarks>
    [Fact]
    public void TestAcceptsAKeyIfAnyDuplicateOccurrenceIsOptional()
    {
        Dictionary<string, List<string>> example = Example(("OPTIONAL", ["", ""]));
        Dictionary<string, List<string?>> comments = new() { ["OPTIONAL"] = [null, "optional"] };

        ValidationResult result = Validate(example, new Dictionary<string, string>(), comments);

        result.Valid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }
}
