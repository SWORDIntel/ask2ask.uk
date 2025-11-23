# Ask2Ask.com

A single-page ASP.NET Core website that makes fun of people who ask to ask questions instead of just asking the question.

Inspired by [nohello.com](http://nohello.com).

## ⚠️ DATA COLLECTION EXPERIMENT ⚠️

**This website includes comprehensive tracking and fingerprinting capabilities for research/educational purposes.**

### Two-Page Structure

1. **Disclosure Page (`/Disclosure`)** - Landing page with NO tracking
   - Comprehensive disclosure of all data collection
   - Informed consent mechanism (Yes/No buttons)
   - Complete transparency about what will be collected
   - No tracking scripts - completely safe to view

2. **Main Page (`/Index`)** - Tracking experiment page
   - Full tracking and fingerprinting system
   - Real-time data display for transparency
   - Only accessible after consent

All visitors **must** pass through the disclosure page first, where they receive full information about data collection and can provide explicit consent before any tracking begins.

See [TRACKING_DOCUMENTATION.md](TRACKING_DOCUMENTATION.md) for complete details.

## About

This website addresses the common (and annoying) behavior in chat rooms and forums where people ask "Can I ask a question?" instead of just asking their actual question. It's a humorous but practical guide to better online communication etiquette.

The tracking experiment serves as an educational tool to demonstrate online privacy concerns.

## Running the Application

### Prerequisites
- .NET 8.0 SDK or later

### To Run Locally

```bash
cd ask2ask
dotnet run
```

Then open your browser to `https://localhost:5001` or `http://localhost:5000`

### To Build for Production

```bash
dotnet publish -c Release -o ./publish
```

## Deployment

### Docker + Caddy (Recommended for Production)

This application is configured to run securely in Docker with Caddy as a reverse proxy, providing automatic HTTPS and comprehensive security headers.

#### Prerequisites
- Docker and Docker Compose installed
- Domain name (ask2ask.com) pointing to your server's IP address
- Ports 80 and 443 open on your firewall

#### Quick Start

1. **Clone and navigate to the project:**
   ```bash
   cd ask2ask.com
   ```

2. **Start the services:**
   ```bash
   docker-compose up -d
   ```

3. **View logs:**
   ```bash
   docker-compose logs -f
   ```

4. **Stop the services:**
   ```bash
   docker-compose down
   ```

#### Configuration

- **Caddyfile**: Configured for `ask2ask.com` with automatic HTTPS via Let's Encrypt
- **Ports**: Caddy exposes ports 80 (HTTP) and 443 (HTTPS) externally
- **Internal**: ASP.NET Core app runs on port 8080 (internal only)
- **Volumes**: 
  - `tracking-data`: Persistent storage for tracking data
  - `caddy-data`: Caddy's certificate storage
  - `caddy-config`: Caddy's configuration storage

#### Security Features

- Automatic HTTPS with Let's Encrypt certificate management
- Comprehensive security headers (HSTS, CSP, X-Frame-Options, etc.)
- Container isolation with non-root user
- Health checks and automatic restarts
- Data persistence via Docker volumes

#### Environment Variables

The following environment variables are set in `docker-compose.yml`:
- `ASPNETCORE_ENVIRONMENT=Production`
- `ASPNETCORE_URLS=http://+:8080`
- `ASPNETCORE_ALLOWEDHOSTS=ask2ask.com`

#### Updating the Application

1. **Rebuild and restart:**
   ```bash
   docker-compose up -d --build
   ```

2. **View application logs:**
   ```bash
   docker-compose logs -f ask2ask-app
   ```

3. **View Caddy logs:**
   ```bash
   docker-compose logs -f caddy
   ```

#### Other Deployment Options

This application can also be deployed to:
- Azure App Service
- IIS
- Any hosting platform that supports ASP.NET Core

## Structure

- `Program.cs` - Application entry point
- `Pages/Disclosure.cshtml` - **Landing page** with consent mechanism (NO tracking)
- `Pages/Disclosure.cshtml.cs` - Disclosure page model
- `Pages/Index.cshtml` - Main tracking experiment page
- `Pages/Index.cshtml.cs` - Page model
- `Pages/Tracking.cshtml.cs` - Tracking data endpoint (CNSA 2.0 compliant)
- `wwwroot/css/disclosure.css` - Disclosure page styling
- `wwwroot/css/site.css` - Modern Web 3.0 styling for main page
- `wwwroot/js/advanced-fingerprinting.js` - Advanced fingerprinting techniques
- `wwwroot/js/tracking.js` - Comprehensive fingerprinting system
- `Ask2Ask.csproj` - Project configuration
- `TrackingData/` - Directory for collected data (auto-created)
- `TRACKING_DOCUMENTATION.md` - Complete tracking system documentation

## Data Collection & Security

- **CNSA 2.0 Compliant**: Uses SHA-384 for data integrity
- **Planned**: ML-KEM-1024 and ML-DSA-87 (post-quantum crypto)
- **Transparent**: All data shown to users in real-time
- **Logged**: Data stored in `TrackingData/` directory with SHA-384 hashes

## License

Feel free to use and share this to educate people about better communication practices!