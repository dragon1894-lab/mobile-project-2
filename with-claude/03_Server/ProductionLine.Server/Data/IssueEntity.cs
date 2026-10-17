using ProductionLine.Shared;

namespace ProductionLine.Server.Data;

public sealed class IssueEntity
{
    public Guid Id { get; set; }
    public int LineId { get; set; }
    public IssueType Type { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public IssueStatus Status { get; set; }
    public string CreatedBy { get; set; } = "";
    public string? ActionNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}