using ArchGuard.MCP.Services;
using ArchGuard.MCP.Services.WebhookHandlers;
using ArchGuard_MCP.Tools;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ModelContextProtocol.TestOAuthServer;
using Octokit;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArchGuard_MCP;


// This code (program.cs only) was mostly copied from:
// https://github.com/jzuras/OAuth-Protected-MCP-Server

// Changes made:
//  Enhanced OAuth/OIDC Compliance(Audience vs. Resource): The new code adds support for the audience
//      parameter, which is the more standard way to specify the intended recipient of a token in modern OAuth
//      2.1 and OpenID Connect flows.
//  Stateless Mode was turned off so that the MCP Server can query the Client.
//  Other changes made were specific to the use of my Arch Guard tool (including a namespace/solution/project name change).
//  AI was allowed to refactor the code, resulting in many more files, classes, interfaces.
//  The GitHub App code is entirely new to this project.


public class Program
{
    static async Task Main(string[] args)
    {
        // The public-facing root URL provided by ngrok. (Changes every time ngrok starts, so replace as needed.)
        var publicBaseUrl = "https://4369b859716f.ngrok-free.app/";

        // The issuer is the entity that creates the tokens (our OAuth server at the root).
        var oAuthIssuerUrl = publicBaseUrl;

        // The audience is the public identifier of the resource that consumes the tokens (our MCP server).
        var mcpResourceUrl = $"{publicBaseUrl}mcp/";

        // The local URL Kestrel will listen on. This is what ngrok forwards traffic TO.
        var localListenUrl = "http://localhost:7071/";


        // Parse command line arguments for optional data directory path
        string? dataDirectoryPath = null;
        if (args.Length > 0)
        {
            dataDirectoryPath = args[0];
            Console.WriteLine($"[MCP] Using data directory from command line: {dataDirectoryPath}");
        }

        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            // The authority is the public location of our OAuth server.
            options.Authority = oAuthIssuerUrl;

            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                // The audience MUST be the public URL of the MCP resource.
                ValidAudience = mcpResourceUrl,
                // The issuer MUST be the public URL of the OAuth server.
                ValidIssuer = oAuthIssuerUrl,

                NameClaimType = "name",
                RoleClaimType = "roles"
            };

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var name = context.Principal?.Identity?.Name ?? "unknown";
                    var email = context.Principal?.FindFirstValue("preferred_username") ?? "unknown";
                    Console.WriteLine($"Token validated for: {name} ({email})");
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                    Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    Console.WriteLine($"Challenging client to authenticate with Entra ID");
                    return Task.CompletedTask;
                }
            };
        })
        .AddMcp(options =>
        {
            options.ResourceMetadata = new()
            {
                // The public URL of the MCP resource.
                Resource = new Uri(mcpResourceUrl),
                // The public URL of the server that can authorize access.
                AuthorizationServers = { new Uri(oAuthIssuerUrl) },

                ScopesSupported = ["mcp:tools"],
            };
        });

        builder.Services.AddAuthorization();

        #region Input Schema
        
        // This is an example of setting up an input schema. The intention is to provide more context to the
        // calling AI Agent client. However, even the explicit steps listed for "diffs" can be ignored by
        // GitHub CoPilot in VS 2022. A more robust solution would be to expllicitly tell the agent/client
        // to always gather the diffs and send them to the tool as requested (or similar wording).

        var inputSchema = JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "contextFiles": {
                        "type": "array",
                        "description": "Array of file paths for analysis",
                        "items": {
                            "type": "object",
                            "properties": {
                                "filePath": { "type": "string" }
                            },
                            "required": ["filePath"]
                        }
                    },
                    "diffs": {
                        "type": "array",
                        "description": "IMPORTANT! Do NOT assume that there are no diffs! Always run these steps: Step 1: Run git --no - pager diff HEAD --unified = 3 to get recent changes. Step 2: Pass the output of the git diff command as the 'diffs' parameter to the Validate Dependency Registration tool. Step 3: If the output is empty, pass an empty array([]).",
                        "items": { "type": "string" }
                    }
                },
                "required": ["contextFiles", "diffs"]
            }
        """);

        var dependencyRegTool = McpServerTool.Create(typeof(ArchValidationTool).GetMethod(nameof(ArchValidationTool.ValidateDependencyRegistrationAsync))!);
        dependencyRegTool.ProtocolTool.Name = nameof(ArchValidationTool.ValidateDependencyRegistrationAsync);
        dependencyRegTool.ProtocolTool.InputSchema = inputSchema;
        #endregion

        McpServerOptions mcpServerOptions = new()
        {
            ServerInfo = new Implementation
            {
                Name = "Arch Guard MCP Server",
                Version = "1.0.0" // Note - Claude Code does not see the version number - unsure why.
            },
            // Server description for MCP client routing
            ServerInstructions = "Performs various source code validations to guard against architecture guideline violations.",
            Capabilities = new ServerCapabilities
            {
                Tools = new ToolsCapability
                {
                    ToolCollection = new McpServerPrimitiveCollection<McpServerTool>
                    {
                        dependencyRegTool
                        // Another way to define a tool, if an input schema is not needed:
                        //McpServerTool.Create(typeof(ArchValidationTool).GetMethod(nameof(ArchValidationTool.ValidateDependencyRegistrationAsync))!),

                    }
                }
            }
        };

        builder.Services.AddMcpServer(options =>
        {
            options.ServerInfo = mcpServerOptions.ServerInfo;
            options.ServerInstructions = mcpServerOptions.ServerInstructions;
            options.Capabilities = mcpServerOptions.Capabilities; // This is the crucial line to copy the capabilities with your tools
        })
        .WithHttpTransport(options =>
        {
            // The Stateless option is turned off because the validation tool(s) need to know the root directory,
            // which is obtained from a query back to the calling client of the MCP Server (see ArchValidationTool.GetRootOrThrowExceptionAsync).
            // An earlier project had to use this option for the AI Foundry AI Agent, so that client may no longer work for this MCP Server.
            //options.Stateless = true;
        });

        // Github services
        builder.Services.AddTransient<GitHubCheckService>();
        builder.Services.AddSingleton<GitHubAppAuthService>();
        builder.Services.AddSingleton<IGitHubClient>(sp =>
        {
            // GitHubClient will be initialized with a dummy ProductHeaderValue here,
            // as the actual credentials will be set per request using the auth service.
            return new GitHubClient(new ProductHeaderValue("RulesCheckGihubApp", "1.0"));
        });

        // Repository cloning services
        builder.Services.AddScoped<IRepositoryCloneService, RepositoryCloneService>();
        builder.Services.AddScoped<IPathConverter, PathConverter>();
        builder.Services.AddHostedService<RepositoryCleanupService>();

        // Webhook services
        builder.Services.AddScoped<IGitHubWebhookService, GitHubWebhookService>();
        builder.Services.AddScoped<IGitHubWebhookAuthenticator, GitHubWebhookAuthenticator>();
        builder.Services.AddScoped<IGitHubWebhookRouter, GitHubWebhookRouter>();
        builder.Services.AddScoped<IRepositoryPathResolver, RepositoryPathResolver>();
        
        // Webhook handlers
        builder.Services.AddScoped<IWebhookHandler, PingWebhookHandler>();
        builder.Services.AddScoped<IWebhookHandler, PushWebhookHandler>();
        builder.Services.AddScoped<IWebhookHandler, PullRequestWebhookHandler>();
        builder.Services.AddScoped<IWebhookHandler, CheckRunWebhookHandler>();
        builder.Services.AddScoped<IWebhookHandler, CheckSuiteWebhookHandler>();

        var app = builder.Build();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapMcp("/mcp").RequireAuthorization();

        AddGHWebhookEndpoint(app);

        AddOAuthEndpoints(app, oAuthIssuerUrl, mcpResourceUrl);

        Console.WriteLine($"MCP server listening on {localListenUrl}");
        Console.WriteLine($"Public MCP resource URL (Audience): {mcpResourceUrl}");
        Console.WriteLine($"Public OAuth server URL (Issuer): {oAuthIssuerUrl}");
        Console.WriteLine($"Protected Resource Metadata URL: {publicBaseUrl}.well-known/oauth-protected-resource");
        Console.WriteLine("Press Ctrl+C to stop the server");

        // Tell Kestrel to listen on the LOCAL address.
        await app.RunAsync(localListenUrl);
    }

    #region GH Webhook
    public static void AddGHWebhookEndpoint(WebApplication app)
    {
        app.MapPost("/github/webhook", async (
            [FromHeader(Name = "X-GitHub-Delivery")] string githubDelivery,
            [FromHeader(Name = "X-GitHub-Event")] string githubEvent,
            [FromHeader(Name = "X-Hub-Signature-256")] string? githubSignature,
            // Keep this parameter for logging/debugging, but rely more on payload
            [FromHeader(Name = "X-GitHub-Hook-Installation-Target-ID")] long? githubInstallationIdFromHeader,
            HttpRequest request,
            IGitHubWebhookService webhookService) =>
        {
            return await webhookService.HandleWebhookAsync(request, githubEvent, githubDelivery, githubSignature, githubInstallationIdFromHeader);
        })
        .WithName("GitHubWebhook")
        .AllowAnonymous();
    }

    public static string GetRootFromRepo(string repo)
    {
        if (repo.Contains("RulesDemo", StringComparison.OrdinalIgnoreCase))
        {
            return "/mnt/c/Users/Jim/source/DevChecks/RulesDemo";
        }

        return "unknown repo";
    }

    public static class GitHubSignatureValidator
    {
        public static bool IsValidSignature(string payloadBody, string secret, string signatureHeader)
        {
            if (string.IsNullOrWhiteSpace(payloadBody) || string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signatureHeader))
            {
                return false;
            }

            // Signature header format: "sha256=<signature>"
            const string signaturePrefix = "sha256=";
            if (!signatureHeader.StartsWith(signaturePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var signature = signatureHeader.Substring(signaturePrefix.Length);

            var secretBytes = Encoding.UTF8.GetBytes(secret);
            var payloadBytes = Encoding.UTF8.GetBytes(payloadBody);

            using (var hmac = new System.Security.Cryptography.HMACSHA256(secretBytes))
            {
                var hash = hmac.ComputeHash(payloadBytes);
                var hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                return hashString.Equals(signature, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
    #endregion

    #region Auth Code
    // Coding Standard Exception (TODO) - these properties should be referenced by ClassName.PropertyName

    // --- State Management for In-Memory Auth Server ---
    private static RSA Rsa { get; } = CreatePersistentRSAKey();
    private static string KeyId { get; } = Guid.NewGuid().ToString();
    private static ConcurrentDictionary<string, ClientInfo> Clients { get; } = new();
    private static ConcurrentDictionary<string, AuthorizationCodeInfo> AuthCodes { get; } = new();
    private static ConcurrentDictionary<string, TokenInfo> Tokens { get; } = new();
    private static bool HasIssuedExpiredToken { get; set; } = false;

    private static string ClientsFile { get; } = "oauth-clients.json";

    /// <summary>
    /// Adds all necessary OAuth 2.0 and OIDC endpoints to the application pipeline.
    /// </summary>
    public static void AddOAuthEndpoints(WebApplication app, string issuerUrl, string resourceUrl)
    {
        // Load existing clients from file
        LoadClientsFromFile();

        var validResources = new[] { resourceUrl };
        var demoClientId = "demo-client";
        var demoClientSecret = "demo-secret";

        // --- Pre-configured Clients ---
        // (these will overwrite if they exist)
        Clients[demoClientId] = new ClientInfo
        {
            ClientId = demoClientId,
            ClientSecret = demoClientSecret,
            RedirectUris = ["http://localhost:1179/callback"],
        };

        Clients["test-refresh-client"] = new ClientInfo
        {
            ClientId = "test-refresh-client",
            ClientSecret = "test-refresh-secret",
            RedirectUris = ["http://localhost:1179/callback"],
        };

        // --- Endpoint Definitions ---
        app.MapGet("/", () => $"MCP Demo Server with In-Memory OAuth 2.0 at {issuerUrl}")
           .ExcludeFromDescription();

        string[] metadataEndpoints = [
            "/.well-known/oauth-authorization-server", 
            "/.well-known/openid-configuration"
        ];

        foreach (var endpoint in metadataEndpoints)
        {
            app.MapGet(endpoint, () => Results.Ok(new OAuthServerMetadata
            {
                Issuer = issuerUrl,
                AuthorizationEndpoint = $"{issuerUrl}authorize",
                TokenEndpoint = $"{issuerUrl}token",
                JwksUri = $"{issuerUrl}.well-known/jwks.json",
                ResponseTypesSupported = ["code"],
                SubjectTypesSupported = ["public"],
                IdTokenSigningAlgValuesSupported = ["RS256"],
                ScopesSupported = ["openid", "profile", "email", "mcp:tools"],
                TokenEndpointAuthMethodsSupported = ["client_secret_post"],
                ClaimsSupported = ["sub", "iss", "name", "email", "aud"],
                CodeChallengeMethodsSupported = ["S256"],
                GrantTypesSupported = ["authorization_code", "refresh_token", "client_credentials"],
                IntrospectionEndpoint = $"{issuerUrl}introspect",
                RegistrationEndpoint = $"{issuerUrl}register"
            })).ExcludeFromDescription();
        }

        app.MapGet("/.well-known/jwks.json", () =>
        {
            var parameters = Rsa.ExportParameters(false);
            
            return Results.Ok(new JsonWebKeySet
            {
                Keys = [
                    new JsonWebKey 
                    {
                        KeyType = "RSA", 
                        Use = "sig", 
                        KeyId = KeyId, 
                        Algorithm = "RS256",
                        Exponent = WebEncoders.Base64UrlEncode(parameters.Exponent!),
                        Modulus = WebEncoders.Base64UrlEncode(parameters.Modulus!)
                    }
                ]
            });
        }).ExcludeFromDescription();

        app.MapGet("/authorize", (
            [FromQuery] string client_id, 
            [FromQuery] string? redirect_uri, 
            [FromQuery] string response_type,
            [FromQuery] string code_challenge, 
            [FromQuery] string code_challenge_method, 
            [FromQuery] string? scope,
            [FromQuery] string? state, 
            [FromQuery] string? resource,
            [FromQuery] string? audience) =>
        {
            // Validate client
            if (!Clients.TryGetValue(client_id, out var client))
            {
                return Results.BadRequest(new OAuthErrorResponse
                {
                    Error = "invalid_client",
                    ErrorDescription = "Client not found"
                });
            }

            // Validate redirect_uri
            if (string.IsNullOrEmpty(redirect_uri))
            {
                if (client.RedirectUris.Count == 1)
                {
                    redirect_uri = client.RedirectUris[0];
                }
                else
                {
                    return Results.BadRequest(new OAuthErrorResponse 
                    { 
                        Error = "invalid_request", 
                        ErrorDescription = "redirect_uri is required" 
                    });
                }
            }
            else if (!client.RedirectUris.Contains(redirect_uri))
            {
                return Results.BadRequest(new OAuthErrorResponse 
                { 
                    Error = "invalid_request", 
                    ErrorDescription = "Unregistered redirect_uri" 
                });
            }

            // Validate response_type
            if (response_type != "code")
            {
                return Results.Redirect($"{redirect_uri}?error=unsupported_response_type&state={state}");
            }
            
            // Validate code_challenge_method
            if (code_challenge_method != "S256")
            {
                return Results.Redirect($"{redirect_uri}?error=invalid_request&error_description=Only+S256+is+supported&state={state}");
            }
            
            // Validate resource/audience (OAuth spec compliance)
            // Priority: audience (standard OAuth/OIDC) > resource (RFC 8707 Resource Indicators)
            var targetResource = !string.IsNullOrEmpty(audience) ? audience : resource;
            if (string.IsNullOrEmpty(targetResource) || !validResources.Contains(targetResource))
            {
                var errorParam = !string.IsNullOrEmpty(audience) ? "audience" : "resource";
                return Results.Redirect($"{redirect_uri}?error=invalid_target&error_description=Invalid+{errorParam}&state={state}");
            }

            // Generate authorization code and store it
            var code = GenerateRandomToken();
            
            AuthCodes[code] = new AuthorizationCodeInfo 
            { 
                ClientId = client_id, 
                RedirectUri = redirect_uri, 
                CodeChallenge = code_challenge, 
                Scope = scope?.Split(' ').ToList() ?? [], 
                Resource = new Uri(targetResource) 
            };
            
            // Build redirect URL with code
            var redirectUrl = $"{redirect_uri}?code={code}";
            
            if (!string.IsNullOrEmpty(state))
            {
                redirectUrl += $"&state={Uri.EscapeDataString(state)}";
            }
            
            return Results.Redirect(redirectUrl);
        }).ExcludeFromDescription();

        app.MapPost("/token", async (HttpContext context) =>
        {
            var form = await context.Request.ReadFormAsync();
            var client = AuthenticateClient(form);

            if (client is null)
            {
                return Results.Unauthorized();
            }

            // Extract resource/audience parameters (OAuth spec compliance)
            var resource = form["resource"].ToString();
            var audience = form["audience"].ToString();
            
            // Priority: audience (standard OAuth/OIDC) > resource (RFC 8707 Resource Indicators)
            var targetResource = !string.IsNullOrEmpty(audience) ? audience : resource;

            if (string.IsNullOrEmpty(targetResource) || !validResources.Contains(targetResource))
            {
                var errorParam = !string.IsNullOrEmpty(audience) ? "audience" : "resource";
                return Results.BadRequest(new OAuthErrorResponse
                {
                    Error = "invalid_target",
                    ErrorDescription = $"The specified {errorParam} is not valid."
                });
            }

            var grantType = form["grant_type"].ToString();

            if (grantType == "client_credentials")
            {
                // Note: The 'scope' parameter from the form is ignored for client_credentials; we grant a default scope.
                return Results.Ok(GenerateJwtTokenResponse(client.ClientId, ["mcp:tools"], new Uri(targetResource), issuerUrl));
            }

            if (grantType == "authorization_code")
            {
                var code = form["code"].ToString();

                if (string.IsNullOrEmpty(code) || !AuthCodes.TryRemove(code, out var codeInfo))
                {
                    return Results.BadRequest(new OAuthErrorResponse
                    {
                        Error = "invalid_grant",
                        ErrorDescription = "Invalid authorization code"
                    });
                }

                if (codeInfo.ClientId != client.ClientId)
                {
                    return Results.BadRequest(new OAuthErrorResponse
                    {
                        Error = "invalid_grant",
                        ErrorDescription = "Code was not issued to this client"
                    });
                }

                if (!VerifyCodeChallenge(form["code_verifier"].ToString(), codeInfo.CodeChallenge))
                {
                    return Results.BadRequest(new OAuthErrorResponse
                    {
                        Error = "invalid_grant",
                        ErrorDescription = "Invalid code_verifier"
                    });
                }

                return Results.Ok(GenerateJwtTokenResponse(client.ClientId, codeInfo.Scope, codeInfo.Resource, issuerUrl));
            }

            if (grantType == "refresh_token")
            {
                var refreshToken = form["refresh_token"].ToString();

                if (string.IsNullOrEmpty(refreshToken) || !Tokens.TryRemove(refreshToken, out var tokenInfo) || tokenInfo.ClientId != client.ClientId)
                {
                    return Results.BadRequest(new OAuthErrorResponse
                    {
                        Error = "invalid_grant",
                        ErrorDescription = "Invalid refresh token"
                    });
                }

                return Results.Ok(GenerateJwtTokenResponse(client.ClientId, tokenInfo.Scopes, tokenInfo.Resource, issuerUrl));
            }

            return Results.BadRequest(new OAuthErrorResponse
            {
                Error = "unsupported_grant_type",
                ErrorDescription = "Unsupported grant type"
            });

        }).ExcludeFromDescription();

        app.MapPost("/introspect", async (HttpContext context) =>
        {
            var form = await context.Request.ReadFormAsync();
            var token = form["token"].ToString();
            
            if (string.IsNullOrEmpty(token))
            {
                return Results.BadRequest(new OAuthErrorResponse
                {
                    Error = "invalid_request",
                    ErrorDescription = "Token is required"
                });
            }

            if (Tokens.TryGetValue(token, out var tokenInfo))
            {
                if (tokenInfo.ExpiresAt < DateTimeOffset.UtcNow)
                {
                    return Results.Ok(new TokenIntrospectionResponse { Active = false });
                }
                
                return Results.Ok(new TokenIntrospectionResponse 
                { 
                    Active = true, 
                    ClientId = tokenInfo.ClientId, 
                    Scope = string.Join(" ", tokenInfo.Scopes), 
                    ExpirationTime = tokenInfo.ExpiresAt.ToUnixTimeSeconds(), 
                    Audience = tokenInfo.Resource?.ToString() 
                });
            }
            
            return Results.Ok(new TokenIntrospectionResponse { Active = false });
        }).ExcludeFromDescription();

        // Dynamic Client Registration endpoint (RFC 7591)
        app.MapPost("/register", async (HttpContext context) =>
        {
            var registrationRequest = await context.Request.ReadFromJsonAsync<ClientRegistrationRequest>();

            if (registrationRequest is null)
            {
                return Results.BadRequest(new OAuthErrorResponse
                {
                    Error = "invalid_request",
                    ErrorDescription = "Invalid registration request"
                });
            }

            // Validate redirect URIs are provided
            if (registrationRequest.RedirectUris.Count == 0)
            {
                return Results.BadRequest(new OAuthErrorResponse
                {
                    Error = "invalid_redirect_uri",
                    ErrorDescription = "At least one redirect URI must be provided"
                });
            }

            // Validate redirect URIs format
            foreach (var redirectUri in registrationRequest.RedirectUris)
            {
                if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri) || 
                    (uri.Scheme != "http" && uri.Scheme != "https"))
                {
                    return Results.BadRequest(new OAuthErrorResponse
                    {
                        Error = "invalid_redirect_uri",
                        ErrorDescription = $"Invalid redirect URI: {redirectUri}"
                    });
                }
            }

            // Generate client credentials and store the registered client
            var clientId = $"dyn-{Guid.NewGuid():N}";
            var clientSecret = GenerateRandomToken();
            
            Clients[clientId] = new ClientInfo 
            { 
                ClientId = clientId, 
                ClientSecret = clientSecret, 
                RedirectUris = registrationRequest.RedirectUris 
            };

            SaveClientsToFile();

            return Results.Ok(new ClientRegistrationResponse
            {
                ClientId = clientId,
                ClientSecret = clientSecret,
                ClientIdIssuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                RedirectUris = registrationRequest.RedirectUris,
                GrantTypes = ["authorization_code", "refresh_token"],
                ResponseTypes = ["code"],
                TokenEndpointAuthMethod = "client_secret_post",
            });
        }).ExcludeFromDescription();
        Console.WriteLine("\n--- To get a demo access token, use this command: ---");
        Console.WriteLine($"curl -k -d \"grant_type=client_credentials&client_id={demoClientId}&client_secret={demoClientSecret}&resource={resourceUrl}\" -X POST {issuerUrl}token\n");
    }

    // --- Helper Methods ---
    private static ClientInfo? AuthenticateClient(IFormCollection form)
    {
        var clientId = form["client_id"].ToString();
        var clientSecret = form["client_secret"].ToString();
        
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            return null;
        }
        
        return Clients.TryGetValue(clientId, out var client) && client.ClientSecret == clientSecret ? client : null;
    }

    private static TokenResponse GenerateJwtTokenResponse(string clientId, List<string> scopes, Uri? resource, string issuerUrl)
    {
        var expiresIn = TimeSpan.FromHours(8);
        var issuedAt = DateTimeOffset.UtcNow;
        
        if (clientId == "test-refresh-client" && !HasIssuedExpiredToken)
        {
            HasIssuedExpiredToken = true;
            expiresIn = TimeSpan.FromHours(-1);
        }

        var expiresAt = issuedAt.Add(expiresIn);
        var jwtId = Guid.NewGuid().ToString();
        
        var header = new Dictionary<string, string> 
        { 
            { "alg", "RS256" }, 
            { "typ", "JWT" }, 
            { "kid", KeyId } 
        };
        
        var payload = new Dictionary<string, object>
        {
            { "iss", issuerUrl }, 
            { "sub", $"user-{clientId}" }, 
            { "name", $"Test User for {clientId}" },
            { "aud", resource?.ToString() ?? clientId }, 
            { "client_id", clientId }, 
            { "jti", jwtId },
            { "iat", issuedAt.ToUnixTimeSeconds() }, 
            { "exp", expiresAt.ToUnixTimeSeconds() }, 
            { "scope", string.Join(" ", scopes) }
        };

        var headerJson = JsonSerializer.Serialize(header);
        var payloadJson = JsonSerializer.Serialize(payload);
        var headerBase64 = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var payloadBase64 = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        var dataToSign = $"{headerBase64}.{payloadBase64}";
        var signature = Rsa.SignData(Encoding.UTF8.GetBytes(dataToSign), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var jwtToken = $"{headerBase64}.{payloadBase64}.{WebEncoders.Base64UrlEncode(signature)}";
        var refreshToken = GenerateRandomToken();

        Tokens[refreshToken] = new TokenInfo 
        { 
            ClientId = clientId, 
            Scopes = scopes, 
            IssuedAt = issuedAt, 
            ExpiresAt = expiresAt, 
            Resource = resource, 
            JwtId = jwtId 
        };

        return new TokenResponse 
        { 
            AccessToken = jwtToken, 
            RefreshToken = refreshToken, 
            TokenType = "Bearer", 
            ExpiresIn = (int)expiresIn.TotalSeconds, 
            Scope = string.Join(" ", scopes) 
        };
    }

    private static string GenerateRandomToken()
    {
        return WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    }

    private static bool VerifyCodeChallenge(string codeVerifier, string codeChallenge)
    {
        using var sha256 = SHA256.Create();
        var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
        return codeChallenge == WebEncoders.Base64UrlEncode(challengeBytes);
    }

    private static void LoadClientsFromFile()
    {
        if (File.Exists(ClientsFile))
        {
            try
            {
                var json = File.ReadAllText(ClientsFile);
                var clients = JsonSerializer.Deserialize<Dictionary<string, ClientInfo>>(json);
                if (clients is not null)
                {
                    foreach (var kvp in clients)
                    {
                        Clients[kvp.Key] = kvp.Value;
                    }
                    Console.WriteLine($"Loaded {clients.Count} registered clients from {ClientsFile}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading clients: {ex.Message}");
            }
        }
    }

    private static void SaveClientsToFile()
    {
        try
        {
            var json = JsonSerializer.Serialize(Clients.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), 
                new JsonSerializerOptions
                    { WriteIndented = true });
                File.WriteAllText(ClientsFile, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving clients: {ex.Message}");
        }
    }

    private static RSA CreatePersistentRSAKey()
    {
        var keyFile = "oauth-key.xml";
        if (File.Exists(keyFile))
        {
            var rsa = RSA.Create();
            rsa.FromXmlString(File.ReadAllText(keyFile));
            return rsa;
        }
        else
        {
            var rsa = RSA.Create(2048);
            File.WriteAllText(keyFile, rsa.ToXmlString(true));
            return rsa;
        }
    }
    #endregion
}
