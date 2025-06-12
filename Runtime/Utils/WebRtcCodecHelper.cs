using UnityEngine;
using Unity.WebRTC;
using System.Linq;
using System.Collections.Generic;

namespace UnityVerseBridge.Core.Utils
{
    /// <summary>
    /// WebRTC 코덱 설정을 도와주는 헬퍼 클래스
    /// 모바일 디바이스의 하드웨어 디코더 호환성 문제를 해결합니다.
    /// </summary>
    public static class WebRtcCodecHelper
    {
        /// <summary>
        /// 비디오 트랙에 대한 인코딩 파라미터를 설정합니다.
        /// 모바일 호환성을 위해 낮은 비트레이트와 키프레임 간격을 설정합니다.
        /// </summary>
        public static void ConfigureVideoEncoding(RTCRtpSender sender, int width, int height, int framerate = 30)
        {
            if (sender == null)
            {
                Debug.LogError("[WebRtcCodecHelper] Sender is null");
                return;
            }

            var parameters = sender.GetParameters();
            if (parameters.encodings == null || parameters.encodings.Length == 0)
            {
                Debug.LogWarning("[WebRtcCodecHelper] No encodings found in sender parameters");
                return;
            }

            // 모바일 최적화된 인코딩 설정
            foreach (var encoding in parameters.encodings)
            {
                encoding.active = true;
                
                // 해상도에 따른 적응형 비트레이트 설정
                if (width <= 640 && height <= 360)
                {
                    encoding.maxBitrate = 500000; // 500 kbps for 360p
                    encoding.minBitrate = 100000; // 100 kbps minimum
                }
                else if (width <= 1280 && height <= 720)
                {
                    encoding.maxBitrate = 1000000; // 1 Mbps for 720p
                    encoding.minBitrate = 200000; // 200 kbps minimum
                }
                else
                {
                    encoding.maxBitrate = 2000000; // 2 Mbps for 1080p+
                    encoding.minBitrate = 500000; // 500 kbps minimum
                }
                
                encoding.maxFramerate = (uint)framerate;
                
                // 스케일링 설정 (모바일에서 중요)
                encoding.scaleResolutionDownBy = 1.0f;
            }

            sender.SetParameters(parameters);
            
            Debug.Log($"[WebRtcCodecHelper] Configured video encoding: {width}x{height}@{framerate}fps, " +
                     $"Bitrate: {parameters.encodings[0].minBitrate/1000}-{parameters.encodings[0].maxBitrate/1000} kbps");
        }

