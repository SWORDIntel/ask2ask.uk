# Tracking Methods Audit

## Current Status: ❌ INCOMPLETE

Many tracking methods collected by JavaScript are NOT stored in dedicated database fields. They're only in the JSON blob.

## Tracking Methods Collected by JavaScript

### ✅ Currently in Database (Dedicated Fields)

1. **Canvas Fingerprint** - `Visit.CanvasFingerprint`
2. **WebGL Fingerprint** - `Visit.WebGLFingerprint`
3. **Audio Fingerprint** - `Visit.AudioFingerprint`
4. **Hardware Concurrency** - `Visit.HardwareConcurrency`
5. **Max Touch Points** - `Visit.MaxTouchPoints`
6. **Screen Resolution** - `Visit.ScreenResolution`
7. **Color Depth** - `Visit.ColorDepth`
8. **Pixel Ratio** - `Visit.PixelRatio`
9. **Connection Type** - `Visit.ConnectionType`
10. **Effective Type** - `Visit.EffectiveType`

### ❌ Missing from Database (Only in JSON)

#### Basic Browser Info
11. **Platform** - navigator.platform
12. **Languages** - navigator.languages (array)
13. **Cookie Enabled** - navigator.cookieEnabled
14. **Do Not Track** - navigator.doNotTrack
15. **Vendor** - navigator.vendor
16. **Product** - navigator.product
17. **App Name** - navigator.appName
18. **App Version** - navigator.appVersion
19. **Online Status** - navigator.onLine

#### Timezone & Locale
20. **Timezone** - Intl.DateTimeFormat().resolvedOptions().timeZone
21. **Timezone Offset** - date.getTimezoneOffset()
22. **Locale** - Intl.DateTimeFormat().resolvedOptions().locale
23. **Calendar** - Intl.DateTimeFormat().resolvedOptions().calendar
24. **Numbering System** - Intl.DateTimeFormat().resolvedOptions().numberingSystem

#### Screen & Display (Extended)
25. **Available Width** - screen.availWidth
26. **Available Height** - screen.availHeight
27. **Pixel Depth** - screen.pixelDepth
28. **Screen Orientation** - screen.orientation.type
29. **Inner Width** - window.innerWidth
30. **Inner Height** - window.innerHeight
31. **Outer Width** - window.outerWidth
32. **Outer Height** - window.outerHeight
33. **Screen X** - window.screenX
34. **Screen Y** - window.screenY

#### Fonts
35. **Installed Fonts** - Font detection array
36. **Font Count** - Number of detected fonts

#### Plugins & Extensions
37. **Plugins List** - navigator.plugins
38. **Plugin Count** - navigator.plugins.length
39. **MIME Types** - navigator.mimeTypes
40. **Extensions Detected** - Extension fingerprinting results

#### Battery
41. **Battery Level** - navigator.getBattery().level
42. **Battery Charging** - navigator.getBattery().charging
43. **Charging Time** - navigator.getBattery().chargingTime
44. **Discharging Time** - navigator.getBattery().dischargingTime

#### Network (Extended)
45. **Downlink** - navigator.connection.downlink
46. **RTT** - navigator.connection.rtt
47. **Save Data** - navigator.connection.saveData

#### Geolocation
48. **Latitude** - geolocation.coords.latitude
49. **Longitude** - geolocation.coords.longitude
50. **Accuracy** - geolocation.coords.accuracy
51. **Altitude** - geolocation.coords.altitude

#### Performance Metrics
52. **DOM Content Loaded** - performance.timing
53. **Load Complete** - performance.timing
54. **First Paint** - performance.timing
55. **Memory Used** - performance.memory.usedJSHeapSize
56. **Memory Limit** - performance.memory.jsHeapSizeLimit

#### Storage
57. **Local Storage** - Available/Size
58. **Session Storage** - Available/Size
59. **IndexedDB** - Available
60. **Cookies** - Available/Count

#### Permissions
61. **Notifications** - Permission state
62. **Geolocation Permission** - Permission state
63. **Camera** - Permission state
64. **Microphone** - Permission state
65. **Clipboard** - Permission state

#### Advanced Fingerprinting (advanced-fingerprinting.js)
66. **CPU Fingerprint** - Performance benchmarking
67. **Math Operations Time** - CPU benchmark
68. **String Operations Time** - CPU benchmark
69. **Array Operations Time** - CPU benchmark
70. **Crypto Operations Time** - CPU benchmark
71. **Performance Score** - Calculated score
72. **Media Devices** - Audio/Video input devices
73. **Media Device Count** - Number of devices
74. **WebRTC Fingerprint** - Local/Public IPs
75. **WebRTC Local IPs** - Array of local IPs
76. **WebRTC Public IPs** - Array of public IPs
77. **Speech Synthesis** - Voices available
78. **Voice Count** - Number of voices
79. **Gamepad Support** - Gamepad API availability
80. **Gamepad Count** - Number of gamepads
81. **VR Support** - WebXR/WebVR availability
82. **VR Devices** - VR device list
83. **Bluetooth Support** - Web Bluetooth API
84. **USB Support** - WebUSB API
85. **NFC Support** - Web NFC API
86. **Sensor APIs** - Accelerometer, Gyroscope, etc.

