using UnityEngine;

namespace BugFixerGame
{
    public class CameraController : MonoBehaviour
    {
        [Header("第一人称设置")]
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private bool invertMouseY = false;
        [SerializeField] private bool lockCursor = true;

        [Header("旋转限制")]
        [SerializeField] private bool limitRotation = true;
        [SerializeField] private float minVerticalAngle = -60f;
        [SerializeField] private float maxVerticalAngle = 60f;

        private float currentRotationX = 0f;
        private Camera cameraComponent;

        #region Unity生命周期

        private void Awake()
        {
            cameraComponent = GetComponent<Camera>();
            if (cameraComponent == null)
            {
                Debug.LogError("CameraController需要挂载在有Camera组件的GameObject上！");
                enabled = false;
                return;
            }
        }

        private void Start()
        {
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Update()
        {
            HandleMouseLook();
            HandleInput();
        }

        #endregion

        #region 输入处理

        private void HandleInput()
        {
            // R键重置相机
            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetCamera();
            }
        }

        private void HandleMouseLook()
        {
            // 只有在鼠标锁定时才处理视角
            if (Cursor.lockState != CursorLockMode.Locked) return;

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            if (invertMouseY)
                mouseY = -mouseY;

            // 水平旋转（Y轴）- 旋转父物体（玩家）
            if (transform.parent != null)
            {
                transform.parent.Rotate(Vector3.up * mouseX);
            }
            else
            {
                transform.Rotate(Vector3.up * mouseX);
            }

            // 垂直旋转（X轴）- 只旋转相机
            currentRotationX -= mouseY;
            if (limitRotation)
            {
                currentRotationX = Mathf.Clamp(currentRotationX, minVerticalAngle, maxVerticalAngle);
            }

            transform.localRotation = Quaternion.Euler(currentRotationX, 0, 0);
        }

        #endregion

        #region 相机控制

        public void ToggleCursorLock()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Debug.Log("鼠标解锁 - 可以点击UI");
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                Debug.Log("鼠标锁定 - 第一人称模式");
            }
        }

        public void SetCursorLocked(bool locked)
        {
            if (locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        public void ResetCamera()
        {
            currentRotationX = 0f;
            transform.localRotation = Quaternion.identity;

            Debug.Log("相机已重置");
        }

        public void SetMouseSensitivity(float sensitivity)
        {
            mouseSensitivity = Mathf.Max(0.1f, sensitivity);
        }

        #endregion

        #region 公共接口

        public Camera GetCamera()
        {
            return cameraComponent;
        }

        public bool IsCursorLocked()
        {
            return Cursor.lockState == CursorLockMode.Locked;
        }

        public float GetCurrentVerticalRotation()
        {
            return currentRotationX;
        }

        public void SetVerticalRotation(float angle)
        {
            currentRotationX = limitRotation ? Mathf.Clamp(angle, minVerticalAngle, maxVerticalAngle) : angle;
            transform.localRotation = Quaternion.Euler(currentRotationX, 0, 0);
        }

        #endregion

        #region 调试功能

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(Screen.width - 300, 10, 280, 120));
            GUILayout.Label("=== Camera Controller ===");
            GUILayout.Label($"垂直旋转角度: {currentRotationX:F1}°");
            GUILayout.Label($"鼠标锁定: {(IsCursorLocked() ? "是" : "否")}");
            GUILayout.Label($"鼠标灵敏度: {mouseSensitivity:F1}");

            GUILayout.Space(10);
            GUILayout.Label("控制说明:");
            GUILayout.Label("鼠标 - 视角旋转");
            GUILayout.Label("ESC - 切换鼠标锁定");
            GUILayout.Label("R - 重置相机");

            if (GUILayout.Button("重置相机"))
            {
                ResetCamera();
            }

            GUILayout.EndArea();
        }

        #endregion
    }
}