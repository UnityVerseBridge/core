namespace UnityVerseBridge.Core.DataChannel.Data
{
    /// <summary>
    /// 햅틱 피드백 명령 타입
    /// </summary>
    public enum HapticCommandType
    {
        VibrateDefault,
        VibrateShort,
        VibrateLong,
        VibrateCustom,
        PlaySound
    }

    /// <summary>
    /// 햅틱 피드백 명령 데이터
    /// </summary>
    [System.Serializable]
    public class HapticCommand : DataChannelMessageBase
    {
        public HapticCommandType commandType;
        public float duration; // 진동 지속 시간 (초)
        public float intensity; // 진동 강도 (0-1)
        public string soundName; // 사운드 재생 시 사용

        public HapticCommand()
        {
            this.type = "haptic";
        }

        public HapticCommand(HapticCommandType type, float duration = 0.1f, float intensity = 1.0f)
        {
            this.type = "haptic";
            this.commandType = type;
            this.duration = duration;
            this.intensity = intensity;
        }
    }
}
