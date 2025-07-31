using UnityEngine;

namespace BugFixerGame
{
    public class CameraController : MonoBehaviour
    {
        [Header("第一人称设置")]
        [SerializeField] private bool enableMovement = true;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private bool invertMouseY = false;
        [SerializeField] private bool lockCursor = true;  // 第一人称锁定鼠标

        [Header("移动限制")]
        [SerializeField] private bool limitMovement = true;
        [SerializeField] private Vector3 minPosition = new Vector3(-10, 0, -10);
        [SerializeField] private Vector3 maxPosition = new Vector3(10, 5, 10);

        [Header("旋转限制")]
        [SerializeField] private bool limitRotation = true;
        [SerializeField] private float minVerticalAngle = -60f;
        [SerializeField] private float maxVerticalAngle = 60f;

        [Header("缩放设置")]
        [SerializeField] private bool enableZoom = true;
        [SerializeField] private float zoomSpeed = 2f;
        [SerializeField] private float minFieldOfView = 20f;
        [SerializeField] private float maxFieldOfView = 80f;

        private float currentRotationX = 0f;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private float originalFieldOfView;
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

            // 保存原始状态
            originalPosition = transform.position;
            originalRotation = transform.rotation;
            originalFieldOfView = cameraComponent.fieldOfView;
        }

        private void Start()
        {
            // 第一人称模式：锁定鼠标到屏幕中心
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Update()
        {
            HandleInput();

            if (enableMovement)
            {
                HandleMovement();
                HandleMouseLook();  // 第一人称：不需要按键，直接处理鼠标视角
            }

            if (enableZoom)
            {
                HandleZoom();
            }
        }

        #endregion

        #region 输入处理

        private void HandleInput()
        {
            // ESC键切换鼠标锁定状态
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ToggleCursorLock();
            }

            // R键重置相机
            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetCamera();
            }
        }

        private void HandleMovement()
        {
            if (!enableMovement) return;

            Vector3 movement = Vector3.zero;

            // WASD移动
            if (Input.GetKey(KeyCode.W))
                movement += transform.forward;
            if (Input.GetKey(KeyCode.S))
                movement -= transform.forward;
            if (Input.GetKey(KeyCode.A))
                movement -= transform.right;
            if (Input.GetKey(KeyCode.D))
                movement += transform.right;

            // 上下移动
            if (Input.GetKey(KeyCode.Q))
                movement += Vector3.up;
            if (Input.GetKey(KeyCode.E))
                movement += Vector3.down;

            // 应用移动
            if (movement != Vector3.zero)
            {
                Vector3 newPosition = transform.position + movement.normalized * moveSpeed * Time.deltaTime;

                // 限制移动范围
                if (limitMovement)
                {
                    newPosition.x = Mathf.Clamp(newPosition.x, minPosition.x, maxPosition.x);
                    newPosition.y = Mathf.Clamp(newPosition.y, minPosition.y, maxPosition.y);
                    newPosition.z = Mathf.Clamp(newPosition.z, minPosition.z, maxPosition.z);
                }

                transform.position = newPosition;
            }
        }

        private void HandleMouseLook()
        {
            // 第一人称模式：只有在鼠标锁定时才处理视角
            if (Cursor.lockState != CursorLockMode.Locked) return;

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            if (invertMouseY)
                mouseY = -mouseY;

            // 水平旋转
            transform.Rotate(Vector3.up * mouseX);

            // 垂直旋转
            currentRotationX -= mouseY;
            if (limitRotation)
            {
                currentRotationX = Mathf.Clamp(currentRotationX, minVerticalAngle, maxVerticalAngle);
            }

            transform.localRotation = Quaternion.Euler(currentRotationX, transform.localEulerAngles.y, 0);
        }

        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float newFOV = cameraComponent.fieldOfView - scroll * zoomSpeed * 10f;
                cameraComponent.fieldOfView = Mathf.Clamp(newFOV, minFieldOfView, maxFieldOfView);
            }
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
            transform.position = originalPosition;
            transform.rotation = originalRotation;
            cameraComponent.fieldOfView = originalFieldOfView;
            currentRotationX = 0f;

            Debug.Log("相机已重置到初始状态");
        }

        public void FocusOnTarget(Transform target, float distance = 5f)
        {
            if (target == null) return;

            Vector3 direction = (transform.position - target.position).normalized;
            Vector3 newPosition = target.position + direction * distance;

            transform.position = newPosition;
            transform.LookAt(target);

            Debug.Log($"相机聚焦到: {target.name}");
        }

        #endregion

        #region 公共接口

        public void SetMovementEnabled(bool enabled)
        {
            enableMovement = enabled;
        }

        public void SetZoomEnabled(bool enabled)
        {
            enableZoom = enabled;
        }

        public Camera GetCamera()
        {
            return cameraComponent;
        }

        public bool IsCursorLocked()
        {
            return Cursor.lockState == CursorLockMode.Locked;
        }

        #endregion

        #region 调试功能

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(Screen.width - 300, 10, 280, 200));
            GUILayout.Label("=== Camera Controller ===");
            GUILayout.Label($"位置: {transform.position:F2}");
            GUILayout.Label($"旋转: {transform.eulerAngles:F1}");
            GUILayout.Label($"FOV: {cameraComponent.fieldOfView:F1}");
            GUILayout.Label($"鼠标锁定: {(IsCursorLocked() ? "是" : "否")}");

            GUILayout.Space(10);
            GUILayout.Label("控制说明:");
            GUILayout.Label("WASD - 移动");
            GUILayout.Label("QE - 上下移动");
            GUILayout.Label("鼠标 - 视角旋转");
            GUILayout.Label("滚轮 - 缩放");
            GUILayout.Label("ESC - 切换鼠标锁定");
            GUILayout.Label("R - 重置相机");

            if (GUILayout.Button("重置相机"))
            {
                ResetCamera();
            }

            GUILayout.EndArea();
        }

        private void OnDrawGizmosSelected()
        {
            if (!limitMovement) return;

            // 绘制移动范围
            Gizmos.color = Color.yellow;
            Vector3 center = (minPosition + maxPosition) * 0.5f;
            Vector3 size = maxPosition - minPosition;
            Gizmos.DrawWireCube(center, size);
        }

        #endregion
    }
}