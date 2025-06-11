using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Unity.WebRTC;

namespace UnityVerseBridge.Core
{
    /// <summary>
    /// 오디오 스트리밍 기능을 처리하는 핸들러
    /// </summary>
    [RequireComponent(typeof(UnityVerseBridgeManager))]
    public class AudioStreamHandler : MonoBehaviour
    {
        [Header("Audio Settings")]
        [SerializeField] private bool enableMicrophone = true;
        [SerializeField] private bool enableSpeaker = true;
        [SerializeField] private int sampleRate = 48000;
        [SerializeField] private float microphoneVolume = 1.0f;
        [SerializeField] private float speakerVolume = 1.0f;
        
        [Header("Microphone Settings")]
        [SerializeField] private string preferredMicrophoneName = null;
        [SerializeField] private int recordingBufferLength = 1;
        
        private UnityVerseBridgeManager bridgeManager;
        private WebRtcManager webRtcManager;
        private UnityVerseBridgeManager.BridgeMode mode;
        
        // Audio components
        private AudioSource microphoneSource;
        private AudioSource speakerSource;
        private AudioStreamTrack audioSendTrack;
        private AudioStreamTrack audioReceiveTrack;
        
        private string activeMicDevice;
        private bool isSending = false;
        private bool isReceiving = false;
        private bool isInitialized = false;

        public void Initialize(UnityVerseBridgeManager manager, WebRtcManager rtcManager, UnityVerseBridgeManager.BridgeMode bridgeMode)
        {
            bridgeManager = manager;
            webRtcManager = rtcManager;
            mode = bridgeMode;
            
            CheckMicrophonePermission();
            InitializeSpeakerSource();
            
            // Subscribe to events
            webRtcManager.OnWebRtcConnected += OnWebRtcConnected;
            webRtcManager.OnWebRtcDisconnected += OnWebRtcDisconnected;
            webRtcManager.OnAudioTrackReceived += OnAudioTrackReceived;
            
            // For multi-peer mode
            webRtcManager.OnMultiPeerAudioTrackReceived += OnMultiPeerAudioTrackReceived;
            
            isInitialized = true;
        }

        void OnDestroy()
        {
            Cleanup();
        }

        private void CheckMicrophonePermission()
        {
            #if UNITY_ANDROID || UNITY_IOS
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                Debug.Log("[AudioStreamHandler] Requesting microphone permission...");
                Application.RequestUserAuthorization(UserAuthorization.Microphone);
            }
            #endif
        }

        private void InitializeSpeakerSource()
        {
            if (speakerSource == null)
            {
                speakerSource = gameObject.AddComponent<AudioSource>();
                speakerSource.playOnAwake = false;
                speakerSource.volume = speakerVolume;
                speakerSource.spatialBlend = 0f; // 2D sound
            }
        }

        private async void OnWebRtcConnected()
        {
            Debug.Log("[AudioStreamHandler] WebRTC Connected. Initializing audio streaming...");
            
            // Wait for negotiation to complete
            await System.Threading.Tasks.Task.Delay(1000);
            
            if (enableMicrophone)
            {
                StartMicrophoneStreaming();
            }
        }

        private void OnWebRtcDisconnected()
        {
            Debug.Log("[AudioStreamHandler] WebRTC Disconnected. Stopping audio streaming...");
            StopAudioStreaming();
        }

