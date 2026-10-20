using System.Security.Cryptography;
using System.Text.Json;
using ProductionLine.Shared;

namespace ProductionLine.Server.Infrastructure;

public sealed class AppStore
{
    private readonly object _gate = new();
    private readonly string _filePath;
    private readonly string _imageDirectory;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private StoreData _data;

    public AppStore(IHostEnvironment environment)
    {
        var dataDirectory = Path.Combine(environment.ContentRootPath, "data");
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "store.json");
        _imageDirectory = Path.Combine(dataDirectory, "inspection-images");
        Directory.CreateDirectory(_imageDirectory);
        _data = LoadOrSeed();
    }

    public UserRecord? ValidateLogin(string id, string password)
    {
        lock (_gate)
        {
            var user = _data.Users.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            return user is { IsActive: true } && PasswordHasher.Verify(password, user.PasswordHash) ? user : null;
        }
    }

    public IReadOnlyList<UserDto> GetUsers()
    {
        lock (_gate) return _data.Users.Select(ToDto).ToList();
    }

    public UserDto AddUser(CreateUserRequest request)
    {
        lock (_gate)
        {
            if (_data.Users.Any(x => x.Id.Equals(request.Id, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("이미 사용 중인 사용자 ID입니다.");

            var user = new UserRecord
            {
                Id = request.Id.Trim(), Name = request.Name.Trim(), Role = UserRole.User,
                PasswordHash = PasswordHasher.Hash(request.Password), IsActive = true,
                AllowedLineIds = request.AllowedLineIds.Distinct().ToList()
            };
            _data.Users.Add(user);
            Save();
            return ToDto(user);
        }
    }

    public UserDto UpdateUser(string id, UpdateUserRequest request)
    {
        lock (_gate)
        {
            var user = _data.Users.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                       ?? throw new KeyNotFoundException("사용자를 찾을 수 없습니다.");
            if (user.Role != UserRole.User)
                throw new InvalidOperationException("이 화면에서는 작업자 계정만 수정할 수 있습니다.");
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new InvalidOperationException("사용자 이름을 입력하세요.");
            if (request.NewPassword is { Length: > 0 and < 6 } ||
                request.NewPassword is { Length: > 0 } && string.IsNullOrWhiteSpace(request.NewPassword))
                throw new InvalidOperationException("비밀번호는 6자 이상 입력하세요.");
            if (request.AllowedLineIds.Any(lineId => !_data.Lines.Any(line => line.Id == lineId)))
                throw new InvalidOperationException("존재하지 않는 라인은 허용할 수 없습니다.");
            user.Name = request.Name.Trim();
            user.IsActive = request.IsActive;
            user.AllowedLineIds = request.AllowedLineIds.Distinct().ToList();
            if (!string.IsNullOrWhiteSpace(request.NewPassword))
                user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
            Save();
            return ToDto(user);
        }
    }

    public IReadOnlyList<LineStatusDto> GetLines(IEnumerable<int>? allowedLineIds = null)
    {
        lock (_gate)
        {
            var allowed = allowedLineIds?.ToHashSet();
            return _data.Lines.Where(x => allowed is null || allowed.Contains(x.Id)).ToList();
        }
    }

    public LineStatusDto? FindLine(int id)
    {
        lock (_gate) return _data.Lines.FirstOrDefault(x => x.Id == id);
    }

    public InspectionDto AddInspection(InspectionDto inspection, byte[]? image = null)
    {
        lock (_gate)
        {
            var previousInspections = _data.Inspections.ToList();
            var previousLines = _data.Lines.ToList();
            inspection = inspection with { HasImage = image != null };
            var imagePath = Path.Combine(_imageDirectory, inspection.Id.ToString("N") + ".jpg");
            if (image != null) File.WriteAllBytes(imagePath, image);
            try
            {
                _data.Inspections.Insert(0, inspection);
                var index = _data.Lines.FindIndex(x => x.Id == inspection.LineId);
                if (index >= 0)
                {
                    var line = _data.Lines[index];
                    _data.Lines[index] = line with
                    {
                        NormalCount = line.NormalCount + (inspection.Result == InspectionResultType.Normal ? 1 : 0),
                        DefectCount = line.DefectCount + (inspection.Result == InspectionResultType.Defect ? 1 : 0),
                        UndeterminedCount = line.UndeterminedCount + (inspection.Result == InspectionResultType.Undetermined ? 1 : 0),
                        InspectionStatus = inspection.Result == InspectionResultType.Undetermined
                            ? inspection.DetectionConfidence.HasValue ? "휴대폰 검출 완료 · 품질 판정 미지원" : "검사 확인 필요"
                            : "검사 중",
                        UpdatedAt = inspection.InspectedAt
                    };
                }
                if (_data.Inspections.Count > 500)
                    _data.Inspections.RemoveRange(500, _data.Inspections.Count - 500);
                Save();
            }
            catch
            {
                _data.Inspections = previousInspections;
                _data.Lines = previousLines;
                if (image != null) File.Delete(imagePath);
                throw;
            }
            foreach (var expired in previousInspections.Where(x => x.HasImage && !_data.Inspections.Any(current => current.Id == x.Id)))
            {
                try { File.Delete(Path.Combine(_imageDirectory, expired.Id.ToString("N") + ".jpg")); }
                catch (IOException) { /* Retained orphan file does not change a successfully saved record. */ }
                catch (UnauthorizedAccessException) { }
            }
            return inspection;
        }
    }

    public IReadOnlyList<InspectionDto> GetInspections(int lineId)
    {
        lock (_gate) return _data.Inspections.Where(x => x.LineId == lineId).Take(100).ToList();
    }

    public InspectionDto? FindInspection(Guid id)
    {
        lock (_gate) return _data.Inspections.FirstOrDefault(x => x.Id == id);
    }

    public byte[]? GetInspectionImage(Guid id)
    {
        lock (_gate)
        {
            if (_data.Inspections.FirstOrDefault(x => x.Id == id) is not { HasImage: true }) return null;
            var path = Path.Combine(_imageDirectory, id.ToString("N") + ".jpg");
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
    }

    public IssueDto AddIssue(CreateIssueRequest request, string userId)
    {
        lock (_gate)
        {
            var now = DateTimeOffset.Now;
            var issue = new IssueDto(Guid.NewGuid(), request.LineId, request.Type, request.Title.Trim(),
                request.Description.Trim(), IssueStatus.Occurred, userId, null, now, now);
            _data.Issues.Insert(0, issue);
            Save();
            return issue;
        }
    }

    public IReadOnlyList<IssueMessageDto> GetIssueMessages(Guid id, long? afterId)
    {
        lock (_gate) return _data.Messages.Where(x => x.IssueId == id && x.Id > (afterId ?? 0)).OrderBy(x => x.Id).ToList();
    }

    public IssueMessageDto AddIssueMessage(Guid id, UserDto sender, string text)
    {
        lock (_gate)
        {
            var issue = _data.Issues.FirstOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException("문제를 찾을 수 없습니다.");
            if (issue.Status == IssueStatus.Closed) throw new InvalidOperationException("종결된 문제는 대화를 조회만 할 수 있습니다.");
            var messageId = Math.Max(_data.NextMessageId, _data.Messages.Select(x => x.Id).DefaultIfEmpty(0).Max() + 1);
            var message = new IssueMessageDto(messageId, id, sender.Id, sender.Name, sender.Role, text.Trim(), DateTimeOffset.Now);
            var previousId = _data.NextMessageId;
            _data.Messages.Add(message);
            _data.NextMessageId = messageId + 1;
            try { Save(); }
            catch { _data.Messages.Remove(message); _data.NextMessageId = previousId; throw; }
            return message;
        }
    }

    public IssueDto UpdateIssue(Guid id, UpdateIssueStatusRequest request, UserRole role)
    {
        lock (_gate)
        {
            var index = _data.Issues.FindIndex(x => x.Id == id);
            if (index < 0) throw new KeyNotFoundException("문제를 찾을 수 없습니다.");
            var previous = _data.Issues[index];
            var validTransition = role == UserRole.Admin
                ? previous.Status == IssueStatus.CompletionRequested && request.Status is IssueStatus.Closed or IssueStatus.InProgress
                : (previous.Status, request.Status) is
                    (IssueStatus.Occurred, IssueStatus.Acknowledged) or
                    (IssueStatus.Acknowledged, IssueStatus.InProgress) or
                    (IssueStatus.InProgress, IssueStatus.InProgress) or
                    (IssueStatus.InProgress, IssueStatus.CompletionRequested);
            if (!validTransition) throw new InvalidOperationException("현재 상태에서는 해당 단계로 변경할 수 없습니다. 새로고침 후 확인하세요.");
            if (string.IsNullOrWhiteSpace(request.ActionNote)) throw new InvalidOperationException("확인 또는 조치 내용을 입력하세요.");
            var note = $"[{(role == UserRole.Admin ? "관리자" : "작업자")} · {request.Status} · {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}]\r\n{request.ActionNote.Trim()}";
            if (!string.IsNullOrWhiteSpace(previous.ActionNote)) note = previous.ActionNote + "\r\n\r\n" + note;
            var updated = previous with
            {
                Status = request.Status, ActionNote = note, UpdatedAt = DateTimeOffset.Now
            };
            _data.Issues[index] = updated;
            Save();
            return updated;
        }
    }

    public IssueDto? FindIssue(Guid id)
    {
        lock (_gate) return _data.Issues.FirstOrDefault(x => x.Id == id);
    }

    public IReadOnlyList<IssueDto> GetIssues(IEnumerable<int>? allowedLineIds = null)
    {
        lock (_gate)
        {
            var allowed = allowedLineIds?.ToHashSet();
            return _data.Issues.Where(x => allowed is null || allowed.Contains(x.LineId)).ToList();
        }
    }

    private StoreData LoadOrSeed()
    {
        if (File.Exists(_filePath))
        {
            var loaded = JsonSerializer.Deserialize<StoreData>(File.ReadAllText(_filePath), _json);
            if (loaded is not null) return loaded;
        }

        var now = DateTimeOffset.Now;
        var seed = new StoreData
        {
            Users =
            [
                new UserRecord { Id = "admin", Name = "관리자", Role = UserRole.Admin, PasswordHash = PasswordHasher.Hash("admin123!"), IsActive = true, AllowedLineIds = [1, 2, 3, 4] },
                new UserRecord { Id = "worker1", Name = "1라인 작업자", Role = UserRole.User, PasswordHash = PasswordHasher.Hash("worker123!"), IsActive = true, AllowedLineIds = [1] }
            ],
            Lines =
            [
                new(1, "LINE 1 · 대표 라인", true, "가동", "검사 대기", 0, 0, 0, now),
                new(2, "LINE 2 · 가상", false, "가동", "가상 데이터", 96, 2, 0, now),
                new(3, "LINE 3 · 가상", false, "공정 정지", "가상 데이터", 81, 3, 0, now),
                new(4, "LINE 4 · 가상", false, "가동", "가상 데이터", 103, 1, 2, now)
            ]
        };
        _data = seed;
        Save();
        return seed;
    }

    private void Save()
    {
        var temporary = _filePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(_data, _json));
        File.Move(temporary, _filePath, true);
    }

    public static UserDto ToDto(UserRecord user) =>
        new(user.Id, user.Name, user.Role, user.IsActive, user.AllowedLineIds);
}

public sealed class StoreData
{
    public List<IssueMessageDto> Messages { get; set; } = [];
    public long NextMessageId { get; set; } = 1;
    public List<UserRecord> Users { get; set; } = [];
    public List<LineStatusDto> Lines { get; set; } = [];
    public List<InspectionDto> Inspections { get; set; } = [];
    public List<IssueDto> Issues { get; set; } = [];
}

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
        var parts = stored.Split('.');
        if (parts.Length != 2) return false;
        var salt = Convert.FromBase64String(parts[0]);
        var expected = Convert.FromBase64String(parts[1]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
