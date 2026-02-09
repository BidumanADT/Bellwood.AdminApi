using System.Text.Json.Serialization;

namespace Bellwood.AdminApi.Models;

public record AdminUserDto
{
    [JsonPropertyName("userId")]
    public string UserId { get; init; } = string.Empty;
    
    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;
    
    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;
    
    [JsonPropertyName("firstName")]
    public string? FirstName { get; init; }
    
    [JsonPropertyName("lastName")]
    public string? LastName { get; init; }
    
    [JsonPropertyName("roles")]
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    
    [JsonPropertyName("isDisabled")]
    public bool IsDisabled { get; init; }
    
    [JsonPropertyName("createdAtUtc")]
    public DateTime? CreatedAtUtc { get; init; }
    
    [JsonPropertyName("modifiedAtUtc")]
    public DateTime? ModifiedAtUtc { get; init; }
    
    [JsonPropertyName("createdByUserId")]
    public string? CreatedByUserId { get; init; }
    
    [JsonPropertyName("modifiedByUserId")]
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
