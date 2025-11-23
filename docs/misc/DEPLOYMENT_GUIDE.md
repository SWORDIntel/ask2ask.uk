# Ask2Ask Deployment Guide
## Secure Production Deployment with CNSA 2.0 Compliance

---

## Overview

This guide covers deploying Ask2Ask with:
- ✅ Dual network architecture (public + telemetry)
- ✅ CNSA 2.0 compliant API endpoints
- ✅ mTLS authentication for sensitive endpoints
- ✅ Automatic HTTPS with Let's Encrypt
- ✅ Rate limiting and security headers
- ✅ Database export for Elasticsearch integration

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                         INTERNET                             │
└────────────────┬───────────────────────┬────────────────────┘
                 │                       │
         ┌───────▼────────┐      ┌──────▼──────────┐
         │  ask2ask.com   │      │ api.ask2ask.com │
         │   (Public)     │      │  (Telemetry)    │
         └───────┬────────┘      └──────┬──────────┘
                 │                      │
         ┌───────▼────────────────────  ▼──────────┐
         │         Caddy Reverse Proxy             │
         │  - Auto HTTPS (Let's Encrypt)           │
         │  - Security Headers                     │
         │  - Rate Limiting                        │
         │  - mTLS for API endpoints               │
         └───────┬────────────────────┬────────────┘
                 │                    │
    ┌────────────▼──────┐    ┌────────▼─────────┐
    │  Public Network   │    │ Telemetry Network│
    │  172.20.0.0/16    │    │  10.10.0.0/24    │
    │  (Web Traffic)    │    │  (API/Internal)  │
    └────────┬──────────┘    └────────┬─────────┘
             │                        │
         ┌───▼────────────────────────▼───┐
         │      ASP.NET Core App          │
         │  - Tracking System             │
         │  - API Endpoints               │
         │  - SQLite Database             │
         └────────────────────────────────┘
```

---

## Prerequisites

### Server Requirements
- **OS**: Linux (Ubuntu 22.04+ recommended)
- **RAM**: 2GB minimum, 4GB recommended
- **Storage**: 20GB minimum
- **Docker**: 20.10+
- **Docker Compose**: 2.0+

### Domain Setup
- Domain name (e.g., `ask2ask.com`)
- DNS A records:
  - `ask2ask.com` → Server IP
  - `api.ask2ask.com` → Server IP
  - `telemetry.ask2ask.com` → Server IP (optional)

### Firewall
- Port 80 (HTTP) - Open
- Port 443 (HTTPS) - Open
- Port 22 (SSH) - Restricted to your IP

---

## Step 1: Initial Setup

### 1.1 Clone Repository

```bash
cd /opt
git clone https://github.com/yourusername/ask2ask.com.git
cd ask2ask.com
```

### 1.2 Generate API Keys

```bash
bash scripts/generate-api-keys.sh > api-keys.txt
cat api-keys.txt
```

Save these keys securely! You'll need them for API access.

### 1.3 Update API Configuration

Edit `appsettings.Api.json`:

```bash
nano appsettings.Api.json
```

Replace the placeholder keys with your generated keys:

```json
{
  "ApiKeys": [
    {
      "Key": "paste-read-key-here",
      "Scopes": ["read"],
      "Description": "Read-only access"
    },
    {
      "Key": "paste-export-key-here",
      "Scopes": ["read", "export"],
      "Description": "Export access"
    },
    {
      "Key": "paste-admin-key-here",
      "Scopes": ["*"],
      "Description": "Admin access"
    }
  ]
}
```

---

## Step 2: Generate mTLS Certificates

### 2.1 Create Certificates Directory

```bash
mkdir -p certs
cd certs
```

### 2.2 Generate CA Certificate

```bash
openssl req -x509 -newkey rsa:4096 -sha384 -days 3650 -nodes \
  -keyout ca-key.pem -out ca.crt \
  -subj '/CN=Ask2Ask API CA/O=Ask2Ask/C=UK'
```

**⚠️ IMPORTANT**: Store `ca-key.pem` offline and encrypted. Never commit to git!

### 2.3 Generate Client Certificate

```bash
# Generate client key and CSR
openssl req -newkey rsa:4096 -sha384 -nodes \
  -keyout client-key.pem -out client-req.pem \
  -subj '/CN=API Client/O=Ask2Ask/C=UK'

# Sign with CA
openssl x509 -req -in client-req.pem -days 365 -sha384 \
  -CA ca.crt -CAkey ca-key.pem -CAcreateserial \
  -out client-cert.pem

# Verify
openssl verify -CAfile ca.crt client-cert.pem
```

### 2.4 Get Certificate Thumbprint

```bash
openssl x509 -in client-cert.pem -outform DER | \
  openssl dgst -sha384 -binary | base64
```

Add this thumbprint to `appsettings.Api.json`:

```json
{
  "AllowedCertificateThumbprints": [
    "paste-thumbprint-here"
  ]
}
```

### 2.5 Secure Certificates

```bash
cd ..
chmod 600 certs/ca-key.pem certs/client-key.pem
chmod 644 certs/ca.crt certs/client-cert.pem
```

---

## Step 3: Configure Production Environment

### 3.1 Update docker-compose.yml

```bash
nano docker-compose.yml
```

Change Caddyfile and enable certificate mounting:

```yaml
caddy:
  volumes:
    - ./Caddyfile.production:/etc/caddy/Caddyfile:ro  # Production config
    - ./certs/ca.crt:/etc/caddy/certs/ca.crt:ro       # CA certificate
    - caddy-data:/data
    - caddy-config:/config
```

Set telemetry network to internal:

```yaml
networks:
  telemetry-network:
    driver: bridge
    internal: true  # Fully isolate from internet
    ipam:
      config:
        - subnet: 10.10.0.0/24
```

### 3.2 Update Caddyfile.production

```bash
nano Caddyfile.production
```

Verify domain names are correct:

```caddyfile
ask2ask.com {
    # Main site configuration
}

api.ask2ask.com {
    # API configuration with mTLS
}
```

### 3.3 Update appsettings.Production.json

```bash
nano appsettings.Production.json
```

Ensure correct domain:

```json
{
  "AllowedHosts": "ask2ask.com,api.ask2ask.com,localhost,ask2ask-app"
}
```

---

## Step 4: Deploy

### 4.1 Build and Start

```bash
docker-compose down
docker-compose up -d --build
```

### 4.2 Monitor Logs

```bash
# App logs
docker logs -f ask2ask-app

# Caddy logs
docker logs -f ask2ask-caddy
```

### 4.3 Verify Health

```bash
# Check containers
docker ps

# Check networks
docker network ls
docker network inspect ask2askcom_telemetry-network

# Check volumes
docker volume ls
```

---

## Step 5: Verify Deployment

### 5.1 Test Public Site

```bash
curl -I https://ask2ask.com
```

Expected: `200 OK` with security headers

### 5.2 Test API (from server)

```bash
# Stats endpoint
curl "https://api.ask2ask.com/api/stats" \
  -H "X-API-Key: your-read-key"

# Export endpoint (requires mTLS)
curl "https://api.ask2ask.com/api/export?format=json&limit=1" \
  -H "X-API-Key: your-export-key" \
  --cert certs/client-cert.pem \
  --key certs/client-key.pem \
  --cacert certs/ca.crt
```

### 5.3 Test from External Client

On your local machine:

```bash
# Copy client certificates
scp user@server:/opt/ask2ask.com/certs/client-cert.pem .
scp user@server:/opt/ask2ask.com/certs/client-key.pem .
scp user@server:/opt/ask2ask.com/certs/ca.crt .

# Test API
curl "https://api.ask2ask.com/api/stats" \
  -H "X-API-Key: your-read-key"
```

---

## Step 6: Elasticsearch Integration

### 6.1 Export Data

```bash
curl "https://api.ask2ask.com/api/export?format=bulk" \
  -H "X-API-Key: your-export-key" \
  --cert certs/client-cert.pem \
  --key certs/client-key.pem \
  --cacert certs/ca.crt \
  -o tracking-export.ndjson
```

### 6.2 Import to Elasticsearch

```bash
curl -X POST "https://your-elasticsearch:9200/_bulk" \
  -H "Content-Type: application/x-ndjson" \
  --data-binary @tracking-export.ndjson
```

### 6.3 Automated Sync (Cron Job)

```bash
crontab -e
```

Add:

```cron
# Export to Elasticsearch every hour
0 * * * * /opt/ask2ask.com/scripts/sync-to-elasticsearch.sh
```

Create sync script:

```bash
nano scripts/sync-to-elasticsearch.sh
```

```bash
#!/bin/bash
EXPORT_KEY="your-export-key"
ES_URL="https://your-elasticsearch:9200"

curl -s "https://api.ask2ask.com/api/export?format=bulk&since=$(date -u -d '1 hour ago' +%Y-%m-%dT%H:%M:%S)" \
  -H "X-API-Key: $EXPORT_KEY" \
  --cert /opt/ask2ask.com/certs/client-cert.pem \
  --key /opt/ask2ask.com/certs/client-key.pem \
  --cacert /opt/ask2ask.com/certs/ca.crt | \
curl -s -X POST "$ES_URL/_bulk" \
  -H "Content-Type: application/x-ndjson" \
  --data-binary @-
```

```bash
chmod +x scripts/sync-to-elasticsearch.sh
```

---

## Step 7: Monitoring & Maintenance

### 7.1 Log Rotation

```bash
# Configure Docker log rotation
cat > /etc/docker/daemon.json <<EOF
{
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "10m",
    "max-file": "3"
  }
}
EOF

