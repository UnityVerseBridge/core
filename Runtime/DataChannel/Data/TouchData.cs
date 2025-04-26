using System;
using UnityEngine; // Vector2 사용을 위해 추가 (또는 x, y 필드 분리)

namespace UnityVerseBridge.Core.DataChannel.Data
{
    /// <summary>
    /// 터치 입력의 상태를 나타내는 열거형입니다.
    /// </summary>
    public enum TouchPhase
    {
        Began,  // 터치가 시작됨
        Moved,  // 터치된 채로 이동함
        Ended,  // 터치가 화면에서 떼어짐
        Canceled // 시스템 등에 의해 터치가 취소됨 (선택 사항)
        // Stationary // 터치된 채로 가만히 있음 (선택 사항)
    }
    
    /// <summary>
    /// 모바일 화면의 터치 입력 정보를 나타내는 데이터 클래스입니다.
    /// DataChannelMessageBase를 상속받으며, type은 "touch"로 설정될 것입니다.
    /// </summary>
    [Serializable]
    public class TouchData : DataChannelMessageBase
    {
        /// <summary>
        /// 이 터치 이벤트를 구분하기 위한 고유 ID (멀티 터치 지원 시 필요).
        /// </summary>
        public int touchId;

        /// <summary>
        /// 터치 이벤트의 상태 (Began, Moved, Ended 등).
        /// </summary>
        public TouchPhase phase;

        /// <summary>
        /// 터치 위치의 X 좌표 (화면 좌측=0.0, 우측=1.0 으로 정규화된 값).
        /// </summary>
        public float positionX;

        /// <summary>
        /// 터치 위치의 Y 좌표 (화면 하단=0.0, 상단=1.0 으로 정규화된 값).
        /// </summary>
        public float positionY;

        // 필요시 다른 정보 추가 가능 (예: 압력(pressure), 터치 시간 등)

        // 생성자 (편의상 추가)
        public TouchData(int id, TouchPhase touchPhase, Vector2 normalizedPosition)
        {
            this.type = "touch"; // 메시지 타입 설정
            this.touchId = id;
            this.phase = touchPhase;
            this.positionX = normalizedPosition.x;
            this.positionY = normalizedPosition.y;
        }

        // 기본 생성자 (JsonUtility 사용 시 필요할 수 있음)
        public TouchData()
        {
            this.type = "touch";
        }

        public Vector2 GetPosition()
        {
            return new Vector2(positionX, positionY);
        }
    }
}