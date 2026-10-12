#:property PublishAot=false
// Stand-in for macOS `/usr/bin/security` (the Keychain command line), used by MacosKeychainParityTest. The parity test copies
// this app's apphost as `security[.exe]` and points FAKE_SECURITY_DIR at a vault directory:
//   security find-generic-password -s <service> -a <account> -w         -> prints the password plus "\n"; exit 44 when missing
//   security add-generic-password [-U] -s <service> -a <account> -l <label> -w <password>
//                                                                        -> stores it; exit 45 when it exists and -U is absent
//   security delete-generic-password -s <service> -a <account>           -> removes it; exit 44 when missing
// Items live in <dir>/keychain/<service>_<account> (label in a .label file). Every call is appended to <dir>/calls.log as
// `security <args...>`. Anything else exits 2.
using System.Text;
using System.Text.RegularExpressions;

string dir = Environment.GetEnvironmentVariable("FAKE_SECURITY_DIR") ?? ".";
string Sanitize(string value) => Regex.Replace(value, "[^A-Za-z0-9]", "_");
string Option(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault() ?? "";

Directory.CreateDirectory(Path.Combine(dir, "keychain"));
File.AppendAllText(Path.Combine(dir, "calls.log"), $"security {string.Join(' ', args)}\n");
Console.OutputEncoding = new UTF8Encoding(false);

string file = Path.Combine(dir, "keychain", Sanitize(Option("-s") + ":" + Option("-a")));
const string NotFound = "security: SecKeychainSearchCopyNext: The specified item could not be found in the keychain.";

switch (args.FirstOrDefault())
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
        if (File.Exists(file) && !args.Contains("-U"))
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
