# .NET 11 Runtime Async, Classic vs Runtime

Source code for the dotnetleet.com post
[.NET 11 Runtime Async: Await Without the State Machine](https://dotnetleet.com/Blogs/DotNetRuntimeAsync).

One console application built twice from the same source: once with the classic compiler state
machines and once with runtime async. Every claim of the post is a check that prints `PASS` or `FAIL`.

## Requirements

- .NET 11 SDK 11.0.100 or later ([download](https://dotnet.microsoft.com/download/dotnet/11.0))

## Run

Runtime async is turned on by one line in `DotnetRuntimeAsync.csproj`:

```xml
<Features>$(Features);runtime-async=on</Features>
```

```
dotnet run -c Release --no-incremental                        # runtime async
dotnet run -c Release --no-incremental -p:RuntimeAsync=false  # classic state machines
```

Build both and compare them (Windows PowerShell or PowerShell 7):

```
pwsh ./verify.ps1
```

## What the checks show

`verify.ps1` on SDK 11.0.100, AMD Ryzen 9 9950X3D, Windows x64, median of three runs:

| | Classic | Runtime async |
|---|---|---|
| `CallerAsync` | `[AsyncStateMachine] <CallerAsync>d__3` | no state machine type |
| Live stack trace, three nested calls | 17 frames | 5 frames |
| Two-layer chain that completes synchronously | 25.0 ns, 216 B | 14.6 ns, 0 B |
| Exception through 30 awaits | 44.0 us | 8.8 us |

`async void` and async iterators keep the compiler state machine in both builds, as documented.

Timings are a `Stopwatch` loop, not BenchmarkDotNet: good for the direction on your machine. The
official BenchmarkDotNet results are in the .NET Blog post below.

## Files

| File | What it shows |
|---|---|
| `Demo.cs` | The Learn stack trace example, a sync-completing chain, a 30-level throw, `async void`, an async iterator |
| `Program.cs` | The checks for one build |
| `verify.ps1` | Builds the classic and runtime variants and compares them |

## Official Microsoft documentation

- [What's new in .NET 11 runtime](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/runtime)
- [Performance Improvements in .NET 11](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-11/) (.NET Blog)
- [What's new in .NET 11](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview)
