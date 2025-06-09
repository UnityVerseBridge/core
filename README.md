# UnityVerseBridge Core Package

Unity 환경에서 WebRTC를 통해 VR 기기(Quest)와 모바일 기기 간 실시간 통신을 구현하는 핵심 패키지입니다.

## 🎯 개요

이 패키지는 다음 기능을 제공합니다:
- WebRTC P2P 연결 관리 (1:1 및 1:N 지원)
- 시그널링 서버 통신
- 비디오/오디오 스트리밍
- 양방향 오디오 통신 지원
- 실시간 데이터 채널 통신 (터치, 햅틱 등)
- 플랫폼별 WebSocket 어댑터

## 🏗️ 아키텍처

```
UnityVerseBridge.Core
├── WebRTC 관리
│   ├── WebRtcManager (1:1 연결)
│   ├── MultiPeerWebRtcManager (1:N 연결)
│   ├── AudioStreamManager (양방향 오디오)
│   └── DataChannel 통신
├── 시그널링 (SignalingClient)
│   ├── WebSocket 연결
│   ├── SDP/ICE 교환
│   ├── 룸 기반 매칭
│   └── 타겟팅된 메시지 지원
└── 플랫폼 어댑터
    ├── SystemWebSocketAdapter (Quest/Editor)
    └── NativeWebSocketAdapter (Mobile)
```

## 📦 설치 방법

### Package Manager를 통한 설치

1. Unity 프로젝트에서 Window > Package Manager 열기
2. `+` 버튼 클릭 > "Add package from git URL..."
3. 다음 URL 입력:
   ```
   https://github.com/UnityVerseBridge/core.git
   ```

### 로컬 패키지로 설치

1. Unity Package Manager 열기
2. `+` 버튼 > "Add package from disk..."
3. `core/package.json` 파일 선택

## 🔧 필수 의존성

- Unity 6 LTS (6000.0.33f1) 이상 또는 Unity 2022.3 LTS 이상
- Unity WebRTC Package 3.0.0-pre.8 이상
- TextMeshPro

## 📁 프로젝트 구조

```
Runtime/
├── WebRtcManager.cs          # WebRTC 1:1 연결 관리
├── MultiPeerWebRtcManager.cs # WebRTC 1:N 연결 관리
├── AudioStreamManager.cs     # 양방향 오디오 스트리밍
├── WebRtcConfiguration.cs    # 설정 데이터 구조
├── ConnectionConfig.cs       # 연결 설정 ScriptableObject
├── Signaling/
│   ├── SignalingClient.cs    # 시그널링 로직
│   ├── IWebSocketClient.cs   # WebSocket 인터페이스
│   ├── Adapters/             # 플랫폼별 WebSocket 구현
│   ├── Messages/             # 메시지 타입 정의
│   └── Data/
│       └── RoomMessages.cs   # 룸 기반 메시지 타입
├── DataChannel/
│   └── Data/                 # 데이터 구조체
└── Utils/
    └── AuthenticationHelper.cs
```

## 💡 핵심 컴포넌트 설명

### WebRtcManager
WebRTC 연결의 전체 생명주기를 관리하는 중앙 컴포넌트입니다.

**주요 기능:**
- PeerConnection 생성 및 관리
- Offer/Answer 교환 프로세스
- ICE candidate 처리
- 미디어 트랙 추가/제거
- DataChannel 메시지 송수신

**사용 예시:**
```csharp
// 1. WebRtcManager 설정
webRtcManager.SetRole(true); // true: Offerer, false: Answerer
webRtcManager.SetupSignaling(signalingClient);

// 2. 비디오 트랙 추가
VideoStreamTrack videoTrack = new VideoStreamTrack(renderTexture);
webRtcManager.AddVideoTrack(videoTrack);

// 3. 오디오 트랙 추가 (AudioStreamManager 사용)
AudioStreamTrack audioTrack = new AudioStreamTrack(audioSource);
webRtcManager.AddAudioTrack(audioTrack);

// 4. 데이터 전송
var touchData = new TouchData { 
    touchId = 0, 
    positionX = 0.5f, 
    positionY = 0.5f 
};
webRtcManager.SendDataChannelMessage(touchData);
```

### SignalingClient
WebSocket을 통해 시그널링 서버와 통신하며 WebRTC 연결 설정을 중재합니다.

**주요 기능:**
- 룸 기반 피어 매칭
- SDP(Session Description Protocol) 교환
- ICE candidate 교환
- 연결 상태 관리
- 타겟팅된 메시지 전송 (1:N 연결 지원)

### MultiPeerWebRtcManager
하나의 호스트가 여러 클라이언트와 동시에 연결할 수 있는 1:N 연결을 지원합니다.

