namespace LocalAITaskManager.Core.Abstractions;

public interface IProcessRelationshipProvider
{
    IProcessRelationshipSnapshot GetSnapshot();
}
