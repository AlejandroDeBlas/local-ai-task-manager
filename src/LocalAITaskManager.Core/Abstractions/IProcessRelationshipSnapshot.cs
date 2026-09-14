namespace LocalAITaskManager.Core.Abstractions;

public interface IProcessRelationshipSnapshot
{
    bool IsDescendantOf(int pid, int ancestorPid);
    IReadOnlyList<int> GetAncestryChain(int pid, int maxDepth = 16);
    int? GetParentPid(int pid);
}
