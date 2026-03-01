namespace Bellwood.AdminApi.Models;

public sealed record BookerProfileUpsertRequest(
    string? FirstName,
    string? LastName,
    string? EmailAddress,
    string? PhoneNumber);
