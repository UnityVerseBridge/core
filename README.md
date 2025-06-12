# UnityVerseBridge Core

Unity-based WebRTC bridge package for real-time peer-to-peer communication between Meta Quest VR headsets and mobile devices.

## Features

- 🎮 **Cross-Platform WebRTC**: Seamless streaming between Quest and Mobile
- 📱 **Touch Input Bridge**: Send mobile touch input to VR environment
- 🔌 **Easy Integration**: Simple setup with Unity menu tools
- 🏗️ **Modular Architecture**: Clean separation of concerns with extensions
- 🔒 **Secure Connection**: Room-based peer discovery with authentication support
- 🎯 **Optimized Streaming**: H264 codec with adaptive quality (360p-1080p)
- 🔄 **Platform-Specific Adapters**: Native WebSocket support for each platform

## Requirements

- Unity 6 LTS (6000.0.33f1) or Unity 2022.3 LTS
- Unity WebRTC Package 3.0.0-pre.8+
- Meta XR SDK (for Quest platform)
- iOS 12.0+ / Android API 26+ (for mobile platform)

## Installation

1. Import the UnityVerseBridge Core package into your Unity project
2. Open Unity Package Manager
3. Add required dependencies:
   - `com.unity.webrtc` (3.0.0-pre.8 or higher)
   - Input System Package (if using new Input System)

## Quick Start

### For Quest/VR (Host)
1. Go to `GameObject > UnityVerseBridge > Quest Setup`
2. Configure the UnityVerseConfig:
   - Set Signaling URL (default: `ws://localhost:8080`)
   - Set Room ID
   - Enable Auto Connect
3. Build and deploy to Quest

### For Mobile (Client)
1. Go to `GameObject > UnityVerseBridge > Mobile Setup`
2. Configure the UnityVerseConfig:
   - Use same Signaling URL as Quest
   - Use same Room ID
   - Enable Auto Connect
3. Build and deploy to iOS/Android

## Architecture

### Core Components
- **UnityVerseBridgeManager**: Main manager component
- **WebRtcManager**: Handles WebRTC connections
- **SignalingClient**: WebSocket communication

### Extensions
- **Quest Extensions**: Video streaming, touch receiving, haptics
- **Mobile Extensions**: Video display, touch input

### UI Components
- **UIManager**: Centralized UI management
- **RoomListUI**: Dynamic room discovery
- **RoomInputUI**: Manual room entry with QR support

## Configuration

### UnityVerseConfig ScriptableObject
```
- Signaling URL: WebSocket server address
- Room ID: Unique room identifier
- Role Detection: Automatic/Manual
- Auto Connect: Connect on start
- Enable Debug Logging: Console logs
- Require Authentication: Enable auth flow
```

### Platform-Specific Settings
- **Quest**: Requires Meta XR SDK, Android build
- **Mobile**: Supports iOS and Android
- **Editor**: Use Meta XR Simulator for testing

## API Usage

### Basic Connection
```csharp
// Get the manager instance
var bridgeManager = FindObjectOfType<UnityVerseBridgeManager>();

// Set room and connect
bridgeManager.SetRoomId("my-room");
bridgeManager.Connect();
```

### Sending Touch Data (Mobile)
```csharp
// Automatically handled by MobileInputExtension
// Touch input is captured and sent via data channel
```

### Receiving Touch (Quest)
```csharp
// Subscribe to touch events
var touchExtension = GetComponent<QuestTouchExtension>();
touchExtension.OnTouchReceived += (touchData) => {
    // Handle touch at normalized coordinates
};
```

## Troubleshooting

### Video Not Streaming
1. Ensure WebRTC.Update() coroutine is running
2. Check RenderTexture format (BGRA32)
3. Verify same room ID on both devices

### Connection Issues
1. Check firewall settings
2. Ensure signaling server is running
3. Verify network connectivity

### Performance
- Default resolution: 640x360 for optimal performance
- Supported resolutions: 360p (minimal), 720p, 1080p
- Frame rate: 30 FPS
- Enable hardware acceleration (H264 codec)
- Adaptive quality based on peer count

## Support

For issues and feature requests, please visit the [GitHub repository](https://github.com/UnityVerseBridge/core).

## License

See LICENSE file for details.