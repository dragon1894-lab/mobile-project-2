namespace ProductionLine.Shared;

// [공용 양식] 앱과 서버가 주고받는 데이터의 모양입니다. 앱과 서버가 같은 파일을 씁니다.
// - Request = 앱이 서버에 보내는 것, Dto/Response = 서버가 앱에 돌려주는 것
// - 이 파일을 바꾸면 모든 앱이 영향을 받으므로 팀 합의 후 바꾸고, 바뀐 파일을 조원에게 다시 보내야 합니다.
// - 새로 추가만 하는 것은 기존 앱 코드를 깨지 않습니다.

// 아래 목록(enum)은 DB와 통신에서 숫자로 저장됩니다. 순서를 바꾸면 기존 데이터의 뜻이 바뀌니 바꾸지 마세요.
public enum UserRole { Admin, User }                                                      // 0 관리자, 1 작업자
public enum InspectionResultType { Normal, Defect, Undetermined }                          // 0 정상, 1 불량, 2 판정 불가
public enum IssueType { ProductionLine, InspectionSystem }                                 // 0 생산라인 문제, 1 검사 시스템 문제
public enum IssueStatus { Occurred, Acknowledged, InProgress, CompletionRequested, Closed } // 0 발생, 1 확인, 2 조치 중, 3 완료 요청, 4 종결

public sealed record LoginRequest(string UserId, string Password);
public sealed record LoginResponse(string Token, UserDto User);

public sealed record UserDto(
    string Id,
    string Name,
    UserRole Role,
    bool IsActive,
    IReadOnlyList<int> AllowedLineIds);

public sealed record CreateUserRequest(
    string Id,
    string Name,
    string Password,
    IReadOnlyList<int> AllowedLineIds);

public sealed record UpdateUserRequest(
    string Name,
    bool IsActive,
    IReadOnlyList<int> AllowedLineIds,
    string? NewPassword = null);

public sealed record LineStatusDto(
    int Id,
    string Name,
    bool IsReal,
    string OperationStatus,
    string InspectionStatus,
    int NormalCount,
    int DefectCount,
    int UndeterminedCount,
    DateTimeOffset UpdatedAt);

public sealed record InspectionRequest(
    int LineId,
    double ShapeScore,
    double ColorScore,
    bool ImageAcquired = true);

public sealed record InspectionDto(
    Guid Id,
    int LineId,
    InspectionResultType Result,
    double ShapeScore,
    double ColorScore,
    string Reason,
    DateTimeOffset InspectedAt);

public sealed record CreateIssueRequest(int LineId, IssueType Type, string Title, string Description);

public sealed record IssueDto(
    Guid Id,
    int LineId,
    IssueType Type,
    string Title,
    string Description,
    IssueStatus Status,
    string CreatedBy,
    string? ActionNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record UpdateIssueStatusRequest(IssueStatus Status, string ActionNote);
public sealed record AiConsultRequest(Guid IssueId, string Question);
public sealed record AiConsultResponse(string Answer, string ManualSource, bool NeedAdmin);
public sealed record ApiMessage(string Message);

// 문제 한 건에 연결된 작업자 문의·관리자 답변 메시지입니다. AI 상담 기록과는 별개입니다.
public sealed record CreateIssueMessageRequest(string Message);

public sealed record IssueMessageDto(
    long Id,
    Guid IssueId,
    string SenderId,
    string SenderName,
    UserRole SenderRole,
    string Message,
    DateTimeOffset CreatedAt);
