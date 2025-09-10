using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArchGuard.MCP.Services;

#region Interfaces

public interface IGitHubWebhookAuthenticator
{
    WebhookAuthenticationResult Authenticate(string requestBody, string? signature, string secret);
    long ExtractInstallationId(string requestBody, long? headerInstallationId);
}

#endregion

#region Implementation

public class GitHubWebhookAuthenticator : IGitHubWebhookAuthenticator
{
    private ILogger<GitHubWebhookAuthenticator> Logger { get; set; }

    public GitHubWebhookAuthenticator(ILogger<GitHubWebhookAuthenticator> logger)
    {
        this.Logger = logger;
    }

    public WebhookAuthenticationResult Authenticate(string requestBody, string? signature, string secret)
    {
        // Signature validation
        if (!string.IsNullOrEmpty(secret) && !string.IsNullOrEmpty(signature))
        {
            if (!IsValidSignature(requestBody, secret, signature))
            {
                this.Logger.LogWarning("Invalid GitHub webhook signature received");
                return WebhookAuthenticationResult.InvalidSignature();
            }
        }
        else if (!string.IsNullOrEmpty(secret))
        {
            this.Logger.LogWarning("Missing GitHub webhook signature");
            return WebhookAuthenticationResult.MissingSignature();
        }

        return WebhookAuthenticationResult.Success();
    }

    public long ExtractInstallationId(string requestBody, long? headerInstallationId)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(requestBody);
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty("installation", out JsonElement installationElement) &&
                installationElement.TryGetProperty("id", out JsonElement idElement) &&
                idElement.ValueKind == JsonValueKind.Number)
            {
                var payloadInstallationId = idElement.GetInt64();
                
                // Log mismatch but use payload ID
                if (headerInstallationId.HasValue && headerInstallationId.Value != payloadInstallationId)
                {
                    this.Logger.LogWarning("Header installation ID ({HeaderId}) differs from payload ID ({PayloadId}). Using payload ID.",
                        headerInstallationId.Value, payloadInstallationId);
                }

                return payloadInstallationId;
            }
        }
        catch (JsonException ex)
        {
            this.Logger.LogError(ex, "Failed to parse installation ID from webhook payload");
        }

        throw new InvalidOperationException("Installation ID not found in webhook payload");
    }

    private static bool IsValidSignature(string payloadBody, string secret, string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(payloadBody) || string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signatureHeader))
        {
            return false;
        }

        const string signaturePrefix = "sha256=";
        if (!signatureHeader.StartsWith(signaturePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var signature = signatureHeader.Substring(signaturePrefix.Length);
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadBody);

        using var hmac = new HMACSHA256(secretBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        var hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

        return hashString.Equals(signature, StringComparison.OrdinalIgnoreCase);
    }
}

public class WebhookAuthenticationResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int? StatusCode { get; private set; }

    private WebhookAuthenticationResult(bool isSuccess, string? errorMessage = null, int? statusCode = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        StatusCode = statusCode;
    }

    public static WebhookAuthenticationResult Success() => new(true);
    public static WebhookAuthenticationResult InvalidSignature() => new(false, "Invalid signature", 401);
    public static WebhookAuthenticationResult MissingSignature() => new(false, "Missing signature", 400);
}

#endregion
