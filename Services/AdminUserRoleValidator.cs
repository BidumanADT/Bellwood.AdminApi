using System.Collections.Immutable;

namespace Bellwood.AdminApi.Services;

public static class AdminUserRoleValidator
{
    private static readonly IReadOnlyDictionary<string, string> RoleToNormalized =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Booker"] = "booker",      // FIXED: Changed from "Passenger" to "Booker"
            ["Driver"] = "driver",
            ["Dispatcher"] = "dispatcher",
            ["Admin"] = "admin"
        };

    private static readonly IReadOnlyDictionary<string, string> NormalizedToDisplay =
        RoleToNormalized.ToDictionary(
            pair => pair.Value,
            pair => pair.Key,
            StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> AllowedDisplayRoles => RoleToNormalized.Keys.ToImmutableArray();

    public static bool TryNormalizeRoles(
        IEnumerable<string>? roles,
        out List<string> normalizedRoles,
        out string? errorMessage)
    {
        normalizedRoles = new List<string>();
        errorMessage = null;

        if (roles is null)
        {
            errorMessage = "Roles are required.";
            return false;
        }

        foreach (var role in roles)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                errorMessage = "Role names cannot be empty.";
                return false;
            }

            if (!RoleToNormalized.TryGetValue(role.Trim(), out var normalized))
            {
                errorMessage = $"Invalid role '{role}'. Allowed roles: {string.Join(", ", AllowedDisplayRoles)}.";
                return false;
            }

            normalizedRoles.Add(normalized);
        }

        normalizedRoles = normalizedRoles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedRoles.Count == 0)
        {
            errorMessage = "At least one role is required.";
            return false;
        }

        return true;
    }

    public static IReadOnlyList<string> ToDisplayRoles(IEnumerable<string>? roles)
    {
        if (roles is null)
        {
            return Array.Empty<string>();
        }

        return roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role =>
            {
                if (NormalizedToDisplay.TryGetValue(role.Trim(), out var display))
                {
                    return display;
                }

                return role.Trim();
            })
            .ToList();
    }
}
