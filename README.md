# UnityVerseBridge Core

Unity 기반 WebRTC 브리지 패키지로 Meta Quest VR 헤드셋과 모바일 디바이스 간의 실시간 P2P 통신을 제공합니다.

## 주요 기능

- 🎮 **크로스 플랫폼 WebRTC**: Quest와 모바일 간 원활한 스트리밍
- 📱 **터치 입력 브리지**: 모바일 터치 입력을 VR 환경으로 전송
- 🔌 **간편한 통합**: Unity 메뉴 도구로 간단한 설정
- 🏗️ **모듈식 아키텍처**: Extension 시스템으로 깔끔한 관심사 분리
- 🔒 **보안 연결**: 인증 지원을 포함한 룸 기반 피어 검색
- 🎯 **최적화된 스트리밍**: 적응형 품질(360p-1080p)의 H264 코덱
- 🔄 **플랫폼별 어댑터**: 각 플랫폼을 위한 네이티브 WebSocket 지원

## 요구사항

- Unity 6 LTS (6000.0.33f1) 또는 Unity 2022.3 LTS
- Unity WebRTC Package 3.0.0-pre.8+
- Meta XR SDK (Quest 플랫폼용)
- iOS 12.0+ / Android API 26+ (모바일 플랫폼용)

## 설치 방법

1. Unity 프로젝트에 UnityVerseBridge Core 패키지 임포트
2. Unity Package Manager 열기
3. 필수 의존성 추가:
   - `com.unity.webrtc` (3.0.0-pre.8 이상)
   - Input System Package (새 Input System 사용 시)

## 빠른 시작

### Quest/VR (호스트)
1. `GameObject > UnityVerseBridge > Quest Setup` 메뉴 실행
2. UnityVerseConfig 설정:
   - Signaling URL 설정 (기본값: `ws://localhost:8080`)
   - Room ID 설정
   - Auto Connect 활성화
3. Quest로 빌드 및 배포

### 모바일 (클라이언트)
1. `GameObject > UnityVerseBridge > Mobile Setup` 메뉴 실행
2. UnityVerseConfig 설정:
   - Quest와 동일한 Signaling URL 사용
   - Quest와 동일한 Room ID 사용
   - Auto Connect 활성화
3. iOS/Android로 빌드 및 배포

## 아키텍처

### 핵심 컴포넌트
- **UnityVerseBridgeManager**: 메인 매니저 컴포넌트
- **WebRtcManager**: WebRTC 연결 처리
- **SignalingClient**: WebSocket 통신
- **ConnectionStateManager**: 연결 상태 관리
- **ReconnectionManager**: 자동 재연결 처리

### Extensions
- **Quest Extensions**: 
  - QuestVideoExtension: VR 카메라 스트리밍
  - QuestTouchExtension: 터치 입력 수신
  - QuestHapticExtension: 햅틱 명령 전송
- **Mobile Extensions**: 
  - MobileVideoExtension: 비디오 스트림 표시
  - MobileInputExtension: 터치 입력 캡처
  - MobileHapticExtension: 햅틱 피드백 재생

### UI 컴포넌트
- **UIManager**: 중앙집중식 UI 관리
- **RoomListUI**: 동적 룸 탐색
- **RoomInputUI**: QR 지원을 포함한 수동 룸 입력

## 설정

### UnityVerseConfig ScriptableObject
```
- Signaling URL: WebSocket 서버 주소
- Room ID: 고유한 룸 식별자
- Role Detection: 자동/수동
- Auto Connect: 시작 시 연결
- Enable Debug Logging: 콘솔 로그
- Require Authentication: 인증 플로우 활성화
```

### 플랫폼별 설정
- **Quest**: Meta XR SDK 필요, Android 빌드
- **Mobile**: iOS 및 Android 지원
- **Editor**: 테스트를 위한 Meta XR Simulator 사용

## API 사용법

### 기본 연결
```csharp
// 매니저 인스턴스 가져오기
var bridgeManager = FindObjectOfType<UnityVerseBridgeManager>();

// 룸 설정 및 연결
bridgeManager.SetRoomId("my-room");
bridgeManager.Connect();
```

### 터치 데이터 전송 (모바일)
```csharp
// MobileInputExtension이 자동으로 처리
// 터치 입력이 캡처되어 데이터 채널을 통해 전송됨
```

### 터치 수신 (Quest)
```csharp
// 터치 이벤트 구독
var touchExtension = GetComponent<QuestTouchExtension>();
touchExtension.OnTouchReceived += (touchData) => {
    // 정규화된 좌표에서 터치 처리
    Vector2 position = touchData.GetPosition();
};
```

### 햅틱 피드백
```csharp
// Quest에서 햅틱 명령 전송
var hapticExtension = GetComponent<QuestHapticExtension>();
hapticExtension.SendHaptic(HapticCommandType.VibrateShort);
```

## 메시지 형식

### TouchData
```csharp
{
    "type": "touch",
    "touchId": 0,
    "phase": "Began|Moved|Ended",
    "positionX": 0.5,  // 0-1 정규화
    "positionY": 0.5   // 0-1 정규화
}
```

### HapticCommand
```csharp
{
    "type": "haptic",
    "commandType": "VibrateShort|VibrateLong|VibrateCustom",
    "duration": 0.1,
    "intensity": 1.0
}
```

## 문제 해결

### 비디오가 스트리밍되지 않음
1. WebRTC.Update() 코루틴이 실행 중인지 확인
2. RenderTexture 형식 확인 (BGRA32)
3. 양쪽 디바이스가 동일한 룸 ID 사용 확인
4. VideoStreamTrack.IsEncoderInitialized 확인

### 연결 문제
1. 방화벽 설정 확인
2. 시그널링 서버 실행 확인
3. 네트워크 연결 확인
4. 같은 네트워크에 있는지 확인

### 성능
- 기본 해상도: 최적 성능을 위한 640x360
- 지원 해상도: 360p(최소), 720p, 1080p
- 프레임 레이트: 30 FPS
- 하드웨어 가속 활성화 (H264 코덱)
- 피어 수에 따른 적응형 품질

## 지원

이슈 및 기능 요청은 [GitHub 저장소](https://github.com/UnityVerseBridge/core)를 방문해주세요.

## 라이선스

자세한 내용은 LICENSE 파일을 참조하세요.