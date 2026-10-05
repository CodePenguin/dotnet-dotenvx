namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>Reference source: dotenvx/src/lib/helpers/executeDynamic.js (armorBanner) - generated from that file's text.</remarks>
internal static class ArmorBanner
{
    private static readonly string[] Lines =
    [
        "                      [www.dotenvx.com/armor]",
        "–––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––––",
        "                            __ ____ __ ",
        "                           |    ||    |",
        "                           |  __||__  |",
        "                           |  ‾‾||‾‾  |",
        "                           |    ||    |",
        "                            \\   ||   /",
        "                              \\ __ /",
        "",
        "                          Dotenvx Armor ⛨",
        "",
        "                           ARMORED KEYS",
        "               Private keys. Off device. Under guard.",
        "",
        "                                -",
        "",
        "                            Install one",
        "            [curl -sfS https://dotenvx.sh/armor | sh]",
        "              [npm i @dotenvx/dotenvx-armor --save]",
        "",
        "                                -",
        "",
        "                              Then",
        "                       [dotenvx armor up]",
        "                     (sign in when prompted)",
        ""
    ];

    /// <summary>`armorBanner()`: the lines padded to at least 67 columns inside a box.</summary>
    public static string Render()
    {
        int innerWidth = Math.Max(67, Lines.Max(line => line.Length));
        string top = " " + new string('_', innerWidth);
        string middle = string.Join("\n", Lines.Select(line => "|" + line.PadRight(innerWidth) + "|"));
        string bottom = "|" + new string('_', innerWidth) + "|";

        return top + "\n" + middle + "\n" + bottom;
    }
}
