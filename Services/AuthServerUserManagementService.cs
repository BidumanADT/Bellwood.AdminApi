using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bellwood.AdminApi.Models;
using Microsoft.AspNetCore.WebUtilities;

namespace Bellwood.AdminApi.Services;

public sealed class AuthServerUserManagementService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthServerUserManagementService> _logger;
    private readonly string _authServerBaseUrl;

    public AuthServerUserManagementService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<AuthServerUserManagementService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _authServerBaseUrl = configuration["AuthServer:Url"]?.TrimEnd('/')
                             ?? throw new InvalidOperationException("AuthServer:Url configuration is missing.");
    }

    public async Task<AuthServerListResponse<AdminUserDto>> ListUsersAsync(
        int take,
        int skip,
        string? bearerToken,
        CancellationToken ct = default)
    {
        var query = new Dictionary<string, string?>
        {
            ["take"] = take.ToString(),
            ["skip"] = skip.ToString()
        };

        var pathWithQuery = QueryHelpers.AddQueryString("/api/admin/users", query);
        using var request = CreateRequest(HttpMethod.Get, pathWithQuery, bearerToken);
        var response = await _httpClient.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return new AuthServerListResponse<AdminUserDto>(
                false,
                response.StatusCode,
                Array.Empty<AdminUserDto>(),
                null,
                ExtractErrorMessage(content));
        }

        var (users, total) = ParseUsers(content);
        var mapped = users.Select(MapToAdminUser).ToList();

        return new AuthServerListResponse<AdminUserDto>(
            true,
            response.StatusCode,
            mapped,
            total,
            null);
    }

    public async Task<AuthServerResponse<AdminUserDto>> CreateUserAsync(
        CreateUserRequest request,
        IReadOnlyList<string> normalizedRoles,
        string? bearerToken,
        CancellationToken ct = default)
    {
        var payload = new AuthServerCreateUserRequest
        {
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            TempPassword = request.TempPassword,
            Roles = normalizedRoles
        };

        using var httpRequest = CreateRequest(HttpMethod.Post, "/api/admin/users", bearerToken);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        return await SendAsync(httpRequest, MapToAdminUser, ct);
    }

    public async Task<AuthServerResponse<AdminUserDto>> UpdateRolesAsync(
        string userId,
        IReadOnlyList<string> normalizedRoles,
        string? bearerToken,
        CancellationToken ct = default)
    {
        var payload = new AuthServerUpdateRolesRequest
        {
            Roles = normalizedRoles
        };

        using var httpRequest = CreateRequest(HttpMethod.Put, $"/api/admin/users/{userId}/roles", bearerToken);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        return await SendAsync(httpRequest, MapToAdminUser, ct);
    }

    public async Task<AuthServerResponse<AdminUserDto>> DisableUserAsync(
        string userId,
        string? bearerToken,
        CancellationToken ct = default)
    {
        using var httpRequest = CreateRequest(HttpMethod.Put, $"/api/admin/users/{userId}/disable", bearerToken);
        httpRequest.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        return await SendAsync(httpRequest, MapToAdminUser, ct);
    }

    public async Task<AuthServerResponse<AdminUserDto>> EnableUserAsync(
        string userId,
        string? bearerToken,
        CancellationToken ct = default)
    {
        using var httpRequest = CreateRequest(HttpMethod.Put, $"/api/admin/users/{userId}/enable", bearerToken);
        httpRequest.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        return await SendAsync(httpRequest, MapToAdminUser, ct);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, string? bearerToken)
    {
        var request = new HttpRequestMessage(method, new Uri(new Uri(_authServerBaseUrl), path));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return request;
    }

    private async Task<AuthServerResponse<AdminUserDto>> SendAsync(
        HttpRequestMessage request,
        Func<AuthServerUserDto, AdminUserDto> mapper,
        CancellationToken ct)
    {
        var response = await _httpClient.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return new AuthServerResponse<AdminUserDto>(
                false,
                response.StatusCode,
                null,
                ExtractErrorMessage(content));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return new AuthServerResponse<AdminUserDto>(
                false,
                HttpStatusCode.InternalServerError,
                null,
                "AuthServer returned an empty response.");
        }

        try
        {
            var user = JsonSerializer.Deserialize<AuthServerUserDto>(content, JsonOptions);
            if (user is null)
            {
                return new AuthServerResponse<AdminUserDto>(
                    false,
                    HttpStatusCode.InternalServerError,
                    null,
                    "Unable to parse AuthServer response.");
            }

            return new AuthServerResponse<AdminUserDto>(
                true,
                response.StatusCode,
                mapper(user),
                null);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse AuthServer response: {Response}", content);
            return new AuthServerResponse<AdminUserDto>(
                false,
                HttpStatusCode.InternalServerError,
                null,
                "Unable to parse AuthServer response.");
        }
    }

    private static (List<AuthServerUserDto> Users, int? TotalCount) ParseUsers(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return (new List<AuthServerUserDto>(), null);
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                var list = JsonSerializer.Deserialize<List<AuthServerUserDto>>(root.GetRawText(), JsonOptions)
                           ?? new List<AuthServerUserDto>();
                return (list, null);
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("users", out var usersElement))
                {
                    var list = JsonSerializer.Deserialize<List<AuthServerUserDto>>(usersElement.GetRawText(), JsonOptions)
                               ?? new List<AuthServerUserDto>();
                    return (list, ExtractTotal(root));
                }

                if (root.TryGetProperty("items", out var itemsElement))
                {
                    var list = JsonSerializer.Deserialize<List<AuthServerUserDto>>(itemsElement.GetRawText(), JsonOptions)
                               ?? new List<AuthServerUserDto>();
                    return (list, ExtractTotal(root));
                }
            }
        }
        catch (JsonException)
        {
            return (new List<AuthServerUserDto>(), null);
        }

        return (new List<AuthServerUserDto>(), null);
    }

    private static int? ExtractTotal(JsonElement root)
    {
        if (root.TryGetProperty("total", out var totalElement) && totalElement.TryGetInt32(out var total))
        {
            return total;
        }

        if (root.TryGetProperty("pagination", out var pagination) &&
            pagination.TryGetProperty("total", out var paginationTotal) &&
            paginationTotal.TryGetInt32(out var pagedTotal))
        {
            return pagedTotal;
        }

        return null;
    }

    private static string? ExtractErrorMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("error", out var errorElement))
                {
                    return errorElement.GetString();
                }

                if (root.TryGetProperty("message", out var messageElement))
                {
                    return messageElement.GetString();
                }

                if (root.TryGetProperty("detail", out var detailElement))
                {
                    return detailElement.GetString();
                }
            }
        }
        catch (JsonException)
        {
            return content;
        }

        return content;
    }

    private static AdminUserDto MapToAdminUser(AuthServerUserDto user)
    {
        var userId = user.UserId ?? user.Id ?? string.Empty;
        var email = user.Email ?? user.Username ?? string.Empty;

        return new AdminUserDto
        {
            UserId = userId,
            Email = email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Roles = AdminUserRoleValidator.ToDisplayRoles(user.Roles),
            IsDisabled = user.IsDisabled,
            CreatedAtUtc = user.CreatedAtUtc,
            ModifiedAtUtc = user.ModifiedAtUtc,
            CreatedByUserId = user.CreatedByUserId,
            ModifiedByUserId = user.ModifiedByUserId
        };
    }

    private sealed record AuthServerCreateUserRequest
    {
        public string Email { get; init; } = string.Empty;
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public string TempPassword { get; init; } = string.Empty;
        public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    }

    private sealed record AuthServerUpdateRolesRequest
    {
        public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    }

    private sealed record AuthServerUserDto
    {
        [JsonPropertyName("userId")]
        public string? UserId { get; init; }

        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("email")]
        public string? Email { get; init; }

        [JsonPropertyName("username")]
        public string? Username { get; init; }

        [JsonPropertyName("firstName")]
        public string? FirstName { get; init; }

        [JsonPropertyName("lastName")]
        public string? LastName { get; init; }

        [JsonPropertyName("roles")]
        public List<string>? Roles { get; init; }

        [JsonPropertyName("isDisabled")]
        public bool? IsDisabled { get; init; }

        [JsonPropertyName("createdAtUtc")]
        public DateTime? CreatedAtUtc { get; init; }

        [JsonPropertyName("modifiedAtUtc")]
        public DateTime? ModifiedAtUtc { get; init; }

        [JsonPropertyName("createdByUserId")]
        public string? CreatedByUserId { get; init; }

        [JsonPropertyName("modifiedByUserId")]
        public string? ModifiedByUserId { get; init; }
    }
}

public record AuthServerResponse<T>(
    bool Success,
    HttpStatusCode StatusCode,
    T? Data,
    string? ErrorMessage);

public record AuthServerListResponse<T>(
    bool Success,
    HttpStatusCode StatusCode,
    IReadOnlyList<T> Items,
    int? Total,
    string? ErrorMessage);
