namespace Bellwood.AdminApi.Services
{
    public sealed class EmailOptions
    {
        public string Mode { get; set; } = "Disabled";
        public EmailSmtpOptions Smtp { get; set; } = new();
        public EmailOverrideRecipientsOptions OverrideRecipients { get; set; } = new();
        public bool IncludeOriginalRecipientInSubject { get; set; } = false;

        // Convenience properties for mode checks
        public bool IsAlphaSandbox => Mode.Equals("AlphaSandbox", StringComparison.OrdinalIgnoreCase);
        public bool IsDisabled => Mode.Equals("Disabled", StringComparison.OrdinalIgnoreCase);

        // Backward-compatible accessors for existing sender behavior.
        public string Host => Smtp.Host;
        public int Port => Smtp.Port;
        public string? Username => Smtp.Username;
        public string? Password => Smtp.Password;
        public string From => Smtp.From;
        // public bool UseStartTls { get; set; } = false;
        public bool UseStartTls => Smtp.UseStartTls;

        public string To { get; set; } = "reservations+quotes@bellwoodelite.dev";
        public string SubjectPrefix { get; set; } = "[Quote]";
    }

    public sealed class EmailSmtpOptions
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 25;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public bool UseStartTls { get; set; } = false;
        // Minimum ms between sends in AlphaSandbox; tune via appsettings without a code change.
        public int ThrottleMs { get; set; } = 2000;
    }

    public sealed class EmailOverrideRecipientsOptions
    {
        public bool Enabled { get; set; } = false;
        public string Address { get; set; } = string.Empty;
    }
}
