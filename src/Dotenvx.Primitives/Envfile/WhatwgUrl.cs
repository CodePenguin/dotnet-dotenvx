using System.Globalization;
using System.Numerics;
using System.Text;

namespace Dotenvx.Primitives;

/// <summary>
/// Whether the WHATWG URL parser accepts a string as an absolute URL (`new URL(input)` does not throw). Only the
/// accept/reject decision is reproduced, following the URL Standard's basic URL parser: scheme, special-scheme authority
/// (credentials, host, port), non-special authority, file hosts; paths, queries and fragments never reject.
/// </summary>
internal static class WhatwgUrl
{
    private static readonly string[] Special = ["ftp", "file", "http", "https", "ws", "wss"];

    public static bool IsValid(string input)
    {
        int start = 0;
        int end = input.Length;
        while (start < end && input[start] <= ' ')
        {
            start++;
        }

        while (end > start && input[end - 1] <= ' ')
        {
            end--;
        }

        string text = new([.. input[start..end].Where(c => c is not ('\t' or '\n' or '\r'))]);

        int colon = text.IndexOf(':');
        if (colon <= 0 || !char.IsAsciiLetter(text[0]) ||
            !text[..colon].All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.'))
        {
            return false;
        }

        string scheme = text[..colon].ToLowerInvariant();
        string rest = text[(colon + 1)..];
        bool special = Special.Contains(scheme);

        if (scheme == "file")
        {
            return IsValidFile(rest);
        }

        if (special)
        {
            int i = 0;
            while (i < rest.Length && rest[i] is '/' or '\\')
            {
                i++;
            }

            return IsValidAuthority(rest[i..], true);
        }

        if (rest.StartsWith("//"))
        {
            return IsValidAuthority(rest[2..], false);
        }

        return true; // path or opaque path
    }

    private static bool IsValidFile(string rest)
    {
        bool slashes = rest.Length >= 2 && rest[0] is '/' or '\\' && rest[1] is '/' or '\\';
        if (!slashes)
        {
            return true;
        }

        string authority = TakeAuthority(rest[2..]);
        if (authority.Length == 0)
        {
            return true;
        }

        // file host state: a Windows drive letter ("C:") in the host position is a path, not a host
        if (authority.Length == 2 && char.IsAsciiLetter(authority[0]) && authority[1] is ':' or '|')
        {
            return true;
        }

        return ParseHost(authority, true, out string host) && (host.Length >= 0);
    }

    private static string TakeAuthority(string rest)
    {
        int end = 0;
        while (end < rest.Length && rest[end] is not ('/' or '\\' or '?' or '#'))
        {
            end++;
        }

        return rest[..end];
    }

    private static bool IsValidAuthority(string rest, bool special)
    {
        string authority = special ? TakeAuthority(rest) : TakeAuthorityOpaque(rest);
        int at = authority.LastIndexOf('@');
        if (at >= 0)
        {
            authority = authority[(at + 1)..];
            if (authority.Length == 0)
            {
                return false;
            }
        }

        // host and port: the port starts at the last ':' that is outside square brackets
        int portStart = -1;
        bool inBrackets = false;
        for (int i = 0; i < authority.Length; i++)
        {
            char c = authority[i];
            if (c == '[')
            {
                inBrackets = true;
            }
            else if (c == ']')
            {
                inBrackets = false;
            }
            else if (c == ':' && !inBrackets)
            {
                portStart = i;
                break;
            }
        }

        string hostText = portStart >= 0 ? authority[..portStart] : authority;
        if (hostText.Length == 0)
        {
            if (special)
            {
                return false;
            }

            if (portStart >= 0 && at >= 0)
            {
                return false;
            }
        }

        if (portStart >= 0)
        {
            string port = authority[(portStart + 1)..];
            if (!port.All(char.IsAsciiDigit))
            {
                return false;
            }

            if (port.Length > 0 && BigInteger.Parse(port, CultureInfo.InvariantCulture) > 65535)
            {
                return false;
            }
        }

        return hostText.Length == 0 || ParseHost(hostText, special, out _);
    }

    private static string TakeAuthorityOpaque(string rest)
    {
        int end = 0;
        while (end < rest.Length && rest[end] is not ('/' or '?' or '#'))
        {
            end++;
        }

        return rest[..end];
    }

    private static readonly char[] ForbiddenHost =
    [
        '\0', '\t', '\n', '\r', ' ', '#', '/', ':', '<', '>', '?', '@', '[', '\\', ']', '^', '|'
    ];

    private static bool ParseHost(string input, bool notOpaque, out string host)
    {
        host = input;
        if (input.StartsWith('['))
        {
            return input.EndsWith(']') && ParseIpv6(input[1..^1]);
        }

        if (!notOpaque)
        {
            if (input.IndexOfAny(ForbiddenHost) >= 0)
            {
                return false;
            }

            return true;
        }

        string domain;
        try
        {
            domain = PercentDecode(input);
        }
        catch (ArgumentException)
        {
            return false;
        }

        string ascii = ToAscii(domain);
        if (ascii.Length == 0)
        {
            return false;
        }

        if (ascii.Any(c => c <= 0x20 || ForbiddenHost.Contains(c) || c == '%' || c == 0x7F))
        {
            return false;
        }

        host = ascii;
        return !EndsInNumber(ascii) || ParseIpv4(ascii);
    }

    private static string PercentDecode(string input)
    {
        List<byte> bytes = [];
        byte[] raw = Encoding.UTF8.GetBytes(input);
        for (int i = 0; i < raw.Length; i++)
        {
            if (raw[i] == '%' && i + 2 < raw.Length + 0 && Uri.IsHexDigit((char)raw[i + 1]) && Uri.IsHexDigit((char)raw[i + 2]))
            {
                bytes.Add(Convert.ToByte(((char)raw[i + 1]).ToString() + (char)raw[i + 2], 16));
                i += 2;
            }
            else
            {
                bytes.Add(raw[i]);
            }
        }

        return new UTF8Encoding(false, false).GetString([.. bytes]);
    }

    private static string ToAscii(string domain)
    {
        if (domain.All(c => c < 0x80))
        {
            return domain.ToLowerInvariant();
        }

        IdnMapping mapping = new() { AllowUnassigned = true, UseStd3AsciiRules = false };
        List<string> labels = [];
        foreach (string label in domain.Replace('。', '.').Replace('．', '.').Replace('｡', '.').Split('.'))
        {
            if (label.Length == 0 || label.All(c => c < 0x80))
            {
                labels.Add(label.ToLowerInvariant());
                continue;
            }

            try
            {
                labels.Add(mapping.GetAscii(label));
            }
            catch (ArgumentException)
            {
                return "";
            }
        }

        return string.Join('.', labels);
    }

    private static bool EndsInNumber(string host)
    {
        List<string> parts = [.. host.Split('.')];
        if (parts[^1].Length == 0)
        {
            if (parts.Count == 1)
            {
                return false;
            }

            parts.RemoveAt(parts.Count - 1);
        }

        string last = parts[^1];
        return (last.Length > 0 && last.All(char.IsAsciiDigit)) || ParseIpv4Number(last) != null;
    }

    private static BigInteger? ParseIpv4Number(string input)
    {
        if (input.Length == 0)
        {
            return null;
        }

        int radix = 10;
        string text = input;
        if (text.Length >= 2 && text[0] == '0' && text[1] is 'x' or 'X')
        {
            text = text[2..];
            radix = 16;
        }
        else if (text.Length >= 2 && text[0] == '0')
        {
            text = text[1..];
            radix = 8;
        }

        if (text.Length == 0)
        {
            return BigInteger.Zero;
        }

        BigInteger value = BigInteger.Zero;
        foreach (char c in text)
        {
            int digit = c is >= '0' and <= '9' ? c - '0' : c is >= 'a' and <= 'f' ? c - 'a' + 10 : c is >= 'A' and <= 'F' ? c - 'A' + 10 : 99;
            if (digit >= radix)
            {
                return null;
            }

            value = value * radix + digit;
        }

        return value;
    }

    private static bool ParseIpv4(string host)
    {
        List<string> parts = [.. host.Split('.')];
        if (parts[^1].Length == 0 && parts.Count > 1)
        {
            parts.RemoveAt(parts.Count - 1);
        }

        if (parts.Count > 4)
        {
            return false;
        }

        List<BigInteger> numbers = [];
        foreach (string part in parts)
        {
            BigInteger? number = ParseIpv4Number(part);
            if (number == null)
            {
                return false;
            }

            numbers.Add(number.Value);
        }

        if (numbers.Take(numbers.Count - 1).Any(n => n > 255))
        {
            return false;
        }

        return numbers[^1] < BigInteger.Pow(256, 5 - numbers.Count);
    }

    private static bool ParseIpv6(string input)
    {
        int[] address = new int[8];
        int pieceIndex = 0;
        int? compress = null;
        int p = 0;
        int Cp(int i)
        {
            return i < input.Length ? input[i] : -1;
        }

        if (Cp(p) == ':')
        {
            if (Cp(p + 1) != ':')
            {
                return false;
            }

            p += 2;
            pieceIndex++;
            compress = pieceIndex;
        }

        while (Cp(p) != -1)
        {
            if (pieceIndex == 8)
            {
                return false;
            }

            if (Cp(p) == ':')
            {
                if (compress != null)
                {
                    return false;
                }

                p++;
                pieceIndex++;
                compress = pieceIndex;
                continue;
            }

            int value = 0;
            int length = 0;
            while (length < 4 && Cp(p) != -1 && Uri.IsHexDigit((char)Cp(p)))
            {
                value = value * 16 + Convert.ToInt32(((char)Cp(p)).ToString(), 16);
                p++;
                length++;
            }

            if (Cp(p) == '.')
            {
                if (length == 0)
                {
                    return false;
                }

                p -= length;
                if (pieceIndex > 6)
                {
                    return false;
                }

                int numbersSeen = 0;
                while (Cp(p) != -1)
                {
                    int? ipv4Piece = null;
                    if (numbersSeen > 0)
                    {
                        if (Cp(p) == '.' && numbersSeen < 4)
                        {
                            p++;
                        }
                        else
                        {
                            return false;
                        }
                    }

                    if (Cp(p) == -1 || !char.IsAsciiDigit((char)Cp(p)))
                    {
                        return false;
                    }

                    while (Cp(p) != -1 && char.IsAsciiDigit((char)Cp(p)))
                    {
                        int number = Cp(p) - '0';
                        if (ipv4Piece == null)
                        {
                            ipv4Piece = number;
                        }
                        else if (ipv4Piece == 0)
                        {
                            return false;
                        }
                        else
                        {
                            ipv4Piece = ipv4Piece * 10 + number;
                        }

                        if (ipv4Piece > 255)
                        {
                            return false;
                        }

                        p++;
                    }

                    address[pieceIndex] = address[pieceIndex] * 0x100 + ipv4Piece!.Value;
                    numbersSeen++;
                    if (numbersSeen is 2 or 4)
                    {
                        pieceIndex++;
                    }
                }

                return numbersSeen == 4;
            }

            if (Cp(p) == ':')
            {
                p++;
                if (Cp(p) == -1)
                {
                    return false;
                }
            }
            else if (Cp(p) != -1)
            {
                return false;
            }

            address[pieceIndex] = value;
            pieceIndex++;
        }

        if (compress != null)
        {
            return pieceIndex != 8 || true;
        }

        return pieceIndex == 8;
    }
}
