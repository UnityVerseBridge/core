using System; // Action, Action<T1, T2> 사용 위해
using System.Threading.Tasks; // Task 사용 위해
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools; // [UnityTest] 사용 시 필요
using System.Collections;
using System.Collections.Generic;
using UnityVerseBridge.Core.Signaling; // ISignalingClient
using UnityVerseBridge.Core.Signaling.Data; // SignalingMessageBase 등
using UnityVerseBridge.Core.DataChannel.Data; // TouchData 사용 위해
using System.Linq;

namespace UnityVerseBridge.Core.Tests.Runtime
{
    public class WebRtcManagerTests
    {
        // --- Mock Signaling Client (테스트를 위해 인터페이스 구현) ---
        private class MockSignalingClient : ISignalingClient
        {
            public bool ConnectCalled { get; private set; }
            public string ConnectUrl { get; private set; }
            public bool DisconnectCalled { get; private set; }
            public List<SignalingMessageBase> SentMessages { get; private set; } = new List<SignalingMessageBase>(); // <-- 메시지 리스트 추가
            public int SendMessageCallCount => SentMessages.Count; // 카운트는 리스트 크기로 계산
            
            // IsConnected 속성 구현 추가 (테스트에서 상태 설정 가능하도록 public setter 추가)
            public bool IsConnected { get; set; } = false; // 기본값은 false

            // 테스트에서 이벤트를 수동으로 발생시키기 위한 Action
            public event Action OnConnected;
            public event Action OnDisconnected;
            public event Action<string, string> OnSignalingMessageReceived;

            /// <summary>
            /// ISignalingClient 인터페이스 구현을 위한 InitializeAndConnect 메서드입니다.
            /// Mock 객체에서는 실제 연결 대신 상태를 설정하고 이벤트를 발생시킬 수 있습니다.
            /// </summary>
            /// <param name="adapter">사용될 WebSocket 어댑터 (Mock에서는 사용 안 함)</param>
            /// <param name="url">접속할 서버 URL (기록만 할 수 있음)</param>
            /// <returns>완료된 Task</returns>
            public Task InitializeAndConnect(IWebSocketClient adapter, string url)
            {
                Debug.Log($"[Mock] InitializeAndConnect called with URL: {url} and Adapter: {adapter?.GetType().Name ?? "null"}");
                // Connect 메서드와 유사하게 처리하거나, 필요에 따라 상태만 설정
                ConnectCalled = true; // Connect 호출된 것으로 간주
                ConnectUrl = url;
                IsConnected = true;   // 즉시 연결 성공 상태로 시뮬레이션
                OnConnected?.Invoke(); // 연결 성공 이벤트 발생
                return Task.CompletedTask; // 즉시 완료된 Task 반환
            }

            public Task Connect(string url)
            {
                Debug.LogWarning("[Mock] Connect called, but InitializeAndConnect should be used for setup.");
                // return InitializeAndConnect(null, url); // 또는 이렇게 연결하거나 예외 발생
                return Task.CompletedTask;
            }

            public Task Disconnect()
            {
                DisconnectCalled = true;
                IsConnected = false; // 연결 종료 시뮬레이션
                OnDisconnected?.Invoke(); // 연결 종료 이벤트 발생
                return Task.CompletedTask;
            }

            public Task SendMessage<T>(T message) where T : SignalingMessageBase
            {
                // LastSentMessage = message;
                SentMessages.Add(message); // <-- 리스트에 추가
                Debug.Log($"[Mock] SendMessage Called: Type={message.type}, Count={SentMessages.Count}");
                return Task.CompletedTask;
            }

            // 테스트 시작 시 리스트 초기화를 위한 메서드 추가 (선택 사항)
            public void Reset()
            {
                ConnectCalled = false;
                ConnectUrl = null;
                DisconnectCalled = false;
                SentMessages.Clear();
                IsConnected = false;
            }

            // 테스트 코드에서 Mock 객체의 이벤트를 직접 발생시키기 위한 메서드
            public void TriggerConnected() => OnConnected?.Invoke();
            public void TriggerDisconnected() => OnDisconnected?.Invoke();
            public void TriggerMessageReceived(string type, string jsonData) => OnSignalingMessageReceived?.Invoke(type, jsonData);

            /// <summary>
            /// ISignalingClient 인터페이스 구현을 위한 DispatchMessages 메서드입니다.
            /// Mock 객체에서는 특별한 동작 없이 로그만 남기거나 비워둘 수 있습니다.
            /// </summary>
            public void DispatchMessages()
            {
                // Debug.Log("[Mock] DispatchMessages() Called"); // 필요시 로그 추가
                // 실제 로직은 없음 (NativeWebSocketAdapter 등이 실제 처리를 함)
            }
        }


        // --- Test Fixture Setup ---
        private GameObject testGameObject;
        private WebRtcManager webRtcManager;
        private MockSignalingClient mockSignalingClient;
        private WebRtcConfiguration testConfiguration;

