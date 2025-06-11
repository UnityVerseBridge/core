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
├── 통합 관리자
│   └── UnityVerseBridgeManager (Host/Client 모드 자동 관리)
├── WebRTC 관리
│   ├── WebRtcManager (1:1 및 1:N 연결 통합)
│   ├── WebRtcConnectionHandler (개별 피어 연결 관리)
│   └── DataChannel 통신
├── 핸들러 시스템
│   ├── VideoStreamHandler (비디오 스트리밍)
│   ├── AudioStreamHandler (양방향 오디오)
│   ├── TouchInputHandler (터치 입력)
│   └── HapticHandler (햅틱 피드백)
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
├── UnityVerseBridgeManager.cs # 통합 관리자
├── WebRtcManager.cs          # WebRTC 연결 관리 (1:1/1:N 통합)
├── WebRtcConnectionHandler.cs # 개별 피어 연결 핸들러
├── WebRtcConfiguration.cs    # 설정 데이터 구조
├── ConnectionConfig.cs       # 연결 설정 ScriptableObject
├── Handlers/                 # 기능별 핸들러
│   ├── VideoStreamHandler.cs # 비디오 스트리밍
│   ├── AudioStreamHandler.cs # 양방향 오디오
│   ├── TouchInputHandler.cs  # 터치 입력 처리
│   └── HapticHandler.cs      # 햅틱 피드백
├── Signaling/
│   ├── SignalingClient.cs    # 시그널링 로직
│   ├── ISignalingClient.cs   # 시그널링 인터페이스
│   ├── IWebSocketClient.cs   # WebSocket 인터페이스
│   ├── Adapters/             # 플랫폼별 WebSocket 구현
│   ├── Messages/             # 메시지 타입 정의
│   └── Data/
│       └── RoomMessages.cs   # 룸 기반 메시지 타입
├── DataChannel/
│   └── Data/                 # 데이터 구조체
└── Utils/
    └── UnityMainThreadDispatcher.cs
```

## 💡 핵심 컴포넌트 설명

### UnityVerseBridgeManager
모든 WebRTC 기능을 통합 관리하는 최상위 컴포넌트입니다.

**주요 기능:**
- Host/Client 모드 자동 설정
- 모든 핸들러 자동 초기화 및 관리
- ConnectionConfig 기반 설정
- 프리팹을 통한 간편한 사용

**사용 예시:**
```csharp
// 프리팹을 사용하거나 GameObject에 컴포넌트 추가
GameObject bridgeObject = Instantiate(unityVerseBridgePrefab);
// 또는
var bridgeManager = gameObject.AddComponent<UnityVerseBridgeManager>();

// ConnectionConfig ScriptableObject 설정
bridgeManager.connectionConfig = myConnectionConfig;

// 자동으로 모든 것이 초기화되고 연결됨
```

### WebRtcManager
WebRTC 연결의 전체 생명주기를 관리하는 컴포넌트입니다. 1:1 및 1:N 연결을 모두 지원합니다.

**주요 기능:**
- PeerConnection 생성 및 관리
- Offer/Answer 교환 프로세스
- ICE candidate 처리
- 미디어 트랙 추가/제거
- DataChannel 메시지 송수신
- Multi-peer 모드 지원

**내부적으로 UnityVerseBridgeManager가 자동 관리하므로 직접 사용할 필요는 거의 없습니다.**

### SignalingClient
WebSocket을 통해 시그널링 서버와 통신하며 WebRTC 연결 설정을 중재합니다.

**주요 기능:**
- 룸 기반 피어 매칭
- SDP(Session Description Protocol) 교환
- ICE candidate 교환
- 연결 상태 관리
- 타겟팅된 메시지 전송 (1:N 연결 지원)

### 핸들러 시스템

UnityVerseBridgeManager가 자동으로 관리하는 기능별 핸들러들입니다.

#### VideoStreamHandler
비디오 스트리밍을 처리합니다.
- Host: 카메라 영상을 RenderTexture로 캡처 및 전송
- Client: 수신한 비디오를 UI에 표시

#### AudioStreamHandler
양방향 오디오 스트리밍을 관리합니다.
- 마이크 권한 자동 처리
- 플랫폼별 오디오 최적화
- 볼륨 컨트롤

#### TouchInputHandler
터치 입력을 처리합니다.
- Client: 터치 입력을 감지하여 Host로 전송
- Host: 수신한 터치 데이터를 VR 환경에 적용

#### HapticHandler
햅틱 피드백을 처리합니다.
- Host: 햅틱 명령 전송
- Client: 수신한 햅틱 명령을 디바이스에서 실행

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

### 1. ConnectionConfig 생성
Unity Editor에서 ScriptableObject 생성:
1. Project 창에서 우클릭
2. Create > UnityVerseBridge > Connection Config
3. 설정값 입력:
   - Signaling Server URL
   - Room ID
   - Client Type (Quest/Mobile)
   - Auto Connect 옵션

### 2. 프리팹 사용 (권장)
```csharp
public class AppInitializer : MonoBehaviour
{
    [SerializeField] private GameObject unityVerseBridgePrefab;
    [SerializeField] private ConnectionConfig connectionConfig;
    
    void Start()
    {
        // WebRTC.Update() 코루틴 필수
        StartCoroutine(WebRTC.Update());
        
        // 프리팹 인스턴스화
        GameObject bridge = Instantiate(unityVerseBridgePrefab);
        
        // ConnectionConfig 설정
        var manager = bridge.GetComponent<UnityVerseBridgeManager>();
        manager.connectionConfig = connectionConfig;
        
        // 자동으로 연결 시작됨
    }
}
```

### 3. 수동 설정 (고급)
```csharp
public class ManualSetup : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(WebRTC.Update());
        
        // UnityVerseBridgeManager 추가
        var bridgeManager = gameObject.AddComponent<UnityVerseBridgeManager>();
        bridgeManager.connectionConfig = myConfig;
        
        // 필요한 경우 특정 핸들러만 활성화
        bridgeManager.enableVideo = true;
        bridgeManager.enableAudio = true;
        bridgeManager.enableTouch = true;
        bridgeManager.enableHaptics = false;
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

- ✅ 통합 UnityVerseBridgeManager 컴포넌트
- ✅ 핸들러 기반 모듈식 아키텍처
- ✅ ConnectionConfig ScriptableObject 지원
- ✅ 프리팹을 통한 간편한 설정
- ✅ WebRtcManager에 1:1 및 1:N 연결 통합
- ✅ 플랫폼 독립적인 핸들러 시스템

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
