using System.Security.Claims;

namespace Bellwood.AdminApi.Services;

/// <summary>
/// Helper methods for user authorization and ownership checks.
/// Phase 1 of User Data Access Enforcement.
/// </summary>
public static class UserAuthorizationHelper
{
    // =====================================================================
    // CLAIM EXTRACTION
    // =====================================================================

    /// <summary>
    /// Get the user's unique ID from the JWT claims for ownership tracking.
    /// Phase 1: Prefers 'userId' claim (always Identity GUID from AuthServer).
    /// Falls back to 'uid' claim, then 'sub' (username) if neither exists.
    /// </summary>
    /// <param name="user">The ClaimsPrincipal from HttpContext.User</param>
    /// <returns>The user's ID, or null if no identifying claims exist</returns>
    public static string? GetUserId(ClaimsPrincipal user)
    {
        // Phase 1: Prefer userId claim (always Identity GUID for audit tracking)
        // AuthServer sets this explicitly for all users (not overridden by custom uid)
        var userId = user.FindFirst("userId")?.Value;
        if (!string.IsNullOrEmpty(userId))
            return userId;
        
        // Fallback 1: Use the uid claim (default is Identity GUID, drivers have custom)
        var uid = user.FindFirst("uid")?.Value;
        if (!string.IsNullOrEmpty(uid))
            return uid;

        // Fallback 2: Use sub claim (username) if neither userId nor uid exists
        // Log warning since this indicates a potential AuthServer issue
        var sub = user.FindFirst("sub")?.Value ?? user.Identity?.Name;
        if (!string.IsNullOrEmpty(sub))
        {
            Console.WriteLine($"?? Warning: User '{sub}' missing 'userId' and 'uid' claims, falling back to 'sub'");
        }
        
        return sub;
    }

    /// <summary>
    /// Get the user's role from JWT claims.
    /// </summary>
    public static string? GetUserRole(ClaimsPrincipal user)
    {
        return user.FindFirst("role")?.Value;
    }

    /// <summary>
    /// Get the user's email from JWT claims.
    /// </summary>
    public static string? GetUserEmail(ClaimsPrincipal user)
    {
        return user.FindFirst("email")?.Value;
    }

    // =====================================================================
    // ROLE CHECKS
    // =====================================================================

    /// <summary>
    /// Check if the user has admin or dispatcher role (staff access).
    /// In Phase 1, this treats admin as staff. Phase 2 will add dispatcher.
    /// </summary>
    public static bool IsStaffOrAdmin(ClaimsPrincipal user)
    {
        var role = GetUserRole(user);
        return role == "admin" || role == "dispatcher";
    }

    /// <summary>
    /// Check if the user is an admin.
    /// </summary>
    public static bool IsAdmin(ClaimsPrincipal user)
    {
        return GetUserRole(user) == "admin";
    }

    /// <summary>
    /// Check if the user is a driver.
    /// </summary>
    public static bool IsDriver(ClaimsPrincipal user)
    {
        return GetUserRole(user) == "driver";
    }

    /// <summary>
    /// Check if the user is a booker (passenger/concierge).
    /// </summary>
    public static bool IsBooker(ClaimsPrincipal user)
    {
        return GetUserRole(user) == "booker";
    }

    // =====================================================================
    // Phase 2: DISPATCHER ROLE & FIELD MASKING
    // =====================================================================

    /// <summary>
    /// Check if the user is a dispatcher (operational staff with limited access).
    /// Dispatchers can see all operational data but NOT billing information.
    /// </summary>
    public static bool IsDispatcher(ClaimsPrincipal user)
    {
        return GetUserRole(user) == "dispatcher";
    }

    /// <summary>
    /// Mask billing/sensitive fields in a DTO for dispatchers.
    /// Admins see full data; dispatchers see operational data only.
    /// Phase 2: Prepares for future payment integration.
    /// </summary>
    /// <param name="user">The authenticated user</param>
    /// <param name="dto">DTO object with billing properties</param>
    public static void MaskBillingFields(ClaimsPrincipal user, object dto)
    {
        // Only mask for dispatchers (admins see everything)
        if (!IsDispatcher(user)) return;
        
        // Use reflection to null out billing-related properties
        var type = dto.GetType();
        
        var billingProps = new[]
        {
            "PaymentMethodId",
            "PaymentMethodLast4", 
            "CardLast4",
            "PaymentAmount",
            "TotalAmount",
            "TotalFare",
            "EstimatedCost",
            "BillingNotes"
        };
        
        foreach (var propName in billingProps)
        {
            var prop = type.GetProperty(propName);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(dto, null);
            }
        }
    }

    // =====================================================================
    // OWNERSHIP CHECKS
    // =====================================================================

    /// <summary>
    /// Check if the user owns a record (created it).
    /// Admins/dispatchers have access to all records.
    /// Drivers have no ownership-based access (use AssignedDriverUid instead).
    /// Bookers can only access records they created.
    /// </summary>
    /// <param name="user">The authenticated user</param>
    /// <param name="createdByUserId">The CreatedByUserId from the record</param>
    /// <returns>True if user can access this record</returns>
    public static bool CanAccessRecord(ClaimsPrincipal user, string? createdByUserId)
    {
        // Staff (admin/dispatcher) can access all records
        if (IsStaffOrAdmin(user))
            return true;

        // Legacy records with no owner: only staff can access
        if (string.IsNullOrEmpty(createdByUserId))
            return false;

        // For bookers: check if they created the record
        var currentUserId = GetUserId(user);
        if (string.IsNullOrEmpty(currentUserId))
            return false;

        return createdByUserId == currentUserId;
    }

    /// <summary>
    /// Check if a driver can access a booking (based on assignment).
    /// </summary>
    /// <param name="user">The authenticated user</param>
    /// <param name="assignedDriverUid">The AssignedDriverUid from the booking</param>
    /// <returns>True if the driver is assigned to this booking</returns>
    public static bool CanDriverAccessBooking(ClaimsPrincipal user, string? assignedDriverUid)
    {
        if (!IsDriver(user))
            return false;

        var driverUid = user.FindFirst("uid")?.Value;
        if (string.IsNullOrEmpty(driverUid) || string.IsNullOrEmpty(assignedDriverUid))
            return false;

        return driverUid == assignedDriverUid;
    }

    /// <summary>
    /// Comprehensive check for booking access.
    /// - Admins/dispatchers: full access
    /// - Drivers: access if assigned to booking
    /// - Bookers: access if they created the booking
    /// </summary>
    public static bool CanAccessBooking(
        ClaimsPrincipal user, 
        string? createdByUserId, 
        string? assignedDriverUid)
    {
        // Staff has full access
        if (IsStaffOrAdmin(user))
            return true;

        // Drivers: check assignment
        if (IsDriver(user))
            return CanDriverAccessBooking(user, assignedDriverUid);

        // Bookers: check ownership
        return CanAccessRecord(user, createdByUserId);
    }
}
