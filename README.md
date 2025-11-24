# Ask2Ask – For when ya didnt ask but really wanna know
# Fingerprints, Pings & Zero-Knowledge Witchcraft 🕵️‍♂️

Ask2Ask is an ASP.NET Core service for when **“who hit this URL?”** isn’t good enough.

It turns a simple hit into a **rich visitor profile**:

- Deep **browser fingerprinting**
- **ASN ping timing** & rough location inference
- **VPN / proxy suspicion** scoring
- **CNSA-grade API keys** with ECDSA P-384 + SHA-384
- JSON / NDJSON / Elasticsearch-ready **exports**

Think: fraud hunting, investigation support, and cross-session correlation without hoarding passwords or exotic PII.

---

## What It Actually Does

### 🧬 1. Builds a high-entropy browser fingerprint

Every visit gets rolled up into a **stable fingerprint hash** and a detailed record:

- User agent, platform, screen, languages, timezones
- Canvas / audio / WebGL / WebGPU fingerprints
- Storage & runtime capabilities (cookies, local/session storage, IndexedDB, WASM, service workers)
- Device hints (input devices, media devices, battery, etc.)

You get **“this is probably the same human/machine”** without tying it to a name.

**Relevant bits:**  
`TrackingDbContext.cs`, `TrackingService.cs`  
`wwwroot/js/tracking.js`  
`wwwroot/js/advanced-fingerprinting.js`  
`wwwroot/js/novel-fingerprinting-2025.js`

---

### 🌐 2. Calls out VPNs, proxies & weird paths

Each visit gets a `VPNProxyDetection` record:

- ASN, ISP, datacenter vs residential
- Tor / localhost / private IP flags
- Header hints: `Via`, `X-Forwarded-For`, hop count
- A normalized **suspicion level** and “likely VPN/proxy” flag

Over time you can see a visitor’s **VPN history** – which providers they bounce through, and how “normal” they look.

**Relevant bits:**  
`TrackingDbContext.cs (VPNProxyDetection)`  
`TrackingService.cs`

---

### 📡 3. Plays timing games with ASNs

Ask2Ask runs **multi-ASN ping timing** and keeps the receipts:

- Min / avg / max latency, jitter, success/failure
- **Location inference** from timing (`InferredLatitude`, `InferredLongitude`, `LocationConfidence`)
- **Pattern similarity** across visits:
  - `PatternSimilarity`, `MatchingASNs`, `AverageDeviation`
- Flags when someone is clearly **behind a VPN** vs likely “true” region

Useful when you want more than “they used Cloudflare” and closer to “this is how they sit on the map and move over time.”

**Relevant bits:**  
`AsnPingTiming.cs`, `AsnPingTimingService.cs`  
`AsnHelperService.cs`  
`wwwroot/js/asn-ping-timing.js`

---

### 🔐 4. CNSA-style API keys & ZKP-ish signatures

The API isn’t “just slap a token header on it”:

- **API keys**
  - 512-bit random secrets, stored only as **SHA-384** hashes
  - Per-key scopes: `read`, `export`, or `"*"` for god-mode
  - Built-in rate limiting (general vs export-heavy endpoints)

- **Signed requests**
  - Client signs:  
    `METHOD | PATH | body_hash | timestamp | nonce`
  - Uses **ECDSA P-384 + SHA-384** (CNSA 2.0 compatible)
  - Server verifies with the stored public key
  - Timestamp + nonce checked to kill replays

- **Middleware**
  - `X-Api-Key` for all `/api/*`
  - `X-Signature`, `X-Timestamp`, `X-Nonce` for `/api/export` & admin paths
  - Drops in some basic security headers too

**Relevant bits:**  
`ApiAuthenticationService.cs`, `ZkpAuthenticationService.cs`  
`Middleware/ApiAuthenticationMiddleware.cs`  
`appsettings.Api.json`  
`scripts/generate-api-keys.sh`, `scripts/generate-zkp-keypair.sh`  
`test-zkp.py`, `test-zkp-auth.sh`, `test-api.sh`

---

### 📊 5. Export the evidence (JSON, NDJSON, Elasticsearch bulk)

You get a small, sharp API surface:

- `GET /api/stats`  
  High-level stats: visitors, visits, VPN hits, etc.  
  **Requires:** `read` scope.

- `GET /api/visits?page=&pageSize=`  
  Paginated visit feed with fingerprint + VPN metadata.  
  **Requires:** `read` scope.

- `GET /api/visitor?hash=<fingerprintHash>`  
  Full visitor drill-down (visits + VPN history + timing).  
  **Requires:** `read` scope.

- `GET /api/export?format=ndjson|json|bulk&since=YYYY-MM-DD`  
  Machine-friendly exports:
  - `ndjson` – one JSON per line
  - `json` – array of records
  - `bulk` – Elasticsearch bulk format (action + doc)  

  **Requires:** `export` scope **and** a ZKP-protected key.

**Relevant bits:**  
`Pages/Api/Stats.cshtml(.cs)`  
`Pages/Api/Visits.cshtml(.cs)`  
`Pages/Api/Visitor.cshtml(.cs)`  
`Pages/Api/Export.cshtml(.cs)`

---

## Running It

### 1. Clone

```bash
git clone https://github.com/SWORDIntel/ask2ask.com.git
cd ask2ask.com
````

### 2. Generate keys

```bash
# CNSA-style API keys
./scripts/generate-api-keys.sh

# ECDSA P-384 keypair for ZKP auth
./scripts/generate-zkp-keypair.sh
```

Drop the generated JSON into `appsettings.Api.json` under `ApiKeys`, set scopes/labels to taste.

### 3. Docker mode (recommended)

```bash
docker compose up --build -d
```

You get:

* App on `http://localhost:5000`
* Redis cache
* SQLite in a local volume

### 4. Kick the tyres

```bash
./test-api.sh        # basic API key tests
./test-zkp-auth.sh   # signed export request tests
```

---

## Dev Without Docker

```bash
dotnet restore
dotnet build
dotnet run
```

Make sure Redis is reachable or tweak `appsettings*.json` accordingly.

---

## Where It Fits

Use Ask2Ask when you need:

* **Fraud / abuse** triage with more signal than “one IP, one UA”
* **Investigation tooling** to correlate “anonymous” hits across time and infra
* **Exportable telemetry** you can feed into your own data lake / SIEM / ML pipeline

It doesn’t try to be a dashboard product. It’s the **sensor and attester** you plug into your own stack.

---

## License & Contributions

Licensed under **MIT**.

Issues, PRs, and strange use-cases are welcome. If you wire this into a wild fraud-hunting pipeline, documenting it here is highly encouraged.

```
::contentReference[oaicite:0]{index=0}
```
