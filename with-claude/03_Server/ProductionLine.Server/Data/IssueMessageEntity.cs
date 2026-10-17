using ProductionLine.Shared;

namespace ProductionLine.Server.Data;

// 문제 한 건에 연결된 작업자 문의와 관리자 답변입니다.
public sealed class IssueMessageEntity
{
    public long Id { get; set; }
    public Guid IssueId { get; set; }
    public string SenderId { get; set; } = "";
    public string SenderName { get; set; } = "";
    public UserRole SenderRole { get; set; }
    public string Message { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
