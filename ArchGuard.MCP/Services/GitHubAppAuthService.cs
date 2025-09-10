using Microsoft.IdentityModel.Tokens;
using Octokit;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace ArchGuard.MCP.Services;

public class GitHubAppAuthService
{
    private string AppId { get; set; }
    private string PrivateKey { get; set; }
    private ILogger<GitHubAppAuthService> Logger { get; set; }

    // In a production app, cache tokens to avoid regenerating for every webhook
    private static Dictionary<long, (string Token, DateTime ExpiresAt)> InstallationTokenCache { get; set; } = new();
    private static SemaphoreSlim Semaphore { get; } = new SemaphoreSlim(1, 1); // For thread-safe caching

    public GitHubAppAuthService(IConfiguration configuration, ILogger<GitHubAppAuthService> logger)
    {
        this.AppId = configuration["GitHub:AppId"] ?? throw new ArgumentNullException("GitHub:AppId not configured.");

        // Read the path to the PEM file from configuration
        var privateKeyFilePath = configuration["GitHub:PrivateKeyFilePath"] ?? throw new ArgumentNullException("GitHub:PrivateKeyFilePath not configured.");

        if (!File.Exists(privateKeyFilePath))
        {
            throw new FileNotFoundException($"GitHub App Private Key file not found at: {privateKeyFilePath}");
        }

        this.PrivateKey = File.ReadAllText(privateKeyFilePath);

        this.Logger = logger;
    }

    private string GenerateJwt_original()
    {
        var now = DateTimeOffset.UtcNow;
        var handler = new JwtSecurityTokenHandler();

        // code to verify that pem file is being read correctly, can likely remove if not needed:
        try
        {
            // The this.PrivateKey string MUST contain the full PEM content,
            // including "-----BEGIN RSA PRIVATE KEY-----" and "-----END RSA PRIVATE KEY-----"
            // and all the newline characters in between.

            // Validate that the string looks like a PEM file before attempting to import
            if (string.IsNullOrWhiteSpace(this.PrivateKey) ||
                !this.PrivateKey.Contains("-----BEGIN RSA PRIVATE KEY-----") ||
                !this.PrivateKey.Contains("-----END RSA PRIVATE KEY-----"))
            {
                this.Logger.LogError("GitHub App private key is not in expected PEM format. It must include BEGIN/END headers and correct line breaks.");
                throw new InvalidOperationException("GitHub App private key is malformed or missing. Check configuration source (appsettings.json, environment variable, or file) for correct format.");
            }

            //rsa.ImportFromPem(this.PrivateKey);
            //securityKey = new RsaSecurityKey(rsa);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to import RSA private key from PEM string. Double-check that the private key content is accurate and complete, including all line breaks and PEM headers/footers.");
            throw new InvalidOperationException("Error importing RSA private key for JWT generation. See inner exception for details.", ex);
        }



        var privateKeyBytes = Encoding.UTF8.GetBytes(this.PrivateKey);
        var securityKey = new Microsoft.IdentityModel.Tokens.RsaSecurityKey(new System.Security.Cryptography.RSAParameters
        {
            //Modulus = Base64UrlEncoder.DecodeBytes(privateKeyBytes.Skip(26).Take(384).ToArray()), // Extract modulus (simplified)
            //Exponent = Base64UrlEncoder.DecodeBytes("AQAB") // Common exponent
        });
        // NOTE: A more robust way to load the private key is using a library like RsaKeyUtils.
        // For PEM files, this requires System.Security.Cryptography.Pkcs or similar parsing.
        // For now, this is a simplified example assuming a specific PEM format.
        // A production-ready solution would load it via System.Security.Cryptography.RSA.CreateFromPem(privateKey);
        // Replace the RsaSecurityKey creation with this more robust approach:
        using (var rsa = System.Security.Cryptography.RSA.Create())
        {
            rsa.ImportFromPem(this.PrivateKey); // this.PrivateKey should contain the full PEM content string
            securityKey = new RsaSecurityKey(rsa);
        }
        // Remove the separate Exponent line, it's handled by ImportFromPem

        var descriptor = new SecurityTokenDescriptor
        {
            Audience = "https://github.com/login/oauth/access_token",
            Issuer = this.AppId,
            IssuedAt = now.UtcDateTime,
            Expires = now.AddMinutes(9).UtcDateTime, // JWT valid for 10 minutes, good practice to make it slightly less
            SigningCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256)
        };

