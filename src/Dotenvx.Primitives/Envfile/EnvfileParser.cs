using System.Globalization;
using System.Text;

namespace Dotenvx.Primitives;

/// <summary>A position in the Envfile source (offset in UTF-16 units, 1-based line and column), as the generated parser reports it.</summary>
public readonly record struct EnvfileLocation(int Offset, int Line, int Column);

/// <summary>One thing the parser expected where it failed: a literal, a character class, any character, end of input, or a named rule.</summary>
public sealed record EnvfileExpectation(string Type, string? Text = null, string? Description = null, string[]? Parts = null,
    bool Inverted = false);

/// <summary>An Envfile that is malformed (syntax or configuration). The message is `[MALFORMED_ENVFILE] ...`.</summary>
public sealed class MalformedEnvfileException : Exception
{
    public MalformedEnvfileException(string message) : base($"[MALFORMED_ENVFILE] {message}")
    {
    }

    public string Code => "MALFORMED_ENVFILE";
    public string MessageWithHelp => Message;

    /// <summary>The start of the offending text; null for a configuration error that has no source position.</summary>
    public EnvfileLocation? Location { get; init; }

    /// <summary>What the parser expected at <see cref="Location" />; null for an error raised by a rule's own check.</summary>
    public IReadOnlyList<EnvfileExpectation>? Expected { get; init; }

    /// <summary>The message of the underlying parser error (what the CLI shows instead of <see cref="Exception.Message" />).</summary>
    public string? CauseMessage { get; init; }
}

internal sealed class EnvDeclaration
{
    public string Name { get; init; } = "";
    public bool? Required { get; set; }
    public string? Type { get; set; }
    public bool? Encrypted { get; set; }
    public bool? Redacted { get; set; }
    public string? Min { get; set; }
    public string? Max { get; set; }
    public List<string>? Enum { get; set; }

    /// <summary>`{...this, ...over}`: the properties the other declaration sets win.</summary>
    public EnvDeclaration MergedWith(EnvDeclaration over)
    {
        return new EnvDeclaration
        {
            Name = over.Name,
            Required = over.Required ?? Required,
            Type = over.Type ?? Type,
            Encrypted = over.Encrypted ?? Encrypted,
            Redacted = over.Redacted ?? Redacted,
            Min = over.Min ?? Min,
            Max = over.Max ?? Max,
            Enum = over.Enum ?? Enum
        };
    }
}

internal sealed class EnvfileFileBlock
{
    public string File { get; init; } = "";
    public bool? Strict { get; init; }
    public List<EnvDeclaration> Declarations { get; init; } = [];
}

internal sealed class EnvfileProfile
{
    public string Profile { get; init; } = "";
    public List<string> Paths { get; init; } = [];
}

internal sealed class EnvfileDocument
{
    public bool? Strict { get; init; }
    public List<EnvDeclaration> Declarations { get; init; } = [];
    public List<EnvfileFileBlock> Files { get; init; } = [];
    public List<EnvfileProfile> Profiles { get; init; } = [];
}

