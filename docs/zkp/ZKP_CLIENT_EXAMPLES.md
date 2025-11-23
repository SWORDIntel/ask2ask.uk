# ZKP Client Examples
## Zero Knowledge Proof Authentication for Ask2Ask API

This document provides examples of how to authenticate API requests using Zero Knowledge Proof (ZKP) signatures.

## Overview

ZKP authentication uses ECDSA P-384 signatures with SHA-384 hashing (CNSA 2.0 compliant) to prove identity without revealing secrets. The signature covers:
- HTTP method
- Request path
- Request body hash
- Timestamp
- Nonce

## Prerequisites

1. **API Key**: Your API key secret
2. **Private Key**: ECDSA P-384 private key (base64 encoded)
3. **Public Key**: ECDSA P-384 public key (must be registered in `appsettings.Api.json`)

## C# Example

```csharp
using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

public class ZkpApiClient
{
    private readonly string _apiKey;
    private readonly string _privateKeyBase64;
    private readonly string _baseUrl;
    private readonly HttpClient _httpClient;

    public ZkpApiClient(string apiKey, string privateKeyBase64, string baseUrl = "https://api.ask2ask.com")
    {
        _apiKey = apiKey;
        _privateKeyBase64 = privateKeyBase64;
        _baseUrl = baseUrl;
        _httpClient = new HttpClient();
    }

    public async Task<string> MakeRequestAsync(string method, string path, string? body = null)
    {
        // Generate timestamp and nonce
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = Guid.NewGuid().ToString("N");

        // Create signature
        var signature = SignRequest(method, path, body, timestamp, nonce);

        // Create request
        var request = new HttpRequestMessage(new HttpMethod(method), $"{_baseUrl}{path}");
        request.Headers.Add("X-API-Key", _apiKey);
        request.Headers.Add("X-Signature", signature);
        request.Headers.Add("X-Timestamp", timestamp.ToString());
        request.Headers.Add("X-Nonce", nonce);

        if (!string.IsNullOrEmpty(body))
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        // Send request
        var response = await _httpClient.SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }

    private string SignRequest(string method, string path, string? body, long timestamp, string nonce)
    {
        // Compute body hash
        var bodyHash = string.IsNullOrEmpty(body)
            ? string.Empty
            : ComputeSHA384Hash(body);

        // Create message: method|path|bodyHash|timestamp|nonce
        var message = $"{method}|{path}|{bodyHash}|{timestamp}|{nonce}";
        var messageBytes = Encoding.UTF8.GetBytes(message);

        // Sign with ECDSA P-384
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(_privateKeyBase64), out _);
        
        var signature = ecdsa.SignData(messageBytes, HashAlgorithmName.SHA384);
        return Convert.ToBase64String(signature);
    }

    private static string ComputeSHA384Hash(string input)
    {
        using var sha384 = SHA384.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha384.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}

// Usage example
var client = new ZkpApiClient(
    apiKey: "your-api-key",
    privateKeyBase64: "your-private-key-base64"
);

// Make a request to /api/stats
var response = await client.MakeRequestAsync("GET", "/api/stats");
Console.WriteLine(response);

// Make a request to /api/export
var exportResponse = await client.MakeRequestAsync("GET", "/api/export?format=json&limit=10");
Console.WriteLine(exportResponse);
```

## Python Example