systemctl restart docker
```

### 7.2 Database Backups

```bash
# Backup script
nano scripts/backup-database.sh
```

```bash
#!/bin/bash
BACKUP_DIR="/opt/backups/ask2ask"
DATE=$(date +%Y%m%d-%H%M%S)

mkdir -p $BACKUP_DIR

# Backup database
docker exec ask2ask-app cat /app/TrackingData/tracking.db > \
  $BACKUP_DIR/tracking-$DATE.db

# Compress
gzip $BACKUP_DIR/tracking-$DATE.db

# Keep only last 30 days
find $BACKUP_DIR -name "tracking-*.db.gz" -mtime +30 -delete
```

```bash
chmod +x scripts/backup-database.sh
```

Add to cron:

```cron
# Daily backup at 2 AM
0 2 * * * /opt/ask2ask.com/scripts/backup-database.sh
```

### 7.3 Health Checks

```bash
# Create health check script
nano scripts/health-check.sh
```

```bash
#!/bin/bash
WEBHOOK_URL="your-slack-webhook-url"

# Check if containers are running
if ! docker ps | grep -q ask2ask-app; then
    curl -X POST $WEBHOOK_URL -d '{"text":"❌ ask2ask-app is down!"}'
fi

if ! docker ps | grep -q ask2ask-caddy; then
    curl -X POST $WEBHOOK_URL -d '{"text":"❌ ask2ask-caddy is down!"}'