/// <remarks>
/// The Envfile grammar of @dotenvx/primitives 3.3.0, ported rule by rule from its generated (peggy) parser so that the
/// reported position and the "expected" set at the furthest failure are the same. Rule names follow the grammar:
/// Document, Profile, File block, Strict, Env declaration with its options. A rule's own checks (duplicate settings and
/// options) run when the rule matches, as in the generated code.
/// </remarks>
internal sealed class EnvfileParser
{
    // expectations, named after what the generated parser calls them
    private static readonly EnvfileExpectation ExpectSpace = Class([" ", "\t"], false);
    private static readonly EnvfileExpectation ExpectNotQuoteOrLineBreak = Class(["\"", "\r", "\n", "\0"], true);
    private static readonly EnvfileExpectation ExpectNotApostropheOrLineBreak = Class(["'", "\r", "\n", "\0"], true);
    private static readonly EnvfileExpectation ExpectSign = Class(["+", "-"], false);
    private static readonly EnvfileExpectation ExpectDigit = Class(["0-9"], false);
    private static readonly EnvfileExpectation ExpectNotDoubleQuoted = Class(["\"", "\\", "\r", "\n"], true);
    private static readonly EnvfileExpectation ExpectNotSingleQuoted = Class(["'", "\\", "\r", "\n"], true);
    private static readonly EnvfileExpectation ExpectEscape = Class(["\"", "'", "\\", "n", "r", "t"], false);
    private static readonly EnvfileExpectation ExpectNameStart = Class(["A-Z", "a-z", "_"], false);
    private static readonly EnvfileExpectation ExpectNameChar = Class(["A-Z", "a-z", "0-9", "_"], false);
    private static readonly EnvfileExpectation ExpectNotLineBreak = Class(["\r", "\n"], true);
    private static readonly EnvfileExpectation ExpectLineBreak = Class(["\n", "\r"], false);
    private static readonly EnvfileExpectation ExpectAny = new("any");
    private static readonly EnvfileExpectation ExpectEnd = new("end");
    private static readonly EnvfileExpectation OtherFilename = new("other", Description: "a quoted filename");
    private static readonly EnvfileExpectation OtherInteger = new("other", Description: "an integer");
    private static readonly EnvfileExpectation OtherType = new("other",
        Description: "a supported type (string, integer, boolean, port, url, email, or ip)");
    private static readonly EnvfileExpectation OtherBoolean = new("other", Description: "true or false");
    private static readonly EnvfileExpectation OtherName = new("other", Description: "a quoted variable name");

    private static EnvfileExpectation Literal(string text)
    {
        return new EnvfileExpectation("literal", text);
    }

    private static EnvfileExpectation Class(string[] parts, bool inverted)
    {
        return new EnvfileExpectation("class", Parts: parts, Inverted: inverted);
    }

    private readonly string _src;
    private int _pos;
    private int _silent;
    private int _maxFailPos;
    private List<EnvfileExpectation> _maxFailExpected = [];
    private int _ruleStart; // `location()` of the rule whose action is running: start offset

    private EnvfileParser(string src)
    {
        _src = src;
    }

    public static EnvfileDocument Parse(string src)
    {
        EnvfileParser parser = new(src);
        try
        {
            return parser.Document();
        }
        catch (EnvfileSyntaxError error)
        {
            throw new MalformedEnvfileException(error.Message)
            {
                Location = error.Start, Expected = error.Expected, CauseMessage = error.Message
            };
        }
    }

    // ---- failure tracking (the generated parser's v(), A counter and Yo)

    private void Fail(EnvfileExpectation expectation)
    {
        if (_silent != 0 || _pos < _maxFailPos)
        {
            return;
        }

        if (_pos > _maxFailPos)
        {
            _maxFailPos = _pos;
            _maxFailExpected = [];
        }

        _maxFailExpected.Add(expectation);
    }

    private sealed class EnvfileSyntaxError(string message, EnvfileLocation start, List<EnvfileExpectation>? expected)
        : Exception(message)
    {
        public EnvfileLocation Start { get; } = start;
        public List<EnvfileExpectation>? Expected { get; } = expected;
    }

