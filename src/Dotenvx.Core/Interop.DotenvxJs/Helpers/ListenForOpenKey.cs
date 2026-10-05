namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <summary>The slice of `process.stdin` that listenForOpenKey.js uses.</summary>
public interface IKeyInput
{
    bool IsTty { get; }
    bool IsRaw { get; }
    void SetRawMode(bool raw);
    void Resume();
    void Pause();

    /// <summary>Raised with each keystroke chunk: carriage return, "y", "n", Ctrl+C (character 3), ...</summary>
    event Action<string> Data;
}

/// <remarks>Reference source: dotenvx/src/lib/helpers/listenForOpenKey.js (TTY only).</remarks>
public static class ListenForOpenKey
{
    // built from char codes: this file must not contain literal escape characters (see AGENTS.md tooling cautions)
    private static readonly string CarriageReturn = ((char)13).ToString();
    private static readonly string LineFeed = ((char)10).ToString();
    private static readonly string CtrlC = ((char)3).ToString();

    private static readonly Action DefaultInterrupt = () => Environment.Exit(130);

    /// <summary>`process.kill(process.pid, 'SIGINT')`. Swappable for tests.</summary>
    public static Action Interrupt { get; internal set; } = DefaultInterrupt;

    /// <summary>The keyboard. Swappable for tests (upstream replaces `process.stdin`).</summary>
    public static Func<IKeyInput> Input { get; internal set; } = () => new ConsoleKeyInput();

    internal static void ResetToDefaults()
    {
        Interrupt = DefaultInterrupt;
        Input = () => new ConsoleKeyInput();
    }

    /// <summary>Starts watching the keyboard: Enter or `y` runs <paramref name="onOpen" /> once, `n` or Ctrl+C interrupt. Returns the cleanup.</summary>
    public static Action Listen(Action onOpen)
    {
        IKeyInput stdin = Input();
        if (!stdin.IsTty)
        {
            return () => { };
        }

        bool wasRawMode = stdin.IsRaw;
        bool didHandleOpenChoice = false;

        void Cleanup()
        {
            stdin.Data -= OnData;
            stdin.SetRawMode(wasRawMode);
            stdin.Pause();
        }

        void OnData(string key)
        {
            string lower = key.ToLowerInvariant();

            if (key == CtrlC)
            {
                Cleanup();
                Interrupt();

                return;
            }

            if (key == CarriageReturn || key == LineFeed || lower == "y")
            {
                if (!didHandleOpenChoice)
                {
                    didHandleOpenChoice = true;
                    try
                    {
                        onOpen();
                    }
                    catch
                    {
                        // `Promise.resolve(onOpen()).catch(() => {})`
                    }
                }

                return;
            }

            if (lower == "n")
            {
                Cleanup();
                Interrupt();
            }
        }

        stdin.SetRawMode(true);
        stdin.Resume();
        stdin.Data += OnData;

        return Cleanup;
    }

    /// <summary>The real keyboard: a background thread turns keystrokes into <see cref="IKeyInput.Data" /> events.</summary>
    private sealed class ConsoleKeyInput : IKeyInput
    {
        private CancellationTokenSource? _cts;

        public bool IsTty => !Console.IsInputRedirected;

        public bool IsRaw => false;

        public event Action<string>? Data;

        public void SetRawMode(bool raw)
        {
            // Console.ReadKey(true) already reads without echo or line buffering
        }

        public void Resume()
        {
            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;
            new Thread(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    if (!Console.KeyAvailable)
                    {
                        Thread.Sleep(50);
                        continue;
                    }

                    ConsoleKeyInfo key = Console.ReadKey(true);
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    string text = key.Key == ConsoleKey.Enter
                        ? CarriageReturn
                        : key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control)
                            ? CtrlC
                            : key.KeyChar.ToString();
                    Data?.Invoke(text);
                }
            }) { IsBackground = true }.Start();
        }

        public void Pause()
        {
            _cts?.Cancel();
        }
    }
}

/// <remarks>Reference source: dotenvx/src/lib/helpers/formatCode.js</remarks>
public static class FormatCodeHelper
{
    /// <summary>Groups of four characters joined by dashes.</summary>
    public static string FormatCode(string value)
    {
        var parts = new List<string>();
        for (int i = 0; i < value.Length; i += 4)
        {
            parts.Add(value.Substring(i, Math.Min(4, value.Length - i)));
        }

        return string.Join("-", parts);
    }
}
