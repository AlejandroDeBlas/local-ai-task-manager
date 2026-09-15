using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LocalAITaskManager.App;
using LocalAITaskManager.App.ViewModels;
using LocalAITaskManager.Core.Models;
using Xunit;

namespace LocalAITaskManager.Windows.Tests;

public class ScreenshotGeneratorTests
{
    [Fact]
    public void GenerateProductScreenshot()
    {
        var thread = new Thread(() =>
        {
            var app = Application.Current ?? new LocalAITaskManager.App.App();
            if (app.Resources.MergedDictionaries.Count == 0)
            {
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/LocalAITaskManager;component/Themes/DarkTheme.xaml")
                });
            }

            var vm = new MainViewModel();

            var gpu = new GpuDeviceSnapshot(
                Id: "GPU-0",
                Index: 0,
                Name: "NVIDIA GeForce RTX 4070 Ti SUPER",
                TotalVramBytes: 16UL * 1024 * 1024 * 1024,
                UsedVramBytes: (ulong)(11.2 * 1024 * 1024 * 1024),
                GpuUtilizationPercent: 38.0,
                TemperatureCelsius: 54.0,
                PowerWatts: 165.0,
                DriverVersion: "572.16"
            );

            var ram = new SystemMemorySnapshot(
                TotalPhysicalBytes: 32UL * 1024 * 1024 * 1024,
                AvailablePhysicalBytes: (ulong)(17.2 * 1024 * 1024 * 1024),
                UsedPhysicalBytes: (ulong)(14.8 * 1024 * 1024 * 1024)
            );

            var p46588 = new GpuProcessSnapshot(
                Pid: 46588,
                ProcessName: "ollama_llama_server.exe",
                LocalGpuMemoryBytes: (ulong)(10.8 * 1024 * 1024 * 1024),
                NonLocalGpuMemoryBytes: 0,
                TotalCommittedGpuMemoryBytes: (ulong)(10.8 * 1024 * 1024 * 1024),
                DedicatedGpuMemoryBytes: (ulong)(10.8 * 1024 * 1024 * 1024),
                SharedGpuMemoryBytes: 0,
                WorkingSetBytes: (ulong)(1.2 * 1024 * 1024 * 1024),
                CpuPercent: 0.4,
                ExecutablePath: @"C:\Program Files\Ollama\lib\ollama_llama_server.exe",
                CommandLine: "ollama_llama_server.exe --model qwen2.5:14b"
            );

            var p1240 = new GpuProcessSnapshot(
                Pid: 1240,
                ProcessName: "ollama.exe",
                LocalGpuMemoryBytes: 15UL * 1024 * 1024,
                NonLocalGpuMemoryBytes: 0,
                TotalCommittedGpuMemoryBytes: 15UL * 1024 * 1024,
                DedicatedGpuMemoryBytes: 15UL * 1024 * 1024,
                SharedGpuMemoryBytes: 0,
                WorkingSetBytes: 45UL * 1024 * 1024,
                CpuPercent: 0.1,
                ExecutablePath: @"C:\Program Files\Ollama\ollama.exe",
                CommandLine: "ollama app.exe"
            );

            var p52140 = new GpuProcessSnapshot(
                Pid: 52140,
                ProcessName: "llama-server.exe",
                LocalGpuMemoryBytes: (ulong)(2.4 * 1024 * 1024 * 1024),
                NonLocalGpuMemoryBytes: 0,
                TotalCommittedGpuMemoryBytes: (ulong)(2.4 * 1024 * 1024 * 1024),
                DedicatedGpuMemoryBytes: (ulong)(2.4 * 1024 * 1024 * 1024),
                SharedGpuMemoryBytes: 0,
                WorkingSetBytes: 850UL * 1024 * 1024,
                CpuPercent: 0.2,
                ExecutablePath: @"C:\Tools\llama.cpp\llama-server.exe",
                CommandLine: "llama-server.exe -m Llama-3.2-3B-Instruct-Q4_K_M.gguf -c 8192"
            );

            var dwm = new GpuProcessSnapshot(
                Pid: 1276,
                ProcessName: "dwm.exe",
                LocalGpuMemoryBytes: 380UL * 1024 * 1024,
                NonLocalGpuMemoryBytes: 0,
                TotalCommittedGpuMemoryBytes: 380UL * 1024 * 1024,
                DedicatedGpuMemoryBytes: 380UL * 1024 * 1024,
                SharedGpuMemoryBytes: 0,
                WorkingSetBytes: 150UL * 1024 * 1024,
                CpuPercent: 1.2,
                ExecutablePath: @"C:\Windows\System32\dwm.exe",
                CommandLine: "dwm.exe"
            );

            var chrome = new GpuProcessSnapshot(
                Pid: 14200,
                ProcessName: "chrome.exe",
                LocalGpuMemoryBytes: 220UL * 1024 * 1024,
                NonLocalGpuMemoryBytes: 0,
                TotalCommittedGpuMemoryBytes: 220UL * 1024 * 1024,
                DedicatedGpuMemoryBytes: 220UL * 1024 * 1024,
                SharedGpuMemoryBytes: 0,
                WorkingSetBytes: 840UL * 1024 * 1024,
                CpuPercent: 0.5,
                ExecutablePath: @"C:\Program Files\Google\Chrome\chrome.exe",
                CommandLine: "chrome.exe --type=gpu-process"
            );

            var code = new GpuProcessSnapshot(
                Pid: 8812,
                ProcessName: "code.exe",
                LocalGpuMemoryBytes: 150UL * 1024 * 1024,
                NonLocalGpuMemoryBytes: 0,
                TotalCommittedGpuMemoryBytes: 150UL * 1024 * 1024,
                DedicatedGpuMemoryBytes: 150UL * 1024 * 1024,
                SharedGpuMemoryBytes: 0,
                WorkingSetBytes: 420UL * 1024 * 1024,
                CpuPercent: 0.2,
                ExecutablePath: @"C:\Tools\VSCode\code.exe",
                CommandLine: "code.exe --type=gpu-process"
            );

            var telem = new SystemSnapshot(
                Timestamp: DateTimeOffset.UtcNow,
                Gpus: [gpu],
                Memory: ram,
                GpuProcesses: [p46588, p1240, p52140, dwm, chrome, code],
                Warnings: []
            );

            var m1 = new DetectedModelIdentity("Qwen2.5 14B Q4_K_M", null, "Q4_K_M", null, "14B", 32768, (ulong)(10.8 * 1024 * 1024 * 1024));
            var m2 = new DetectedModelIdentity("Llama-3.2-3B-Instruct", "C:\\Models\\Llama-3.2-3B.gguf", "Q4_K_M", null, "3B", 8192, null);

            var w1 = new AiWorkloadSnapshot(
                WorkloadId: "ollama:model:46588",
                Kind: AiWorkloadKind.Model,
                Runtime: AiRuntimeKind.Ollama,
                DisplayName: "Qwen2.5 14B Q4_K_M",
                PrimaryPid: 46588,
                RuntimeRootPid: 1240,
                Model: m1,
                RuntimeConfidence: DetectionConfidence.Confirmed,
                ModelConfidence: DetectionConfidence.Confirmed,
                ProcessPids: [46588, 1240],
                PrimaryLocalGpuMemoryBytes: (ulong)(10.8 * 1024 * 1024 * 1024),
                PrimaryWorkingSetBytes: (ulong)(1.2 * 1024 * 1024 * 1024),
                PrimaryCpuPercent: 0.4,
                Evidence: [
                    new(DetectionEvidenceKind.ProcessAncestry, "ollama.exe"),
                    new(DetectionEvidenceKind.LocalApi, "Ollama API /api/ps model qwen2.5:14b")
                ]
            );

            var w2 = new AiWorkloadSnapshot(
                WorkloadId: "llamacpp:model:52140",
                Kind: AiWorkloadKind.Model,
                Runtime: AiRuntimeKind.LlamaCpp,
                DisplayName: "Llama-3.2-3B-Instruct",
                PrimaryPid: 52140,
                RuntimeRootPid: 52140,
                Model: m2,
                RuntimeConfidence: DetectionConfidence.High,
                ModelConfidence: DetectionConfidence.High,
                ProcessPids: [52140],
                PrimaryLocalGpuMemoryBytes: (ulong)(2.4 * 1024 * 1024 * 1024),
                PrimaryWorkingSetBytes: 850UL * 1024 * 1024,
                PrimaryCpuPercent: 0.2,
                Evidence: [
                    new(DetectionEvidenceKind.CommandLine, "llama-server.exe -m Llama-3.2-3B-Instruct-Q4_K_M.gguf")
                ]
            );

            var det = new DetectionSnapshot(
                Timestamp: DateTimeOffset.UtcNow,
                Processes: new Dictionary<int, AiProcessIdentity>(),
                UnmappedModels: [],
                Warnings: []
            );

            var appSnap = new AppSnapshot(telem, det, [w1, w2]);
            vm.UpdateSnapshot(appSnap);
            vm.SelectedWorkload = vm.Workloads[0];

            var window = new MainWindow
            {
                DataContext = vm,
                Width = 1180,
                Height = 780,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false
            };

            window.Show();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
            window.UpdateLayout();

            var rtb = new RenderTargetBitmap(1180, 780, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(window);

            window.Close();

            string assetsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/assets"));
            if (!Directory.Exists(assetsDir))
            {
                Directory.CreateDirectory(assetsDir);
            }

            string targetPng = Path.Combine(assetsDir, "local-ai-task-manager-v0.1.png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = File.Create(targetPng);
            encoder.Save(fs);
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        string expectedPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/assets/local-ai-task-manager-v0.1.png"));
        Assert.True(File.Exists(expectedPath), $"Screenshot file not created at {expectedPath}");
        var fileInfo = new FileInfo(expectedPath);
        Assert.True(fileInfo.Length > 10000, $"Screenshot file is too small ({fileInfo.Length} bytes).");
    }
}
