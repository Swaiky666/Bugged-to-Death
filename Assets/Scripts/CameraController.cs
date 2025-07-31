using UnityEngine;

namespace BugFixerGame
{
    public class CameraController : MonoBehaviour
    {
        [Header("��һ�˳�����")]
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private bool invertMouseY = false;
        [SerializeField] private bool lockCursor = true;

        [Header("��ת����")]
        [SerializeField] private bool limitRotation = true;
        [SerializeField] private float minVerticalAngle = -60f;
        [SerializeField] private float maxVerticalAngle = 60f;

        [Header("��������")]
        [SerializeField] private bool enableZoom = true;
        [SerializeField] private float zoomSpeed = 2f;
        [SerializeField] private float minFieldOfView = 20f;
        [SerializeField] private float maxFieldOfView = 80f;

        private float currentRotationX = 0f;
        private float originalFieldOfView;
        private Camera cameraComponent;

        #region Unity��������

        private void Awake()
        {
            cameraComponent = GetComponent<Camera>();
            if (cameraComponent == null)
            {
                Debug.LogError("CameraController��Ҫ��������Camera�����GameObject�ϣ�");
                enabled = false;
                return;
            }

            originalFieldOfView = cameraComponent.fieldOfView;
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
            HandleZoom();
            HandleInput();
        }

        #endregion

        #region ���봦��

        private void HandleInput()
        {
            // R���������
            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetCamera();
            }
        }

        private void HandleMouseLook()
        {
            // ֻ�����������ʱ�Ŵ����ӽ�
            if (Cursor.lockState != CursorLockMode.Locked) return;

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            if (invertMouseY)
                mouseY = -mouseY;

            // ˮƽ��ת��Y�ᣩ- ��ת�����壨��ң�
            if (transform.parent != null)
            {
                transform.parent.Rotate(Vector3.up * mouseX);
            }
            else
            {
                transform.Rotate(Vector3.up * mouseX);
            }

            // ��ֱ��ת��X�ᣩ- ֻ��ת���
            currentRotationX -= mouseY;
            if (limitRotation)
            {
                currentRotationX = Mathf.Clamp(currentRotationX, minVerticalAngle, maxVerticalAngle);
            }

            transform.localRotation = Quaternion.Euler(currentRotationX, 0, 0);
        }

        private void HandleZoom()
        {
            if (!enableZoom) return;

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float newFOV = cameraComponent.fieldOfView - scroll * zoomSpeed * 10f;
                cameraComponent.fieldOfView = Mathf.Clamp(newFOV, minFieldOfView, maxFieldOfView);
            }
        }

        #endregion

        #region �������

        public void ToggleCursorLock()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Debug.Log("������ - ���Ե��UI");
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                Debug.Log("������� - ��һ�˳�ģʽ");
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
            cameraComponent.fieldOfView = originalFieldOfView;

            Debug.Log("���������");
        }

        public void SetMouseSensitivity(float sensitivity)
        {
            mouseSensitivity = Mathf.Max(0.1f, sensitivity);
        }

        #endregion

        #region �����ӿ�

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

        #region ���Թ���

        [Header("����")]
        [SerializeField] private bool showDebugInfo = false;

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(Screen.width - 300, 10, 280, 150));
            GUILayout.Label("=== Camera Controller ===");
            GUILayout.Label($"��ֱ��ת�Ƕ�: {currentRotationX:F1}��");
            GUILayout.Label($"��Ұ�Ƕ�: {cameraComponent.fieldOfView:F1}��");
            GUILayout.Label($"�������: {(IsCursorLocked() ? "��" : "��")}");
            GUILayout.Label($"���������: {mouseSensitivity:F1}");

            GUILayout.Space(10);
            GUILayout.Label("����˵��:");
            GUILayout.Label("��� - �ӽ���ת");
            GUILayout.Label("���� - ����");
            GUILayout.Label("ESC - �л��������");
            GUILayout.Label("R - �������");

            if (GUILayout.Button("�������"))
            {
                ResetCamera();
            }

            GUILayout.EndArea();
        }

        #endregion
    }
}