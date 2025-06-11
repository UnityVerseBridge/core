using System;
using System.Collections;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;

namespace UnityVerseBridge.Core
{
    /// <summary>
    /// Handles WebRTC connection logic for a single peer connection.
    /// This class extracts the core WebRTC functionality to be reused by both
    /// WebRtcManager (1:1 connections) and MultiPeerWebRtcManager (1:N connections).
    /// </summary>
    public class WebRtcConnectionHandler
    {
        private RTCPeerConnection peerConnection;
        private RTCDataChannel dataChannel;
        private readonly string peerId;
        private readonly bool isOfferer;
        private readonly WebRtcConfiguration configuration;
        
        // Track management
        private readonly Dictionary<MediaStreamTrack, RTCRtpSender> trackSenders = new Dictionary<MediaStreamTrack, RTCRtpSender>();
        
        // Events
        public event Action<string> OnDataChannelMessage;
        public event Action<RTCDataChannel> OnDataChannelOpen;
        public event Action<RTCDataChannel> OnDataChannelClose;
        public event Action<MediaStreamTrack> OnVideoTrackReceived;
        public event Action<MediaStreamTrack> OnAudioTrackReceived;
        public event Action<RTCIceCandidate> OnIceCandidateGenerated;
        public event Action OnConnectionStateChanged;
        public event Action OnNegotiationNeeded;
        
        // State properties
        public RTCPeerConnectionState ConnectionState => peerConnection?.ConnectionState ?? RTCPeerConnectionState.Closed;
        public RTCIceConnectionState IceConnectionState => peerConnection?.IceConnectionState ?? RTCIceConnectionState.Closed;
        public RTCDataChannelState DataChannelState => dataChannel?.ReadyState ?? RTCDataChannelState.Closed;
        public bool IsConnected => ConnectionState == RTCPeerConnectionState.Connected;
        public bool IsDataChannelOpen => DataChannelState == RTCDataChannelState.Open;
        
        public WebRtcConnectionHandler(string peerId, bool isOfferer, WebRtcConfiguration configuration)
        {
            this.peerId = peerId;
            this.isOfferer = isOfferer;
            this.configuration = configuration;
        }
        
        /// <summary>
        /// Initialize the peer connection with the specified configuration
        /// </summary>
        public void Initialize()
        {
            if (peerConnection != null)
            {
                Debug.LogWarning($"[WebRtcConnectionHandler] Connection for peer {peerId} already initialized");
                return;
            }
            
            var rtcConfig = configuration.ToRTCConfiguration();
            peerConnection = new RTCPeerConnection(ref rtcConfig);
            
            // Setup event handlers
            peerConnection.OnIceCandidate = candidate => OnIceCandidateGenerated?.Invoke(candidate);
            peerConnection.OnConnectionStateChange = state => 
            {
                Debug.Log($"[WebRtcConnectionHandler] Peer {peerId} connection state: {state}");
                OnConnectionStateChanged?.Invoke();
            };
            peerConnection.OnIceConnectionChange = state => 
            {
                Debug.Log($"[WebRtcConnectionHandler] Peer {peerId} ICE connection state: {state}");
            };
            peerConnection.OnNegotiationNeeded = () => OnNegotiationNeeded?.Invoke();
            peerConnection.OnTrack = evt => HandleTrackReceived(evt);
            
            // Create data channel for offerer
            if (isOfferer)
            {
                CreateDataChannel();
            }
            else
            {
                // For answerer, wait for data channel from offerer
                peerConnection.OnDataChannel = channel =>
                {
                    Debug.Log($"[WebRtcConnectionHandler] Data channel received from peer {peerId}");
                    SetupDataChannel(channel);
                };
            }
        }
        
        /// <summary>
        /// Create a WebRTC offer
        /// </summary>
        public IEnumerator CreateOffer(Action<RTCSessionDescription> onSuccess, Action<string> onError)
        {
            if (peerConnection == null)
            {
                onError?.Invoke("Peer connection not initialized");
                yield break;
            }
            
            var offerOptions = new RTCOfferAnswerOptions
            {
                iceRestart = false
            };
            
            var op = peerConnection.CreateOffer(ref offerOptions);
            yield return op;
            
            if (op.IsError)
            {
                onError?.Invoke($"Create offer failed: {op.Error.message}");
                yield break;
            }
            
            var desc = op.Desc;
            var setOp = peerConnection.SetLocalDescription(ref desc);
            yield return setOp;
            
            if (setOp.IsError)
            {
                onError?.Invoke($"Set local description failed: {setOp.Error.message}");
                yield break;
            }
            
            onSuccess?.Invoke(desc);
        }
        
        /// <summary>
        /// Create a WebRTC answer
        /// </summary>
        public IEnumerator CreateAnswer(Action<RTCSessionDescription> onSuccess, Action<string> onError)
        {
            if (peerConnection == null)
            {
                onError?.Invoke("Peer connection not initialized");
                yield break;
            }
            
            var answerOptions = new RTCOfferAnswerOptions
            {
                iceRestart = false
            };
            
            var op = peerConnection.CreateAnswer(ref answerOptions);
            yield return op;
            
            if (op.IsError)
            {
                onError?.Invoke($"Create answer failed: {op.Error.message}");
                yield break;
            }
            
            var desc = op.Desc;
            var setOp = peerConnection.SetLocalDescription(ref desc);
            yield return setOp;
            
            if (setOp.IsError)
            {
                onError?.Invoke($"Set local description failed: {setOp.Error.message}");
                yield break;
            }
            
            onSuccess?.Invoke(desc);
        }
        
