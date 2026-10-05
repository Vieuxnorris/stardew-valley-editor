using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace ValleyEditor.Server;

/// <summary>Runs work queued from HTTP threads on the game thread, where game state may be touched safely.</summary>
internal sealed class GameThreadDispatcher
{
    /// <summary>Upper bound on work items run per tick, so a burst of requests can't stall a frame.</summary>
    private const int MaxPerTick = 32;

    private readonly ConcurrentQueue<Action> queue = new();

    /// <summary>Queue a function for the game thread and get a task for its result.</summary>
    public Task<T> Run<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        this.queue.Enqueue(() =>
        {
            try
            {
                tcs.SetResult(func());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    /// <summary>Run queued work. Must be called from the game thread (UpdateTicked).</summary>
    public void Drain()
    {
        for (int i = 0; i < MaxPerTick && this.queue.TryDequeue(out Action? action); i++)
            action();
    }
}
