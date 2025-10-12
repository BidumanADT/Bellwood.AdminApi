namespace Bellwood.AdminApi.Services
{
    public sealed class EmailOptions
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 25;
        public bool UseStartTls { get; set; } = false;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string From { get; set; } = "no-reply@bellwoodelite.dev";
        public string To { get; set; } = "reservations+quotes@bellwoodelite.dev";
        public string SubjectPrefix { get; set; } = "[Quote] ";
    }
}
