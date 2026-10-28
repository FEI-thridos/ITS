namespace KukaFm.Middleware.Api.Sync;

/// <summary>
/// Startup gate (system-definition §5): mission sync waits until the first robot sync succeeded with at least
/// one robot; dispatch and cancel are allowed only after that and the first successful mission sync tick.
/// </summary>
public class SyncReadiness
{
    private readonly TaskCompletionSource _robotsReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _missionsReady;

    public bool IsReady => _robotsReady.Task.IsCompleted && _missionsReady;

    /// <summary>Returns true only on the first call.</summary>
    public bool MarkRobotsReady() => _robotsReady.TrySetResult();

    public Task WaitForRobotsAsync(CancellationToken ct) => _robotsReady.Task.WaitAsync(ct);

    /// <summary>Returns true only on the first call.</summary>
    public bool MarkMissionsReady()
    {
        if (_missionsReady)
            return false;
        _missionsReady = true;
        return true;
    }
}