```python
import hashlib
import base64
import time
import uuid
import requests
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec
from cryptography.hazmat.backends import default_backend

class ZkpApiClient:
    def __init__(self, api_key, private_key_base64, base_url="https://api.ask2ask.com"):
        self.api_key = api_key
        self.base_url = base_url
        self.private_key = serialization.load_der_private_key(
            base64.b64decode(private_key_base64),
            password=None,
            backend=default_backend()
        )

    def compute_sha384_hash(self, data):
        """Compute SHA-384 hash (CNSA 2.0 compliant)"""
        return base64.b64encode(
            hashlib.sha384(data.encode('utf-8')).digest()
        ).decode('utf-8')

    def sign_request(self, method, path, body, timestamp, nonce):
        """Create ECDSA P-384 signature"""
        # Compute body hash
        body_hash = self.compute_sha384_hash(body) if body else ""
        
        # Create message: method|path|bodyHash|timestamp|nonce
        message = f"{method}|{path}|{body_hash}|{timestamp}|{nonce}"
        
        # Sign with ECDSA P-384
        signature = self.private_key.sign(
            message.encode('utf-8'),
            ec.ECDSA(hashes.SHA384())
        )
        
        return base64.b64encode(signature).decode('utf-8')

    def make_request(self, method, path, body=None):
        """Make authenticated API request"""
        # Generate timestamp and nonce
        timestamp = int(time.time())
        nonce = str(uuid.uuid4()).replace('-', '')
        
        # Create signature
        signature = self.sign_request(method, path, body or "", timestamp, nonce)
        
        # Prepare headers
        headers = {
            "X-API-Key": self.api_key,
            "X-Signature": signature,
            "X-Timestamp": str(timestamp),
            "X-Nonce": nonce
        }
        
        # Make request
        url = f"{self.base_url}{path}"
        if method == "GET":
            response = requests.get(url, headers=headers)
        elif method == "POST":
            headers["Content-Type"] = "application/json"
            response = requests.post(url, headers=headers, data=body)
        else:
            raise ValueError(f"Unsupported method: {method}")
        
        return response.text

# Usage example
client = ZkpApiClient(
    api_key="your-api-key",
    private_key_base64="your-private-key-base64"
)

# Make a request to /api/stats
response = client.make_request("GET", "/api/stats")
print(response)

# Make a request to /api/export
response = client.make_request("GET", "/api/export?format=json&limit=10")
print(response)
```

## Bash/cURL Example

```bash
#!/bin/bash
# ZKP Authentication Example using OpenSSL and cURL

API_KEY="your-api-key"
PRIVATE_KEY_FILE="private-key.pem"
BASE_URL="https://api.ask2ask.com"

# Function to compute SHA-384 hash
compute_sha384() {
    echo -n "$1" | openssl dgst -sha384 -binary | base64 -w 0
}

# Function to sign request
sign_request() {
    local method=$1
    local path=$2
    local body=$3
    local timestamp=$4
    local nonce=$5
    
    # Compute body hash
    local body_hash=""
    if [ -n "$body" ]; then
        body_hash=$(compute_sha384 "$body")
    fi
    
    # Create message: method|path|bodyHash|timestamp|nonce
    local message="${method}|${path}|${body_hash}|${timestamp}|${nonce}"
    
    # Sign with ECDSA P-384
    echo -n "$message" | openssl dgst -sha384 -sign "$PRIVATE_KEY_FILE" | base64 -w 0
}

# Example: GET /api/stats
METHOD="GET"
PATH="/api/stats"
TIMESTAMP=$(date +%s)
NONCE=$(uuidgen | tr -d '-')
SIGNATURE=$(sign_request "$METHOD" "$PATH" "" "$TIMESTAMP" "$NONCE")

curl -X "$METHOD" "${BASE_URL}${PATH}" \
  -H "X-API-Key: $API_KEY" \
  -H "X-Signature: $SIGNATURE" \
  -H "X-Timestamp: $TIMESTAMP" \
  -H "X-Nonce: $NONCE"

# Example: GET /api/export
METHOD="GET"
PATH="/api/export?format=json&limit=10"
TIMESTAMP=$(date +%s)
NONCE=$(uuidgen | tr -d '-')
SIGNATURE=$(sign_request "$METHOD" "$PATH" "" "$TIMESTAMP" "$NONCE")

curl -X "$METHOD" "${BASE_URL}${PATH}" \
  -H "X-API-Key: $API_KEY" \
  -H "X-Signature: $SIGNATURE" \
  -H "X-Timestamp: $TIMESTAMP" \
  -H "X-Nonce: $NONCE"
```

