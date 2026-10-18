using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ProductionLine.Server.Data;
using ProductionLine.Shared;

namespace ProductionLine.Server.Infrastructure;

public sealed class AuthService
{
    private readonly ConcurrentDictionary<string, AuthSession> _sessions = new();
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public AuthService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public string Create(UserRecord user)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _sessions[token] = new AuthSession(
            user.Id, user.Role, user.AllowedLineIds.ToArray(), DateTimeOffset.UtcNow.AddHours(8));
        return token;
    }

    public AuthSession? Get(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        var token = header[7..].Trim();
        if (!_sessions.TryGetValue(token, out var session)) return null;

        if (session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _sessions.TryRemove(token, out _);
            return null;
        }

        // 로그인 당시의 권한 대신 요청 시점의 DB 정보를 확인합니다.
        using var db = _contextFactory.CreateDbContext();
        var user = db.Users.AsNoTracking().SingleOrDefault(x => x.Id == session.UserId);
        if (user is null || !user.IsActive || !Enum.IsDefined(user.Role))
        {
            _sessions.TryRemove(token, out _);
            return null;
        }

        var lineIds = db.UserLineAccesses.AsNoTracking()
            .Where(x => x.UserId == user.Id)
            .OrderBy(x => x.LineId)
            .Select(x => x.LineId)
            .ToArray();

        // 통신 기록에 요청한 사용자를 남깁니다.
        context.Items[RequestLogItems.UserId] = user.Id;

        // 요청마다 만료 시간을 연장하지 않고 최초 로그인 만료를 유지합니다.
        return new AuthSession(user.Id, user.Role, lineIds, session.ExpiresAt);
    }
}

public sealed record AuthSession(
    string UserId, UserRole Role, IReadOnlyList<int> AllowedLineIds, DateTimeOffset ExpiresAt)
{
    public bool CanAccessLine(int lineId) => Role == UserRole.Admin || AllowedLineIds.Contains(lineId);
}
