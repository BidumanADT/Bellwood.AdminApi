namespace Bellwood.AdminApi.Models;

public sealed class SavedLocation
{
    public Guid   Id         { get; set; } = Guid.NewGuid();
    public string UserId     { get; set; } = "";
    public string Label      { get; set; } = "";
    public string Address    { get; set; } = "";
    public double Latitude   { get; set; }
    public double Longitude  { get; set; }
    public bool   IsFavorite { get; set; }
    public int    UseCount   { get; set; }
    public DateTime CreatedUtc  { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
}
