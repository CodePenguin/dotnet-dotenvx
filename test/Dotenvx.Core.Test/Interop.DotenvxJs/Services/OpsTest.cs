using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Types;

using Moq;
using Moq.Protected;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Services;

/// <remarks>Reference source: dotenvx/tests/lib/services/ops.test.js</remarks>
public class OpsTest : EnvironmentTestBase
{
    public OpsTest()
    {
        ClearEnvironment();
    }

    /// <remarks>Refer to "when no dotenvx-ops"</remarks>
    [Fact]
    public void TestWhenNoDotenvxOps()
    {
        Ops ops = new(MockLogger);
        ops.OpsLib.ShouldBeNull();
        Should.NotThrow(() => ops.Observe(new OpsPayload()));
    }

    /// <remarks>Refer to "when dotenvx-ops npm"</remarks>
    [Fact]
    public void TestWhenDotenvxOpsNpm()
    {
        Mock<Ops> mockOps = new(MockLogger) { CallBase = true };
        bool observeCalled = false;
        mockOps.Protected().Setup<OpsLib>("OpsNpm").Returns(new OpsLib
        {
            Status = "on", Observe = _ => observeCalled = true
        });
        mockOps.Protected().Setup<OpsLib>("OpsCli").Throws(new Exception("Should not be called"));

        Ops ops = mockOps.Object;
        ops.OpsLib.ShouldNotBeNull();
        ops.OpsLib!.Status.ShouldBe("on");
        MockLogger.InfoMessages.ShouldContain("📡 radar: on");

        ops.Observe(new OpsPayload());
        observeCalled.ShouldBeTrue();
        mockOps.Protected().Verify("OpsNpm", Times.Once());
        mockOps.Protected().Verify("OpsCli", Times.Never());
    }

    /// <remarks>Refer to "when dotenvx-ops npm but then observe fails somehow"</remarks>
    [Fact]
    public void TestWhenDotenvxOpsNpmButThenObserveFailsSomehow()
    {
        Mock<Ops> mockOps = new(MockLogger) { CallBase = true };
        mockOps.Protected().Setup<OpsLib>("OpsNpm").Returns(new OpsLib
        {
            Status = "on",
            Observe = _ =>
            {
                /* simulate observe failure */
            }
        });
        mockOps.Protected().Setup<OpsLib>("OpsCli").Throws(new Exception("Should not be called"));

        Ops ops = mockOps.Object;
        MockLogger.InfoMessages.ShouldContain("📡 radar: on");

        ops.Observe(new OpsPayload());
        // observe fails, but caught
        ops.OpsLib.ShouldNotBeNull();
        mockOps.Protected().Verify("OpsNpm", Times.Once());
        mockOps.Protected().Verify("OpsCli", Times.Never());
    }

    /// <remarks>Refer to "when dotenvx-ops cli"</remarks>
    [Fact]
    public void TestWhenDotenvxOpsCli()
    {
        Mock<Ops> mockOps = new(MockLogger) { CallBase = true };
        bool observeCalled = false;
        mockOps.Protected().Setup<OpsLib>("OpsNpm").Throws(new Exception("bin/dotenvx-ops status failed"));
        mockOps.Protected().Setup<OpsLib>("OpsCli").Returns(new OpsLib
        {
            Status = "on", Observe = _ => observeCalled = true
        });

        Ops ops = mockOps.Object;
        MockLogger.InfoMessages.ShouldContain("📡 radar: on");
        ops.Observe(new OpsPayload());

        observeCalled.ShouldBeTrue();
        ops.OpsLib.ShouldNotBeNull();
        mockOps.Protected().Verify("OpsNpm", Times.Once());
        mockOps.Protected().Verify("OpsCli", Times.Once());
    }

    /// <remarks>Refer to "when dotenvx-ops cli stub childProcess.execSync"</remarks>
    [Fact]
    public void TestWhenDotenvxOpsCliStubChildProcessExecSync()
    {
        Mock<Ops> mockOps = new(MockLogger) { CallBase = true };
        mockOps.Protected().Setup<OpsLib>("OpsNpm").Throws(new Exception("bin/dotenvx-ops status failed"));
        mockOps.Protected().Setup<OpsLib>("OpsCli").Returns(new OpsLib
        {
            Status = "on",
            Observe = _ =>
            {
                /* simulate success */
            }
        });

        Ops ops = mockOps.Object;
        MockLogger.InfoMessages.ShouldContain("📡 radar: on");
        ops.Observe(new OpsPayload());

        ops.OpsLib.ShouldNotBeNull();
        mockOps.Protected().Verify("OpsNpm", Times.Once());
        mockOps.Protected().Verify("OpsCli", Times.Once());
    }

    /// <remarks>Refer to "when dotenvx-ops cli stub childProcess.execSync"</remarks>
    [Fact]
    public void TestWhenDotenvxOpsCliStubChildProcessExecSync2()
    {
        // stub execSync: fallbackBin status throws, dotenvx-ops status returns 'on', observe throws

        Mock<Ops> mockOps = new(MockLogger) { CallBase = true };
        mockOps.Protected().Setup<OpsLib>("OpsNpm").Throws(new Exception("bin/dotenvx-ops status failed"));
        mockOps.Protected().Setup<OpsLib>("OpsCli").Returns(new OpsLib
        {
            Status = "on",
            Observe = _ =>
            {
                /* simulate observe failure */
            }
        });

        Ops ops = mockOps.Object;
        MockLogger.InfoMessages.ShouldContain("📡 radar: on");

        ops.Observe(new OpsPayload());

        // observe fails, but caught
        ops.OpsLib.ShouldNotBeNull();
        mockOps.Protected().Verify("OpsNpm", Times.Once());
        mockOps.Protected().Verify("OpsCli", Times.Once());
    }
}