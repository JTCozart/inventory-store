using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InventoryStore.Application.Interfaces.Services;

namespace InventoryStore.App.Email;

// Credentials that override the saved settings for a single send -- used by the admin
// "send test email" button so an operator can validate keys before saving them.
public sealed record EmailCredentials(string ApiKey, string SecretKey, string FromAddress, string? FromName);

// Sends transactional email through Mailjet's Send API v3.1 using the operator's own
// API key + secret (bring-your-own-key). Credentials and the sender identity are read from
// ISettingsService -- same "module.<key>" flat-value convention the AI module uses
// (see ChatModel.cs), no separate secret store.
public sealed class EmailSender(IHttpClientFactory httpFactory, ISettingsService settings, ILogger<EmailSender> logger)
    : IEmailSender
{
    public const string HttpClientName = "mailjet";
    private const string SendUrl = "https://api.mailjet.com/v3.1/send";

    public const string ApiKeySettingKey = "module.email.mailjetApiKey";
    public const string SecretKeySettingKey = "module.email.mailjetSecretKey";
    public const string FromAddressSettingKey = "module.email.fromAddress";
    public const string FromNameSettingKey = "module.email.fromName";

    public async Task<bool> IsConfiguredAsync()
    {
        var apiKey = await settings.GetAsync(ApiKeySettingKey);
        var secretKey = await settings.GetAsync(SecretKeySettingKey);
        var fromAddress = await settings.GetAsync(FromAddressSettingKey);
        return !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(secretKey)
            && !string.IsNullOrWhiteSpace(fromAddress);
    }

    public Task<EmailResult> SendAsync(
        string toEmail, string subject, string htmlBody, string? textBody = null, CancellationToken ct = default) =>
        SendAsync(toEmail, subject, htmlBody, overrides: null, textBody, ct);

    // overrides lets the admin "send test" UI supply credentials that haven't been saved yet.
    public async Task<EmailResult> SendAsync(
        string toEmail, string subject, string htmlBody,
        EmailCredentials? overrides, string? textBody = null, CancellationToken ct = default)
    {
        var apiKey = overrides?.ApiKey ?? await settings.GetAsync(ApiKeySettingKey);
        var secretKey = overrides?.SecretKey ?? await settings.GetAsync(SecretKeySettingKey);
        var fromEmail = overrides?.FromAddress ?? await settings.GetAsync(FromAddressSettingKey);
        var fromName = overrides?.FromName ?? await settings.GetAsync(FromNameSettingKey);

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(fromEmail))
            return EmailResult.Fail("Email is not configured. Set the Mailjet API key, secret key, and sender address.");

        var payload = new
        {
            Messages = new[]
            {
                new
                {
                    From = new { Email = fromEmail, Name = string.IsNullOrWhiteSpace(fromName) ? fromEmail : fromName },
                    To = new[] { new { Email = toEmail } },
                    Subject = subject,
                    TextPart = string.IsNullOrWhiteSpace(textBody) ? StripHtml(htmlBody) : textBody,
                    HTMLPart = htmlBody,
                },
            },
        };

        try
        {
            using var client = httpFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, SendUrl);
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:{secretKey}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
                return EmailResult.Ok;

            var body = await response.Content.ReadAsStringAsync(ct);
            // Error, not Warning: a rejected send means the recipient never got the email
            // (e.g. a password-reset link), which is worth surfacing to the Windows Event Log.
            logger.LogError("Mailjet send failed ({Status}): {Body}", (int)response.StatusCode, body);
            var status = (int)response.StatusCode;
            var hint = status == 401 ? "Check the API key and secret key." : ExtractError(body);
            return EmailResult.Fail($"Mailjet rejected the request (HTTP {status}). {hint}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Mailjet send threw for {To}", toEmail);
            return EmailResult.Fail($"Could not reach Mailjet: {ex.Message}");
        }
    }

    // Pull a human-readable message out of Mailjet's error JSON, best-effort. Mailjet returns
    // request-level errors at the root and per-message errors under Messages[].Errors[].
    private static string ExtractError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("ErrorMessage", out var rootMsg))
                return rootMsg.GetString() ?? "";

            if (root.TryGetProperty("Messages", out var msgs) && msgs.ValueKind == JsonValueKind.Array
                && msgs.GetArrayLength() > 0
                && msgs[0].TryGetProperty("Errors", out var errs) && errs.ValueKind == JsonValueKind.Array
                && errs.GetArrayLength() > 0)
            {
                var err = errs[0];
                var message = err.TryGetProperty("ErrorMessage", out var em) ? em.GetString() : null;
                var relatedTo = err.TryGetProperty("ErrorRelatedTo", out var rel) && rel.ValueKind == JsonValueKind.Array
                    && rel.GetArrayLength() > 0 ? rel[0].GetString() : null;
                if (!string.IsNullOrEmpty(message))
                    return relatedTo is null ? message : $"{message} (field: {relatedTo})";
            }
        }
        catch { /* not JSON -- fall through */ }
        return "Check the API key, secret key, and that the sender address is a verified Mailjet sender.";
    }

    // Crude HTML-to-text fallback for the plain-text alternative part.
    private static string StripHtml(string html)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
    }
}
