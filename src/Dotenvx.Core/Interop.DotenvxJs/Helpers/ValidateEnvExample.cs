using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Primitives;

using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/validateEnvExample.js: scans `.env.example` with primitives'
/// `scan` (keys plus per-occurrence comments) and validates the env against it; a missing file is
/// MISSING_ENV_EXAMPLE and failed validations are one VALIDATION_FAILED joined with "; ".
/// </remarks>
public static partial class Helpers
{
    public static DotenvxException? ValidateEnvExample(IReadOnlyDictionary<string, string> env,
        string filepath = ".env.example")
    {
        if (!Fsx.ExistsSync(filepath))
        {
            return new MissingEnvExampleException();
        }

        ScanResult scanned = Scan(Fsx.ReadFileX(filepath), new ScanOptions());
        ValidationResult validation = Validate(scanned.Parsed, env, scanned.Comments);

        if (!validation.Valid)
        {
            return new ValidationFailedException(string.Join("; ", validation.Errors.Select(e => e.Message)));
        }

        return null;
    }
}
