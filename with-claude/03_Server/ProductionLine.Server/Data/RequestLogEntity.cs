namespace ProductionLine.Server.Data;

// 앱과 서버가 주고받은 요청 한 건의 기록입니다. 나중에 보냈는지·받았는지 확인하는 근거로 씁니다.
// 비밀번호·토큰·메시지 내용은 저장하지 않습니다.
public sealed class RequestLogEntity
{
    public long Id { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public string? UserId { get; set; }
    public string? ClientIp { get; set; }
    public string Method { get; set; } = "";
    public string Path { get; set; } = "";
    public string Action { get; set; } = "";
    public Guid? IssueId { get; set; }
    public int StatusCode { get; set; }
    public int DurationMs { get; set; }
    public string? Note { get; set; }
}
