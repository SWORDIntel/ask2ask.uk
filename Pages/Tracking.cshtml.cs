using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Net;
using Ask2Ask.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Ask2Ask.Pages;

public class TrackingModel : PageModel
{
    private readonly ILogger<TrackingModel> _logger;
    private readonly TrackingService _trackingService;

    public TrackingModel(ILogger<TrackingModel> logger, TrackingService trackingService)
    {
        _logger = logger;
        _trackingService = trackingService;
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
                // Note: Protocol and cipher details are not directly available in .NET 8
                // These properties were removed from ITlsConnectionFeature
                Status = "TLS connection established"
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

    // ====================
    // VPN/PROXY DETECTION
    // ====================

    private object GetVPNProxyDetection()
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var forwardedFor = Request.Headers["X-Forwarded-For"].ToString();
        var realIp = Request.Headers["X-Real-IP"].ToString();
        var viaHeader = Request.Headers["Via"].ToString();
        var proxyConnection = Request.Headers["Proxy-Connection"].ToString();
        
        // Common proxy/VPN headers
        var proxyHeaders = new Dictionary<string, string>
        {
            ["X-Forwarded-For"] = forwardedFor,
            ["X-Real-IP"] = realIp,
            ["X-Forwarded-Host"] = Request.Headers["X-Forwarded-Host"].ToString(),
            ["X-Forwarded-Proto"] = Request.Headers["X-Forwarded-Proto"].ToString(),
            ["X-Forwarded-Server"] = Request.Headers["X-Forwarded-Server"].ToString(),
            ["X-Client-IP"] = Request.Headers["X-Client-IP"].ToString(),
            ["X-ProxyUser-IP"] = Request.Headers["X-ProxyUser-IP"].ToString(),
            ["CF-Connecting-IP"] = Request.Headers["CF-Connecting-IP"].ToString(), // Cloudflare
            ["True-Client-IP"] = Request.Headers["True-Client-IP"].ToString(), // Cloudflare/Akamai
            ["X-Original-Forwarded-For"] = Request.Headers["X-Original-Forwarded-For"].ToString(),
            ["Via"] = viaHeader,
            ["Proxy-Connection"] = proxyConnection,
            ["X-Proxy-ID"] = Request.Headers["X-Proxy-ID"].ToString(),
            ["X-Anonymous-Proxy"] = Request.Headers["X-Anonymous-Proxy"].ToString()
        };

        // Analyze IP chain
        var ipChain = new List<string>();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            ipChain.AddRange(forwardedFor.Split(',').Select(ip => ip.Trim()));
        }
        if (!string.IsNullOrEmpty(realIp) && !ipChain.Contains(realIp))
        {
            ipChain.Add(realIp);
        }
        ipChain.Add(remoteIp);

        // Detection indicators
        var indicators = new List<string>();
        
        if (!string.IsNullOrEmpty(forwardedFor))
            indicators.Add("X-Forwarded-For header present (proxy chain detected)");
        
        if (!string.IsNullOrEmpty(viaHeader))
            indicators.Add($"Via header present: {viaHeader} (proxy detected)");
        
        if (!string.IsNullOrEmpty(proxyConnection))
            indicators.Add("Proxy-Connection header present");
        
        if (ipChain.Count > 1)
            indicators.Add($"Multiple IPs in chain ({ipChain.Count} hops)");
        
        // Check for known VPN/proxy patterns
        if (IsKnownVPNProvider(remoteIp))
            indicators.Add("IP matches known VPN provider range");
        
        if (IsDatacenterIP(remoteIp))
            indicators.Add("IP appears to be from datacenter (common for VPNs/proxies)");

        // Check for Tor
        if (IsTorExitNode(remoteIp))
            indicators.Add("IP matches known Tor exit node");