        /// <summary>
        /// Handle received offer
        /// </summary>
        public IEnumerator HandleOffer(string sdp, Action onSuccess, Action<string> onError)
        {
            if (peerConnection == null)
            {
                onError?.Invoke("Peer connection not initialized");
                yield break;
            }
            
            var desc = new RTCSessionDescription
            {
                type = RTCSdpType.Offer,
                sdp = sdp
            };
            
            var op = peerConnection.SetRemoteDescription(ref desc);
            yield return op;
            
            if (op.IsError)
            {
                onError?.Invoke($"Set remote description failed: {op.Error.message}");
                yield break;
            }
            
            onSuccess?.Invoke();
        }
        
        /// <summary>
        /// Handle received answer
        /// </summary>
        public IEnumerator HandleAnswer(string sdp, Action onSuccess, Action<string> onError)
        {
            if (peerConnection == null)
            {
                onError?.Invoke("Peer connection not initialized");
                yield break;
            }
            
            var desc = new RTCSessionDescription
            {
                type = RTCSdpType.Answer,
                sdp = sdp
            };
            
            var op = peerConnection.SetRemoteDescription(ref desc);
            yield return op;
            
            if (op.IsError)
            {
                onError?.Invoke($"Set remote description failed: {op.Error.message}");
                yield break;
            }
            
            onSuccess?.Invoke();
        }
        
        /// <summary>
        /// Add ICE candidate
        /// </summary>
        public void AddIceCandidate(RTCIceCandidate candidate)
        {
            if (peerConnection == null)
            {
                Debug.LogError($"[WebRtcConnectionHandler] Cannot add ICE candidate - peer connection not initialized");
                return;
            }
            
            peerConnection.AddIceCandidate(candidate);
        }
        
        /// <summary>
        /// Add video track to the connection
        /// </summary>
        public RTCRtpSender AddVideoTrack(MediaStreamTrack track)
        {
            if (peerConnection == null || track == null)
            {
                Debug.LogError($"[WebRtcConnectionHandler] Cannot add video track - invalid state");
                return null;
            }
            
            var sender = peerConnection.AddTrack(track);
            trackSenders[track] = sender;
            return sender;
        }
        
        /// <summary>
        /// Add audio track to the connection
        /// </summary>
        public RTCRtpSender AddAudioTrack(MediaStreamTrack track)
        {
            if (peerConnection == null || track == null)
            {
                Debug.LogError($"[WebRtcConnectionHandler] Cannot add audio track - invalid state");
                return null;
            }
            
            var sender = peerConnection.AddTrack(track);
            trackSenders[track] = sender;
            return sender;
        }
        
        /// <summary>
        /// Remove track from the connection
        /// </summary>
        public void RemoveTrack(MediaStreamTrack track)
        {
            if (track == null || !trackSenders.ContainsKey(track))
            {
                return;
            }
            
            var sender = trackSenders[track];
            peerConnection?.RemoveTrack(sender);
            trackSenders.Remove(track);
        }
        
        /// <summary>
        /// Send data through the data channel
        /// </summary>
        public void SendDataChannelMessage(string message)
        {
            if (!IsDataChannelOpen)
            {
                Debug.LogWarning($"[WebRtcConnectionHandler] Cannot send message - data channel not open");
                return;
            }
            
            dataChannel.Send(message);
        }
        
        /// <summary>
        /// Close the connection and cleanup resources
        /// </summary>
        public void Close()
        {
            // Close data channel
            if (dataChannel != null)
            {
                dataChannel.Close();
                dataChannel.Dispose();
                dataChannel = null;
            }
            
            // Remove all tracks
            foreach (var track in new List<MediaStreamTrack>(trackSenders.Keys))
            {
                RemoveTrack(track);
            }
            trackSenders.Clear();
            
            // Close peer connection
            if (peerConnection != null)
            {
                peerConnection.Close();
                peerConnection.Dispose();
                peerConnection = null;
            }
        }
        
        private void CreateDataChannel()
        {
            var config = new RTCDataChannelInit
            {
                ordered = true
            };
            
            var channel = peerConnection.CreateDataChannel("data", config);
            SetupDataChannel(channel);
        }
        
        private void SetupDataChannel(RTCDataChannel channel)
        {
            dataChannel = channel;
            
            dataChannel.OnOpen = () =>
            {
                Debug.Log($"[WebRtcConnectionHandler] Data channel opened for peer {peerId}");
                OnDataChannelOpen?.Invoke(dataChannel);
            };
            
            dataChannel.OnClose = () =>
            {
                Debug.Log($"[WebRtcConnectionHandler] Data channel closed for peer {peerId}");
                OnDataChannelClose?.Invoke(dataChannel);
            };
            
            dataChannel.OnMessage = bytes =>
            {
                var message = System.Text.Encoding.UTF8.GetString(bytes);
                OnDataChannelMessage?.Invoke(message);
            };
        }
        
        private void HandleTrackReceived(RTCTrackEvent evt)
        {
            var track = evt.Track;
            Debug.Log($"[WebRtcConnectionHandler] Track received from peer {peerId}: {track.Kind}");
            
            if (track is VideoStreamTrack videoTrack)
            {
                OnVideoTrackReceived?.Invoke(videoTrack);
            }
            else if (track is AudioStreamTrack audioTrack)
            {
                OnAudioTrackReceived?.Invoke(audioTrack);
            }
        }
    }
}