## JavaScript/Node.js Example

```javascript
const crypto = require('crypto');
const https = require('https');

class ZkpApiClient {
    constructor(apiKey, privateKeyBase64, baseUrl = 'https://api.ask2ask.com') {
        this.apiKey = apiKey;
        this.baseUrl = baseUrl;
        this.privateKey = crypto.createPrivateKey({
            key: Buffer.from(privateKeyBase64, 'base64'),
            format: 'der',
            type: 'pkcs8'
        });
    }

    computeSHA384Hash(input) {
        return crypto.createHash('sha384').update(input).digest('base64');
    }

    signRequest(method, path, body, timestamp, nonce) {
        // Compute body hash
        const bodyHash = body ? this.computeSHA384Hash(body) : '';
        
        // Create message: method|path|bodyHash|timestamp|nonce
        const message = `${method}|${path}|${bodyHash}|${timestamp}|${nonce}`;
        
        // Sign with ECDSA P-384
        const sign = crypto.createSign('SHA384');
        sign.update(message);
        const signature = sign.sign(this.privateKey, 'base64');
        
        return signature;
    }

    async makeRequest(method, path, body = null) {
        const timestamp = Math.floor(Date.now() / 1000);
        const nonce = crypto.randomUUID().replace(/-/g, '');
        const signature = this.signRequest(method, path, body, timestamp, nonce);
        
        const headers = {
            'X-API-Key': this.apiKey,
            'X-Signature': signature,
            'X-Timestamp': timestamp.toString(),
            'X-Nonce': nonce
        };
        
        if (body) {
            headers['Content-Type'] = 'application/json';
        }
        
        return new Promise((resolve, reject) => {
            const url = new URL(path, this.baseUrl);
            const options = {
                hostname: url.hostname,
                port: url.port || 443,
                path: url.pathname + url.search,
                method: method,
                headers: headers
            };
            
            const req = https.request(options, (res) => {
                let data = '';
                res.on('data', (chunk) => { data += chunk; });
                res.on('end', () => resolve(data));
            });
            
            req.on('error', reject);
            
            if (body) {
                req.write(body);
            }
            
            req.end();
        });
    }
}

// Usage example
const client = new ZkpApiClient(
    'your-api-key',
    'your-private-key-base64'
);

// Make a request to /api/stats
client.makeRequest('GET', '/api/stats')
    .then(response => console.log(response))
    .catch(error => console.error(error));

// Make a request to /api/export
client.makeRequest('GET', '/api/export?format=json&limit=10')
    .then(response => console.log(response))
    .catch(error => console.error(error));
```

## Key Generation

Generate a key pair using the provided script:

```bash
cd /path/to/ask2ask.com
bash scripts/generate-zkp-keypair.sh
```

This will generate:
- `private-key.pem` - Keep secret, use for signing requests
- `public-key.pem` - Add to `appsettings.Api.json` as `PublicKey`

## Security Notes

1. **Never share your private key** - Keep it secure
2. **Rotate keys regularly** - Generate new key pairs periodically
3. **Use HTTPS** - Always use TLS 1.3 in production
4. **Validate timestamps** - Server checks ±5 minute window
5. **Unique nonces** - Each request must have a unique nonce
6. **Request integrity** - Signature covers full request including body

## Troubleshooting

### "Invalid ZKP signature"
- Check that private key matches public key in config
- Verify timestamp is within ±5 minutes
- Ensure nonce is unique
- Check that signature covers correct message format

### "Public key not configured"
- Ensure `PublicKey` field is set in `appsettings.Api.json`
- Verify public key is base64 encoded correctly
- Check that API key matches the one in config

### "Timestamp out of window"
- Ensure system clock is synchronized (use NTP)
- Check timestamp is Unix epoch seconds (not milliseconds)