using LocalAITaskManager.Core.Models;

namespace LocalAITaskManager.Core.Abstractions;

public interface ISystemSnapshotProvider
{
    Task<SystemSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
