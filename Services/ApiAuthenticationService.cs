using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace Ask2Ask.Services;

/// <summary>
/// CNSA 2.0 Compliant API Authentication Service
/// Uses SHA-384 for API key hashing and validation
/// </summary>
public class ApiAuthenticationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiAuthenticationService> _logger;
    private readonly IMemoryCache _cache;
    
    // CNSA 2.0: SHA-384 for cryptographic hashing
    private const string HASH_ALGORITHM = "SHA-384";
    
    public ApiAuthenticationService(
        IConfiguration configuration, 
        ILogger<ApiAuthenticationService> logger,
        IMemoryCache cache)
    {
        _configuration = configuration;
        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// Validates API key using CNSA 2.0 compliant SHA-384 hashing
    /// </summary>
    public bool ValidateApiKey(string apiKey, string requiredScope = "read")
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("API key validation failed: empty key");
            return false;
        }

        // Check rate limiting
        var keyHash = ComputeSHA384Hash(apiKey);
        if (IsRateLimited(keyHash))
        {
            _logger.LogWarning($"API key rate limited: {keyHash[..16]}...");
            return false;
        }

        // Get valid API keys from configuration
        var validKeys = _configuration.GetSection("ApiKeys").Get<List<ApiKeyConfig>>() ?? new List<ApiKeyConfig>();
        
        foreach (var validKey in validKeys)
        {
            var validKeyHash = ComputeSHA384Hash(validKey.Key);
            if (SecureCompare(keyHash, validKeyHash))
            {
                // Check scope
                if (validKey.Scopes.Contains(requiredScope) || validKey.Scopes.Contains("*"))
                {
                    _logger.LogInformation($"API key validated successfully for scope: {requiredScope}");
                    RecordApiKeyUsage(keyHash);
                    return true;
                }
                
                _logger.LogWarning($"API key valid but insufficient scope. Required: {requiredScope}, Has: {string.Join(",", validKey.Scopes)}");
                return false;
            }
        }

        _logger.LogWarning("API key validation failed: invalid key");
        return false;
    }


    /// <summary>
    /// Compute CNSA 2.0 compliant SHA-384 hash
    /// </summary>
    public static string ComputeSHA384Hash(string input)
    {
        using var sha384 = SHA384.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha384.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Constant-time comparison to prevent timing attacks
    /// </summary>
    private static bool SecureCompare(string a, string b)
    {
        if (a.Length != b.Length)
            return false;

        var result = 0;
        for (var i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }
        return result == 0;
    }

    /// <summary>
    /// Rate limiting: 100 requests per minute per API key
    /// </summary>
    private bool IsRateLimited(string keyHash)
    {
        var cacheKey = $"ratelimit_{keyHash}";
        
        if (_cache.TryGetValue(cacheKey, out int requestCount))
        {
            if (requestCount >= 100)
            {
                return true;
            }
            _cache.Set(cacheKey, requestCount + 1, TimeSpan.FromMinutes(1));
        }
        else
        {
            _cache.Set(cacheKey, 1, TimeSpan.FromMinutes(1));
        }
        
        return false;
    }

    /// <summary>
    /// Record API key usage for audit trail
    /// </summary>
    private void RecordApiKeyUsage(string keyHash)
    {
        var cacheKey = $"apiusage_{keyHash}";
        _cache.Set(cacheKey, DateTime.UtcNow, TimeSpan.FromHours(24));
    }

    /// <summary>
    /// Generate a new CNSA 2.0 compliant API key
    /// </summary>
    public static string GenerateApiKey()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[64]; // 512 bits
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}

/// <summary>
/// API Key Configuration
/// </summary>
public class ApiKeyConfig
{
    public string Key { get; set; } = string.Empty;
    public string? PublicKey { get; set; } // ECDSA P-384 public key (base64) for ZKP authentication
    public List<string> Scopes { get; set; } = new List<string>();
    public string Description { get; set; } = string.Empty;
}

