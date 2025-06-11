namespace UnityVerseBridge.Core
{
    /// <summary>
    /// 클라이언트 타입을 정의하는 열거형
    /// </summary>
    public enum ClientType
    {
        /// <summary>
        /// Quest VR 헤드셋 (Host/Offerer 역할)
        /// </summary>
        Quest,
        
        /// <summary>
        /// 모바일 디바이스 (Client/Answerer 역할)
        /// </summary>
        Mobile
    }
}