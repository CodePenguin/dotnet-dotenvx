using dotnet_dotenvx.Actions;

namespace dotnet_dotenvx;

public class DotenvxRootCommand : LoggingRootCommand
{
    public DotenvxRootCommand(
        RunAction runAction
    ) : base("A secure dotenv for .NET – based on dotenvx from the creator of `dotenv`")
    {
        // Add Actions
        Subcommands.Add(runAction);
    }
}