using LocalAITaskManager.Windows.Processes;
using Xunit;

namespace LocalAITaskManager.Windows.Tests;

public class ProcessAncestryTests
{
    [Fact]
    public void IsDescendantOf_MultiLevelChain_ReturnsTrue()
    {
        // 100 -> 50 -> 10
        var map = new Dictionary<int, int>
        {
            [100] = 50,
            [50] = 10
        };

        var snapshot = new ProcessRelationshipSnapshot(map);

        Assert.True(snapshot.IsDescendantOf(100, 50));
        Assert.True(snapshot.IsDescendantOf(100, 10));
        Assert.True(snapshot.IsDescendantOf(50, 10));
        Assert.False(snapshot.IsDescendantOf(10, 100));
        Assert.False(snapshot.IsDescendantOf(100, 100));
    }

    [Fact]
    public void IsDescendantOf_MissingParentOrInvalid_ReturnsFalse()
    {
        var map = new Dictionary<int, int>
        {
            [100] = 50
        };

        var snapshot = new ProcessRelationshipSnapshot(map);

        Assert.False(snapshot.IsDescendantOf(999, 100));
        Assert.False(snapshot.IsDescendantOf(100, 999));
        Assert.False(snapshot.IsDescendantOf(0, 100));
        Assert.False(snapshot.IsDescendantOf(100, 0));
    }

    [Fact]
    public void IsDescendantOf_CyclicHierarchy_HandlesGracefullyWithoutInfiniteLoop()
    {
        // Cycle: 100 -> 50 -> 100
        var map = new Dictionary<int, int>
        {
            [100] = 50,
            [50] = 100
        };

        var snapshot = new ProcessRelationshipSnapshot(map);

        Assert.False(snapshot.IsDescendantOf(100, 999));
        Assert.True(snapshot.IsDescendantOf(100, 50));
    }

    [Fact]
    public void IsDescendantOf_SelfReferentialParent_ReturnsFalse()
    {
        // 100 -> 100
        var map = new Dictionary<int, int>
        {
            [100] = 100
        };

        var snapshot = new ProcessRelationshipSnapshot(map);

        Assert.False(snapshot.IsDescendantOf(100, 100));
        Assert.Null(snapshot.GetParentPid(100));
    }

    [Fact]
    public void GetAncestryChain_ReturnsOrderedAncestors()
    {
        // 100 -> 50 -> 10
        var map = new Dictionary<int, int>
        {
            [100] = 50,
            [50] = 10
        };

        var snapshot = new ProcessRelationshipSnapshot(map);
        var chain = snapshot.GetAncestryChain(100);

        Assert.Equal([50, 10], chain);
    }
}