#### Novel Techniques 2025 (novel-fingerprinting-2025.js)
87. **TLS/HTTP2 Fingerprint** - Protocol detection
88. **HTTP Version** - HTTP/1.1, HTTP/2, HTTP/3
89. **HTTP/2 Support** - Boolean
90. **HTTP/3 Support** - Boolean
91. **Resource Protocols** - Array of protocols used
92. **Multiplexing Detected** - HTTP/2 multiplexing
93. **Fetch Keepalive** - Support detection
94. **Server Push Supported** - HTTP/2 server push
95. **WebGPU Fingerprint** - GPU identification
96. **WebGPU Vendor** - GPU vendor
97. **WebGPU Architecture** - GPU architecture
98. **WebGPU Features** - Supported features array
99. **WebGPU Limits** - GPU limits object
100. **CSS Feature Detection** - Supported CSS features
101. **CSS Grid Support** - Boolean
102. **CSS Flexbox Support** - Boolean
103. **CSS Custom Properties** - Boolean
104. **CSS Animations** - Boolean
105. **Pointer Events** - Touch/Pen/Mouse capabilities
106. **Pointer Types** - Array of pointer types
107. **Hover Capability** - Boolean
108. **Service Worker** - Support and registration
109. **Service Worker Active** - Boolean
110. **Web Workers** - Support detection
111. **Shared Workers** - Support detection
112. **WebAssembly** - Support and features
113. **WebAssembly SIMD** - SIMD support
114. **WebAssembly Threads** - Threading support
115. **Credential Management** - API support
116. **Payment Request** - API support
117. **Web Authentication** - WebAuthn support
118. **Clipboard API** - Advanced clipboard features
119. **File System Access** - API support
120. **Idle Detection** - API support
121. **Wake Lock** - API support
122. **Screen Capture** - API support
123. **Media Session** - API support
124. **Picture-in-Picture** - Support detection
125. **WebCodecs** - API support
126. **WebTransport** - API support
127. **WebHID** - Human Interface Device API
128. **WebSerial** - Serial port API
129. **WebMIDI** - MIDI API support
130. **Temporal API** - New date/time API

#### Behavioral Tracking
131. **Mouse Movements** - Array of coordinates
132. **Click Events** - Array of click locations
133. **Scroll Events** - Array of scroll positions
134. **Keystroke Patterns** - Timing patterns
135. **Timing Patterns** - Behavioral timing

## Recommendation

### Option 1: Keep JSON-Only (Current)
- ✅ All data is stored in `TrackingDataJson` field
- ✅ Flexible - can add new tracking without schema changes
- ❌ Can't query/filter by specific tracking methods
- ❌ Can't create indexes on specific fields
- ❌ Harder to analyze patterns

### Option 2: Add All Fields to Database
- ✅ Can query/filter by any tracking method
- ✅ Can create indexes for fast queries
- ✅ Easier to analyze patterns
- ❌ Large database schema (130+ fields)
- ❌ Schema changes needed for new tracking methods
- ❌ More complex migrations

### Option 3: Hybrid Approach (RECOMMENDED)
- ✅ Store most important fields in dedicated columns
- ✅ Keep full data in JSON for flexibility
- ✅ Balance between queryability and flexibility
- ✅ Reasonable schema size

## Recommended Fields to Add

### High Priority (Unique Identifiers)
1. **Timezone** - Strong identifier
2. **Installed Fonts Hash** - Very unique
3. **CPU Fingerprint** - Hardware identifier
4. **WebGPU Fingerprint** - GPU identifier
5. **Media Devices Hash** - Device identifier
6. **WebRTC IPs** - Network identifier
7. **HTTP/2 Support** - Protocol fingerprint
8. **WebAssembly Features** - Runtime capabilities
9. **Battery Level** - Device state
10. **Geolocation** - Physical location

### Medium Priority (Useful for Analysis)
11. **Do Not Track** - Privacy preference
12. **Cookie Enabled** - Privacy setting
13. **Local Storage Available** - Storage capability
14. **Permissions Granted** - Permission state
15. **Service Worker Active** - PWA detection

### Low Priority (Already in JSON, rarely queried)
- Most other fields can stay in JSON only

## Action Items

1. ✅ Audit complete - 130+ tracking methods identified
2. ⏳ Decide on storage strategy (Option 1, 2, or 3)
3. ⏳ Update database schema if needed
4. ⏳ Update TrackingService to extract additional fields
5. ⏳ Create migration for existing data
6. ⏳ Update documentation

