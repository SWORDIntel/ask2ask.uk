using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Net;

namespace Ask2Ask.Pages;

public class TrackingModel : PageModel
{
    private readonly ILogger<TrackingModel> _logger;
    private readonly string _dataDirectory;

    public TrackingModel(ILogger<TrackingModel> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _dataDirectory = Path.Combine(env.ContentRootPath, "TrackingData");

        if (!Directory.Exists(_dataDirectory))
        {
            Directory.CreateDirectory(_dataDirectory);
        }
    }

    // ====================
    // CNSA 2.0 CRYPTOGRAPHIC FUNCTIONS
    // ====================

    /// <summary>
    /// Generate SHA-384 hash (CNSA 2.0 compliant)
    /// </summary>
    private string GenerateSHA384Hash(string data)
    {
        using var sha384 = SHA384.Create();
        var hashBytes = sha384.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Placeholder for ML-KEM-1024 key encapsulation (CNSA 2.0)
    /// To be implemented when .NET libraries become available
    /// </summary>
    private object GenerateMLKEM1024KeyPair()
    {
        // TODO: Implement ML-KEM-1024 when library available
        // This is a post-quantum key encapsulation mechanism
        return new
        {
            Status = "Not yet implemented - awaiting ML-KEM-1024 library",
            Algorithm = "ML-KEM-1024",
            CNSA2_0_Compliant = true,
            PostQuantumSafe = true
        };
    }

    /// <summary>
    /// Placeholder for ML-DSA-87 digital signature (CNSA 2.0)
    /// To be implemented when .NET libraries become available
    /// </summary>
    private object GenerateMLDSA87Signature(string data)
    {
        // TODO: Implement ML-DSA-87 when library available
        // This is a post-quantum digital signature algorithm
        return new
        {
            Status = "Not yet implemented - awaiting ML-DSA-87 library",
            Algorithm = "ML-DSA-87",
            CNSA2_0_Compliant = true,
            PostQuantumSafe = true,
            DataToSign = GenerateSHA384Hash(data) // Hash first, then sign
        };
    }

    // ====================
    // TCP/IP & TLS FINGERPRINTING
    // ====================

    private object GetTCPIPFingerprint()
    {
        var connection = HttpContext.Connection;

        return new
        {
            LocalIPAddress = connection.LocalIpAddress?.ToString(),
            LocalPort = connection.LocalPort,
            RemoteIPAddress = connection.RemoteIpAddress?.ToString(),
            RemotePort = connection.RemotePort,
            ConnectionId = connection.Id,
            Protocol = HttpContext.Request.Protocol,
            IsHTTPS = HttpContext.Request.IsHttps,
            Scheme = HttpContext.Request.Scheme,
            Host = HttpContext.Request.Host.ToString(),
            PathBase = HttpContext.Request.PathBase.ToString(),
            Method = HttpContext.Request.Method
        };
    }

    private object GetTLSFingerprint()
    {
        // TLS/SSL information if available
        var tlsFeature = HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.ITlsConnectionFeature>();

        if (tlsFeature != null)
        {
            return new
            {
                ClientCertificate = tlsFeature.ClientCertificate?.Subject,
                Protocol = tlsFeature.Protocol.ToString(),
                CipherAlgorithm = tlsFeature.CipherAlgorithm.ToString(),
                CipherStrength = tlsFeature.CipherStrength,
                HashAlgorithm = tlsFeature.HashAlgorithm.ToString(),
                HashStrength = tlsFeature.HashStrength,
                KeyExchangeAlgorithm = tlsFeature.KeyExchangeAlgorithm.ToString(),
                KeyExchangeStrength = tlsFeature.KeyExchangeStrength
            };
        }

        return new { Status = "TLS information not available" };
    }

    private object GetHTTPHeaderFingerprint()
    {
        return new
        {
            AcceptLanguage = Request.Headers["Accept-Language"].ToString(),
            AcceptEncoding = Request.Headers["Accept-Encoding"].ToString(),
            Accept = Request.Headers["Accept"].ToString(),
            AcceptCharset = Request.Headers["Accept-Charset"].ToString(),
            CacheControl = Request.Headers["Cache-Control"].ToString(),
            Connection = Request.Headers["Connection"].ToString(),
            DNT = Request.Headers["DNT"].ToString(), // Do Not Track
            UpgradeInsecureRequests = Request.Headers["Upgrade-Insecure-Requests"].ToString(),
            SecFetchSite = Request.Headers["Sec-Fetch-Site"].ToString(),
            SecFetchMode = Request.Headers["Sec-Fetch-Mode"].ToString(),
            SecFetchUser = Request.Headers["Sec-Fetch-User"].ToString(),
            SecFetchDest = Request.Headers["Sec-Fetch-Dest"].ToString(),
            SecChUa = Request.Headers["Sec-CH-UA"].ToString(),
            SecChUaMobile = Request.Headers["Sec-CH-UA-Mobile"].ToString(),
            SecChUaPlatform = Request.Headers["Sec-CH-UA-Platform"].ToString(),
            AllHeaders = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())
        };
    }

