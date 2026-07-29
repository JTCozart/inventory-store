namespace InventoryStore.Application.Interfaces.Services;

// Outcome of an email send attempt.
public sealed record EmailResult(bool Succeeded, string? Error)
{
    public static EmailResult Ok { get; } = new(true, null);
    public static EmailResult Fail(string error) => new(false, error);
}

public interface IEmailSender
{
    // True when the operator has configured outbound email (Mailjet key, secret, sender address).
    Task<bool> IsConfiguredAsync();

    Task<EmailResult> SendAsync(
        string toEmail, string subject, string htmlBody,
        string? textBody = null, CancellationToken ct = default);
}