        [SetUp] // 각 테스트 실행 전에 호출됨
        public void Setup()
        {
            // 테스트를 위한 GameObject 및 WebRtcManager 컴포넌트 생성
            testGameObject = new GameObject("WebRtcManagerTest");
            webRtcManager = testGameObject.AddComponent<WebRtcManager>();

            // Mock Signaling Client 생성 및 주입 (실제로는 Initialize 등을 통해 주입)
            mockSignalingClient = new MockSignalingClient();
            testConfiguration = new WebRtcConfiguration(); // 기본 설정 사용

            // Reflection 대신 새로 만든 메서드로 깔끔하게 주입 및 초기화
            webRtcManager.InitializeForTest(mockSignalingClient, testConfiguration);
        }

        [TearDown] // 각 테스트 실행 후에 호출됨
        public void Teardown()
        {
            // 테스트 오브젝트 파괴
            GameObject.DestroyImmediate(testGameObject);
        }

        // --- Test Cases ---

        [UnityTest]
        public IEnumerator StartPeerConnection_WhenSignalingConnected_ShouldTryToSendOffer()
        {
            // Arrange
            // Setup에서 InitializeForTest 호출됨
            mockSignalingClient.Reset(); // Mock 상태 초기화
            mockSignalingClient.IsConnected = true; // <-- 시그널링 연결 상태로 설정!
            webRtcManager.InitializeForTest(mockSignalingClient, testConfiguration); // Setup에서 호출되지만 명시적으로
            mockSignalingClient.TriggerConnected(); // 연결 상태 및 이벤트 발생 보장

            int initialSendCount = mockSignalingClient.SendMessageCallCount;

            // Act
            webRtcManager.StartPeerConnection();

            // Offer 메시지가 전송될 때까지 대기 (타임아웃 포함)
            float timeout = Time.time + 3.0f;
            yield return new WaitUntil(() => mockSignalingClient.SentMessages.Any(msg => msg.type == "offer") || Time.time > timeout);

            // Assert
            Assert.Less(Time.time, timeout, "Test timed out waiting for Offer message.");
            Assert.IsTrue(mockSignalingClient.SentMessages.Any(msg => msg.type == "offer"), "An 'offer' message should have been sent.");

            // Assert
            // SendMessage가 최소 1번 이상 호출되었는지 (Offer + ICE Candidates)
            Assert.GreaterOrEqual(mockSignalingClient.SendMessageCallCount, 1, "SendMessage should have been called at least once.");

            // 전송된 메시지 목록(SentMessages) 중에 'offer' 타입 메시지가 있는지 확인
            bool offerWasSent = mockSignalingClient.SentMessages.Any(msg => msg.type == "offer");
            Assert.IsTrue(offerWasSent, "An 'offer' message should have been sent.");

            // (선택 사항) Offer 메시지 내용 검증
            var offerMessage = mockSignalingClient.SentMessages.FirstOrDefault(msg => msg.type == "offer") as SessionDescriptionMessage;
            Assert.IsNotNull(offerMessage, "Offer message object should exist.");
            // Assert.IsNotEmpty(offerMessage.sdp, "Offer SDP should not be empty."); // SDP 내용 검증

            // 추가: 테스트 실패 시 원인 파악을 위해 로그 추가
            if (mockSignalingClient.SendMessageCallCount == 0)
            {
                Debug.LogError("SendMessage for Offer was NOT called!");
                // webRtcManager 내부 상태 로그 등을 추가하면 더 좋음
            }
        }

        [UnityTest]
        public IEnumerator HandleSignalingMessage_OfferReceived_ShouldTryToSendAnswer()
        {
            // Arrange
            mockSignalingClient.Reset(); // Mock 상태 초기화
            mockSignalingClient.IsConnected = true; // 연결 상태 설정
            // InitializeForTest는 Setup에서 호출됨

            string offerSdp = "v=0\r\no=- 12345 67890 IN IP4 127.0.0.1\r\ns=-\r\nt=0 0\r\n"; // 유효한 SDP 형식
            string offerJson = JsonUtility.ToJson(new SessionDescriptionMessage("offer", offerSdp));
            int initialSendCount = mockSignalingClient.SendMessageCallCount;

            // Act
            mockSignalingClient.TriggerMessageReceived("offer", offerJson); // Offer 수신 시뮬레이션
            // !!! 이 메서드 안에 webRtcManager.StartPeerConnection(); 호출이 있으면 안 됨 !!!

            // Coroutine 완료 및 ICE Candidate 생성 시간 고려하여 잠시 대기
            // 정확한 대기를 위해서는 WebRtcManager에서 Offer 전송 완료 이벤트를 발생시키는 것이 가장 좋음
            float timeout = Time.time + 3.0f; // 3초 타임아웃
            yield return new WaitUntil(() => mockSignalingClient.SentMessages.Any(msg => msg.type == "answer") || Time.time > timeout);

             Assert.Less(Time.time, timeout, "Test timed out waiting for Answer message."); // 타임아웃 메시지 확인

            // SendMessage가 최소 한 번 호출되었는지 확인 (Answer + ICE 가능성)
            // Assert.Greater(mockSignalingClient.SendMessageCallCount, initialSendCount, "SendMessage should have been called for Answer.");

            // 전송된 메시지 목록에 'answer' 타입 메시지가 있는지 확인
            bool answerWasSent = mockSignalingClient.SentMessages.Any(msg => msg.type == "answer");
            Assert.IsTrue(answerWasSent, "An 'answer' message should have been sent."); // <--- 실패 메시지 문자열 수정!

            // (선택 사항) Answer 메시지 내용 검증
            if (answerWasSent)
            {
                var answerMessage = mockSignalingClient.SentMessages.FirstOrDefault(msg => msg.type == "answer") as SessionDescriptionMessage;
                Assert.IsNotNull(answerMessage, "Answer message object should exist.");
                // Assert.IsNotEmpty(answerMessage.sdp, "Answer SDP should not be empty.");
            }
        }

