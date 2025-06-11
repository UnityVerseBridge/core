using System;
using System.Linq;
using UnityEngine;
using Unity.WebRTC;
using System.Collections;

namespace UnityVerseBridge.Core
{
    /// <summary>
    /// 양방향 오디오 스트리밍을 관리하는 통합 클래스입니다.
    /// Quest와 Mobile 앱 모두에서 사용 가능합니다.
    /// </summary>
    public class AudioStreamManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private WebRtcManager webRtcManager;
        
        [Header("오디오 설정")]
        [SerializeField] private bool enableMicrophone = true;
        [SerializeField] private bool enableSpeaker = true;
        [SerializeField] private int sampleRate = 48000; // Opus 권장
        [SerializeField] private float microphoneVolume = 1.0f;
        [SerializeField] private float speakerVolume = 1.0f;
        
        [Header("마이크 설정")]
        [SerializeField] private string preferredMicrophoneName = null;
        [SerializeField] private int recordingBufferLength = 1; // 초
        
        // 송신용
        private AudioSource microphoneSource;
        private AudioStreamTrack audioSendTrack;
        private string activeMicDevice;
        private bool isSending = false;
        
        // 수신용
        private AudioSource speakerSource;
        private AudioStreamTrack audioReceiveTrack;
        private bool isReceiving = false;
        
        // 상태 프로퍼티
        public bool IsSending => isSending;
        public bool IsReceiving => isReceiving;
        public bool IsMicrophoneEnabled => enableMicrophone;
        public bool IsSpeakerEnabled => enableSpeaker;
        
        // 이벤트
        public event Action<bool> OnMicrophoneStateChanged;
        public event Action<bool> OnSpeakerStateChanged;
        public event Action<float> OnMicrophoneLevelChanged; // 오디오 레벨 모니터링
        
        void Awake()
        {
            if (webRtcManager == null)
            {
                webRtcManager = GetComponent<WebRtcManager>();
                if (webRtcManager == null)
                {
                    Debug.LogError("[AudioStreamManager] WebRtcManager not found!");
                    enabled = false;
                }
            }
        }
        
        void Start()
        {
            // 권한 체크
            CheckMicrophonePermission();
            
            // WebRTC 이벤트 구독
            webRtcManager.OnWebRtcConnected += OnWebRtcConnected;
            webRtcManager.OnWebRtcDisconnected += OnWebRtcDisconnected;
            webRtcManager.OnAudioTrackReceived += OnAudioTrackReceived;
            
            // 스피커용 AudioSource 초기화
            InitializeSpeakerSource();
        }
        
        void OnDestroy()
        {
            if (webRtcManager != null)
            {
                webRtcManager.OnWebRtcConnected -= OnWebRtcConnected;
                webRtcManager.OnWebRtcDisconnected -= OnWebRtcDisconnected;
                webRtcManager.OnAudioTrackReceived -= OnAudioTrackReceived;
            }
            
            StopAudioStreaming();
        }
        
