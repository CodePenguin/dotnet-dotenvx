namespace Dotenvx.Core.Interop.DotenvxJs.Helpers;

public static partial class Helpers
{
    public static string GuessEnvironment(string filepath)
    {
        string filename = Path.GetFileName(filepath);

        string[] parts = filename.Split('.');
        string[] possibleEnvironmentList = parts.Skip(2).ToArray();

        return possibleEnvironmentList.Length switch
        {
            0 => // handle .env1 -> development1
                filename.Replace(".env", "development"),
            1 => possibleEnvironmentList[0],
            2 => string.Join("_", possibleEnvironmentList),
            _ => string.Join("_", possibleEnvironmentList.Take(2))
        };
    }
}