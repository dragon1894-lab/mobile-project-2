using ProductionLine.Shared;

namespace ProductionLine.Server.Data;

public sealed class IssueHistoryEntity
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public IssueStatus PreviousStatus { get; set; }
    public IssueStatus NewStatus { get; set; }
    public string ChangedBy { get; set; } = "";
    public DateTimeOffset ChangedAt { get; set; }
    public string ActionNote { get; set; } = "";
}
