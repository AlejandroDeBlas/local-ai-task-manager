using LocalAITaskManager.Core.Abstractions;
using LocalAITaskManager.Core.Models;

namespace LocalAITaskManager.App.Services;

public sealed class TelemetrySamplerService : IAsyncDisposable
{
    private readonly ISystemSnapshotProvider _snapshotProvider;
    private readonly IWorkloadDetectionCoordinator? _detectionCoordinator;
    private readonly IAiWorkloadComposer? _workloadComposer;
    private readonly Action<AppSnapshot> _onSnapshotReady;
    private readonly TimeSpan _interval;
    private readonly CancellationTokenSource _cts = new();
    private Task? _samplingTask;
    private int _isRunning;

    public string? LastUnexpectedError { get; private set; }

    public TelemetrySamplerService(
        ISystemSnapshotProvider snapshotProvider,
        Action<AppSnapshot> onSnapshotReady,
        IWorkloadDetectionCoordinator? detectionCoordinator = null,
        IAiWorkloadComposer? workloadComposer = null,
        TimeSpan? interval = null)
    {
        _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
        _onSnapshotReady = onSnapshotReady ?? throw new ArgumentNullException(nameof(onSnapshotReady));
        _detectionCoordinator = detectionCoordinator;
        _workloadComposer = workloadComposer;
        _interval = interval ?? TimeSpan.FromSeconds(1);
    }

    public void Start()
    {
        if (Interlocked.Exchange(ref _isRunning, 1) == 1)
        {
            return;
        }

        _samplingTask = Task.Run(SamplingLoopAsync);
    }

    private async Task SamplingLoopAsync()
    {
        using var timer = new PeriodicTimer(_interval);
        CancellationToken ct = _cts.Token;

        try
        {
            // Perform immediate first collection
            await CollectOnceAsync(ct).ConfigureAwait(false);

            while (!ct.IsCancellationRequested)
            {
                if (!await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                {
                    break;
                }

                await CollectOnceAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Clean cancellation during shutdown
        }
        catch (Exception ex)
        {
            LastUnexpectedError = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    private async Task CollectOnceAsync(CancellationToken ct)
    {
        try
        {
            SystemSnapshot snapshot = await _snapshotProvider.GetSnapshotAsync(ct).ConfigureAwait(false);

            DetectionSnapshot detection;
            if (_detectionCoordinator is not null)
            {
                detection = await _detectionCoordinator.DetectWorkloadsAsync(snapshot, ct).ConfigureAwait(false);
            }
            else
            {
                detection = new DetectionSnapshot(DateTimeOffset.UtcNow, new Dictionary<int, AiProcessIdentity>(), [], []);
            }

            IReadOnlyList<AiWorkloadSnapshot> workloads = _workloadComposer is not null
                ? _workloadComposer.Compose(snapshot, detection)
                : [];

            var appSnapshot = new AppSnapshot(snapshot, detection, workloads);

            if (!ct.IsCancellationRequested)
            {
                _onSnapshotReady(appSnapshot);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LastUnexpectedError = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isRunning, 0) == 0)
        {
            return;
        }

        _cts.Cancel();

        if (_samplingTask is not null)
        {
            try
            {
                await _samplingTask.ConfigureAwait(false);
            }
            catch
            {
                // Ignore task cancellation on dispose
            }
        }

        _cts.Dispose();
    }
}