        private void CheckMicrophonePermission()
        {
            #if UNITY_ANDROID || UNITY_IOS
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                Debug.Log("[AudioStreamManager] Requesting microphone permission...");
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
                speakerSource.spatialBlend = 0f; // 2D 사운드
            }
        }
        
        private async void OnWebRtcConnected()
        {
            Debug.Log("[AudioStreamManager] WebRTC Connected. Initializing audio streaming...");
            
            // 협상 완료 대기
            await System.Threading.Tasks.Task.Delay(1000);
            
            if (enableMicrophone)
            {
                StartMicrophoneStreaming();
            }
        }
        
        private void OnWebRtcDisconnected()
        {
            Debug.Log("[AudioStreamManager] WebRTC Disconnected. Stopping audio streaming...");
            StopAudioStreaming();
        }
        
        // --- 송신 (마이크) 관련 ---
        private void StartMicrophoneStreaming()
        {
            if (isSending)
            {
                Debug.LogWarning("[AudioStreamManager] Already sending audio");
                return;
            }
            
            if (!SetupMicrophone())
            {
                Debug.LogError("[AudioStreamManager] Failed to setup microphone");
                return;
            }
            
            try
            {
                audioSendTrack = new AudioStreamTrack(microphoneSource);
                audioSendTrack.Enabled = true;
                
                webRtcManager.AddAudioTrack(audioSendTrack);
                isSending = true;
                
                OnMicrophoneStateChanged?.Invoke(true);
                Debug.Log("[AudioStreamManager] Microphone streaming started");
                
                // 오디오 레벨 모니터링 시작
                StartCoroutine(MonitorMicrophoneLevel());
            }
            catch (Exception e)
            {
                Debug.LogError($"[AudioStreamManager] Failed to start microphone streaming: {e.Message}");
                isSending = false;
            }
        }
        
        private bool SetupMicrophone()
        {
            string[] devices = Microphone.devices;
            if (devices.Length == 0)
            {
                Debug.LogError("[AudioStreamManager] No microphone devices found");
                return false;
            }
            
            // 마이크 선택
            activeMicDevice = string.IsNullOrEmpty(preferredMicrophoneName) ? 
                devices[0] : 
                devices.FirstOrDefault(d => d.Contains(preferredMicrophoneName)) ?? devices[0];
            
            Debug.Log($"[AudioStreamManager] Using microphone: {activeMicDevice}");
            
            // AudioSource 설정
            if (microphoneSource == null)
            {
                microphoneSource = gameObject.AddComponent<AudioSource>();
            }
            
            // 마이크 시작
            microphoneSource.clip = Microphone.Start(activeMicDevice, true, recordingBufferLength, sampleRate);
            microphoneSource.loop = true;
            microphoneSource.volume = 0f; // 로컬 피드백 방지
            
            // 마이크 준비 대기
            int waitTime = 0;
            while (!(Microphone.GetPosition(activeMicDevice) > 0) && waitTime < 1000)
            {
                waitTime++;
            }
            
            if (waitTime >= 1000)
            {
                Debug.LogError("[AudioStreamManager] Microphone initialization timeout");
                return false;
            }
            
            microphoneSource.Play();
            return true;
        }
        
        // --- 수신 (스피커) 관련 ---
        private void OnAudioTrackReceived(MediaStreamTrack track)
        {
            var audioTrack = track as AudioStreamTrack;
            if (audioTrack == null)
            {
                Debug.LogError("[AudioStreamManager] Received track is not an audio track");
                return;
            }
            
            if (!enableSpeaker)
            {
                Debug.Log("[AudioStreamManager] Speaker disabled, ignoring received audio track");
                return;
            }
            
            Debug.Log($"[AudioStreamManager] Audio track received: {audioTrack.Id}");
            
            audioReceiveTrack = audioTrack;
            audioReceiveTrack.Enabled = true;
            
            isReceiving = true;
            OnSpeakerStateChanged?.Invoke(true);
            
            // 플랫폼별 오디오 최적화
            ConfigurePlatformAudio();
        }
        
        private void ConfigurePlatformAudio()
        {
            #if UNITY_ANDROID
            AudioConfiguration config = AudioSettings.GetConfiguration();
            config.dspBufferSize = 256; // 저지연
            AudioSettings.Reset(config);
            #elif UNITY_IOS
            AudioConfiguration config = AudioSettings.GetConfiguration();
            config.dspBufferSize = 256;
            config.sampleRate = 48000;
            AudioSettings.Reset(config);
            #elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            // Quest (Windows) 최적화
            AudioConfiguration config = AudioSettings.GetConfiguration();
            config.dspBufferSize = 512;
            config.sampleRate = 48000;
            AudioSettings.Reset(config);
            #endif
        }
        
        // --- 컨트롤 메서드 ---
        public void SetMicrophoneEnabled(bool enabled)
        {
            enableMicrophone = enabled;
            
            if (enabled && webRtcManager.IsWebRtcConnected && !isSending)
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
            
            OnSpeakerStateChanged?.Invoke(enabled);
        }
        
        public void SetMicrophoneVolume(float volume)
        {
            microphoneVolume = Mathf.Clamp01(volume);
            // WebRTC 트랙의 볼륨은 직접 제어 불가, 대신 게인 조정 필요
        }
        
        public void SetSpeakerVolume(float volume)
        {
            speakerVolume = Mathf.Clamp01(volume);
            if (speakerSource != null)
            {
                speakerSource.volume = speakerVolume;
            }
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
            OnMicrophoneStateChanged?.Invoke(false);
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
            OnSpeakerStateChanged?.Invoke(false);
        }
        
        // 오디오 레벨 모니터링 (UI 표시용)
        private IEnumerator MonitorMicrophoneLevel()
        {
            float[] samples = new float[256];
            
            while (isSending && microphoneSource != null && microphoneSource.clip != null)
            {
                microphoneSource.clip.GetData(samples, Microphone.GetPosition(activeMicDevice));
                
                float sum = 0;
                foreach (float sample in samples)
                {
                    sum += Mathf.Abs(sample);
                }
                float average = sum / samples.Length;
                
                OnMicrophoneLevelChanged?.Invoke(average);
                
                yield return new WaitForSeconds(0.1f);
            }
        }
    }
}
