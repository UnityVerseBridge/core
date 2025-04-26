using System;

namespace UnityVerseBridge.Core.DataChannel.Data
{
    /// <summary>
    /// 모바일 기기에 요청할 햅틱 피드백의 종류를 정의하는 열거형입니다.
    /// </summary>
    public enum HapticCommandType
    {
        VibrateDefault,     // 기본 진동 (짧게 한 번)
        VibrateShort,       // 짧은 진동
        VibrateLong,        // 긴 진동
        VibrateCustom,      // 사용자 정의 시간/강도 진동
        PlaySound           // 특정 사운드 재생 (미래 확장용)
        // 필요한 다른 햅틱 종류 추가...
    }
    
    /// <summary>
    /// 모바일 기기에 햅틱 피드백(주로 진동)을 요청하는 데이터 클래스입니다.
    /// DataChannelMessageBase를 상속받으며, type은 "haptic"으로 설정될 것입니다.
    /// </summary>
    [Serializable]
    public class HapticCommand : DataChannelMessageBase
    {
        /// <summary>
        /// 요청할 햅틱 명령의 종류 (VibrateDefault, VibrateCustom 등).
        /// </summary>
        public HapticCommandType commandType;

        /// <summary>
        /// 진동 지속 시간 (초 단위). VibrateCustom 등 특정 commandType에서 사용됩니다.
        /// </summary>
        public float duration;

        /// <summary>
        /// 진동 강도 (0.0 ~ 1.0). 지원하는 기기에서만 의미가 있을 수 있습니다.
        /// VibrateCustom 등 특정 commandType에서 사용됩니다.
        /// </summary>
        public float intensity;

        /// <summary>
        /// 재생할 사운드 이름 (PlaySound commandType에서 사용).
        /// </summary>
        public string soundName;

        // 편의를 위한 생성자 예시
        public HapticCommand(HapticCommandType cmdType, float dur = 0f, float intens = 1.0f, string sndName = null)
        {
            this.type = "haptic"; // 메시지 타입 설정
            this.commandType = cmdType;
            this.duration = dur;
            this.intensity = intens;
            this.soundName = sndName;
        }

        // 기본 생성자
        public HapticCommand()
        {
            this.type = "haptic";
        }
    }
}