using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityVerseBridge.Core.Utils
{
    /// <summary>
    /// Generic object pool for Unity GameObjects
    /// Reduces garbage collection by reusing objects
    /// </summary>
    public class ObjectPool<T> where T : Component
    {
        private readonly Queue<T> pool = new Queue<T>();
        private readonly Func<T> createFunc;
        private readonly Action<T> onGet;
        private readonly Action<T> onReturn;
        private readonly Transform poolParent;
        private readonly int maxSize;
        private int currentSize = 0;
        
        public int AvailableCount => pool.Count;
        public int TotalCount => currentSize;
        
        /// <summary>
        /// Creates a new object pool
        /// </summary>
        /// <param name="createFunc">Function to create new instances</param>
        /// <param name="onGet">Action called when getting object from pool</param>
        /// <param name="onReturn">Action called when returning object to pool</param>
        /// <param name="initialSize">Initial pool size</param>
        /// <param name="maxSize">Maximum pool size (0 = unlimited)</param>
        public ObjectPool(
            Func<T> createFunc,
            Action<T> onGet = null,
            Action<T> onReturn = null,
            int initialSize = 0,
            int maxSize = 100)
        {
            this.createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            this.onGet = onGet;
            this.onReturn = onReturn;
            this.maxSize = maxSize;
            
            // Create pool parent object
            var poolParentGO = new GameObject($"ObjectPool_{typeof(T).Name}");
            poolParentGO.SetActive(false);
            this.poolParent = poolParentGO.transform;
            
            // Pre-populate pool
            for (int i = 0; i < initialSize; i++)
            {
                var obj = CreateNew();
                Return(obj);
            }
        }
        
        /// <summary>
        /// Get an object from the pool
        /// </summary>
        public T Get()
        {
            T obj;
            
            if (pool.Count > 0)
            {
                obj = pool.Dequeue();
            }
            else
            {
                obj = CreateNew();
            }
            
            if (obj == null)
            {
                Debug.LogError($"[ObjectPool] Failed to create object of type {typeof(T)}");
                return null;
            }
            
            // Move out of pool parent
            obj.transform.SetParent(null);
            obj.gameObject.SetActive(true);
            
            onGet?.Invoke(obj);
            
            return obj;
        }
        
        /// <summary>
        /// Return an object to the pool
        /// </summary>
        public void Return(T obj)
        {
            if (obj == null) return;
            
            // Check if pool is at max capacity
            if (maxSize > 0 && pool.Count >= maxSize)
            {
                GameObject.Destroy(obj.gameObject);
                currentSize--;
                return;
            }
            
            onReturn?.Invoke(obj);
            
            obj.gameObject.SetActive(false);
            obj.transform.SetParent(poolParent);
            
            pool.Enqueue(obj);
        }
        
        /// <summary>
        /// Clear the pool and destroy all pooled objects
        /// </summary>
        public void Clear()
        {
            while (pool.Count > 0)
            {
                var obj = pool.Dequeue();
                if (obj != null)
                {
                    GameObject.Destroy(obj.gameObject);
                }
            }
            
            currentSize = 0;
            
            if (poolParent != null)
            {
                GameObject.Destroy(poolParent.gameObject);
            }
        }
        
        /// <summary>
        /// Pre-warm the pool to a specific size
        /// </summary>
        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (maxSize > 0 && TotalCount >= maxSize) break;
                
                var obj = CreateNew();
                Return(obj);
            }
        }
        
        private T CreateNew()
        {
            var obj = createFunc();
            if (obj != null)
            {
                currentSize++;
            }
            return obj;
        }
    }
    
    /// <summary>
    /// Simple object pool for non-Component types
    /// </summary>
    public class SimpleObjectPool<T> where T : class, new()
    {
        private readonly Queue<T> pool = new Queue<T>();
        private readonly Action<T> resetAction;
        private readonly int maxSize;
        
        public SimpleObjectPool(Action<T> resetAction = null, int maxSize = 100)
        {
            this.resetAction = resetAction;
            this.maxSize = maxSize;
        }
        
        public T Get()
        {
            return pool.Count > 0 ? pool.Dequeue() : new T();
        }
        
        public void Return(T obj)
        {
            if (obj == null || pool.Count >= maxSize) return;
            
            resetAction?.Invoke(obj);
            pool.Enqueue(obj);
        }
        
        public void Clear()
        {
            pool.Clear();
        }
    }
}