    private EnvfileLocation LocationAt(int offset)
    {
        int line = 1;
        int column = 1;
        for (int i = 0; i < offset && i < _src.Length; i++)
        {
            if (_src[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return new EnvfileLocation(offset, line, column);
    }

    /// <summary>A rule's own check failed (`error(message)` in the grammar): thrown at the rule's start.</summary>
    private EnvfileSyntaxError RuleError(string message)
    {
        return new EnvfileSyntaxError(message, LocationAt(_ruleStart), null);
    }

    private EnvfileSyntaxError FinalError()
    {
        if (_pos < _src.Length)
        {
            Fail(ExpectEnd);
        }

        string? found = _maxFailPos < _src.Length ? RuneAt(_maxFailPos) : null;
        return new EnvfileSyntaxError(BuildMessage(_maxFailExpected, found), LocationAt(_maxFailPos), _maxFailExpected);
    }

    private string RuneAt(int index)
    {
        return Rune.TryGetRuneAt(_src, index, out Rune rune) ? rune.ToString() : _src[index].ToString();
    }

    // ---- primitives

    private bool Lit(string text, EnvfileExpectation expectation)
    {
        if (string.CompareOrdinal(_src, _pos, text, 0, text.Length) == 0 && _pos + text.Length <= _src.Length)
        {
            _pos += text.Length;
            return true;
        }

        Fail(expectation);
        return false;
    }

    private bool Cls(Func<char, bool> test, EnvfileExpectation expectation)
    {
        if (_pos < _src.Length && test(_src[_pos]))
        {
            _pos++;
            return true;
        }

        Fail(expectation);
        return false;
    }

    private static bool IsSpace(char c)
    {
        return c is ' ' or '\t';
    }

    private static bool IsDigit(char c)
    {
        return c is >= '0' and <= '9';
    }

    private void SkipSpaces()
    {
        while (Cls(IsSpace, ExpectSpace))
        {
        }
    }

    private bool OneOrMoreSpaces()
    {
        if (!Cls(IsSpace, ExpectSpace))
        {
            return false;
        }

        SkipSpaces();
        return true;
    }

    /// <summary>`"\r\n" / [\n\r]`</summary>
    private bool LineBreak()
    {
        if (Lit("\r\n", Literal("\r\n")))
        {
            return true;
        }

        return Cls(c => c is '\n' or '\r', ExpectLineBreak);
    }

    /// <summary>`"#" [^\r\n]*`</summary>
    private bool Comment()
    {
        int start = _pos;
        if (!Lit("#", Literal("#")))
        {
            _pos = start;
            return false;
        }

        while (Cls(c => c is not '\r' and not '\n', ExpectNotLineBreak))
        {
        }

        return true;
    }

    /// <summary>`(space / line break / comment)*`</summary>
    private void Filler()
    {
        while (true)
        {
            if (Cls(IsSpace, ExpectSpace))
            {
                continue;
            }

            if (LineBreak())
            {
                continue;
            }

            if (Comment())
            {
                continue;
            }

            return;
        }
    }

    /// <summary>`comment? (line break / !.)`: the end of a statement.</summary>
    private bool EndOfStatement()
    {
        Comment();
        if (LineBreak())
        {
            return true;
        }

        _silent++;
        bool atEnd = _pos >= _src.Length;
        if (!atEnd)
        {
            // the generated parser consumes one character in the lookahead to test it, then restores
            Fail(ExpectAny);
        }

        _silent--;
        return atEnd;
    }

    // ---- grammar

    private EnvfileDocument Document()
    {
        int start = _pos;
        Filler();
        List<object> statements = [];
        while (true)
        {
            int mark = _pos;
            object? statement = ProfileStatement() ?? FileBlock() ?? EnvStatement() ?? (object?)StrictStatement();
            if (statement == null)
            {
                _pos = mark;
                break;
            }

            Filler();
            statements.Add(statement);
        }

        EnvfileDocument document;
        _ruleStart = start;
        List<bool> stricts = [];
        foreach (object statement in statements)
        {
            if (statement is StrictSetting setting)
            {
                stricts.Add(setting.Value);
            }
        }

        if (stricts.Count > 1)
        {
            throw RuleError("Duplicate Envfile setting: strict");
        }

        document = new EnvfileDocument
        {
            Strict = stricts.Count == 1 ? stricts[0] : null,
            Declarations = statements.OfType<EnvDeclaration>().ToList(),
            Files = statements.OfType<EnvfileFileBlock>().ToList(),
            Profiles = statements.OfType<EnvfileProfile>().ToList()
        };

        if (_pos != _src.Length)
        {
            throw FinalError();
        }

        return document;
    }

    private sealed record StrictSetting(bool Value);

    /// <summary>`"profile" _+ Name _* "," Filler "files" _* ":" Filler List _* comment? EOL`</summary>
    private EnvfileProfile? ProfileStatement()
    {
        int start = _pos;
        if (!Lit("profile", Literal("profile")) || !OneOrMoreSpaces())
        {
            _pos = start;
            return null;
        }

        string? name = QuotedFilename();
        if (name == null)
        {
            _pos = start;
            return null;
        }

        SkipSpaces();
        if (!Lit(",", Literal(",")))
        {
            _pos = start;
            return null;
        }

        Filler();
        if (!Lit("files", Literal("files")))
        {
            _pos = start;
            return null;
        }

        SkipSpaces();
        if (!Lit(":", Literal(":")))
        {
            _pos = start;
            return null;
        }

        Filler();
        List<string>? paths = FilenameList();
        if (paths == null)
        {
            _pos = start;
            return null;
        }

        SkipSpaces();
        if (!EndOfStatement())
        {
            _pos = start;
            return null;
        }

        return new EnvfileProfile { Profile = name, Paths = paths };
    }

    /// <summary>`"[" Filler Name (Filler "," Filler Name)* Filler ","? Filler "]" / "[" Filler "]"`</summary>
    private List<string>? FilenameList()
    {
        int start = _pos;
        if (Lit("[", Literal("[")))
        {
            Filler();
            string? first = QuotedFilename();
            if (first != null)
            {
                List<string> items = [first];
                while (true)
                {
                    int mark = _pos;
                    Filler();
                    if (Lit(",", Literal(",")))
                    {
                        Filler();
                        string? next = QuotedFilename();
                        if (next != null)
                        {
                            items.Add(next);
                            continue;
                        }
                    }

                    _pos = mark;
                    break;
                }

                Filler();
                Lit(",", Literal(","));
                Filler();
                if (Lit("]", Literal("]")))
                {
                    return items;
                }
            }
        }

        _pos = start;
        if (Lit("[", Literal("[")))
        {
            Filler();
            if (Lit("]", Literal("]")))
            {
                return [];
            }
        }

        _pos = start;
        return null;
    }

    /// <summary>`"file" _+ Name _+ "do" _* comment? line-break Filler ((env / strict) Filler)* "end" _* comment? EOL`</summary>
    private EnvfileFileBlock? FileBlock()
    {
        int start = _pos;
        if (!Lit("file", Literal("file")) || !OneOrMoreSpaces())
        {
            _pos = start;
            return null;
        }

        string? file = QuotedFilename();
        if (file == null || !OneOrMoreSpaces() || !Lit("do", Literal("do")))
        {
            _pos = start;
            return null;
        }

        SkipSpaces();
        Comment();
        if (!LineBreak())
        {
            _pos = start;
            return null;
        }

        Filler();
        List<object> items = [];
        while (true)
        {
            int mark = _pos;
            object? item = EnvStatement() ?? (object?)StrictStatement();
            if (item == null)
            {
                _pos = mark;
                break;
            }

            Filler();
            items.Add(item);
        }

        if (!Lit("end", Literal("end")))
        {
            _pos = start;
            return null;
        }

        SkipSpaces();
        if (!EndOfStatement())
        {
            _pos = start;
            return null;
        }

        _ruleStart = start;
        List<StrictSetting> stricts = items.OfType<StrictSetting>().ToList();
        if (stricts.Count > 1)
        {
            throw RuleError("Duplicate Envfile setting: strict");
        }

        return new EnvfileFileBlock
        {
            File = file,
            Strict = stricts.Count == 1 ? stricts[0].Value : null,
            Declarations = items.OfType<EnvDeclaration>().ToList()
        };
    }

    /// <summary>`"strict" _+ Boolean _* comment? EOL`</summary>
    private StrictSetting? StrictStatement()
    {
        int start = _pos;
        if (!Lit("strict", Literal("strict")) || !OneOrMoreSpaces())
        {
            _pos = start;
            return null;
        }

        bool? value = BooleanValue();
        if (value == null)
        {
            _pos = start;
            return null;
        }

        SkipSpaces();
        if (!EndOfStatement())
        {
            _pos = start;
            return null;
        }

        return new StrictSetting(value.Value);
    }

    /// <summary>A quoted filename (named rule "a quoted filename").</summary>
    private string? QuotedFilename()
    {
        _silent++;
        string? result = QuotedFilenameBody("\"", ExpectNotQuoteOrLineBreak) ??
                         QuotedFilenameBody("'", ExpectNotApostropheOrLineBreak);
        _silent--;
        if (result == null)
        {
            Fail(OtherFilename);
        }

        return result;
    }

    private string? QuotedFilenameBody(string quote, EnvfileExpectation inner)
    {
        int start = _pos;
        if (!Lit(quote, Literal(quote)))
        {
            return null;
        }

        int textStart = _pos;
        char quoteChar = quote[0];
        bool any = false;
        while (Cls(c => c != quoteChar && c != '\r' && c != '\n' && c != '\0', inner))
        {
            any = true;
        }

        if (!any)
        {
            _pos = start;
            return null;
        }

        string text = _src[textStart.._pos];
        if (!Lit(quote, Literal(quote)))
        {
            _pos = start;
            return null;
        }

        return text;
    }

    /// <summary>`"env" _+ Name Option* _* comment? EOL`</summary>
    private EnvDeclaration? EnvStatement()
    {
        int start = _pos;
        if (!Lit("env", Literal("env")) || !OneOrMoreSpaces())
        {
            _pos = start;
            return null;
        }

        string? name = QuotedVariableName();
        if (name == null)
        {
            _pos = start;
            return null;
        }

        List<(string Key, object Value)> options = [];
        while (EnvOption() is { } option)
        {
            options.Add(option);
        }

        SkipSpaces();
        if (!EndOfStatement())
        {
            _pos = start;
            return null;
        }

        _ruleStart = start;
        EnvDeclaration declaration = new() { Name = name };
        HashSet<string> seen = [];
        foreach ((string key, object value) in options)
        {
            if (seen.Contains(key))
            {
                throw RuleError($"Duplicate Envfile option: {key}");
            }

            if ((key == "optional" && seen.Contains("required")) || (key == "required" && seen.Contains("optional")))
            {
                throw RuleError("Cannot combine required and optional in a Envfile declaration");
            }

            seen.Add(key);
            switch (key)
            {
                case "optional":
                    declaration.Required = !(bool)value;
                    break;
                case "required":
                    declaration.Required = (bool)value;
                    break;
                case "redacted":
                    declaration.Redacted = (bool)value;
                    break;
                case "encrypted":
                    declaration.Encrypted = (bool)value;
                    break;
                case "type":
                    declaration.Type = (string)value;
                    break;
                case "enum":
                    declaration.Enum = (List<string>)value;
                    break;
                case "min":
                    declaration.Min = (string)value;
                    break;
                case "max":
                    declaration.Max = (string)value;
                    break;
            }
        }

        return declaration;
    }

    /// <summary>One `_* "," Filler key _* ":" Filler value`; the key decides the value's shape.</summary>
    private (string Key, object Value)? EnvOption()
    {
        int start = _pos;

        (string, object)? Alternative(string[] keys, Func<object?> value, Func<string, string> map)
        {
            _pos = start;
            SkipSpaces();
            if (!Lit(",", Literal(",")))
            {
                return null;
            }

            Filler();
            string? matched = null;
            foreach (string key in keys)
            {
                if (Lit(key, Literal(key)))
                {
                    matched = key;
                    break;
                }
            }

            if (matched == null)
            {
                return null;
            }

            SkipSpaces();
            if (!Lit(":", Literal(":")))
            {
                return null;
            }

            Filler();
            object? parsed = value();
            return parsed == null ? null : (map(matched), parsed);
        }

        object? Bool()
        {
            return BooleanValue();
        }

        return Alternative(["required"], Bool, _ => "required") ??
               Alternative(["optional"], Bool, _ => "optional") ??
               Alternative(["redacted", "redact"], Bool, _ => "redacted") ??
               Alternative(["encrypted", "encrypt", "encypted"], Bool, _ => "encrypted") ??
               Alternative(["type"], TypeValue, _ => "type") ??
               Alternative(["enum"], EnumValue, _ => "enum") ??
               Alternative(["min", "max"], () => IntegerValue(), key => key) ??
               Reset(start);
    }

    private (string, object)? Reset(int start)
    {
        _pos = start;
        return null;
    }

    /// <summary>Named rule "a quoted variable name".</summary>
    private string? QuotedVariableName()
    {
        _silent++;
        string? result = null;
        foreach (string quote in new[] { "\"", "'" })
        {
            int start = _pos;
            if (Lit(quote, Literal(quote)))
            {
                string? name = VariableName();
                if (name != null && Lit(quote, Literal(quote)))
                {
                    result = name;
                    break;
                }
            }

            _pos = start;
        }

        _silent--;
        if (result == null)
        {
            Fail(OtherName);
        }

        return result;
    }

    private string? VariableName()
    {
        int start = _pos;
        if (!Cls(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_', ExpectNameStart))
        {
            return null;
        }

        while (Cls(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_', ExpectNameChar))
        {
        }

        return _src[start.._pos];
    }

    /// <summary>Named rule "true or false".</summary>
    private bool? BooleanValue()
    {
        _silent++;
        bool? result = null;
        if (Lit("true", Literal("true")))
        {
            result = true;
        }
        else if (Lit("false", Literal("false")))
        {
            result = false;
        }

        _silent--;
        if (result == null)
        {
            Fail(OtherBoolean);
        }

        return result;
    }

    /// <summary>Named rule "an integer": `[+-]? [0-9]+`.</summary>
    private string? IntegerValue()
    {
        _silent++;
        int start = _pos;
        Cls(c => c is '+' or '-', ExpectSign);
        string? result = null;
        if (Cls(IsDigit, ExpectDigit))
        {
            while (Cls(IsDigit, ExpectDigit))
            {
            }

            result = _src[start.._pos];
        }
        else
        {
            _pos = start;
        }

        _silent--;
        if (result == null)
        {
            Fail(OtherInteger);
        }

        return result;
    }

    /// <summary>Named rule "a supported type (...)".</summary>
    private object? TypeValue()
    {
        _silent++;
        string? result = null;
        foreach (string type in new[] { "string", "integer", "boolean", "port", "url", "email", "ip" })
        {
            if (Lit($"\"{type}\"", Literal($"\"{type}\"")) || Lit($"'{type}'", Literal($"'{type}'")))
            {
                result = type;
                break;
            }
        }

        _silent--;
        if (result == null)
        {
            Fail(OtherType);
        }

        return result;
    }

    /// <summary>`"[" Filler Choice (Filler "," Filler Choice)* Filler ","? Filler "]"`</summary>
    private object? EnumValue()
    {
        int start = _pos;
        if (Lit("[", Literal("[")))
        {
            Filler();
            string? first = EnumChoice();
            if (first != null)
            {
                List<string> items = [first];
                while (true)
                {
                    int mark = _pos;
                    Filler();
                    if (Lit(",", Literal(",")))
                    {
                        Filler();
                        string? next = EnumChoice();
                        if (next != null)
                        {
                            items.Add(next);
                            continue;
                        }
                    }

                    _pos = mark;
                    break;
                }

                Filler();
                Lit(",", Literal(","));
                Filler();
                if (Lit("]", Literal("]")))
                {
                    return items;
                }
            }
        }

        _pos = start;
        return null;
    }

    /// <summary>A double-quoted string, a single-quoted string (with `\" \' \\ \n \r \t` escapes), or an integer.</summary>
    private string? EnumChoice()
    {
        foreach ((string quote, EnvfileExpectation inner) in new[]
                 {
                     ("\"", ExpectNotDoubleQuoted), ("'", ExpectNotSingleQuoted)
                 })
        {
            int start = _pos;
            if (Lit(quote, Literal(quote)))
            {
                StringBuilder text = new();
                while (true)
                {
                    if (Escape() is { } escaped)
                    {
                        text.Append(escaped);
                        continue;
                    }

                    char quoteChar = quote[0];
                    int before = _pos;
                    if (Cls(c => c != quoteChar && c != '\\' && c != '\r' && c != '\n', inner))
                    {
                        text.Append(_src[before]);
                        continue;
                    }

                    break;
                }

                if (Lit(quote, Literal(quote)))
                {
                    return text.ToString();
                }
            }

            _pos = start;
        }

        return IntegerValue();
    }

    private char? Escape()
    {
        int start = _pos;
        if (Lit("\\", Literal("\\")) && Cls(c => c is '"' or '\'' or '\\' or 'n' or 'r' or 't', ExpectEscape))
        {
            return _src[_pos - 1] switch { 'n' => '\n', 'r' => '\r', 't' => '\t', var c => c };
        }

        _pos = start;
        return null;
    }

    // ---- the parser's error message ("Expected ... but ... found.")

    private static string BuildMessage(List<EnvfileExpectation> expected, string? found)
    {
        List<string> descriptions = expected.Select(Describe).ToList();
        descriptions.Sort(string.CompareOrdinal);
        List<string> unique = [];
        foreach (string description in descriptions)
        {
            if (unique.Count == 0 || unique[^1] != description)
            {
                unique.Add(description);
            }
        }

        string list = unique.Count switch
        {
            0 => "",
            1 => unique[0],
            2 => unique[0] + " or " + unique[1],
            _ => string.Join(", ", unique.Take(unique.Count - 1)) + ", or " + unique[^1]
        };

        return "Expected " + list + " but " + (string.IsNullOrEmpty(found) ? "end of input" : "\"" + EscapeLiteral(found) + "\"") +
               " found.";
    }

    private static string Describe(EnvfileExpectation expectation)
    {
        switch (expectation.Type)
        {
            case "literal":
                return "\"" + EscapeLiteral(expectation.Text!) + "\"";
            case "class":
                string parts = string.Concat(expectation.Parts!.Select(part =>
                    part.Length == 3 && part[1] == '-' ? EscapeClass(part[0].ToString()) + "-" + EscapeClass(part[2].ToString())
                        : EscapeClass(part)));
                return "[" + (expectation.Inverted ? "^" : "") + parts + "]";
            case "any":
                return "any character";
            case "end":
                return "end of input";
            default:
                return expectation.Description!;
        }
    }

    private static string Hex(string character)
    {
        return char.ConvertToUtf32(character, 0).ToString("X", CultureInfo.InvariantCulture);
    }

    private static string EscapeLiteral(string text)
    {
        return EscapeUnicode(text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\0", "\\0").Replace("\t", "\\t")
            .Replace("\n", "\\n").Replace("\r", "\\r"), true);
    }

    private static string EscapeClass(string text)
    {
        return EscapeUnicode(text.Replace("\\", "\\\\").Replace("]", "\\]").Replace("^", "\\^").Replace("-", "\\-")
            .Replace("\0", "\\0").Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r"), true);
    }

    /// <summary>Control characters as \xNN, then format/mark/other code points as \u{N} (the generated message's escaping).</summary>
    private static string EscapeUnicode(string text, bool _)
    {
        StringBuilder result = new();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c <= 0x0F)
            {
                result.Append("\\x0").Append(Hex(c.ToString()));
                continue;
            }

            if (c is >= (char)0x10 and <= (char)0x1F or >= (char)0x7F and <= (char)0x9F)
            {
                result.Append("\\x").Append(Hex(c.ToString()));
                continue;
            }

            string rune = char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])
                ? text.Substring(i++, 2)
                : c.ToString();
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(rune, 0);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate
                or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned or UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark)
            {
                result.Append("\\u{").Append(Hex(rune)).Append('}');
            }
            else
            {
                result.Append(rune);
            }
        }

        return result.ToString();
    }
}
