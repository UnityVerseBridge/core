using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityVerseBridge.Core.Utils
{
    /// <summary>
    /// Unity 메인 스레드에서 작업을 실행하기 위한 디스패처
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        private readonly Queue<Action> _executionQueue = new Queue<Action>();

        public static UnityMainThreadDispatcher Instance()
        {
            if (_instance == null)
            {
                var go = new GameObject("UnityMainThreadDispatcher");
                _instance = go.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }

        public void Enqueue(Action action)
        {
            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }

        private void Update()
        {
            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                {
                    _executionQueue.Dequeue()?.Invoke();
                }
            }
        }
    }
}