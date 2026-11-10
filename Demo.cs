using System.Diagnostics;

namespace DotnetRuntimeAsync;

public static class Demo
{
    // The live stack trace example from the Microsoft Learn page "What's new in .NET 11 runtime".
    public static async Task<StackTrace> OuterAsync()
    {
        await Task.CompletedTask;
        return await MiddleAsync();
    }

    static async Task<StackTrace> MiddleAsync()
    {
        await Task.CompletedTask;
        return await InnerAsync();
    }

    static async Task<StackTrace> InnerAsync()
    {
        await Task.CompletedTask;
        return new StackTrace();
    }

    // A two-layer chain that completes synchronously: the common await in a web app
    // (cache hit, buffered read, already completed task).
    public static async Task<int> CallerAsync() => await CalleeAsync() + 1;

    static async Task<int> CalleeAsync() => await Task.FromResult(41);

    // An exception thrown 30 awaits deep and caught at the top.
    public static async Task<int> CatchAsync(int depth)
    {
        try { return await ThrowAsync(depth); }
        catch (InvalidOperationException) { return -1; }
    }

    static async Task<int> ThrowAsync(int depth)
    {
        if (depth == 0) throw new InvalidOperationException("thrown at the bottom");
        return await ThrowAsync(depth - 1);
    }

    // Not covered by runtime async: async void keeps the compiler state machine.
    public static async void FireAndForget()
    {
        await Task.Yield();
    }

    // Not covered by runtime async: async iterators keep the compiler state machine.
    public static async IAsyncEnumerable<int> NumbersAsync()
    {
        await Task.Yield();
        yield return 1;
    }
}