        [UnityTest]
        public IEnumerator SignalingConnected_Should_AutomaticallyStartPeerConnectionAndSendOffer()
        {
            // Arrange
            // Setup에서 WebRtcManager와 Mock 초기화 완료 가정
            // webRtcManager.InitializeForTest(mockSignalingClient, testConfiguration); // Setup에서 호출됨
            mockSignalingClient.Reset();
            // 초기 상태는 시그널링 연결 안 됨
            mockSignalingClient.IsConnected = false;
            int initialSendCount = mockSignalingClient.SendMessageCallCount;

            // Act
            // 시그널링 연결 성공 이벤트를 강제로 발생시킴
            mockSignalingClient.TriggerConnected(); // 이 호출로 인해 mockClient.IsConnected = true 가 되고 OnConnected 이벤트 발생 -> WebRtcManager.HandleSignalingConnected 호출 -> StartPeerConnection 호출 기대

            // StartPeerConnection 내부의 CreateOfferAndSend 코루틴이 실행되고
            // Offer 메시지를 SendMessage 할 때까지 기다림
            float timeout = Time.time + 3.0f; // 3초 타임아웃
            yield return new WaitUntil(() => mockSignalingClient.SentMessages.Any(msg => msg.type == "offer") || Time.time > timeout);

            // Assert
            Assert.Less(Time.time, timeout, "Test timed out waiting for Offer to be sent automatically.");
            Assert.IsTrue(mockSignalingClient.SentMessages.Any(msg => msg.type == "offer"), "An 'offer' message should have been sent automatically after signaling connected.");
        }

        [Test] // 이 테스트는 동기적으로 호출 가능 여부만 확인하므로 [Test] 사용 가능
        public void SendDataChannelMessage_WhenConnected_ShouldNotThrowError()
        {
            // Arrange
            // Setup에서 초기화 완료 가정
            mockSignalingClient.Reset();
            mockSignalingClient.IsConnected = true;
            // 중요: WebRTC 연결 및 데이터 채널이 열렸다고 가정하는 상태 설정 필요
            // 이는 Mocking 한계로 정확히 시뮬레이션 어려움.
            // WebRtcManager 내부 상태를 직접 설정하거나, 관련 public 속성(IsWebRtcConnected, IsDataChannelOpen)을 테스트용으로 수정 필요.
            // 여기서는 IsDataChannelOpen 속성이 true라고 가정. (실제로는 이 가정이 필요 없도록 테스트하거나, IsDataChannelOpen 상태를 설정할 방법을 찾아야 함)
            // 예시: Reflection으로 내부 dataChannel 상태를 Open으로 설정 (복잡하고 비권장)
            // 또는 WebRtcManager에 테스트용 메서드 추가: webRtcManager.SetDataChannelStateForTest(RTCDataChannelState.Open);

            var testMessage = new TouchData(1, DataChannel.Data.TouchPhase.Began, Vector2.one * 0.5f); // 샘플 데이터
            int initialSignalingSendCount = mockSignalingClient.SendMessageCallCount;

            // Act & Assert
            // SendDataChannelMessage 호출 시 예외가 발생하지 않는지 확인
            Assert.DoesNotThrow(() => {
                webRtcManager.SendDataChannelMessage(testMessage);
            }, "SendDataChannelMessage should not throw an exception when DC is assumed open.");

            // 시그널링으로 메시지가 가지 않았는지 확인 (데이터 채널로 가야 함)
            Assert.AreEqual(initialSignalingSendCount, mockSignalingClient.SendMessageCallCount, "SendDataChannelMessage should not send messages via signaling.");
        }
    }
}