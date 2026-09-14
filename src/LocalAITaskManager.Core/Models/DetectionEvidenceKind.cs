namespace LocalAITaskManager.Core.Models;

public enum DetectionEvidenceKind
{
    ExecutableName,
    ExecutablePath,
    CommandLine,
    ProcessAncestry,
    LocalApi,
    OfficialCli,
    ModelPath,
    FilenameInference
}