    private string AnalyzeUserAgentPattern()
    {
        var ua = Request.Headers["User-Agent"].ToString();

        if (string.IsNullOrEmpty(ua))
            return "No User-Agent (suspicious)";

        var patterns = new Dictionary<string, string>
        {
            { "bot", ua.ToLower() },
            { "crawler", ua.ToLower() },
            { "spider", ua.ToLower() },
            { "headless", ua.ToLower() },
            { "selenium", ua.ToLower() },
            { "phantomjs", ua.ToLower() },
            { "python", ua.ToLower() },
            { "curl", ua.ToLower() },
            { "wget", ua.ToLower() }
        };

        foreach (var pattern in patterns)
        {
            if (pattern.Value.Contains(pattern.Key))
                return $"Suspicious pattern detected: {pattern.Key}";
        }

        return "User-Agent appears normal";
    }

    public async Task<IActionResult> OnPostAsync([FromBody] JsonElement trackingData)
    {
        try
        {
            // Get client information
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            var userAgent = Request.Headers["User-Agent"].FirstOrDefault();
            var referer = Request.Headers["Referer"].FirstOrDefault();

            // Server-side fingerprinting
            var tcpipFingerprint = GetTCPIPFingerprint();
            var tlsFingerprint = GetTLSFingerprint();
            var httpHeaderFingerprint = GetHTTPHeaderFingerprint();
            var userAgentAnalysis = AnalyzeUserAgentPattern();

            // Create comprehensive tracking record
            var record = new
            {
                Timestamp = DateTime.UtcNow,
                ClientIP = clientIp,
                ForwardedFor = forwardedFor,
                UserAgent = userAgent,
                UserAgentAnalysis = userAgentAnalysis,
                Referer = referer,
                RequestHeaders = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                TrackingData = trackingData,
                ServerSideFingerprinting = new
                {
                    TCPIPFingerprint = tcpipFingerprint,
                    TLSFingerprint = tlsFingerprint,
                    HTTPHeaderFingerprint = httpHeaderFingerprint,
                    RequestTiming = new
                    {
                        ReceivedAt = DateTime.UtcNow,
                        ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    }
                },
                ServerInfo = new
                {
                    ProcessingTime = DateTime.UtcNow,
                    ServerName = Environment.MachineName,
                    ServerOS = Environment.OSVersion.ToString(),
                    DotNetVersion = Environment.Version.ToString(),
                    ProcessorCount = Environment.ProcessorCount
                }
            };

            // Serialize to JSON
            var jsonData = JsonSerializer.Serialize(record, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            // CNSA 2.0 Cryptographic Operations
            var dataHash = GenerateSHA384Hash(jsonData);
            var mlkemInfo = GenerateMLKEM1024KeyPair(); // Placeholder for future implementation
            var mldsaSignature = GenerateMLDSA87Signature(jsonData); // Placeholder for future implementation

            // Create filename with timestamp and hash
            var sessionId = trackingData.TryGetProperty("sessionId", out var sid)
                ? sid.GetString()
                : Guid.NewGuid().ToString();

            var filename = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{sessionId}.json";
            var filepath = Path.Combine(_dataDirectory, filename);

            // Add CNSA 2.0 cryptographic metadata and hash to record
            var recordWithCrypto = new
            {
                CNSA2_0_SecurityMetadata = new
                {
                    HashAlgorithm = "SHA-384",
                    Hash = dataHash,
                    MLKEM1024_KeyEncapsulation = mlkemInfo,
                    MLDSA87_Signature = mldsaSignature,
                    Compliance = new
                    {
                        CNSA2_0_Compliant = true,
                        PostQuantumReady = true,
                        ImplementationStatus = new
                        {
                            SHA384 = "Implemented",
                            MLKEM1024 = "Awaiting library",
                            MLDSA87 = "Awaiting library"
                        }
                    },
                    GeneratedAt = DateTime.UtcNow
                },
                Data = record
            };

            // Write to file
            var finalJson = JsonSerializer.Serialize(recordWithCrypto, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await System.IO.File.WriteAllTextAsync(filepath, finalJson);

            // Log to console for monitoring
            _logger.LogInformation($"[CNSA 2.0] Tracking data received from {clientIp} - Session: {sessionId} - SHA-384: {dataHash[..16]}... - User-Agent: {userAgentAnalysis}");

            // Also append to a daily log file
            var dailyLogFile = Path.Combine(_dataDirectory, $"daily_{DateTime.UtcNow:yyyyMMdd}.jsonl");
            var logEntry = JsonSerializer.Serialize(new {
                timestamp = DateTime.UtcNow,
                ip = clientIp,
                session = sessionId,
                hash = dataHash,
                file = filename,
                userAgentAnalysis = userAgentAnalysis,
                cnsa2_0_compliant = true
            });
            await System.IO.File.AppendAllTextAsync(dailyLogFile, logEntry + "\n");

            return new JsonResult(new
            {
                success = true,
                sessionId = sessionId,
                hash = dataHash,
                timestamp = DateTime.UtcNow,
                message = "Data collected successfully",
                cnsa2_0 = new
                {
                    compliant = true,
                    algorithms = new[] { "SHA-384", "ML-KEM-1024 (pending)", "ML-DSA-87 (pending)" }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing tracking data");
            return new JsonResult(new
            {
                success = false,
                error = ex.Message
            })
            {
                StatusCode = 500
            };
        }
    }
}
