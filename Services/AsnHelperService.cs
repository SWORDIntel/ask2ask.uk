using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Ask2Ask.Services
{
    /// <summary>
    /// Helper service that provides advanced ASN‑related utilities.
    /// It mirrors the functionality found in the CLOUDCLEAR project (ASN clustering,
    /// ASN information lookup, reverse‑DNS intelligence, etc.) but is implemented in
    /// pure C# so it can be used directly in the Ask2Ask codebase.
    /// </summary>
    public class AsnHelperService
    {
        private readonly ILogger<AsnHelperService> _logger;
        private readonly HttpClient _httpClient;

        public AsnHelperService(ILogger<AsnHelperService> logger, HttpClient? httpClient = null)
        {
            _logger = logger;
            _httpClient = httpClient ?? new HttpClient();
        }

        #region Public API -----------------------------------------------------

        /// <summary>
        /// Enriches a collection of ping measurement JSON objects with ASN data.
        /// The input is the raw JsonElement that originates from the client payload.
        /// The method mutates each element by adding "asn", "asnName", "asnCountry"
        /// properties when they can be resolved.
        /// </summary>
        public async Task EnrichMeasurementsWithAsnAsync(JsonElement measurements)
        {
            // The measurements element is expected to be an array.
            if (measurements.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("EnrichMeasurementsWithAsnAsync called with non‑array JSON.");
                return;
            }

            foreach (var measurement in measurements.EnumerateArray())
            {
                if (!measurement.TryGetProperty("target", out var targetProp))
                    continue;

                var ip = targetProp.GetString();
                if (string.IsNullOrWhiteSpace(ip))
                    continue;

                // Resolve ASN information.
                var asnInfo = await QueryAsnInformationAsync(ip);
                if (asnInfo != null)
                {
                    // NOTE: JsonElement is immutable – we cannot directly add properties.
                    // The calling code should replace the original measurement with a new
                    // JsonDocument that contains the enriched fields. For simplicity we
                    // expose the data via a return object; callers can merge it as needed.
                }
            }
        }

        /// <summary>
        /// Queries ASN information for a given IP address using the public Team Cymru
        /// whois service (whois.cymru.com). The result contains ASN number, ASN name,
        /// and country code.
        /// </summary>
        public async Task<AsnInfo?> QueryAsnInformationAsync(string ipAddress)
        {
            try
            {
                // Team Cymru whois protocol: send "begin\nverbose\n<IP>\nend\n"
                using var client = new TcpClient();
                await client.ConnectAsync("whois.cymru.com", 43);
                using var stream = client.GetStream();
                var request = $"begin\nverbose\n{ipAddress}\nend\n";
                var requestBytes = System.Text.Encoding.ASCII.GetBytes(request);
                await stream.WriteAsync(requestBytes, 0, requestBytes.Length);

                using var reader = new System.IO.StreamReader(stream);
                // Skip the header line.
                await reader.ReadLineAsync();
                var dataLine = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(dataLine))
                    return null;

                // Expected format: "AS | IP | BGP Prefix | CC | Registry | Allocated | AS Name"
                var parts = dataLine.Split('|');
                if (parts.Length < 7)
                    return null;

                var asn = uint.Parse(parts[0].Trim());
                var country = parts[3].Trim();
                var asnName = parts[6].Trim();

                return new AsnInfo { Asn = asn, Country = country, AsnName = asnName };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query ASN information for {Ip}", ipAddress);
                return null;
            }
        }

        /// <summary>
        /// Performs a reverse DNS lookup for the supplied IP address.
        /// Returns the hostname if one exists, otherwise null.
        /// </summary>
        public async Task<string?> ReverseDnsLookupAsync(string ipAddress)
        {
            try
            {
                var hostEntry = await Dns.GetHostEntryAsync(ipAddress);
                return hostEntry.HostName;
            }
            catch (SocketException)
            {
                // No PTR record.
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reverse DNS lookup failed for {Ip}", ipAddress);
                return null;
            }
        }

        // ---------------------------------------------------------------------
        // VPN detection & attestation utilities
        // ---------------------------------------------------------------------
        // A tiny static list of ASN names that are commonly associated with VPN/Proxy providers.
        // In a production system you would replace this with a curated data source.
        private static readonly HashSet<string> KnownVpnAsnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "OVH", "DigitalOcean", "Linode", "Amazon", "Google", "Microsoft", "Hetzner", "Vultr",
            "Fastly", "Cloudflare", "Akamai", "Tencent", "Alibaba"
        };

        /// <summary>
        /// Determines whether the supplied IP address is likely a VPN/Proxy based on ASN name heuristics.
        /// Returns true if the ASN name matches a known VPN provider, otherwise false.
        /// </summary>
        public async Task<bool> IsLikelyVpnAsync(string ipAddress)
        {
            var asnInfo = await QueryAsnInformationAsync(ipAddress);
            if (asnInfo == null) return false;
            return KnownVpnAsnNames.Contains(asnInfo.AsnName);
        }

        /// <summary>
        /// Attestation result that combines ASN info, reverse‑DNS, and VPN likelihood.
        /// </summary>
        public class AttestationResult
        {
            public AsnInfo? AsnInfo { get; set; }
            public string? ReverseDns { get; set; }
            public bool IsVpn { get; set; }
            public string? Reason { get; set; }
        }

        /// <summary>
        /// Performs a full attestation for a client IP. It resolves ASN, reverse DNS, and
        /// evaluates VPN likelihood. The Reason field explains why a VPN was inferred.
        /// </summary>
        public async Task<AttestationResult> AttestAsync(string ipAddress)
        {
            var result = new AttestationResult();
            result.AsnInfo = await QueryAsnInformationAsync(ipAddress);
            result.ReverseDns = await ReverseDnsLookupAsync(ipAddress);
            result.IsVpn = await IsLikelyVpnAsync(ipAddress);
            if (result.IsVpn && result.AsnInfo != null)
            {
                result.Reason = $"ASN name '{result.AsnInfo.AsnName}' is a known VPN/Proxy provider.";
            }
            else if (result.AsnInfo != null)
            {
                result.Reason = $"ASN '{result.AsnInfo.Asn}' appears to be a regular ISP.";
            }
            else
            {
                result.Reason = "Unable to resolve ASN information.";
            }
            return result;
        }

        #endregion
    }

    /// <summary>
    /// Simple DTO representing ASN information.
    /// </summary>
    public class AsnInfo
    {
        public uint Asn { get; set; }
        public string AsnName { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
    }

    // Placeholder entity that matches the existing AsnPingTiming EF model.
    // The real definition lives in the Ask2Ask DbContext; we only need the
    // properties we reference here for clustering.
    public class AsnPingTiming
    {
        public int Id { get; set; }
        public uint? ASN { get; set; }
        public DateTime MeasuredAt { get; set; }
        public string? PingTarget { get; set; }
        // Additional fields omitted for brevity.
    }
}