        var token = handler.CreateToken(descriptor);
        return handler.WriteToken(token);
    }

    private string GenerateJwt()
    {
        var now = DateTimeOffset.UtcNow;
        var handler = new JwtSecurityTokenHandler();

        RsaSecurityKey securityKey;
        // --- REMOVE THE 'using' STATEMENT HERE ---
        var rsa = System.Security.Cryptography.RSA.Create(); // No 'using'

        try
        {
            // Validate that the string looks like a PEM file before attempting to import
            if (string.IsNullOrWhiteSpace(this.PrivateKey) ||
                !this.PrivateKey.Contains("-----BEGIN RSA PRIVATE KEY-----") ||
                !this.PrivateKey.Contains("-----END RSA PRIVATE KEY-----"))
            {
                this.Logger.LogError("GitHub App private key is not in expected PEM format (missing BEGIN/END RSA PRIVATE KEY headers or empty). Check configuration source (appsettings.json, environment variable, or file) for correct format.");
                throw new InvalidOperationException("GitHub App private key is malformed or missing expected PEM headers.");
            }

            // Attempt to import the key
            try
            {
                rsa.ImportFromPem(this.PrivateKey);
            }
            catch (Exception importEx)
            {
                this.Logger.LogError(importEx, "Failed to import RSA private key from PEM string. This typically means the key content is corrupted, not a valid RSA key, or in an unsupported PEM format. Ensure the PEM file downloaded from GitHub is intact.");
                throw new InvalidOperationException("Error importing RSA private key. The PEM content might be invalid.", importEx);
            }

            // --- CRITICAL CHECK: Verify RSA parameters after import ---
            var rsaParams = rsa.ExportParameters(true); // true = include private parameters
            if (rsaParams.Modulus is null)
            {
                this.Logger.LogError("After ImportFromPem, the RSA key's Modulus is null. This indicates a failure to load a valid private key from the provided PEM string, despite no direct exception from ImportFromPem itself. The key content is likely invalid or not a supported RSA private key.");
                throw new InvalidOperationException("RSA key parameters (Modulus) are null after importing PEM. The private key content is likely invalid.");
            }

            securityKey = new RsaSecurityKey(rsa); // This line is now safe, as `rsa` won't be disposed yet
        }
        catch (Exception ex)
        {
            // Catch any errors during RSA key creation/import and dispose the RSA object if an error occurs.
            // If the RsaSecurityKey was successfully created, it will manage the RSA object.
            rsa.Dispose(); // Manually dispose on failure
            this.Logger.LogError(ex, "An error occurred during GitHub App JWT generation, likely due to a malformed or invalid private key.");
            throw;
        }


        var descriptor = new SecurityTokenDescriptor
        {
            Audience = "https://github.com/login/oauth/access_token",
            Issuer = this.AppId,
            IssuedAt = now.UtcDateTime,
            Expires = now.AddMinutes(9).UtcDateTime,
            SigningCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256)
        };

        var token = handler.CreateToken(descriptor);
        return handler.WriteToken(token);
    }


    public async Task<string> GetInstallationTokenAsync(long installationId)
    {
        await GitHubAppAuthService.Semaphore.WaitAsync();
        try
        {
            if (GitHubAppAuthService.InstallationTokenCache.TryGetValue(installationId, out var cachedToken) && cachedToken.ExpiresAt > DateTime.UtcNow.AddMinutes(5)) // Refresh before expiry
            {
                this.Logger.LogInformation("Using cached GitHub App installation token for ID: {InstallationId}", installationId);
                return cachedToken.Token;
            }

            this.Logger.LogInformation("Generating new GitHub App installation token for ID: {InstallationId}", installationId);

            var jwt = GenerateJwt();
            var appClient = new GitHubClient(new ProductHeaderValue("RulesCheckGihubApp"))
            {
                Credentials = new Credentials(jwt, AuthenticationType.Bearer)
            };

            var tokenResponse = await appClient.GitHubApps.CreateInstallationToken(installationId);
            GitHubAppAuthService.InstallationTokenCache[installationId] = (tokenResponse.Token, tokenResponse.ExpiresAt.UtcDateTime);

            return tokenResponse.Token;
        }
        finally
        {
            GitHubAppAuthService.Semaphore.Release();
        }
    }
}