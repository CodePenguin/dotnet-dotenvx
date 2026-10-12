// Test fixture standing in for the real `gitleaks` binary - exercised only by
// GitleaksHelperTest (Dotenvx.Core.Test), via `dotnet FakeGitleaks.dll`, so that test doesn't
// require gitleaks to be installed. Usage: FakeGitleaks <exitCode> [O:text|E:text ...] - writes
// each op's text as a line to stdout (O:) or stderr (E:), in argument order, flushing after each
// write, then exits with the given code.

int exitCode = int.Parse(args[0]);

foreach (string op in args.Skip(1))
{
    string text = op[2..];
    if (op[0] == 'O')
    {
        Console.Out.WriteLine(text);
        Console.Out.Flush();
    }
    else
    {
        Console.Error.WriteLine(text);
        Console.Error.Flush();
    }
}

return exitCode;
