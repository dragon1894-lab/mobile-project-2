using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ProductionLine.Server.Data;
using ProductionLine.Shared;

namespace ProductionLine.Server.Infrastructure;

// 기존 API가 사용하는 저장 담당 클래스입니다. 이제 JSON 대신 SQLite를 사용합니다.
public sealed class AppStore
{
    private readonly object _gate = new();
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public AppStore(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public UserRecord? ValidateLogin(string id, string password)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrEmpty(password)) return null;
        using var db = _contextFactory.CreateDbContext();
        var user = FindUser(db, id);
        return user is { IsActive: true } && PasswordHasher.Verify(password, user.PasswordHash)
            ? ToRecord(db, user)
            : null;
    }

    public IReadOnlyList<UserDto> GetUsers()
    {
        using var db = _contextFactory.CreateDbContext();
        var permissions = db.UserLineAccesses.AsNoTracking().ToList();
        return db.Users.AsNoTracking().OrderBy(x => x.Id).ToList()
            .Select(x => new UserDto(x.Id, x.Name, x.Role, x.IsActive,
                permissions.Where(p => p.UserId == x.Id).Select(p => p.LineId).Order().ToList()))
            .ToList();
    }

    public UserDto AddUser(CreateUserRequest request)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Name)
                || string.IsNullOrEmpty(request.Password) || request.Password.Length < 6)
                throw new ArgumentException("ID, 이름, 6자 이상의 비밀번호를 입력하세요.");

            using var db = _contextFactory.CreateDbContext();
            var id = request.Id.Trim();
            if (FindUser(db, id) is not null)
                throw new InvalidOperationException("이미 사용 중인 사용자 ID입니다.");

            var lineIds = ValidateLineIds(db, request.AllowedLineIds);
            var user = new UserEntity
            {
                Id = id,
                Name = request.Name.Trim(),
                Role = UserRole.User,
                PasswordHash = PasswordHasher.Hash(request.Password),
                IsActive = true
            };
            db.Users.Add(user);
            db.UserLineAccesses.AddRange(lineIds.Select(lineId => new UserLineAccessEntity
            {
                UserId = user.Id,
                LineId = lineId
            }));
            db.SaveChanges();
            return new UserDto(user.Id, user.Name, user.Role, user.IsActive, lineIds);
        }
    }

    public UserDto UpdateUser(string id, UpdateUserRequest request)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("이름을 입력하세요.");
            if (!string.IsNullOrWhiteSpace(request.NewPassword) && request.NewPassword.Length < 6)
                throw new ArgumentException("비밀번호는 6자 이상이어야 합니다.");

            using var db = _contextFactory.CreateDbContext();
            var user = FindUser(db, id) ?? throw new KeyNotFoundException("사용자를 찾을 수 없습니다.");
            var lineIds = ValidateLineIds(db, request.AllowedLineIds);
            user.Name = request.Name.Trim();
            user.IsActive = request.IsActive;
            if (!string.IsNullOrWhiteSpace(request.NewPassword))
                user.PasswordHash = PasswordHasher.Hash(request.NewPassword);

            var current = db.UserLineAccesses.Where(x => x.UserId == user.Id).ToList();
            var existingIds = current.Select(x => x.LineId).ToHashSet();
            db.UserLineAccesses.RemoveRange(current.Where(x => !lineIds.Contains(x.LineId)));
            db.UserLineAccesses.AddRange(lineIds.Where(x => !existingIds.Contains(x))
                .Select(lineId => new UserLineAccessEntity { UserId = user.Id, LineId = lineId }));
            db.SaveChanges();
            return new UserDto(user.Id, user.Name, user.Role, user.IsActive, lineIds);
        }
    }

    public IReadOnlyList<LineStatusDto> GetLines(IEnumerable<int>? allowedLineIds = null)
    {
        using var db = _contextFactory.CreateDbContext();
        var query = db.Lines.AsNoTracking();
        if (allowedLineIds is not null)
        {
            var ids = allowedLineIds.Distinct().ToArray();
            query = query.Where(x => ids.Contains(x.Id));
        }
        return query.OrderBy(x => x.Id).ToList().Select(ToDto).ToList();
    }

    public LineStatusDto? FindLine(int id)
    {
        using var db = _contextFactory.CreateDbContext();
        var line = db.Lines.AsNoTracking().SingleOrDefault(x => x.Id == id);
        return line is null ? null : ToDto(line);
    }

    public InspectionDto AddInspection(InspectionDto inspection)
    {
        lock (_gate)
        {
            using var db = _contextFactory.CreateDbContext();
            var line = db.Lines.Find(inspection.LineId)
                ?? throw new ArgumentException("존재하지 않는 라인입니다.");
            db.Inspections.Add(new InspectionEntity
            {
                Id = inspection.Id,
                LineId = inspection.LineId,
                Result = inspection.Result,
                ShapeScore = inspection.ShapeScore,
                ColorScore = inspection.ColorScore,
                Reason = inspection.Reason,
                InspectedAt = inspection.InspectedAt
            });
            if (inspection.Result == InspectionResultType.Normal) line.NormalCount++;
            else if (inspection.Result == InspectionResultType.Defect) line.DefectCount++;
            else if (inspection.Result == InspectionResultType.Undetermined) line.UndeterminedCount++;
            line.InspectionStatus = inspection.Result == InspectionResultType.Undetermined
                ? "검사 확인 필요" : "검사 중";
            line.UpdatedAt = inspection.InspectedAt;
            // 검사 기록과 라인 집계는 한 트랜잭션으로 저장됩니다.
            db.SaveChanges();
            return inspection;
        }
    }

    public IReadOnlyList<InspectionDto> GetInspections(int lineId)
    {
        using var db = _contextFactory.CreateDbContext();
        // SQLite의 DateTimeOffset 정렬 제한을 피하고 실제 시각 기준으로 정렬합니다.
        return db.Inspections.AsNoTracking().Where(x => x.LineId == lineId)
            .AsEnumerable().OrderByDescending(x => x.InspectedAt).ThenBy(x => x.Id)
            .Take(100).Select(ToDto).ToList();
    }

    public IssueDto AddIssue(CreateIssueRequest request, string userId)
    {
        lock (_gate)
        {
            using var db = _contextFactory.CreateDbContext();
            if (string.IsNullOrWhiteSpace(request.Title))
                throw new ArgumentException("문제 제목을 입력하세요.");
            if (!Enum.IsDefined(request.Type))
                throw new ArgumentException("올바른 문제 종류를 선택하세요.");
            if (!db.Lines.Any(x => x.Id == request.LineId))
                throw new ArgumentException("존재하지 않는 라인입니다.");
            var creator = FindUser(db, userId)
                ?? throw new ArgumentException("등록한 사용자를 찾을 수 없습니다.");
            var now = DateTimeOffset.Now;
            var issue = new IssueEntity
            {
                Id = Guid.NewGuid(),
                LineId = request.LineId,
                Type = request.Type,
                Title = request.Title.Trim(),
                Description = request.Description?.Trim() ?? "",
                Status = IssueStatus.Occurred,
                CreatedBy = creator.Id,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Issues.Add(issue);
            db.SaveChanges();
            return ToDto(issue);
        }
    }

    // 변경자는 윈폼 입력이 아니라 인증된 서버 세션에서 전달합니다.
    public IssueDto UpdateIssue(Guid id, UpdateIssueStatusRequest request, string changedBy)
    {
        lock (_gate)
        {
            using var db = _contextFactory.CreateDbContext();
            var issue = db.Issues.Find(id) ?? throw new KeyNotFoundException("문제를 찾을 수 없습니다.");
            var actor = FindUser(db, changedBy)
                ?? throw new ArgumentException("변경한 사용자를 찾을 수 없습니다.");
            if (!Enum.IsDefined(request.Status))
                throw new ArgumentException("올바른 문제 상태를 선택하세요.");

            if (!actor.IsActive || !Enum.IsDefined(actor.Role))
                throw new UnauthorizedAccessException("사용할 수 없는 계정입니다.");
            if (actor.Role != UserRole.Admin && !db.UserLineAccesses.Any(
                    x => x.UserId == actor.Id && x.LineId == issue.LineId))
                throw new UnauthorizedAccessException("이 라인의 문제를 변경할 권한이 없습니다.");

            ValidateIssueTransition(issue, request, actor.Role);

            var now = DateTimeOffset.Now;
            db.IssueHistories.Add(new IssueHistoryEntity
            {
                Id = Guid.NewGuid(),
                IssueId = issue.Id,
                PreviousStatus = issue.Status,
                NewStatus = request.Status,
                ChangedBy = actor.Id,
                ChangedAt = now,
                ActionNote = request.ActionNote ?? ""
            });
            issue.Status = request.Status;
            issue.ActionNote = request.ActionNote;
            issue.UpdatedAt = now;
            // 현재 상태와 변경 이력을 함께 저장합니다.
            db.SaveChanges();
            return ToDto(issue);
        }
    }

    public IssueDto? FindIssue(Guid id)
    {
        using var db = _contextFactory.CreateDbContext();
        var issue = db.Issues.AsNoTracking().SingleOrDefault(x => x.Id == id);
        return issue is null ? null : ToDto(issue);
    }

    public IReadOnlyList<IssueDto> GetIssues(IEnumerable<int>? allowedLineIds = null)
    {
        using var db = _contextFactory.CreateDbContext();
        var query = db.Issues.AsNoTracking();
        if (allowedLineIds is not null)
        {
            var ids = allowedLineIds.Distinct().ToArray();
            query = query.Where(x => ids.Contains(x.LineId));
        }
        return query.AsEnumerable().OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id).Select(ToDto).ToList();
    }

    private static void ValidateIssueTransition(
        IssueEntity issue, UpdateIssueStatusRequest request, UserRole role)
    {
        if (issue.Status == IssueStatus.Closed)
            throw new InvalidOperationException("이미 종결된 문제는 변경할 수 없습니다.");
        if (issue.Status == request.Status)
            throw new InvalidOperationException("현재 상태와 같은 상태로 변경할 수 없습니다.");

        if (role == UserRole.Admin)
        {
            if (request.Status != IssueStatus.Closed && request.Status != IssueStatus.InProgress)
                throw new UnauthorizedAccessException("관리자는 완료 요청을 종결하거나 반려할 수 있습니다.");
            if (issue.Status != IssueStatus.CompletionRequested)
                throw new InvalidOperationException("완료 요청 상태의 문제만 종결하거나 반려할 수 있습니다.");
            if (request.Status == IssueStatus.InProgress && string.IsNullOrWhiteSpace(request.ActionNote))
                throw new ArgumentException("반려 사유를 입력하세요.");
            return;
        }

        if (request.Status == IssueStatus.Closed)
            throw new UnauthorizedAccessException("문제 종결은 관리자만 수행할 수 있습니다.");

        IssueStatus? next = issue.Status switch
        {
            IssueStatus.Occurred => IssueStatus.Acknowledged,
            IssueStatus.Acknowledged => IssueStatus.InProgress,
            IssueStatus.InProgress => IssueStatus.CompletionRequested,
            _ => null
        };

        if (next is null)
            throw new InvalidOperationException("완료 요청 후에는 관리자의 확인을 기다리세요.");
        if (request.Status != next.Value)
            throw new InvalidOperationException("문제 상태는 발생 → 확인 → 조치 중 → 완료 요청 순서로 변경하세요.");
    }

    private static UserEntity? FindUser(AppDbContext db, string id) =>
        // 기존 사용자 ID의 OrdinalIgnoreCase 비교 규칙을 유지합니다.
        db.Users.AsEnumerable().FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    private static int[] ValidateLineIds(AppDbContext db, IReadOnlyList<int>? requested)
    {
        if (requested is null) throw new ArgumentException("접근 가능한 라인 목록을 전달하세요.");
        var ids = requested.Distinct().Order().ToArray();
        if (db.Lines.Count(x => ids.Contains(x.Id)) != ids.Length)
            throw new ArgumentException("존재하지 않는 라인이 포함되어 있습니다.");
        return ids;
    }

    private static UserRecord ToRecord(AppDbContext db, UserEntity user) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Role = user.Role,
        PasswordHash = user.PasswordHash,
        IsActive = user.IsActive,
        AllowedLineIds = db.UserLineAccesses.AsNoTracking().Where(x => x.UserId == user.Id)
            .OrderBy(x => x.LineId).Select(x => x.LineId).ToList()
    };

    public static UserDto ToDto(UserRecord user) =>
        new(user.Id, user.Name, user.Role, user.IsActive, user.AllowedLineIds);

    private static LineStatusDto ToDto(LineEntity line) =>
        new(line.Id, line.Name, line.IsReal, line.OperationStatus, line.InspectionStatus,
            line.NormalCount, line.DefectCount, line.UndeterminedCount, line.UpdatedAt);

    private static InspectionDto ToDto(InspectionEntity inspection) =>
        new(inspection.Id, inspection.LineId, inspection.Result, inspection.ShapeScore,
            inspection.ColorScore, inspection.Reason, inspection.InspectedAt);

    private static IssueDto ToDto(IssueEntity issue) =>
        new(issue.Id, issue.LineId, issue.Type, issue.Title, issue.Description, issue.Status,
            issue.CreatedBy, issue.ActionNote, issue.CreatedAt, issue.UpdatedAt);
}

// AuthServiceとの既存の受け渡し形式は維持します。
public sealed class UserRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public UserRole Role { get; set; }
    public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; }
    public List<int> AllowedLineIds { get; set; } = [];
}

internal static class PasswordHasher
{
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        try
        {
            var parts = stored.Split('.');
            if (parts.Length != 2) return false;
            var salt = Convert.FromBase64String(parts[0]);
            var expected = Convert.FromBase64String(parts[1]);
            if (salt.Length != 16 || expected.Length != 32) return false;
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(hash, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
