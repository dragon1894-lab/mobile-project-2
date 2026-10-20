using System.Collections.Concurrent;
using ProductionLine.Shared;

namespace ProductionLine.Server.Infrastructure;

public sealed class AuthService(AppStore store)
{
    private readonly ConcurrentDictionary<string, AuthSession> _sessions = new();

    public string Create(UserRecord user)
    {
        var token = Convert.ToHexString(Guid.NewGuid().ToByteArray());
        _sessions[token] = new AuthSession(user.Id, user.Role, user.AllowedLineIds, DateTimeOffset.Now.AddHours(8));
        return token;
    }

    public AuthSession? Get(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        var token = header[7..].Trim();
        if (!_sessions.TryGetValue(token, out var session) || session.ExpiresAt <= DateTimeOffset.Now) return null;
        var user = store.GetUsers().FirstOrDefault(value => value.Id.Equals(session.UserId, StringComparison.OrdinalIgnoreCase));
        if (user is not { IsActive: true })
        {
            _sessions.TryRemove(token, out _);
            return null;
        }
        return session with { Role = user.Role, AllowedLineIds = user.AllowedLineIds };
    }

    public void InvalidateUser(string userId)
    {
        foreach (var pair in _sessions.Where(value => value.Value.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase)))
            _sessions.TryRemove(pair.Key, out _);
    }
}

public sealed record AuthSession(string UserId, UserRole Role, IReadOnlyList<int> AllowedLineIds, DateTimeOffset ExpiresAt)
{
    public bool CanAccessLine(int lineId) => Role == UserRole.Admin || AllowedLineIds.Contains(lineId);
}