fi

# Check if site is accessible
if ! curl -s -o /dev/null -w "%{http_code}" https://ask2ask.com | grep -q 200; then
    curl -X POST $WEBHOOK_URL -d '{"text":"❌ ask2ask.com is not responding!"}'
fi
```

```bash
chmod +x scripts/health-check.sh
```

Add to cron:

```cron
# Health check every 5 minutes
*/5 * * * * /opt/ask2ask.com/scripts/health-check.sh
```

---

## Step 8: Security Hardening

### 8.1 Firewall Configuration

```bash
# Install UFW
apt-get install ufw

# Default policies
ufw default deny incoming
ufw default allow outgoing

# Allow SSH (change port if needed)
ufw allow 22/tcp

# Allow HTTP/HTTPS
ufw allow 80/tcp
ufw allow 443/tcp

# Enable
ufw enable
```

### 8.2 Fail2Ban

```bash
# Install
apt-get install fail2ban

# Configure
cat > /etc/fail2ban/jail.local <<EOF
[DEFAULT]
bantime = 3600
findtime = 600
maxretry = 5

[sshd]
enabled = true
EOF

systemctl restart fail2ban
```

### 8.3 Automatic Updates

```bash
apt-get install unattended-upgrades
dpkg-reconfigure -plow unattended-upgrades
```

---

## Troubleshooting

### Issue: Containers won't start

```bash
# Check logs
docker-compose logs

# Check disk space
df -h

# Check Docker status
systemctl status docker
```

### Issue: Can't access API

```bash
# Verify network
docker network inspect ask2askcom_telemetry-network

# Check if telemetry network is internal
# If yes, API only accessible from within Docker network

# Test from app container
docker exec -it ask2ask-app curl http://localhost:8080/api/stats \
  -H "X-API-Key: your-key"
```

### Issue: mTLS not working

```bash
# Verify CA cert is mounted
docker exec ask2ask-caddy ls -la /etc/caddy/certs/

# Check certificate validity
openssl x509 -in certs/client-cert.pem -noout -dates

# Verify thumbprint
openssl x509 -in certs/client-cert.pem -outform DER | \
  openssl dgst -sha384 -binary | base64
```

### Issue: Rate limiting too aggressive

Edit `Services/ApiAuthenticationService.cs`:

```csharp
if (requestCount >= 1000)  // Increase from 100
{
    return true;
}
```

Rebuild:

```bash
docker-compose up -d --build
```

---

## Updating

### Update Application

```bash
cd /opt/ask2ask.com
git pull
docker-compose down
docker-compose up -d --build
```

### Update Certificates (Annual)

```bash
cd /opt/ask2ask.com/certs

# Generate new client cert
openssl req -newkey rsa:4096 -sha384 -nodes \
  -keyout client-key-new.pem -out client-req-new.pem \
  -subj '/CN=API Client/O=Ask2Ask/C=UK'

openssl x509 -req -in client-req-new.pem -days 365 -sha384 \
  -CA ca.crt -CAkey ca-key.pem -CAcreateserial \
  -out client-cert-new.pem

# Update thumbprint in appsettings.Api.json
# Replace old certs
mv client-cert-new.pem client-cert.pem
mv client-key-new.pem client-key.pem

# Restart
docker-compose restart
```

---

## Production Checklist

- [ ] DNS records configured
- [ ] API keys generated and stored securely
- [ ] mTLS certificates generated
- [ ] Certificate thumbprints added to config
- [ ] docker-compose.yml updated for production
- [ ] Caddyfile.production configured
- [ ] Telemetry network set to internal
- [ ] Firewall configured
- [ ] Fail2Ban installed
- [ ] Log rotation configured
- [ ] Database backup cron job created
- [ ] Health check monitoring configured
- [ ] Elasticsearch integration tested
- [ ] SSL certificates verified (Let\'s Encrypt)
- [ ] API endpoints tested with mTLS
- [ ] Rate limiting verified
- [ ] Security headers verified

---

## Support

For issues:
1. Check logs: `docker-compose logs`
2. Review documentation: `API_DOCUMENTATION.md`
3. Verify configuration: `docker-compose config`

---

**Production Ready | CNSA 2.0 Compliant | Secure by Design**