        private void StartMicrophoneStreaming()
        {
            if (isSending)
            {
                Debug.LogWarning("[AudioStreamHandler] Already sending audio");
                return;
            }
            
            if (!SetupMicrophone())
            {
                Debug.LogError("[AudioStreamHandler] Failed to setup microphone");
                return;
            }
            
            try
            {
                audioSendTrack = new AudioStreamTrack(microphoneSource);
                audioSendTrack.Enabled = true;
                
                webRtcManager.AddAudioTrack(audioSendTrack);
                isSending = true;
                
                Debug.Log("[AudioStreamHandler] Microphone streaming started");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AudioStreamHandler] Failed to start microphone streaming: {e.Message}");
                isSending = false;
            }
        }

        private bool SetupMicrophone()
        {
            string[] devices = Microphone.devices;
            if (devices.Length == 0)
            {
                Debug.LogError("[AudioStreamHandler] No microphone devices found");
                return false;
            }
            
            // Select microphone
            activeMicDevice = string.IsNullOrEmpty(preferredMicrophoneName) ? 
                devices[0] : 
                devices.FirstOrDefault(d => d.Contains(preferredMicrophoneName)) ?? devices[0];
            
            Debug.Log($"[AudioStreamHandler] Using microphone: {activeMicDevice}");
            
            // Setup AudioSource
            if (microphoneSource == null)
            {
                microphoneSource = gameObject.AddComponent<AudioSource>();
            }
            
            // Start microphone
            microphoneSource.clip = Microphone.Start(activeMicDevice, true, recordingBufferLength, sampleRate);
            microphoneSource.loop = true;
            microphoneSource.volume = 0f; // Prevent local feedback
            
            // Wait for microphone to be ready
            int waitTime = 0;
            while (!(Microphone.GetPosition(activeMicDevice) > 0) && waitTime < 1000)
            {
                waitTime++;
            }
            
            if (waitTime >= 1000)
            {
                Debug.LogError("[AudioStreamHandler] Microphone initialization timeout");
                return false;
            }
            
            microphoneSource.Play();
            return true;
        }

        private void OnAudioTrackReceived(MediaStreamTrack track)
        {
            var audioTrack = track as AudioStreamTrack;
            if (audioTrack == null)
            {
                Debug.LogError("[AudioStreamHandler] Received track is not an audio track");
                return;
            }
            
            HandleReceivedAudioTrack(audioTrack);
        }

        private void OnMultiPeerAudioTrackReceived(string peerId, MediaStreamTrack track)
        {
            var audioTrack = track as AudioStreamTrack;
            if (audioTrack == null)
            {
                Debug.LogError("[AudioStreamHandler] Received track is not an audio track");
                return;
            }
            
            Debug.Log($"[AudioStreamHandler] Audio track received from peer: {peerId}");
            HandleReceivedAudioTrack(audioTrack);
        }

        private void HandleReceivedAudioTrack(AudioStreamTrack audioTrack)
        {
            if (!enableSpeaker)
            {
                Debug.Log("[AudioStreamHandler] Speaker disabled, ignoring received audio track");
                return;
            }
            
            Debug.Log($"[AudioStreamHandler] Audio track received: {audioTrack.Id}");
            
            audioReceiveTrack = audioTrack;
            audioReceiveTrack.Enabled = true;
            
            isReceiving = true;
            
            // Configure platform-specific audio
            ConfigurePlatformAudio();
        }

        private void ConfigurePlatformAudio()
        {
            #if UNITY_ANDROID
            AudioConfiguration config = AudioSettings.GetConfiguration();
            config.dspBufferSize = 256; // Low latency
            AudioSettings.Reset(config);
            #elif UNITY_IOS
            AudioConfiguration config = AudioSettings.GetConfiguration();
            config.dspBufferSize = 256;
            config.sampleRate = 48000;
            AudioSettings.Reset(config);
            #elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            // Quest (Windows) optimization
            AudioConfiguration config = AudioSettings.GetConfiguration();
            config.dspBufferSize = 512;
            config.sampleRate = 48000;
            AudioSettings.Reset(config);
            #endif
        }

        private void StopMicrophoneStreaming()
        {
            if (Microphone.IsRecording(activeMicDevice))
            {
                Microphone.End(activeMicDevice);
            }
            
            if (microphoneSource != null && microphoneSource.clip != null)
            {
                microphoneSource.Stop();
                Destroy(microphoneSource.clip);
                microphoneSource.clip = null;
            }
            
            if (audioSendTrack != null)
            {
                audioSendTrack.Dispose();
                audioSendTrack = null;
            }
            
            isSending = false;
        }

        private void StopAudioStreaming()
        {
            StopMicrophoneStreaming();
            
            if (audioReceiveTrack != null)
            {
                audioReceiveTrack.Dispose();
                audioReceiveTrack = null;
            }
            
            isReceiving = false;
        }

        private void Cleanup()
        {
            if (!isInitialized) return;
            
            // Unsubscribe events
            if (webRtcManager != null)
            {
                webRtcManager.OnWebRtcConnected -= OnWebRtcConnected;
                webRtcManager.OnWebRtcDisconnected -= OnWebRtcDisconnected;
                webRtcManager.OnAudioTrackReceived -= OnAudioTrackReceived;
            }
            
            if (webRtcManager != null)
            {
                webRtcManager.OnMultiPeerAudioTrackReceived -= OnMultiPeerAudioTrackReceived;
            }
            
            StopAudioStreaming();
        }

        #region Public API
        public void SetMicrophoneEnabled(bool enabled)
        {
            enableMicrophone = enabled;
            
            if (enabled && webRtcManager != null && webRtcManager.IsWebRtcConnected && !isSending)
            {
                StartMicrophoneStreaming();
            }
            else if (!enabled && isSending)
            {
                StopMicrophoneStreaming();
            }
        }

        public void SetSpeakerEnabled(bool enabled)
        {
            enableSpeaker = enabled;
            
            if (speakerSource != null)
            {
                speakerSource.mute = !enabled;
            }
        }

        public void SetMicrophoneVolume(float volume)
        {
            microphoneVolume = Mathf.Clamp01(volume);
        }

        public void SetSpeakerVolume(float volume)
        {
            speakerVolume = Mathf.Clamp01(volume);
            if (speakerSource != null)
            {
                speakerSource.volume = speakerVolume;
            }
        }

        public bool IsSending => isSending;
        public bool IsReceiving => isReceiving;
        #endregion
    }
}