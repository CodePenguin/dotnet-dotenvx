#:property PublishAot=false
// Stand-in for macOS `/usr/bin/security` (the Keychain command line), used by MacosKeychainParityTest. The parity test copies
// this app's apphost as `security[.exe]` and points FAKE_SECURITY_DIR at a vault directory:
//   security find-generic-password -s <service> -a <account> -w         -> prints the password plus "\n"; exit 44 when missing
//   security add-generic-password [-U] -s <service> -a <account> -l <label> -w <password>
//                                                                        -> stores it; exit 45 when it exists and -U is absent
//   security delete-generic-password -s <service> -a <account>           -> removes it; exit 44 when missing
//   security -i                                                          -> interactive: each stdin line is one command whose
//                                                                           arguments are double-quoted (\ and " escaped)
// Items live in <dir>/keychain/<service>_<account> (label in a .label file). Every command is appended to <dir>/calls.log as
// `security <args...>` (the unquoted arguments, also in interactive mode). Anything else exits 2.
using System.Text;
using System.Text.RegularExpressions;

string dir = Environment.GetEnvironmentVariable("FAKE_SECURITY_DIR") ?? ".";
string Sanitize(string value) => Regex.Replace(value, "[^A-Za-z0-9]", "_");

Directory.CreateDirectory(Path.Combine(dir, "keychain"));
Console.OutputEncoding = new UTF8Encoding(false);
Console.InputEncoding = new UTF8Encoding(false);

const string NotFound = "security: SecKeychainSearchCopyNext: The specified item could not be found in the keychain.";

// one `"a" "b\"c"` line -> its arguments
static List<string> Tokens(string line)
{
    List<string> tokens = [];
    int i = 0;
    while (i < line.Length)
    {
        if (line[i] == ' ')
        {
            i++;
            continue;
        }

        if (line[i] != '"')
        {
            int end = line.IndexOf(' ', i);
            end = end < 0 ? line.Length : end;
            tokens.Add(line[i..end]);
            i = end;
            continue;
        }

        StringBuilder token = new();
        i++;
        while (i < line.Length && line[i] != '"')
        {
            if (line[i] == '\\' && i + 1 < line.Length)
            {
                i++;
            }

            token.Append(line[i++]);
        }

        i++;
        tokens.Add(token.ToString());
    }

    return tokens;
}

int Handle(string[] a)
{
    string Option(string name) => a.SkipWhile(x => x != name).Skip(1).FirstOrDefault() ?? "";
    File.AppendAllText(Path.Combine(dir, "calls.log"), $"security {string.Join(' ', a)}\n");
    string file = Path.Combine(dir, "keychain", Sanitize(Option("-s") + ":" + Option("-a")));

    switch (a.FirstOrDefault())
    {
        case "find-generic-password":
            if (!File.Exists(file))
            {
                Console.Error.WriteLine(NotFound);
                return 44;
            }

            Console.Out.Write(File.ReadAllText(file) + "\n");
            return 0;
        case "add-generic-password":
            if (File.Exists(file) && !a.Contains("-U"))
            {
                Console.Error.WriteLine("security: SecKeychainItemCreateFromContent (<unknown>): The specified item already exists in the keychain.");
                return 45;
            }

            File.WriteAllText(file, Option("-w"));
            File.WriteAllText(file + ".label", Option("-l"));
            return 0;
        case "delete-generic-password":
            if (!File.Exists(file))
            {
                Console.Error.WriteLine(NotFound);
                return 44;
            }

            File.Delete(file);
            File.Delete(file + ".label");
            return 0;
    }

    Console.Error.WriteLine("fake security: unsupported arguments");
    return 2;
}

if (args.Length == 1 && args[0] == "-i")
{
    int status = 0;
    string? line;
    while ((line = Console.In.ReadLine()) is not null)
    {
        if (line.Trim().Length == 0)
        {
            continue;
        }

        // interactive security reports a failure on stderr and still exits zero
        status = Handle([.. Tokens(line)]) == 0 ? status : 0;
    }

    return status;
}

return Handle(args);
