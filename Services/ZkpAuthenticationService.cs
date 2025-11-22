using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace Ask2Ask.Services;

/// <summary>
/// Zero Knowledge Proof Authentication Service
/// Uses ECDSA P-384 with SHA-384 (CNSA 2.0 compliant)
/// Proves identity without revealing secrets
/// </summary>
public class ZkpAuthenticationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ZkpAuthenticationService> _logger;
    private readonly IMemoryCache _cache;
    
    // CNSA 2.0: ECDSA P-384 with SHA-384
    private const int SIGNATURE_TIMESTAMP_WINDOW_MINUTES = 5;
    private const int NONCE_CACHE_MINUTES = 10;
    
    public ZkpAuthenticationService(
        IConfiguration configuration,
        ILogger<ZkpAuthenticationService> logger,
        IMemoryCache cache)
    {
        _configuration = configuration;
        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// Generate a new ECDSA P-384 key pair for an API client
    /// </summary>
    public static (string PrivateKey, string PublicKey) GenerateKeyPair()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var privateKey = Convert.ToBase64String(ecdsa.ExportPkcs8PrivateKey());
        var publicKey = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());
        return (privateKey, publicKey);
    }

    /// <summary>
    /// Create a signature over request data (client-side)
    /// </summary>
    public static string SignRequest(
        string privateKeyBase64,
        string method,
        string path,
        string? requestBody,
        long timestamp,
        string nonce)
    {
        // Create message to sign: method + path + body_hash + timestamp + nonce
        var bodyHash = string.IsNullOrEmpty(requestBody)
            ? string.Empty
            : ComputeSHA384Hash(requestBody);
        
        var message = $"{method}|{path}|{bodyHash}|{timestamp}|{nonce}";
        
        // Sign with ECDSA P-384
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyBase64), out _);
        
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var signature = ecdsa.SignData(messageBytes, HashAlgorithmName.SHA384);
        
        return Convert.ToBase64String(signature);
    }

    /// <summary>
    /// Verify signature and validate request (server-side)
    /// </summary>
    public bool VerifyRequest(
        string publicKeyBase64,
        string signatureBase64,
        string method,
        string path,
        string? requestBody,
        long timestamp,
        string nonce,
        string apiKeyId)
    {
        try
        {
            // 1. Validate timestamp (prevent replay attacks)
            var serverTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var timeDiff = Math.Abs(serverTime - timestamp);
            
            if (timeDiff > SIGNATURE_TIMESTAMP_WINDOW_MINUTES * 60)
            {
                _logger.LogWarning($"Request timestamp out of window. Diff: {timeDiff}s, Key: {apiKeyId[..8]}...");
                return false;
            }

            // 2. Validate nonce uniqueness (prevent replay attacks)
            var nonceKey = $"nonce_{apiKeyId}_{timestamp}_{nonce}";
            if (_cache.TryGetValue(nonceKey, out _))
            {
                _logger.LogWarning($"Nonce already used. Key: {apiKeyId[..8]}...");
                return false;
            }
            
            // Cache nonce for NONCE_CACHE_MINUTES
            _cache.Set(nonceKey, true, TimeSpan.FromMinutes(NONCE_CACHE_MINUTES));

            // 3. Reconstruct message
            var bodyHash = string.IsNullOrEmpty(requestBody)
                ? string.Empty
                : ComputeSHA384Hash(requestBody);
            
            var message = $"{method}|{path}|{bodyHash}|{timestamp}|{nonce}";
            var messageBytes = Encoding.UTF8.GetBytes(message);

            // 4. Verify signature
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384);
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
            
            var signature = Convert.FromBase64String(signatureBase64);
            var isValid = ecdsa.VerifyData(messageBytes, signature, HashAlgorithmName.SHA384);

            if (isValid)
            {
                _logger.LogInformation($"ZKP signature verified successfully. Key: {apiKeyId[..8]}...");
            }
            else
            {
                _logger.LogWarning($"ZKP signature verification failed. Key: {apiKeyId[..8]}...");
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error verifying ZKP signature. Key: {apiKeyId[..8]}...");
            return false;
        }
    }

    /// <summary>
    /// Get public key for an API key ID
    /// </summary>
    public string? GetPublicKeyForApiKey(string apiKeyId)
    {
        var apiKeys = _configuration.GetSection("ApiKeys").Get<List<ApiKeyConfig>>() ?? new List<ApiKeyConfig>();
        
        // Find API key by computing hash and comparing
        var apiKeyHash = ApiAuthenticationService.ComputeSHA384Hash(apiKeyId);
        
        foreach (var keyConfig in apiKeys)
        {
            var configKeyHash = ApiAuthenticationService.ComputeSHA384Hash(keyConfig.Key);
            if (SecureCompare(apiKeyHash, configKeyHash))
            {
                return keyConfig.PublicKey;
            }
        }

        return null;
    }

    /// <summary>
    /// Compute SHA-384 hash (CNSA 2.0 compliant)
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
}

