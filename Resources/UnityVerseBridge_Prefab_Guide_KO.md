# UnityVerseBridge 프리팹 설정 가이드

## 개요
UnityVerseBridge는 Unity 프로젝트에 빠르게 통합할 수 있도록 준비된 프리팹을 제공합니다.

## 프리팹 종류

### 1. UnityVerseBridge_Host.prefab
Quest VR 애플리케이션용 (스트림 송신)

**GameObject 구조:**
```
UnityVerseBridge_Host
├── UnityVerseBridgeManager
│   └── 설정:
│       - Bridge Mode: Host
│       - Connection Mode: SinglePeer/MultiPeer
│       - Auto Initialize: ✓
│       - Auto Connect: ✓
│       - Enable Video: ✓
│       - Enable Audio: ✓
│       - Enable Touch: ✓
│       - Enable Haptics: ✓
├── ConnectionConfig (참조)
└── WebRtcConfiguration (참조)
```

### 2. UnityVerseBridge_Client.prefab
모바일 애플리케이션용 (스트림 수신)

**GameObject 구조:**
```
UnityVerseBridge_Client
├── UnityVerseBridgeManager
│   └── 설정:
│       - Bridge Mode: Client
│       - Connection Mode: SinglePeer
│       - Auto Initialize: ✓
│       - Auto Connect: ✓
│       - Enable Video: ✓
│       - Enable Audio: ✓
│       - Enable Touch: ✓
│       - Enable Haptics: ✓
├── ConnectionConfig (참조)
├── WebRtcConfiguration (참조)
└── UI
    └── VideoDisplay (RawImage)
```

### 3. UnityVerseBridge_MultiHost.prefab
1:N 연결용 (여러 모바일 클라이언트)

**GameObject 구조:**
```
UnityVerseBridge_MultiHost
├── UnityVerseBridgeManager
│   └── 설정:
│       - Bridge Mode: Host
│       - Connection Mode: MultiPeer
│       - Auto Initialize: ✓
│       - Auto Connect: ✓
│       - Enable Video: ✓
│       - Enable Audio: ✓
│       - Enable Touch: ✓
│       - Enable Haptics: ✓
├── ConnectionConfig (참조)
└── WebRtcConfiguration (참조)
```

## 빠른 시작

### 1단계: 프리팹 가져오기
1. Unity에서 `Packages/UnityVerseBridge Core/Runtime/Prefabs/`로 이동
2. 적절한 프리팹을 씬으로 드래그

### 2단계: 연결 설정
1. ConnectionConfig 에셋 생성:
   - Project 창에서 우클릭 > Create > UnityVerseBridge > Connection Config
   - 시그널링 서버 URL 설정
   - Room ID 설정 (또는 자동 생성 활성화)

2. ConnectionConfig를 프리팹의 UnityVerseBridgeManager 컴포넌트에 할당

### 3단계: 플랫폼별 설정

**Quest (Host)의 경우:**
```csharp
// 프리팹이 자동으로 OVRCameraRig를 찾습니다
// 또는 수동으로 VR 카메라를 VideoStreamHandler에 할당
```

**Mobile (Client)의 경우:**
```csharp
// 프리팹에 비디오 디스플레이가 포함된 UI 캔버스가 있습니다
// 필요에 따라 UI 레이아웃을 커스터마이즈하세요
```

### 4단계: 실행
1. 시그널링 서버 시작
2. Quest 앱을 먼저 빌드하고 실행
3. Mobile 앱을 빌드하고 실행
4. Room ID를 사용하여 자동으로 연결됩니다

## 커스터마이징

### 코드로 컴포넌트 접근:
```csharp
// 매니저 가져오기
var bridgeManager = FindObjectOfType<UnityVerseBridgeManager>();

// 핸들러 접근
var videoHandler = bridgeManager.GetComponent<VideoStreamHandler>();
var audioHandler = bridgeManager.GetComponent<AudioStreamHandler>();
var touchHandler = bridgeManager.GetComponent<TouchInputHandler>();
var hapticHandler = bridgeManager.GetComponent<HapticHandler>();

// 커스텀 데이터 전송
bridgeManager.SendDataChannelMessage(customData);
```

### 커스텀 이벤트:
```csharp
// 이벤트 구독
bridgeManager.OnConnected.AddListener(() => {
    Debug.Log("연결됨!");
});

bridgeManager.OnError.AddListener((error) => {
    Debug.LogError($"에러: {error}");
});
```

## 고급 설정

### 혼합 현실 모드 (Quest):
```csharp
var videoHandler = bridgeManager.GetComponent<VideoStreamHandler>();
videoHandler.SetMixedRealityMode(true);
```

### 멀티 터치 지원:
```csharp
var touchHandler = bridgeManager.GetComponent<TouchInputHandler>();
// Inspector 또는 코드로 설정
```

### 커스텀 햅틱 패턴:
```csharp
var hapticHandler = bridgeManager.GetComponent<HapticHandler>();
hapticHandler.RequestHapticFeedback(
    HapticCommandType.VibrateCustom, 
    duration: 0.5f, 
    intensity: 0.8f
);
```

## 문제 해결

1. **연결 실패**: 
   - 시그널링 서버가 실행 중인지 확인
   - Room ID가 일치하는지 확인
   - 방화벽 설정 확인

2. **비디오 스트림 없음**:
   - 카메라가 할당되었는지 확인
   - 렌더 텍스처 포맷 확인 (BGRA32)
   - WebRTC.Update()가 실행 중인지 확인

3. **터치 작동 안 함**:
   - Enhanced Touch Support 활성화
   - Host의 레이어 마스크 확인
   - 데이터 채널이 열려있는지 확인

## 샘플 코드

### 최소 Host 설정:
```csharp
public class MyVRApp : MonoBehaviour
{
    void Start()
    {
        // 이게 전부입니다! 프리팹이 모든 것을 처리합니다
    }
}
```

### 최소 Client 설정:
```csharp
public class MyMobileApp : MonoBehaviour
{
    void Start()
    {
        // 프리팹이 자동으로 연결하고 비디오를 표시합니다
    }
}
```

### 수동 제어:
```csharp
public class ManualSetup : MonoBehaviour
{
    private UnityVerseBridgeManager bridge;
    
    void Start()
    {
        bridge = GetComponent<UnityVerseBridgeManager>();
        bridge.autoConnect = false; // 자동 연결 비활성화
    }
    
    public void ConnectButton()
    {
        bridge.Initialize();
        bridge.Connect();
    }
}
```