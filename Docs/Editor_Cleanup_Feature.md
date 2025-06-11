# Unity Editor Cleanup Feature

## Overview
The UnityVerseBridge package now includes comprehensive cleanup tools to help you manage and remove bridge components when needed.

## Menu Options

### UnityVerseBridge > Cleanup > Remove All UnityVerseBridge Components
- Removes ALL UnityVerseBridge instances from the scene hierarchy
- Deletes ALL prefabs containing UnityVerseBridge components
- Removes ALL ConnectionConfig and WebRtcConfiguration assets
- Cleans up empty directories

### UnityVerseBridge > Cleanup > Remove Quest Components Only
- Removes only Quest-specific components and assets
- Preserves Mobile components
- Useful when switching from Quest to Mobile development

### UnityVerseBridge > Cleanup > Remove Mobile Components Only
- Removes only Mobile-specific components and assets
- Preserves Quest components
- Useful when switching from Mobile to Quest development

## What Gets Removed

1. **Scene Instances**
   - All GameObjects with UnityVerseBridgeManager component
   - Their child objects (Extensions)

2. **Project Assets**
   - UnityVerseBridge prefabs (*.prefab)
   - ConnectionConfig assets (*.asset)
   - WebRtcConfiguration assets (*.asset)

3. **Empty Directories**
   - Assets/UnityVerseBridge/ (if empty)
   - Assets/Resources/Prefabs/ (if empty)

## Context Menu

Right-click on any UnityVerseBridgeManager component in the Inspector:
- **Remove This Bridge Instance**: Removes only this specific instance

## Safety Features

- Confirmation dialog before any deletion
- Uses Unity's Undo system (Ctrl/Cmd+Z to undo)
- Detailed console logs of what was removed
- Error handling with user-friendly messages

## Usage Example

1. Go to menu: **UnityVerseBridge > Cleanup**
2. Choose appropriate option
3. Confirm in the dialog
4. Check console for detailed removal log

## Notes

- The cleanup is thorough but safe
- Meta files are automatically handled
- Asset database is refreshed after cleanup
- Compatible with version control systems