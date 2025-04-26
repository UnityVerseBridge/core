using System; // Action, Action<T1, T2> 사용 위해
using System.Threading.Tasks; // Task 사용 위해
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools; // [UnityTest] 사용 시 필요
using System.Collections;
using System.Collections.Generic;
using UnityVerseBridge.Core; // WebRtcManager
using UnityVerseBridge.Core.Signaling; // ISignalingClient
using UnityVerseBridge.Core.Signaling.Data; // SignalingMessageBase 등
using UnityVerseBridge.Core.Common; // WebRtcConfiguration
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

            public Task Connect(string url)
            {
                ConnectCalled = true;
                ConnectUrl = url;
                IsConnected = true; // 연결 성공 시뮬레이션
                OnConnected?.Invoke(); // 연결 이벤트 발생
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

            // Act
            webRtcManager.StartPeerConnection();

            // Coroutine이 SendMessage를 호출할 시간을 주기 위해 한 프레임 이상 대기
            yield return new WaitForSeconds(0.1f);
            // 필요하다면 특정 상태 변화를 기다리는 yield return new WaitUntil(() => mockSignalingClient.SendMessageCallCount > 0); 사용

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
            // webRtcManager.InitializeForTest(mockSignalingClient, testConfiguration); // Setup에서 이미 호출됨
            mockSignalingClient.Reset(); // Mock 상태 초기화
            mockSignalingClient.IsConnected = true; // 연결 상태 설정

            // Act
            webRtcManager.StartPeerConnection();

            // Coroutine 완료 및 ICE Candidate 생성 시간 고려하여 잠시 대기
            // 정확한 대기를 위해서는 WebRtcManager에서 Offer 전송 완료 이벤트를 발생시키는 것이 가장 좋음
            // 여기서는 충분히 기다린다고 가정 (예: 0.1초)
            yield return new WaitForSeconds(0.1f); // 또는 WaitUntil 사용

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

            // TODO: Answer 수신 시 RemoteDescription 설정 로직 검증 (Mocking 한계로 어려움)
            // TODO: ICE Candidate 수신 시 AddIceCandidate 호출 로직 검증 (Mocking 한계로 어려움)
            // TODO: ICE Candidate 생성 시 SendMessage 호출 검증 (PeerConnection 이벤트 Mocking 필요)
        }
    }
}