using Dotenvx.Interop.YoctoSpinnerJs;

using Shouldly;

namespace Dotenvx.Ports.Test;

/// <remarks>
/// Not from a JS test - class-level checks of the yocto-spinner port. A non-interactive spinner (CI set) writes plain lines; the
/// animated TTY output is compared with the real package by PtyParityTest.
/// </remarks>
[Collection("Console.Error")]
public class YoctoSpinnerTest : IDisposable
{
    private readonly TextWriter _originalError = Console.Error;
    private readonly string? _ci = Environment.GetEnvironmentVariable("CI");
    private readonly StringWriter _captured = new();

    public YoctoSpinnerTest()
    {
        Environment.SetEnvironmentVariable("CI", "true");
        Console.SetError(_captured);
    }

    public void Dispose()
    {
        Console.SetError(_originalError);
        Environment.SetEnvironmentVariable("CI", _ci);
    }

    [Fact]
    public void NonInteractiveStartWritesOnePlainLine()
    {
        var spinner = new YoctoSpinner("working", ["-"]);

        spinner.Start();
        spinner.Stop();

        _captured.ToString().ShouldContain("working");
        _captured.ToString().ShouldNotContain("\u001b[?25l");
    }

    [Fact]
    public void StartingTwiceDoesNotWriteTwice()
    {
        var spinner = new YoctoSpinner("once", ["-"]);

        spinner.Start();
        spinner.Start();
        spinner.Stop();

        _captured.ToString().Split("once").Length.ShouldBe(2);
    }

    [Fact]
    public void ChangingTheTextWritesTheNewText()
    {
        var spinner = new YoctoSpinner("first", ["-"]);

        spinner.Start();
        spinner.Text = "second";
        spinner.Stop();

        _captured.ToString().ShouldContain("second");
    }
}
