// .NET 11 runtime async: the claims from the blog post, checked at run time.
//   dotnet run -c Release --no-incremental                        -> runtime async
//   dotnet run -c Release --no-incremental -p:RuntimeAsync=false  -> classic compiler state machines
// verify.ps1 runs both builds and compares them.
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using DotnetRuntimeAsync;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var failed = 0;

void Check(string claim, bool ok, string detail)
{
    Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {claim}");
    Console.WriteLine($"       {detail}");
    if (!ok) failed++;
}

static Type? StateMachineOf(string name) =>
    typeof(Demo).GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!
        .GetCustomAttribute<StateMachineAttribute>()?.StateMachineType;

// A classic async method carries [AsyncStateMachine] pointing at the type the compiler generated.
// With runtime async that type is gone: the JIT handles the suspension.
var stateMachine = StateMachineOf(nameof(Demo.CallerAsync));
bool runtimeAsync = stateMachine is null;

Console.WriteLine($".NET {Environment.Version}, {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
Console.WriteLine($"Build: {(runtimeAsync ? "runtime async" : "classic state machines")}");
Console.WriteLine();

// 1. What the compiler emitted for our own async method.
Check(runtimeAsync ? "No compiler-generated state machine for the async method" : "The compiler generated a state machine for the async method",
    true, $"CallerAsync: {(stateMachine is null ? "no state machine type" : $"[AsyncStateMachine] -> {stateMachine.Name}")}");

// 2. async void and async iterators keep the compiler state machine in both builds.
var voidMachine = StateMachineOf(nameof(Demo.FireAndForget));
var iteratorMachine = StateMachineOf(nameof(Demo.NumbersAsync));
Check("async void and async iterators keep the compiler state machine",
    voidMachine is not null && iteratorMachine is not null,
    $"FireAndForget -> {voidMachine?.Name}, NumbersAsync -> {iteratorMachine?.Name}");

// 3. Live stack trace through three nested async calls.
var trace = await Demo.OuterAsync();
var frames = trace.GetFrames().Select(f => $"{f.GetMethod()?.DeclaringType?.Name}.{f.GetMethod()?.Name}").ToArray();
bool infrastructure = frames.Any(f => f.Contains("MoveNext") || f.Contains("MethodBuilder"));
Check(runtimeAsync ? "The live stack trace shows only our methods" : "The live stack trace is full of state machine frames",
    runtimeAsync ? !infrastructure : infrastructure, $"{trace.FrameCount} frames");
foreach (var frame in frames) Console.WriteLine($"          at {frame}");

// 4. A two-layer chain that completes synchronously.
const int Calls = 10_000_000;
for (int i = 0; i < 100_000; i++) await Demo.CallerAsync(); // warm up the JIT
long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
var sw = Stopwatch.StartNew();
for (int i = 0; i < Calls; i++) await Demo.CallerAsync();
sw.Stop();
double bytesPerCall = (double)(GC.GetAllocatedBytesForCurrentThread() - bytesBefore) / Calls;
Check(runtimeAsync ? "A sync-completing await chain allocates nothing" : "A sync-completing await chain allocates tasks",
    runtimeAsync ? bytesPerCall < 1 : bytesPerCall > 0,
    $"{sw.Elapsed.TotalNanoseconds / Calls:F1} ns and {bytesPerCall:F0} B per call");

// 5. An exception through 30 awaits (timing only; verify.ps1 compares the two builds).
const int Throws = 20_000;
for (int i = 0; i < 1_000; i++) await Demo.CatchAsync(30); // warm up
sw.Restart();
for (int i = 0; i < Throws; i++) await Demo.CatchAsync(30);
sw.Stop();
Console.WriteLine("[INFO] Exception through 30 awaits");
Console.WriteLine($"       {sw.Elapsed.TotalMicroseconds / Throws:F1} us per throw");

Console.WriteLine();
Console.WriteLine(failed == 0 ? "All checks passed." : $"{failed} check(s) failed.");
return failed == 0 ? 0 : 1;
