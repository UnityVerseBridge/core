using NUnit.Framework; // Unity Test Framework (NUnit) 사용
using UnityEngine; // JsonUtility 사용
using UnityVerseBridge.Core.Signaling.Data; // 테스트 대상 데이터 구조

namespace UnityVerseBridge.Core.Tests.Runtime
{
    public class SignalingMessageSerializationTests
    {
        [Test]
        public void SessionDescriptionMessage_Offer_SerializesAndDeserializesCorrectly()
        {
            // Arrange
            string testSdp = "v=0\r\no=- 12345 67890 IN IP4 127.0.0.1\r\n..."; // 실제 SDP 형식 예시 (간략화)
            var originalMessage = new SessionDescriptionMessage("offer", testSdp);

            // Act
            string json = JsonUtility.ToJson(originalMessage);
            Debug.Log($"Serialized Offer JSON: {json}"); // 로그 확인 (선택 사항)
            var deserializedMessage = JsonUtility.FromJson<SessionDescriptionMessage>(json);

            // Assert
            Assert.IsNotNull(deserializedMessage);
            Assert.AreEqual(originalMessage.type, deserializedMessage.type);
            Assert.AreEqual(originalMessage.sdp, deserializedMessage.sdp);
        }

        [Test]
        public void SessionDescriptionMessage_Answer_SerializesAndDeserializesCorrectly()
        {
            // Arrange
            string testSdp = "v=0\r\no=- 98765 43210 IN IP4 127.0.0.1\r\n...";
            var originalMessage = new SessionDescriptionMessage("answer", testSdp);

            // Act
            string json = JsonUtility.ToJson(originalMessage);
             Debug.Log($"Serialized Answer JSON: {json}");
            var deserializedMessage = JsonUtility.FromJson<SessionDescriptionMessage>(json);

            // Assert
            Assert.IsNotNull(deserializedMessage);
            Assert.AreEqual(originalMessage.type, deserializedMessage.type);
            Assert.AreEqual(originalMessage.sdp, deserializedMessage.sdp);
        }

        [Test]
        public void IceCandidateMessage_SerializesAndDeserializesCorrectly()
        {
            // Arrange
            string testCandidate = "candidate:12345 1 udp 2122260223 192.168.0.100 54321 typ host";
            string testSdpMid = "0";
            int? testSdpMLineIndex = 0; // Nullable int? 로부터 생성 시뮬레이션
            var originalMessage = new IceCandidateMessage(testCandidate, testSdpMid, testSdpMLineIndex);

            // Act
            string json = JsonUtility.ToJson(originalMessage);
             Debug.Log($"Serialized ICE Candidate JSON: {json}");
            var deserializedMessage = JsonUtility.FromJson<IceCandidateMessage>(json);

            // Assert
            Assert.IsNotNull(deserializedMessage);
            Assert.AreEqual(originalMessage.type, deserializedMessage.type);
            Assert.AreEqual(originalMessage.candidate, deserializedMessage.candidate);
            Assert.AreEqual(originalMessage.sdpMid, deserializedMessage.sdpMid);
            // JsonUtility는 nullable int를 잘 처리하지 못할 수 있으므로 non-nullable int로 비교
            Assert.AreEqual(originalMessage.sdpMLineIndex, deserializedMessage.sdpMLineIndex);
        }

        [Test]
        public void IceCandidateMessage_HandlesNullSdpMLineIndexCorrectly()
        {
            // Arrange
            // RTCIceCandidate 생성 시 SdpMLineIndex가 null일 경우 시뮬레이션
             string testCandidate = "candidate:...";
            string testSdpMid = "1";
            int? testSdpMLineIndex = null; // Nullable int? 가 null인 경우
            var originalMessage = new IceCandidateMessage(testCandidate, testSdpMid, testSdpMLineIndex);

            // Act
            string json = JsonUtility.ToJson(originalMessage);
            Debug.Log($"Serialized ICE Candidate (Null Index) JSON: {json}");
            var deserializedMessage = JsonUtility.FromJson<IceCandidateMessage>(json);

             // Assert
            Assert.IsNotNull(deserializedMessage);
            // 생성자에서 null일 경우 0으로 처리했는지 확인 (또는 정의된 다른 기본값)
            Assert.AreEqual(0, deserializedMessage.sdpMLineIndex);
             Assert.AreEqual(originalMessage.candidate, deserializedMessage.candidate);
            Assert.AreEqual(originalMessage.sdpMid, deserializedMessage.sdpMid);
        }
    }
}