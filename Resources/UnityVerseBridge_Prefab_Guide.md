# UnityVerseBridge Prefab Setup Guide

## Overview
UnityVerseBridge provides ready-to-use prefabs for quick integration into your Unity projects.

## Prefab Types

### 1. UnityVerseBridge_Host.prefab
For Quest VR applications (Stream sender)

**GameObject Structure:**
```
UnityVerseBridge_Host
├── UnityVerseBridgeManager
│   └── Settings:
│       - Bridge Mode: Host
│       - Connection Mode: SinglePeer/MultiPeer
│       - Auto Initialize: ✓
│       - Auto Connect: ✓
│       - Enable Video: ✓
│       - Enable Audio: ✓
│       - Enable Touch: ✓
│       - Enable Haptics: ✓
├── ConnectionConfig (Reference)
└── WebRtcConfiguration (Reference)
```

### 2. UnityVerseBridge_Client.prefab
For Mobile applications (Stream receiver)

**GameObject Structure:**
```
UnityVerseBridge_Client
├── UnityVerseBridgeManager
│   └── Settings:
│       - Bridge Mode: Client
│       - Connection Mode: SinglePeer
│       - Auto Initialize: ✓
│       - Auto Connect: ✓
│       - Enable Video: ✓
│       - Enable Audio: ✓
│       - Enable Touch: ✓
│       - Enable Haptics: ✓
├── ConnectionConfig (Reference)
├── WebRtcConfiguration (Reference)
└── UI
    └── VideoDisplay (RawImage)
```

### 3. UnityVerseBridge_MultiHost.prefab
For 1:N connections (Multiple mobile clients)

**GameObject Structure:**
```
UnityVerseBridge_MultiHost
├── UnityVerseBridgeManager
│   └── Settings:
│       - Bridge Mode: Host
│       - Connection Mode: MultiPeer
│       - Auto Initialize: ✓
│       - Auto Connect: ✓
│       - Enable Video: ✓
│       - Enable Audio: ✓
│       - Enable Touch: ✓
│       - Enable Haptics: ✓
├── ConnectionConfig (Reference)
└── WebRtcConfiguration (Reference)
```

## Quick Start

### Step 1: Import Prefab
1. In Unity, navigate to `Packages/UnityVerseBridge Core/Runtime/Prefabs/`
2. Drag the appropriate prefab into your scene

### Step 2: Configure Connection
1. Create a ConnectionConfig asset:
   - Right-click in Project > Create > UnityVerseBridge > Connection Config
   - Set your signaling server URL
   - Set room ID (or enable auto-generate)

2. Assign the ConnectionConfig to the prefab's UnityVerseBridgeManager component

### Step 3: Platform-Specific Setup

**For Quest (Host):**
```csharp
// The prefab will automatically find OVRCameraRig
// Or manually assign your VR camera to VideoStreamHandler
```

**For Mobile (Client):**
```csharp
// The prefab includes a UI canvas with video display
// Customize the UI layout as needed
```

### Step 4: Run
1. Start your signaling server
2. Build and run the Quest app first
3. Build and run the Mobile app
4. They will automatically connect using the room ID

## Customization

### Access Components via Code:
```csharp
// Get the manager
var bridgeManager = FindObjectOfType<UnityVerseBridgeManager>();

// Access handlers
var videoHandler = bridgeManager.GetComponent<VideoStreamHandler>();
var audioHandler = bridgeManager.GetComponent<AudioStreamHandler>();
var touchHandler = bridgeManager.GetComponent<TouchInputHandler>();
var hapticHandler = bridgeManager.GetComponent<HapticHandler>();

// Send custom data
bridgeManager.SendDataChannelMessage(customData);
```

### Custom Events:
```csharp
// Subscribe to events
bridgeManager.OnConnected.AddListener(() => {
    Debug.Log("Connected!");
});

bridgeManager.OnError.AddListener((error) => {
    Debug.LogError($"Error: {error}");
});
```

## Advanced Configuration

### Mixed Reality Mode (Quest):
```csharp
var videoHandler = bridgeManager.GetComponent<VideoStreamHandler>();
videoHandler.SetMixedRealityMode(true);
```

### Multi-Touch Support:
```csharp
var touchHandler = bridgeManager.GetComponent<TouchInputHandler>();
// Configure in inspector or via code
```

### Custom Haptic Patterns:
```csharp
var hapticHandler = bridgeManager.GetComponent<HapticHandler>();
hapticHandler.RequestHapticFeedback(
    HapticCommandType.VibrateCustom, 
    duration: 0.5f, 
    intensity: 0.8f
);
```

## Troubleshooting

1. **Connection fails**: 
   - Check signaling server is running
   - Verify room IDs match
   - Check firewall settings

2. **No video stream**:
   - Ensure camera is assigned
   - Check render texture format (BGRA32)
   - Verify WebRTC.Update() is running

3. **Touch not working**:
   - Enable Enhanced Touch Support
   - Check layer masks on Host
   - Verify data channel is open

## Sample Code

### Minimal Host Setup:
```csharp
public class MyVRApp : MonoBehaviour
{
    void Start()
    {
        // That's it! The prefab handles everything
    }
}
```

### Minimal Client Setup:
```csharp
public class MyMobileApp : MonoBehaviour
{
    void Start()
    {
        // The prefab auto-connects and displays video
    }
}
```

### Manual Control:
```csharp
public class ManualSetup : MonoBehaviour
{
    private UnityVerseBridgeManager bridge;
    
    void Start()
    {
        bridge = GetComponent<UnityVerseBridgeManager>();
        bridge.autoConnect = false; // Disable auto-connect
    }
    
    public void ConnectButton()
    {
        bridge.Initialize();
        bridge.Connect();
    }
}
```