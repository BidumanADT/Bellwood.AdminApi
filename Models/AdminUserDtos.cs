namespace Bellwood.AdminApi.Models;

public record AdminUserDto
{
    public string UserId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public bool? IsDisabled { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
    public DateTime? ModifiedAtUtc { get; init; }
    public string? CreatedByUserId { get; init; }
    public string? ModifiedByUserId { get; init; }
}

public record CreateUserRequest
{
    public string Email { get; init; } = string.Empty;
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string TempPassword { get; init; } = string.Empty;
    public List<string> Roles { get; init; } = new();
}

public record UpdateUserRolesRequest
{
    public List<string> Roles { get; init; } = new();
}

public record UpdateUserDisabledRequest
{
    public bool IsDisabled { get; init; }
}