        /// <summary>
        /// PeerConnection에 대한 코덱 우선순위를 설정합니다.
        /// H264를 우선으로 하여 모바일 하드웨어 디코더 호환성을 개선합니다.
        /// </summary>
        public static void ConfigureCodecPreferences(RTCPeerConnection peerConnection)
        {
            if (peerConnection == null)
            {
                Debug.LogError("[WebRtcCodecHelper] PeerConnection is null");
                return;
            }

            try
            {
                var transceivers = peerConnection.GetTransceivers();
                foreach (var transceiver in transceivers)
                {
                    // Unity WebRTC의 버전에 따라 다른 방법으로 미디어 타입 확인
                    bool isVideoTransceiver = false;
                    
                    // 방법 1: Receiver의 Track을 통해 확인
                    if (transceiver.Receiver?.Track != null)
                    {
                        isVideoTransceiver = transceiver.Receiver.Track is VideoStreamTrack;
                    }
                    // 방법 2: Direction이 설정된 경우 Sender의 Track 확인
                    else if (transceiver.Sender?.Track != null)
                    {
                        isVideoTransceiver = transceiver.Sender.Track is VideoStreamTrack;
                    }
                    // 방법 3: Mid 속성을 통한 확인 (SDP에서 video로 시작하는 경우)
                    else if (!string.IsNullOrEmpty(transceiver.Mid))
                    {
                        isVideoTransceiver = transceiver.Mid.ToLower().StartsWith("video");
                    }
                    
                    if (isVideoTransceiver)
                    {
                        var capabilities = RTCRtpReceiver.GetCapabilities(TrackKind.Video);
                        if (capabilities?.codecs != null)
                        {
                            // H264 코덱을 우선순위로 설정 (모바일 하드웨어 디코더 지원)
                            var preferredCodecs = new List<RTCRtpCodecCapability>();
                            
                            // H264 Baseline Profile (가장 넓은 호환성)
                            var h264Codecs = capabilities.codecs.Where(c => 
                                c.mimeType.ToLower().Contains("h264") &&
                                (c.sdpFmtpLine == null || 
                                 c.sdpFmtpLine.ToLower().Contains("profile-level-id=42") || // Baseline
                                 c.sdpFmtpLine.ToLower().Contains("profile-level-id=42e0"))) // Baseline
                                .ToList();
                            
                            if (h264Codecs.Count > 0)
                            {
                                preferredCodecs.AddRange(h264Codecs);
                                Debug.Log($"[WebRtcCodecHelper] Found {h264Codecs.Count} H264 codec(s) with baseline profile");
                            }
                            
                            // VP8 as fallback (good mobile support)
                            var vp8Codecs = capabilities.codecs.Where(c => 
                                c.mimeType.ToLower().Contains("vp8")).ToList();
                            
                            if (vp8Codecs.Count > 0)
                            {
                                preferredCodecs.AddRange(vp8Codecs);
                                Debug.Log($"[WebRtcCodecHelper] Found {vp8Codecs.Count} VP8 codec(s) as fallback");
                            }
                            
                            // Add remaining codecs
                            var remainingCodecs = capabilities.codecs.Where(c => 
                                !preferredCodecs.Contains(c)).ToList();
                            preferredCodecs.AddRange(remainingCodecs);
                            
                            // Set codec preferences
                            transceiver.SetCodecPreferences(preferredCodecs.ToArray());
                            
                            Debug.Log($"[WebRtcCodecHelper] Set codec preferences for video transceiver. " +
                                     $"Total codecs: {preferredCodecs.Count}, Preferred: {preferredCodecs[0].mimeType}");
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[WebRtcCodecHelper] Failed to configure codec preferences: {e.Message}");
            }
        }

        /// <summary>
        /// SDP를 수정하여 모바일 호환성을 개선합니다.
        /// </summary>
        public static string OptimizeSdpForMobile(string sdp)
        {
            if (string.IsNullOrEmpty(sdp))
                return sdp;

            var lines = sdp.Split('\n').ToList();
            var modifiedLines = new List<string>();

            foreach (var line in lines)
            {
                var modifiedLine = line;
                
                // H264 profile-level-id를 baseline으로 강제 설정
                if (line.Contains("profile-level-id") && !line.Contains("profile-level-id=42"))
                {
                    // Baseline profile (42e01f)로 변경
                    modifiedLine = System.Text.RegularExpressions.Regex.Replace(
                        line, 
                        @"profile-level-id=\w+", 
                        "profile-level-id=42e01f"
                    );
                    Debug.Log($"[WebRtcCodecHelper] Modified H264 profile to baseline: {modifiedLine.Trim()}");
                }
                
                // 비디오 대역폭 제한 추가
                if (line.StartsWith("m=video") && !sdp.Contains("b=AS:"))
                {
                    modifiedLines.Add(modifiedLine);
                    modifiedLines.Add("b=AS:1000"); // 1000 kbps max bandwidth
                    Debug.Log("[WebRtcCodecHelper] Added bandwidth constraint: b=AS:1000");
                    continue;
                }
                
                modifiedLines.Add(modifiedLine);
            }

            return string.Join("\n", modifiedLines);
        }

        /// <summary>
        /// 비디오 트랙의 설정을 모바일에 최적화합니다.
        /// </summary>
        public static void OptimizeVideoTrackForMobile(VideoStreamTrack videoTrack)
        {
            if (videoTrack == null)
            {
                Debug.LogError("[WebRtcCodecHelper] VideoStreamTrack is null");
                return;
            }

            // 트랙 활성화
            if (!videoTrack.Enabled)
            {
                videoTrack.Enabled = true;
            }

            Debug.Log($"[WebRtcCodecHelper] Optimized video track for mobile: {videoTrack.Id}");
        }
    }
}