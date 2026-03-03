namespace Bellwood.AdminApi.Models;

public sealed record SavedLocationRequest(
    string Label,
    string Address,
    double Latitude,
    double Longitude,
    bool   IsFavorite
);
