using LocalAITaskManager.Core.Abstractions;

namespace LocalAITaskManager.Windows.Processes;

public sealed class ProcessRelationshipSnapshot : IProcessRelationshipSnapshot
{
    private readonly IReadOnlyDictionary<int, int> _parentByPid;

    public ProcessRelationshipSnapshot(IReadOnlyDictionary<int, int> parentByPid)
    {
        _parentByPid = parentByPid ?? throw new ArgumentNullException(nameof(parentByPid));
    }

    public int? GetParentPid(int pid)
    {
        return _parentByPid.TryGetValue(pid, out int parentPid) && parentPid > 0 && parentPid != pid
            ? parentPid
            : null;
    }

    public bool IsDescendantOf(int pid, int ancestorPid)
    {
        if (pid <= 0 || ancestorPid <= 0 || pid == ancestorPid)
        {
            return false;
        }

        var visited = new HashSet<int> { pid };
        int currentPid = pid;

        for (int depth = 0; depth < 32; depth++)
        {
            if (!_parentByPid.TryGetValue(currentPid, out int parentPid) || parentPid <= 0)
            {
                return false;
            }

            if (parentPid == ancestorPid)
            {
                return true;
            }

            if (!visited.Add(parentPid))
            {
                // Cycle detected
                return false;
            }

            currentPid = parentPid;
        }

        return false;
    }

    public IReadOnlyList<int> GetAncestryChain(int pid, int maxDepth = 16)
    {
        if (pid <= 0 || maxDepth <= 0)
        {
            return [];
        }

        var chain = new List<int>();
        var visited = new HashSet<int> { pid };
        int currentPid = pid;

        while (chain.Count < maxDepth)
        {
            if (!_parentByPid.TryGetValue(currentPid, out int parentPid) || parentPid <= 0)
            {
                break;
            }

            if (!visited.Add(parentPid))
            {
                // Cycle detected
                break;
            }

            chain.Add(parentPid);
            currentPid = parentPid;
        }

        return chain;
    }
}
