# WebRtcManager Unified Architecture

## Overview

WebRtcManager now supports both single-peer (1:1) and multi-peer (1:N) connections in a single component. This eliminates the need for separate WebRtcManager and MultiPeerWebRtcManager classes.

## Key Features

### Single Component, Multiple Modes

```csharp
// Enable multi-peer mode
webRtcManager.SetMultiPeerMode(true, maxConnections: 5);

// Use single-peer mode (default)
webRtcManager.SetMultiPeerMode(false);
```

### Backward Compatibility

The component maintains full backward compatibility with existing code that uses WebRtcManager for 1:1 connections. When `enableMultiPeer` is false, it behaves exactly like the original WebRtcManager.

### Unified Event System

Events work seamlessly in both modes:
- In single-peer mode: Standard events fire normally
- In multi-peer mode: Both multi-peer specific events and compatibility events fire

```csharp
// These events work in both modes
webRtcManager.OnWebRtcConnected += () => { };
webRtcManager.OnVideoTrackReceived += (track) => { };

// Multi-peer specific events (only relevant in multi-peer mode)
webRtcManager.OnPeerConnected += (peerId) => { };
webRtcManager.OnMultiPeerVideoTrackReceived += (peerId, track) => { };
```

### Configuration

```csharp
[Header("Multi-Peer Configuration")]
[SerializeField] private bool enableMultiPeer = false;
[SerializeField] private int maxConnections = 5;
[SerializeField] private string roomId = "default-room";
```

## Implementation Details

### Mode Detection

The component automatically switches behavior based on the `enableMultiPeer` flag:

```csharp
if (enableMultiPeer)
{
    // Use WebRtcConnectionHandler for each peer
    // Manage multiple connections
    // Join room with role-based signaling
}
else
{
    // Use legacy single RTCPeerConnection
    // Maintain 1:1 connection behavior
}
```

### Connection Management

- **Single-peer mode**: Uses direct RTCPeerConnection management
- **Multi-peer mode**: Uses WebRtcConnectionHandler instances for each peer

### Track Management

Tracks are managed differently based on mode:
- **Single-peer**: Direct track addition to peer connection
- **Multi-peer**: Tracks added to shared stream and distributed to all peers

## Usage Examples

### UnityVerseBridgeManager Integration

```csharp
private void CreateWebRtcManager()
{
    var manager = gameObject.AddComponent<WebRtcManager>();
    manager.SetRole(bridgeMode == BridgeMode.Host);
    
    if (connectionMode == ConnectionMode.MultiPeer)
    {
        manager.SetMultiPeerMode(true, connectionConfig.maxConnections);
    }
}
```

### Direct Usage

```csharp
// Single-peer mode (default)
var manager = gameObject.AddComponent<WebRtcManager>();
manager.isOfferer = true;
manager.Connect("room123");

// Multi-peer mode
var manager = gameObject.AddComponent<WebRtcManager>();
manager.SetMultiPeerMode(true, 10);
manager.isOfferer = true; // Host
manager.Connect("room123");
```

## Benefits

1. **Simplified Architecture**: One component handles all connection scenarios
2. **Code Reuse**: WebRtcConnectionHandler is used internally for multi-peer
3. **Easier Maintenance**: Single codebase to maintain
4. **Flexible Scaling**: Can switch between modes at runtime
5. **Backward Compatible**: Existing code continues to work

## Migration Guide

If you're using MultiPeerWebRtcManager:
1. Replace `MultiPeerWebRtcManager` with `WebRtcManager`
2. Call `SetMultiPeerMode(true, maxConnections)` after initialization
3. All existing events and methods work the same way

If you're using WebRtcManager:
- No changes required, it works as before