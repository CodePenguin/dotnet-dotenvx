using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>Reference source: dotenvx/src/lib/services/ops.js</remarks>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
// ReSharper disable VirtualMemberCallInConstructor
public class Ops
{
    private static readonly Func<IDotenvxLogger?, Ops> DefaultFactory =
        logger => new Ops(logger);

    public Ops(IDotenvxLogger? logger = null)
    {
        try
        {
            OpsLib = OpsNpm();
            logger?.SuccessV($"📡 radar: {OpsLib.Status}");
        }
        catch
        {
            try
            {
                OpsLib = OpsCli();
                logger?.SuccessV($"📡 radar: {OpsLib.Status}");
            }
            catch
            {
                // noop
            }
        }
    }

    public OpsLib? OpsLib { get; }

    public static Func<IDotenvxLogger?, Ops> Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }

    public virtual void Observe(OpsPayload payload)
    {
        if (OpsLib == null || OpsLib.Status == "off")
        {
            return;
        }

        string encoded = Encode(payload);
        OpsLib.Observe(encoded);
    }

    private static string Encode(object payload)
    {
        string json = JsonSerializer.Serialize(payload);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        return Convert.ToBase64String(bytes);
    }

    protected virtual OpsLib OpsNpm()
    {
        string fallbackBin = Path.Combine(Environment.CurrentDirectory, "node_modules/.bin/dotenvx-ops");
        ProcessStartInfo psi = new()
        {
            FileName = fallbackBin,
            Arguments = "status",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using Process process = Process.Start(psi)!;
        process.WaitForExit();
        string status = process.StandardOutput.ReadToEnd().Trim();
        return new OpsLib
        {
            Status = status,
            Observe = encoded =>
            {
                try
                {
                    ProcessStartInfo subPsi = new()
                    {
                        FileName = fallbackBin,
                        Arguments = $"observe {encoded}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(subPsi);
                }
                catch
                {
                    // noop
                }
            }
        };
    }

    protected virtual OpsLib OpsCli()
    {
        ProcessStartInfo psi = new()
        {
            FileName = "dotenvx-ops",
            Arguments = "status",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using Process process = Process.Start(psi)!;
        process.WaitForExit();
        string status = process.StandardOutput.ReadToEnd().Trim();
        return new OpsLib
        {
            Status = status,
            Observe = encoded =>
            {
                try
                {
                    ProcessStartInfo subPsi = new()
                    {
                        FileName = "dotenvx-ops",
                        Arguments = $"observe {encoded}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(subPsi);
                }
                catch
                {
                    // noop
                }
            }
        };
    }
}