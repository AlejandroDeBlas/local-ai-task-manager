using System.ComponentModel;
using System.Windows;
using LocalAITaskManager.App.Services;
using LocalAITaskManager.App.ViewModels;
using LocalAITaskManager.Core.Services;
using LocalAITaskManager.Windows.Detection;
using LocalAITaskManager.Windows.Detection.LlamaCpp;
using LocalAITaskManager.Windows.Detection.LmStudio;
using LocalAITaskManager.Windows.Detection.Ollama;
using LocalAITaskManager.Windows.Memory;
using LocalAITaskManager.Windows.Nvidia;
using LocalAITaskManager.Windows.PerformanceCounters;
using LocalAITaskManager.Windows.Processes;
using LocalAITaskManager.Windows.Telemetry;

namespace LocalAITaskManager.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly NvmlGpuTelemetryProvider _gpuProvider;
    private readonly PdhGpuProcessMemoryProvider _pdhProvider;
    private readonly WindowsSystemSnapshotProvider _snapshotProvider;
    private readonly TelemetrySamplerService _samplerService;
    private readonly OllamaApiClient _ollamaClient;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        _gpuProvider = new NvmlGpuTelemetryProvider();
        _pdhProvider = new PdhGpuProcessMemoryProvider();
        var memoryProvider = new Win32SystemMemoryProvider();
        var processInfoProvider = new Win32ProcessInfoProvider();
        var cpuSampler = new ProcessCpuSampler();

        _snapshotProvider = new WindowsSystemSnapshotProvider(
            _gpuProvider,
            _pdhProvider,
            memoryProvider,
            processInfoProvider,
            cpuSampler
        );

        var cmdParser = new WindowsCommandLineParser();
        var relationshipProvider = new Win32ProcessRelationshipProvider();

        _ollamaClient = new OllamaApiClient();
        var llamaDetector = new LlamaCppRuntimeDetector(cmdParser);
        var ollamaDetector = new OllamaRuntimeDetector(_ollamaClient);
        var lmsDetector = new LmStudioRuntimeDetector();

        var coordinator = new WorkloadDetectionCoordinator(
            relationshipProvider,
            [ollamaDetector, lmsDetector, llamaDetector]
        );

        var composer = new AiWorkloadComposer();

        _samplerService = new TelemetrySamplerService(
            _snapshotProvider,
            appSnapshot => Dispatcher.InvokeAsync(() => _viewModel.UpdateSnapshot(appSnapshot)),
            coordinator,
            composer
        );

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _samplerService.Start();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        try
        {
            // Bounded wait with cancellation to prevent blocking UI thread on close
            _samplerService.DisposeAsync().AsTask().Wait(TimeSpan.FromMilliseconds(500));
        }
        catch
        {
            // Best-effort bounded shutdown
        }

        _gpuProvider.Dispose();
        _pdhProvider.Dispose();
        _ollamaClient.Dispose();
    }
}
