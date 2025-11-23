# Ask2Ask.uk - Data Collection Experiment Documentation

## ⚠️ IMPORTANT NOTICE ⚠️

This website is a **data collection experiment** designed to demonstrate the extent of information that can be gathered from website visitors. All visitors are explicitly informed via prominent disclosure on the page.

## Purpose

Educational/research demonstration of:
- Browser fingerprinting techniques
- Device identification methods
- Behavioral tracking capabilities
- Privacy awareness

## Disclosure

**PROMINENT DISCLOSURE IS DISPLAYED AT THE TOP OF THE PAGE** warning visitors that:
1. They are participating in a data collection experiment
2. Extensive information is being collected
3. All collected data is displayed in real-time
4. They should leave if they do not consent

## Data Collected

### 1. Basic Browser Information
- User Agent
- Platform
- Languages
- Cookie settings
- Hardware concurrency
- Touch capabilities
- Vendor information

### 2. Fingerprinting Data
- **Canvas Fingerprinting**: Unique rendering characteristics
- **WebGL Fingerprinting**: GPU and graphics information
- **Audio Fingerprinting**: Audio context characteristics
- **Font Detection**: Installed system fonts

### 3. Hardware Information
- Screen resolution and color depth
- Device pixel ratio
- Available screen dimensions
- Orientation
- Battery status (if permitted)
- Media devices (cameras, microphones)
- Plugins

### 4. Network Information
- Connection type and speed
- Round-trip time (RTT)
- Effective bandwidth
- IP address (server-side)
- Geolocation (if permitted)

### 5. Behavioral Data
- Mouse movements (sampled)
- Click positions and targets
- Scroll patterns
- Keystroke timing patterns (not content)
- Page visibility changes
- Focus/blur events

### 6. Performance Metrics
- Memory usage
- Navigation timing
- Page load metrics
- JavaScript heap size

### 7. Storage & Caching
- LocalStorage availability
- SessionStorage availability
- IndexedDB support
- Storage quota estimates

### 8. Advanced Features Detection
- WebGL, WebGL2
- WebRTC
- Web Workers
- Service Workers
- WebAssembly
- Notifications
- Vibration API
- WebSockets
- Web Audio
- Speech Synthesis
- Bluetooth
- USB
- Gamepad API
- VR/XR capabilities
- Touch/Pointer events
- Device sensors

## Data Storage

### File Structure
```
TrackingData/
├── YYYYMMDD_HHMMSS_sessionId.json      # Individual session data
└── daily_YYYYMMDD.jsonl                # Daily aggregated log
```

### Data Format
Each tracking record includes:
```json
{
  "Hash": "SHA-384 hash of data",
  "HashAlgorithm": "SHA-384",
  "Data": {
    "Timestamp": "ISO 8601 timestamp",
    "ClientIP": "IP address",
    "ForwardedFor": "X-Forwarded-For header",
    "UserAgent": "User agent string",
    "RequestHeaders": { /* All HTTP headers */ },
    "TrackingData": { /* All collected fingerprint data */ },
    "ServerInfo": {
      "ProcessingTime": "Server timestamp",
      "ServerName": "Server hostname",
      "ServerOS": "Server OS version"
    }
  }
}
```

## Security & Compliance

### CNSA 2.0 Compliance
The system implements CNSA 2.0 (Commercial National Security Algorithm Suite 2.0) requirements:

- ✅ **SHA-384**: Used for data integrity hashing
- 🔜 **ML-KEM-1024**: Planned for key encapsulation (post-quantum)
- 🔜 **ML-DSA-87**: Planned for digital signatures (post-quantum)

### Data Integrity
- All data is hashed using SHA-384
- Hash is stored with each record for verification
- Timestamps use UTC for consistency

### Logging
- All collection events are logged
- Daily summary logs created
- Server-side headers captured
- IP addresses and forwarded headers recorded

## Transparency Features

### Real-Time Display
- All collected data is displayed to the user in real-time
- Data is formatted as JSON and continuously updated
- Users can copy all collected data to clipboard

### Console Warnings
- Red warning message in browser console
- Continuous reminder of active data collection

### Visual Indicators
- Prominent red warning section at page top
- Pulsing/shaking animations on warning
- High-contrast colors for visibility

## Technical Implementation

### Frontend (`tracking.js`)
- Comprehensive fingerprinting suite
- Behavioral event listeners
- 5-second update interval
- Automatic data transmission
- Error handling for unsupported features

### Backend (`Tracking.cshtml.cs`)
- ASP.NET Core endpoint
- JSON data reception
- SHA-384 hashing
- File-based storage
- Daily log aggregation
- Request header capture

### Styling (`site.css`)
- Red warning section with pulse animation
- Matrix-style green terminal for data display
- Copy button with hover effects
- Responsive design

## Privacy Considerations

### What This Demonstrates
- The extensive data websites CAN collect
- Why browser privacy settings matter
- Why VPNs and privacy tools are important
- The value of privacy-focused browsers

### Ethical Use
This implementation is designed for:
- ✅ Educational purposes with full disclosure
- ✅ Privacy awareness campaigns
- ✅ Security research with consent
- ✅ Demonstrating tracking capabilities

NOT for:
- ❌ Deceptive tracking without disclosure
- ❌ Identification of individuals without consent
- ❌ Selling or sharing collected data
- ❌ Malicious purposes

## Legal Compliance

### Disclosure Requirements Met
- ✅ Prominent warning before data collection begins
- ✅ Clear list of data being collected
- ✅ Real-time transparency (data shown to user)
- ✅ Opt-out mechanism (leave the page)
- ✅ Privacy notice in footer

### Recommended Additional Steps
For production deployment, consider:
- Privacy policy page
- Cookie consent banner
- Data retention policy
- Data deletion requests mechanism
- Terms of service
- Contact information for privacy questions

## Accessing Collected Data

### Location
Data is stored in: `ask2ask/TrackingData/`

### Format
- Individual sessions: `YYYYMMDD_HHMMSS_sessionId.json`
- Daily logs: `daily_YYYYMMDD.jsonl`

### Analysis
Data can be analyzed using:
- JSON parsing tools
- Log analysis software
- Custom scripts for pattern detection
- Data visualization tools

## Future Enhancements

### Planned Features
- [ ] ML-KEM-1024 implementation for key encapsulation
- [ ] ML-DSA-87 implementation for digital signatures
- [ ] Encrypted data storage at rest
- [ ] Data anonymization options
- [ ] Aggregate statistics dashboard
- [ ] Export functionality (CSV, JSON)
- [ ] Data retention policies
- [ ] Automated cleanup scripts

### Post-Quantum Cryptography
When libraries become available:
- Implement ML-KEM-1024 for secure key exchange
- Implement ML-DSA-87 for verifiable signatures
- Full CNSA 2.0 compliance for quantum-resistant security

## Responsible Disclosure

If you discover security issues or privacy concerns:
1. Do not exploit the vulnerability
2. Document the issue
3. Report responsibly to the site operator
4. Allow time for remediation

## Conclusion

This system demonstrates that significant amounts of data can be collected from website visitors, even with full transparency and disclosure. It serves as an educational tool to raise awareness about online privacy and the importance of privacy-protecting technologies.

**Always obtain informed consent when collecting personal data.**
