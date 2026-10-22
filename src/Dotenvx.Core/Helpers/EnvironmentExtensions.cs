using System.Collections;
using System.Runtime.InteropServices;

namespace Dotenvx.Helpers;

internal static class EnvironmentHelper
{
    /// <summary>
    /// `process.env` as Node lists it: the names in the order of the process's environment block (Windows
    /// GetEnvironmentStringsW, Linux /proc/self/environ), then any added since. (`Environment.GetEnvironmentVariables()`
    /// is an unordered Hashtable.)
    /// </summary>
    public static IDictionary<string, string> GetEnvironmentVariablesAsDictionary()
    {
        var current = Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? string.Empty);

        var result = new Dictionary<string, string>();
        foreach (string key in InitialEnvironOrder().Where(current.ContainsKey).Concat(current.Keys).Distinct())
        {
            result[key] = current[key];
        }

        return result;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetEnvironmentStringsW();

    [DllImport("kernel32.dll")]
    private static extern bool FreeEnvironmentStringsW(IntPtr block);

    [DllImport("libSystem.dylib")]
    private static extern IntPtr _NSGetEnviron();

    /// <summary>macOS has no /proc: `environ` is reached through _NSGetEnviron (a char***).</summary>
    private static List<string> MacEnvironOrder()
    {
        var names = new List<string>();
        IntPtr environ = Marshal.ReadIntPtr(_NSGetEnviron());
        for (int i = 0; ; i++)
        {
            IntPtr entry = Marshal.ReadIntPtr(environ, i * IntPtr.Size);
            if (entry == IntPtr.Zero)
            {
                break;
            }

            names.Add((Marshal.PtrToStringUTF8(entry) ?? "").Split('=', 2)[0]);
        }

        return names;
    }

    private static IEnumerable<string> InitialEnvironOrder()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var names = new List<string>();
                IntPtr block = GetEnvironmentStringsW();
                try
                {
                    for (IntPtr cursor = block; ;)
                    {
                        string entry = Marshal.PtrToStringUni(cursor) ?? "";
                        if (entry.Length == 0)
                        {
                            break;
                        }

                        // "=C:=C:\..." entries are per-drive working directories, not variables
                        if (!entry.StartsWith('='))
                        {
                            names.Add(entry.Split('=', 2)[0]);
                        }

                        cursor += (entry.Length + 1) * 2;
                    }
                }
                finally
                {
                    FreeEnvironmentStringsW(block);
                }

                return names;
            }

            if (OperatingSystem.IsMacOS())
            {
                return MacEnvironOrder();
            }

            return File.ReadAllText("/proc/self/environ").Split((char)0, StringSplitOptions.RemoveEmptyEntries)
                .Select(entry => entry.Split('=', 2)[0]).ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }
}
