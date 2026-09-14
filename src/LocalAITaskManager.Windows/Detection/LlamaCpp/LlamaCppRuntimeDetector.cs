using LocalAITaskManager.Core.Abstractions;
using LocalAITaskManager.Core.Models;
using LocalAITaskManager.Core.Services;

namespace LocalAITaskManager.Windows.Detection.LlamaCpp;

public sealed class LlamaCppRuntimeDetector : IRuntimeDetector
{
    private static readonly HashSet<string> KnownExecutables = new(StringComparer.OrdinalIgnoreCase)
    {
        "llama-server.exe",
        "llama-server",
        "llama-cli.exe",
        "llama-cli",
        "llama-simple.exe",
        "llama-bench.exe"
    };

    private readonly ICommandLineParser _commandLineParser;

    public AiRuntimeKind Runtime => AiRuntimeKind.LlamaCpp;

    public LlamaCppRuntimeDetector(ICommandLineParser commandLineParser)
    {
        _commandLineParser = commandLineParser ?? throw new ArgumentNullException(nameof(commandLineParser));
    }

    public Task<RuntimeDetectionResult> DetectAsync(
        WorkloadDetectionContext context,
        CancellationToken cancellationToken)
    {
        var identified = new List<AiProcessIdentity>();

        foreach (GpuProcessSnapshot process in context.TelemetrySnapshot.GpuProcesses)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string procName = process.ProcessName;
            if (!procName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                procName += ".exe";
            }

            if (!KnownExecutables.Contains(procName))
            {
                continue;
            }

            var evidence = new List<DetectionEvidence>
            {
                new(DetectionEvidenceKind.ExecutableName, $"Executable identified as {procName}")
            };

            string? modelPath = null;
            bool usesModelsDir = false;
            int? contextLength = null;

            if (!string.IsNullOrWhiteSpace(process.CommandLine))
            {
                IReadOnlyList<string> args = _commandLineParser.ParseArguments(process.CommandLine);
                for (int i = 0; i < args.Count; i++)
                {
                    string arg = args[i];

                    if (arg.Equals("-m", StringComparison.OrdinalIgnoreCase) ||
                        arg.Equals("--model", StringComparison.OrdinalIgnoreCase))
                    {
                        if (i + 1 < args.Count && !args[i + 1].StartsWith('-'))
                        {
                            modelPath = args[i + 1];
                            evidence.Add(new(DetectionEvidenceKind.CommandLine, $"{arg} argument found"));
                            i++;
                        }
                    }
                    else if (arg.StartsWith("--model=", StringComparison.OrdinalIgnoreCase))
                    {
                        modelPath = arg["--model=".Length..].Trim('\"');
                        evidence.Add(new(DetectionEvidenceKind.CommandLine, "--model= argument found"));
                    }
                    else if (arg.StartsWith("-m=", StringComparison.OrdinalIgnoreCase))
                    {
                        modelPath = arg["-m=".Length..].Trim('\"');
                        evidence.Add(new(DetectionEvidenceKind.CommandLine, "-m= argument found"));
                    }
                    else if (arg.Equals("--models-dir", StringComparison.OrdinalIgnoreCase))
                    {
                        usesModelsDir = true;
                        evidence.Add(new(DetectionEvidenceKind.CommandLine, "--models-dir argument found"));
                        if (i + 1 < args.Count && !args[i + 1].StartsWith('-'))
                        {
                            i++;
                        }
                    }
                    else if (arg.StartsWith("--models-dir=", StringComparison.OrdinalIgnoreCase))
                    {
                        usesModelsDir = true;
                        evidence.Add(new(DetectionEvidenceKind.CommandLine, "--models-dir= argument found"));
                    }
                    else if (arg.Equals("-c", StringComparison.OrdinalIgnoreCase) ||
                             arg.Equals("--ctx-size", StringComparison.OrdinalIgnoreCase))
                    {
                        if (i + 1 < args.Count && int.TryParse(args[i + 1], out int parsedCtx))
                        {
                            contextLength = parsedCtx;
                            evidence.Add(new(DetectionEvidenceKind.CommandLine, $"{arg} configured context found"));
                            i++;
                        }
                    }
                    else if (arg.StartsWith("--ctx-size=", StringComparison.OrdinalIgnoreCase))
                    {
                        string val = arg["--ctx-size=".Length..].Trim('\"');
                        if (int.TryParse(val, out int parsedCtx))
                        {
                            contextLength = parsedCtx;
                            evidence.Add(new(DetectionEvidenceKind.CommandLine, "--ctx-size= configured context found"));
                        }
                    }
                    else if (arg.StartsWith("-c=", StringComparison.OrdinalIgnoreCase))
                    {
                        string val = arg["-c=".Length..].Trim('\"');
                        if (int.TryParse(val, out int parsedCtx))
                        {
                            contextLength = parsedCtx;
                            evidence.Add(new(DetectionEvidenceKind.CommandLine, "-c= configured context found"));
                        }
                    }
                }
            }

            DetectedModelIdentity? model = null;
            DetectionConfidence modelConfidence = DetectionConfidence.None;

            if (!string.IsNullOrWhiteSpace(modelPath))
            {
                string displayName = Path.GetFileNameWithoutExtension(modelPath);
                string? quant = GgufQuantizationInference.InferFromFileName(modelPath);
                if (quant is not null)
                {
                    evidence.Add(new(DetectionEvidenceKind.FilenameInference, $"Quantization {quant} inferred from model filename"));
                }

                model = new DetectedModelIdentity(
                    DisplayName: displayName,
                    ModelPath: modelPath,
                    Quantization: quant,
                    Architecture: null,
                    ParameterSize: null,
                    ContextLength: contextLength
                );
                modelConfidence = DetectionConfidence.High;
            }
            else if (usesModelsDir)
            {
                evidence.Add(new(DetectionEvidenceKind.CommandLine, "llama.cpp server using models directory; active model cannot be determined from arguments"));
            }

            identified.Add(new AiProcessIdentity(
                Pid: process.Pid,
                Runtime: AiRuntimeKind.LlamaCpp,
                RuntimeConfidence: DetectionConfidence.High,
                Model: model,
                ModelConfidence: modelConfidence,
                Evidence: evidence
            ));
        }

        return Task.FromResult(new RuntimeDetectionResult(identified, [], []));
    }
}