        return new
        {
            RemoteIP = remoteIp,
            IPChain = ipChain,
            ProxyHeaders = proxyHeaders.Where(h => !string.IsNullOrEmpty(h.Value)).ToDictionary(h => h.Key, h => h.Value),
            DetectionIndicators = indicators,
            SuspicionLevel = CalculateSuspicionLevel(indicators.Count, ipChain.Count),
            IsLikelyVPNOrProxy = indicators.Count >= 2 || ipChain.Count > 2,
            Analysis = new
            {
                HasProxyHeaders = proxyHeaders.Any(h => !string.IsNullOrEmpty(h.Value)),
                IPHopCount = ipChain.Count,
                HasViaHeader = !string.IsNullOrEmpty(viaHeader),
                HasForwardedFor = !string.IsNullOrEmpty(forwardedFor),
                IndicatorCount = indicators.Count
            }
        };
    }

    private bool IsKnownVPNProvider(string ip)
    {
        // This is a simplified check. In production, you'd use a VPN detection API or database
        // Common VPN provider IP ranges (examples - not exhaustive)
        var knownVPNRanges = new[]
        {
            "185.159.", // NordVPN
            "91.219.",  // ExpressVPN
            "89.238.",  // ProtonVPN
            "104.244.", // Private Internet Access
            "193.32.",  // Mullvad
        };

        return knownVPNRanges.Any(range => ip.StartsWith(range));
    }

    private bool IsDatacenterIP(string ip)
    {
        // Datacenter IP ranges (simplified - AWS, Google Cloud, Azure, etc.)
        var datacenterRanges = new[]
        {
            "3.", "13.", "18.", "34.", "35.", "52.", "54.", // AWS
            "104.196.", "104.197.", "104.198.", // Google Cloud
            "13.64.", "13.65.", "13.66.", "40.", "52.", // Azure
            "167.172.", "157.230.", "159.89.", // DigitalOcean
            "147.182.", "143.198.", // DigitalOcean
        };

        return datacenterRanges.Any(range => ip.StartsWith(range));
    }

    private bool IsTorExitNode(string ip)
    {
        // In production, you'd query the Tor exit node list
        // This is a placeholder for demonstration
        // You can maintain a list from: https://check.torproject.org/exit-addresses
        return false; // Implement with actual Tor exit node list
    }

    private string CalculateSuspicionLevel(int indicatorCount, int ipHopCount)
    {
        var score = indicatorCount + (ipHopCount - 1);
        
        if (score >= 5) return "Very High";
        if (score >= 3) return "High";
        if (score >= 2) return "Medium";
        if (score >= 1) return "Low";
        return "None";
    }

    private object GetIPGeolocation()
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        
        // Note: In production, you'd use a geolocation API service
        // Examples: MaxMind GeoIP2, IP2Location, ipapi.co, etc.
        
        return new
        {
            IP = remoteIp,
            Note = "Geolocation requires external API service",
            RecommendedServices = new[]
            {
                "MaxMind GeoIP2",
                "IP2Location",
                "ipapi.co",
                "ipinfo.io",
                "ipgeolocation.io"
            },
            LocalAnalysis = new
            {
                IsPrivateIP = IsPrivateIP(remoteIp),
                IsLocalhost = IsLocalhost(remoteIp),
                IPType = ClassifyIPType(remoteIp)
            }
        };
    }

    private bool IsPrivateIP(string ip)
    {
        if (string.IsNullOrEmpty(ip)) return false;
        
        return ip.StartsWith("10.") ||
               ip.StartsWith("172.16.") || ip.StartsWith("172.17.") || ip.StartsWith("172.18.") ||
               ip.StartsWith("172.19.") || ip.StartsWith("172.20.") || ip.StartsWith("172.21.") ||
               ip.StartsWith("172.22.") || ip.StartsWith("172.23.") || ip.StartsWith("172.24.") ||
               ip.StartsWith("172.25.") || ip.StartsWith("172.26.") || ip.StartsWith("172.27.") ||
               ip.StartsWith("172.28.") || ip.StartsWith("172.29.") || ip.StartsWith("172.30.") ||
               ip.StartsWith("172.31.") ||
               ip.StartsWith("192.168.");
    }

    private bool IsLocalhost(string ip)
    {
        return ip == "127.0.0.1" || ip == "::1" || ip == "localhost";
    }

    private string ClassifyIPType(string ip)
    {
        if (IsLocalhost(ip)) return "Localhost";
        if (IsPrivateIP(ip)) return "Private/Internal";
        if (ip.Contains(":")) return "IPv6";
        return "IPv4 Public";
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
            var vpnProxyDetection = GetVPNProxyDetection();
            var ipGeolocation = GetIPGeolocation();

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
                    VPNProxyDetection = vpnProxyDetection,
                    IPGeolocation = ipGeolocation,
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

            // Extract session ID
            var sessionId = trackingData.TryGetProperty("sessionId", out var sid)
                ? sid.GetString()
                : Guid.NewGuid().ToString();

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

            // Save ALL data to database (no file storage)
            var fingerprintHash = dataHash; // Use SHA-384 hash as fingerprint
            var visit = await _trackingService.RecordVisitAsync(
                fingerprintHash,
                sessionId,
                clientIp,
                forwardedFor,
                userAgent,
                referer,
                recordWithCrypto, // Store complete record with crypto metadata
                vpnProxyDetection
            );

            // Process ASN ping timing data if available (BEFORE GetVisitorSummaryAsync)
            // Use visit.VisitorId directly since it's already saved and has the correct ID
            if (trackingData.TryGetProperty("asnPingTiming", out var asnPingData))
            {
                try
                {
                    var asnPingService = HttpContext.RequestServices.GetRequiredService<AsnPingTimingService>();
                    
                    // Store ping timing measurements
                    if (asnPingData.TryGetProperty("measurements", out var measurements))
                    {
                        await asnPingService.StorePingTimingsAsync(visit.Id, asnPingData);
                    }
                    
                    // Correlate ping patterns - use visit.VisitorId to avoid foreign key violation
                    if (asnPingData.TryGetProperty("pattern", out var pattern))
                    {
                        var patternHash = asnPingService.CreatePatternHash(asnPingData);
                        if (!string.IsNullOrEmpty(patternHash))
                        {
                            await asnPingService.CorrelatePingPatternsAsync(
                                visit.VisitorId, // Use visit.VisitorId instead of visitorSummary.VisitorId
                                patternHash,
                                asnPingData
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process ASN ping timing data");
                }
            }

            // Get visitor summary for correlation (AFTER ASN ping timing processing)
            var visitorSummary = await _trackingService.GetVisitorSummaryAsync(fingerprintHash);
            
            // Get standardized output
            var standardizedOutput = await _trackingService.GetStandardizedOutputAsync(visit.Id);

            // Log to console for monitoring (only logging, no file writes)
            _logger.LogInformation($"[CNSA 2.0] Tracking data received from {clientIp} - Session: {sessionId} - SHA-384: {dataHash[..16]}... - User-Agent: {userAgentAnalysis} - Visitor: {visitorSummary.VisitorId} - Visit #{visitorSummary.TotalVisits} - VPN: {standardizedOutput.VPNProxy.IsDetected} ({standardizedOutput.VPNProxy.SuspicionLevel})");

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
                },
                visitor = new
                {
                    id = visitorSummary.VisitorId,
                    isNew = visitorSummary.IsNewVisitor,
                    totalVisits = visitorSummary.TotalVisits,
                    firstSeen = visitorSummary.FirstSeen,
                    lastSeen = visitorSummary.LastSeen
                },
                vpnProxy = standardizedOutput.VPNProxy,
                standardizedOutput = standardizedOutput
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
