# Ask2Ask – ASN‑Enriched Ping Timing Service

## 📖 Overview
Ask2Ask is a C# ASP.NET Core web application that records ping‑timing measurements, enriches them with **ASN information**, performs **VPN detection**, and stores the data for later correlation.  The project includes:

- **`AsnHelperService`** – queries Team Cymru whois for ASN data, does reverse‑DNS look‑ups, caches results (Redis → SQLite), and detects VPNs using a dynamic CSV‑based list.
- **`AsnPingTimingService`** – stores raw ping measurements and now enriches each record with ASN, country, region, and VPN flag.
- **`AttestationController`** – a secured API endpoint (`/api/attest?ip=<IP>`) that returns an `AttestationResult` JSON payload.
- **Docker support** – a single `docker‑compose up` command spins up the web app, a Redis cache, and persists SQLite data.

The UI (Razor pages) already displays a banner that can switch between WebP/GIF/PNG and a default embossed SVG.

---

## 🛠️ Prerequisites
- **Docker Engine** (>= 20.10) and **Docker Compose** (v2).  No local .NET SDK is required – everything builds inside Docker.
- (Optional) `git` to clone the repository.

---

## 🚀 Quick Start (Docker)
```bash
# 1️⃣ Clone the repo (if you haven't already)
git clone https://github.com/your‑org/ask2ask.git
cd ask2ask

# 2️⃣ Ensure the VPN‑ASN CSV exists (example path shown below)
mkdir -p Data
cat > Data/vpn-asn-list.csv <<'EOF'
AsnName
OVH
DigitalOcean
Linode
Amazon
Google
Microsoft
Hetzner
Vultr
Fastly
Cloudflare
Akamai
Tencent
Alibaba
EOF

# 3️⃣ Build and run everything with Docker Compose
docker compose up --build -d

# 4️⃣ Verify the API is reachable
curl http://localhost:5000/api/attest?ip=8.8.8.8
```
The command returns a JSON payload similar to:
```json
{
  "asnInfo": {"asn":15169,"asnName":"Google LLC","country":"US"},
  "reverseDns":"dns.google",
  "isVpn":false,
  "reason":"ASN '15169' appears to be a regular ISP."
}
```

---

## 📂 Project Structure
```
ask2ask/
├─ Controllers/                # API controllers (AttestationController.cs)
├─ Services/                  # Helper services, caching, VPN list loader
│   ├─ AsnHelperService.cs
│   ├─ ICacheService.cs
│   ├─ RedisCacheService.cs
│   ├─ SqliteCacheService.cs
│   ├─ CompositeCacheService.cs
│   └─ VpnAsnProvider.cs
├─ Pages/                     # Razor UI pages (Index.cshtml, etc.)
├─ Data/                      # CSV file with VPN‑ASN names (mounted read‑only)
├─ wwwroot/                   # Static assets (banner images, CSS, JS)
├─ appsettings.json           # Default config (connection strings can be overridden)
├─ Dockerfile                 # Multi‑stage build for the ASP.NET app
├─ docker-compose.yml          # Orchestrates app + Redis + volumes
└─ Ask2Ask.csproj
```

---

## 🔐 Security
- The **Attestation API** is protected with `[Authorize]`.  Configure your authentication scheme (JWT, Identity, etc.) in `Program.cs`.
- Redis connection string is injected via `ConnectionStrings:Redis`.  In production you should set a password (`requirepass`) and reference it through environment variables.
- HTTPS is enabled by default on port 5001 (self‑signed dev cert).  For production, terminate TLS with a reverse proxy (NGINX, Traefik, etc.).

---

## 📊 Data & Analytics
- `AsnPingCorrelation` now includes a `bool IsVpn` column.  Correlations can be queried to see how many visits originated from VPNs.
- The SQLite database lives in `./sqlite-data/ask2ask.db` on the host (mounted volume).  Use any SQLite client to explore tables (`AsnPingTimings`, `AsnPingCorrelations`, etc.).
- Redis caches ASN look‑ups for 12 hours to minimise WHOIS traffic.

---

## 🛠️ Development (outside Docker)
If you prefer to run the app locally:
```bash
# Install .NET SDK 8.0
dotnet restore
dotnet build
# Run with Kestrel (will use local Redis if configured)
dotnet run
```
Make sure you have a Redis instance reachable at `localhost:6379` or adjust `appsettings.json`.

---

## 🧪 Testing
- Unit tests can be added under a `Tests/` project referencing the services.
- Example test scenario: call `AsnHelperService.AttestAsync("8.8.8.8")` and assert `IsVpn == false`.

---

## 📦 Docker Compose Commands Cheat‑Sheet
| Command | Description |
|---------|-------------|
| `docker compose up --build -d` | Build images and start containers in background |
| `docker compose logs -f` | Follow live logs for all services |
| `docker compose down` | Stop containers (preserves volumes) |
| `docker compose down -v` | Stop containers and delete named volumes |
| `docker compose exec app bash` | Open a shell inside the ASP.NET container |

---

## 🗂️ License
This project is released under the **MIT License** – feel free to fork, modify, and use it in commercial applications.

---

## 🙋‍♀️ Contact & Contributions
- **Issues / Feature Requests**: open a GitHub issue.
- **Pull Requests**: welcome!  Please keep the code style consistent and add unit tests for new functionality.
- **Maintainer**: John Doe <john@example.com>

---

*Enjoy building smarter, VPN‑aware ping analytics with Ask2Ask!*
