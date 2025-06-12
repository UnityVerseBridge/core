using UnityEngine;

namespace UnityVerseBridge.Core.Utils
{
    /// <summary>
    /// Simple camera controller for Unity Editor testing
    /// Allows WASD movement and mouse look similar to Meta XR Simulator
    /// </summary>
    public class SimpleEditorCameraController : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float moveSpeed = 5f;
        public float fastMoveSpeed = 10f;
        public float rotationSpeed = 2f;
        
        [Header("Input Settings")]
        public bool requireRightClick = true; // Require right mouse button for rotation
        
        private Vector3 rotationAngles;
        
        void Start()
        {
            rotationAngles = transform.eulerAngles;
            
            #if !UNITY_EDITOR
            // Disable this component in builds
            enabled = false;
            #endif
        }
        
        void Update()
        {
            #if UNITY_EDITOR
            HandleMovement();
            HandleRotation();
            #endif
        }
        
        private void HandleMovement()
        {
            float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? fastMoveSpeed : moveSpeed;
            Vector3 movement = Vector3.zero;
            
            // WASD movement
            if (Input.GetKey(KeyCode.W)) movement += transform.forward;
            if (Input.GetKey(KeyCode.S)) movement -= transform.forward;
            if (Input.GetKey(KeyCode.A)) movement -= transform.right;
            if (Input.GetKey(KeyCode.D)) movement += transform.right;
            
            // QE for up/down
            if (Input.GetKey(KeyCode.Q)) movement -= transform.up;
            if (Input.GetKey(KeyCode.E)) movement += transform.up;
            
            transform.position += movement * currentSpeed * Time.deltaTime;
        }
        
        private void HandleRotation()
        {
            if (requireRightClick && !Input.GetMouseButton(1))
                return;
            
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            
            rotationAngles.y += mouseX * rotationSpeed;
            rotationAngles.x -= mouseY * rotationSpeed;
            rotationAngles.x = Mathf.Clamp(rotationAngles.x, -90f, 90f);
            
            transform.rotation = Quaternion.Euler(rotationAngles);
        }
        
        void OnGUI()
        {
            #if UNITY_EDITOR
            if (!enabled) return;
            
            // Display controls in the corner
            int y = 10;
            int lineHeight = 20;
            GUI.Label(new Rect(10, y, 400, lineHeight), "Editor Camera Controls:"); y += lineHeight;
            GUI.Label(new Rect(10, y, 400, lineHeight), "WASD - Move"); y += lineHeight;
            GUI.Label(new Rect(10, y, 400, lineHeight), "Q/E - Down/Up"); y += lineHeight;
            GUI.Label(new Rect(10, y, 400, lineHeight), "Right Mouse + Drag - Look around"); y += lineHeight;
            GUI.Label(new Rect(10, y, 400, lineHeight), "Shift - Move faster"); y += lineHeight;
            #endif
        }
    }
}