# UnityVerse Tag Setup Guide

## Required Tags

UnityVerseBridge requires the following tag to be defined in your Unity project:

### UnityVerseUI Tag

This tag is used by the UIManager to track and manage UI elements created by UnityVerseBridge.

## How to Add the Tag

1. **Open Unity's Tag Manager**
   - Go to Edit → Project Settings → Tags and Layers
   - OR in the Inspector, click the "Layer" dropdown and select "Add Layer..."

2. **Add the UnityVerseUI Tag**
   - In the Tags section, find an empty slot
   - Click the + button or the empty field
   - Type: `UnityVerseUI`
   - Press Enter to save

3. **Alternative: Using the Tag dropdown**
   - Select any GameObject in your scene
   - In the Inspector, click the "Tag" dropdown
   - Select "Add Tag..."
   - Add `UnityVerseUI` to the list

## Verification

After adding the tag, the runtime error "Tag: UnityVerseUI is not defined" should no longer appear.

## Note

The UIManager has been updated to handle cases where the tag is not defined. It will log a warning instead of throwing an error, but for proper functionality, please ensure the tag is added to your project.