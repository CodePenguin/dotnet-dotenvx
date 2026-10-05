using Dotenvx.Interop.DotenvxJs.Services;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/lib/services/doctor.js. The upstream fixtures (tests/fixtures/doctor) are recreated in
/// a temp directory so the offline suite stays self-contained.
/// </remarks>
public class DoctorTest : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dotenvx-doctor-" + Guid.NewGuid().ToString("N"));

    public DoctorTest()
    {
        Directory.CreateDirectory(_directory);
        Write("App.java", "class App {\n  void m() {\n    // c\n    int x;\n    Dotenv.load();\n    Dotenv.configure();\n  }\n}\n");
        Write("App.kt", "fun main() {\n  val a = 1\n  val b = 2\n  Dotenv.load()\n}\n");
        Write("Program.cs", "using DotNetEnv;\n\nDotNetEnv.Env.Load();\nEnv.Load();\n");
        Write("Program.fs", "open DotNetEnv\n\nDotNetEnv.Env.Load()\n");
        Write("Program.vb", "Imports DotNetEnv\n\nEnv.Load()\n");
        Write("app.py", "import dotenv\n\nload_dotenv(override=True)\ndotenv_values(\".env\")\n");
        Write("app.rb", "Dotenv.load(\".env\")\n");
        Write("config.mjs", "import 'dotenv/config'\n");
        Write("index.js", "require('dotenv').config()\nconst a = 1\nconst dotenv = require('dotenv')\ndotenv.config({ override: true })\n");
        Write("index.php", "<?php\n\n$dotenv = Dotenv" + (char)92 + "Dotenv::createImmutable(__DIR__);\n");
        Write("main.go", "package main\n\nfunc main() {\n  godotenv.Overload()\n}\n");
        Write("main.rs", "fn main() {\n  dotenvy::dotenv();\n  dotenv::dotenv();\n}\n");
        Write("README.md", "# nothing here\nload_dotenv()\n");
    }

    public void Dispose()
    {
        Directory.Delete(_directory, true);
        GC.SuppressFinalize(this);
    }

    private void Write(string relative, string content)
    {
        string path = Path.Combine(_directory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <remarks>Refer to "#run"</remarks>
    [Fact]
    public void TestRun()
    {
        DoctorFinding[] findings = new Doctor(_directory).Run();

        findings.ShouldBe(
        [
            new DoctorFinding("Java", "App.java", 5, "Dotenv.load()", "found java-dotenv load call"),
            new DoctorFinding("Java", "App.java", 6, "Dotenv.configure()", "found java-dotenv load call"),
            new DoctorFinding("Kotlin", "App.kt", 4, "Dotenv.load()", "found java-dotenv load call"),
            new DoctorFinding(".NET", "Program.cs", 3, "DotNetEnv.Env.Load()", "found DotNetEnv load call"),
            new DoctorFinding(".NET", "Program.cs", 4, "Env.Load()", "found DotNetEnv load call"),
            new DoctorFinding(".NET", "Program.fs", 3, "DotNetEnv.Env.Load()", "found DotNetEnv load call"),
            new DoctorFinding(".NET", "Program.vb", 3, "Env.Load()", "found DotNetEnv load call"),
            new DoctorFinding("Python", "app.py", 3, "load_dotenv(override=True)", "found python-dotenv load call"),
            new DoctorFinding("Python", "app.py", 4, "dotenv_values(\".env\")", "found python-dotenv values call"),
            new DoctorFinding("Ruby", "app.rb", 1, "Dotenv.load(\".env\")", "found ruby dotenv load call"),
            new DoctorFinding("Node", "config.mjs", 1, "import 'dotenv/config'", "found dotenv/config import"),
            new DoctorFinding("Node", "index.js", 1, "require('dotenv').config()", "found dotenv config require call"),
            new DoctorFinding("Node", "index.js", 4, "dotenv.config({ override: true })", "found dotenv config call"),
            new DoctorFinding("PHP", "index.php", 3, "Dotenv" + (char)92 + "Dotenv::createImmutable(__DIR__)",
                "found vlucas/phpdotenv loader"),
            new DoctorFinding("Go", "main.go", 4, "godotenv.Overload()", "found godotenv load call"),
            new DoctorFinding("Rust", "main.rs", 2, "dotenvy::dotenv()", "found Rust dotenv load call"),
            new DoctorFinding("Rust", "main.rs", 3, "dotenv::dotenv()", "found Rust dotenv load call")
        ]);
    }

    /// <remarks>
    /// Refer to "#run ignores dependency and build directories". Upstream's exclude globs only match POSIX separators
    /// (fdir hands the callback an absolute path with the platform separator), so on Windows nothing is ignored, as
    /// with the Node CLI itself (verified live).
    /// </remarks>
    [Fact]
    public void TestRunIgnoresDependencyAndBuildDirectories()
    {
        Write("node_modules/ignored.js", "dotenv.config()\n");
        Write("dist/ignored.py", "load_dotenv()\n");

        string[] filepaths = [.. new Doctor(_directory).Run().Select(finding => finding.Filepath)];

        bool ignored = !OperatingSystem.IsWindows();
        filepaths.Contains(Path.Combine("node_modules", "ignored.js")).ShouldBe(!ignored);
        filepaths.Contains(Path.Combine("dist", "ignored.py")).ShouldBe(!ignored);
    }

    /// <remarks>Refer to "#run returns empty when no loaders are found"</remarks>
    [Fact]
    public void TestRunReturnsEmptyWhenNoLoadersAreFound()
    {
        new Doctor(_directory).ScanFile("README.md").ShouldBeEmpty();
    }

    /// <remarks>Not from a JS test - fdir crawls a missing directory as empty (`doctor /nonexistent` verified live).</remarks>
    [Fact]
    public void TestRunOfMissingDirectoryIsEmpty()
    {
        new Doctor(Path.Combine(_directory, "missing")).Run().ShouldBeEmpty();
    }
}
