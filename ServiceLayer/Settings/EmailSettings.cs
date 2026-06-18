namespace ServiceLayer.Settings;

public class EmailSettings
{
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "ChatBot PRN222";
    // Gmail App Password (16 chars), NOT the normal account password.
    public string AppPassword { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(FromEmail) && !string.IsNullOrWhiteSpace(AppPassword);
}
