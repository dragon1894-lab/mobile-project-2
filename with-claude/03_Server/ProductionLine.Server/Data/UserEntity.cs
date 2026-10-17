using ProductionLine.Shared;

namespace ProductionLine.Server.Data;

public sealed class UserEntity
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public UserRole Role { get; set; }
    public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; }
}