**주요 기능:**
- 호스트/클라이언트 역할 관리
- 다중 PeerConnection 관리
- 모든 피어에게 브로드캐스트
- 특정 피어에게 타겟팅된 메시지 전송
- 최대 연결 수 제한

**사용 예시:**
```csharp
// 호스트 설정 (Quest)
multiPeerManager.SetRole(MultiPeerWebRtcManager.PeerRole.Host);
multiPeerManager.maxConnections = 5;
multiPeerManager.OnPeerConnected += OnPeerConnected;

// 모든 피어에게 비디오 브로드캐스트
multiPeerManager.AddVideoTrackToAll(videoTrack);
```

### AudioStreamManager
양방향 오디오 스트리밍을 관리하는 고수준 컴포넌트입니다.

**주요 기능:**
- 마이크 권한 처리
- 오디오 송수신 관리
- 플랫폼별 오디오 최적화
- 볼륨 컨트롤
- 오디오 레벨 모니터링

**사용 예시:**
```csharp
// AudioStreamManager 설정
audioManager.SetMicrophoneEnabled(true);
audioManager.SetSpeakerEnabled(true);
audioManager.OnMicrophoneLevelChanged += UpdateMicUI;
```

### 플랫폼별 WebSocket 어댑터
각 플랫폼의 특성에 맞는 WebSocket 구현체를 제공합니다.

- **SystemWebSocketAdapter**: Quest, Unity Editor에서 사용
- **NativeWebSocketAdapter**: iOS, Android에서 사용

## 🔄 데이터 흐름

```
Quest App (Offerer)                    Mobile App (Answerer)
    |                                        |
    |------ 1. Register to Room ----------->|
    |<----- 2. Peer Joined Notification -----|
    |                                        |
    |------ 3. Create Offer --------------->|
    |<----- 4. Create Answer ---------------|
    |                                        |
    |------ 5. Exchange ICE Candidates ---->|
    |<--------------------------------------|
    |                                        |
    |====== 6. P2P Connection Ready ========|
    |                                        |
    |------ 7. Video Stream --------------->|
    |<----- 8. Touch Data ------------------|
```

## 🎮 사용 방법

### 1. Offerer (Quest) 설정
```csharp
public class QuestAppInitializer : MonoBehaviour
{
    void Start()
    {
        // WebRTC.Update() 코루틴 필수
        StartCoroutine(WebRTC.Update());
        
        // WebRTC Manager 설정
        webRtcManager.SetRole(true); // Offerer
        webRtcManager.SetupSignaling(signalingClient);
        
        // 시그널링 연결 후 PeerConnection 시작
        webRtcManager.StartPeerConnection();
    }
}
```

### 2. Answerer (Mobile) 설정
```csharp
public class MobileAppInitializer : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(WebRTC.Update());
        
        webRtcManager.SetRole(false); // Answerer
        webRtcManager.SetupSignaling(signalingClient);
        // Answerer는 Offer를 기다림
    }
}
```

## ⚠️ 중요 사항

1. **WebRTC.Update() 필수**: 반드시 애플리케이션 시작 시 호출
2. **역할 구분**: Offerer/Answerer 역할을 명확히 설정
3. **시그널링 순서**: Offerer가 먼저 연결 후 Answerer 연결

## 🐛 문제 해결

### 비디오 스트림이 표시되지 않는 경우
1. `WebRTC.Update()` 코루틴 실행 확인
2. RenderTexture 포맷 확인 (BGRA32 권장)
3. `OnVideoReceived` 이벤트 사용

### DataChannel이 열리지 않는 경우
1. WebRTC 연결 상태 확인
2. 양쪽 피어의 DataChannel 라벨 일치 확인
3. 시그널링 서버 연결 상태 확인

## 🔐 보안 고려사항

- 시그널링 서버는 현재 간단한 토큰 인증만 지원 (JWT 미지원)
- 프로덕션 환경에서는 WSS(WebSocket Secure) 사용 권장
- 민감한 정보는 환경 변수로 관리

## ✅ 최근 추가된 기능

- ✅ 양방향 오디오 스트리밍 지원 (AudioStreamManager)
- ✅ 1:N 연결 지원 (MultiPeerWebRtcManager)
- ✅ 타겟팅된 시그널링 메시지 지원
- ✅ 룸 기반 연결 관리 강화

## 🚧 향후 개발 계획

- 연결 품질 모니터링 API
- 자동 재연결 기능 강화
- 동적 비트레이트 조정
- 녹화 기능 지원

## 📄 라이선스

이 프로젝트는 BSD 3-Clause 라이선스를 따릅니다. 자세한 내용은 [LICENSE](LICENSE) 파일을 참고하세요.

## 👥 제작자

- **kugorang** - [GitHub](https://github.com/kugorang)

---

문제가 있거나 제안사항이 있으시면 [Issues](https://github.com/UnityVerseBridge/core/issues)에 등록해주세요.
