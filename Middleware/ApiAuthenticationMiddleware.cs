using Ask2Ask.Services;

namespace Ask2Ask.Middleware;

/// <summary>
/// Middleware to authenticate API requests using CNSA 2.0 compliant methods
/// </summary>
public class ApiAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiAuthenticationMiddleware> _logger;

    public ApiAuthenticationMiddleware(RequestDelegate next, ILogger<ApiAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context, 
        ApiAuthenticationService authService,
        ZkpAuthenticationService zkpService)
    {
        // Only apply to API endpoints
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        // Check for API key in header
        if (!context.Request.Headers.TryGetValue("X-API-Key", out var apiKey))
        {
            _logger.LogWarning($"API request without API key: {context.Request.Path}");
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "API key required", message = "Include X-API-Key header" });
            return;
        }

        // Determine required scope based on endpoint
        var requiredScope = DetermineRequiredScope(context.Request.Path, context.Request.Method);

        // Validate API key (basic validation)
        if (!authService.ValidateApiKey(apiKey!, requiredScope))
        {
            _logger.LogWarning($"Invalid API key for {context.Request.Path}");
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid API key or insufficient permissions" });
            return;
        }

        // ZKP signature verification for high-security endpoints
        if (context.Request.Path.StartsWithSegments("/api/export") || 
            context.Request.Path.StartsWithSegments("/api/admin"))
        {
            // Extract ZKP signature headers
            if (!context.Request.Headers.TryGetValue("X-Signature", out var signature) ||
                !context.Request.Headers.TryGetValue("X-Timestamp", out var timestampStr) ||
                !context.Request.Headers.TryGetValue("X-Nonce", out var nonce))
            {
                _logger.LogWarning($"Missing ZKP signature headers for {context.Request.Path}");
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { 
                    error = "ZKP signature required", 
                    message = "Include X-Signature, X-Timestamp, and X-Nonce headers" 
                });
                return;
            }

            // Parse timestamp
            if (!long.TryParse(timestampStr, out var timestamp))
            {
                _logger.LogWarning($"Invalid timestamp format: {timestampStr}");
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid timestamp format" });
                return;
            }

            // Get request body
            string? requestBody = null;
            if (context.Request.ContentLength > 0 && context.Request.Body.CanSeek)
            {
                var originalPosition = context.Request.Body.Position;
                context.Request.Body.Position = 0;
                using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                requestBody = await reader.ReadToEndAsync();
                context.Request.Body.Position = originalPosition;
            }

            // Get public key for API key
            var apiKeyString = apiKey!.ToString();
            var publicKey = zkpService.GetPublicKeyForApiKey(apiKeyString);
            if (string.IsNullOrEmpty(publicKey))
            {
                _logger.LogWarning($"No public key found for API key: {apiKeyString[..Math.Min(8, apiKeyString.Length)]}...");
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new { error = "Public key not configured for this API key" });
                return;
            }

            // Verify ZKP signature
            var method = context.Request.Method;
            var path = context.Request.Path + context.Request.QueryString;
            
            if (!zkpService.VerifyRequest(
                publicKey,
                signature!.ToString(),
                method,
                path,
                requestBody,
                timestamp,
                nonce!.ToString(),
                apiKeyString))
            {
                _logger.LogWarning($"ZKP signature verification failed for {context.Request.Path}");
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid ZKP signature" });
                return;
            }
        }

        // Add security headers
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("Content-Security-Policy", "default-src 'none'");

        await _next(context);
    }

    private string DetermineRequiredScope(PathString path, string method)
    {
        // Read-only endpoints
        if (method == "GET" && !path.StartsWithSegments("/api/export"))
        {
            return "read";
        }

        // Export endpoints require export scope
        if (path.StartsWithSegments("/api/export"))
        {
            return "export";
        }

        // Admin endpoints require admin scope
        if (path.StartsWithSegments("/api/admin"))
        {
            return "admin";
        }

        // Write operations require write scope
        if (method == "POST" || method == "PUT" || method == "DELETE")
        {
            return "write";
        }

        return "read";
    }
}

