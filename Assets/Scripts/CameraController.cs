using UnityEngine;

namespace BugFixerGame
{
    public class CameraController : MonoBehaviour
    {
        [Header("��һ�˳�����")]
        [SerializeField] private bool enableMovement = true;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private bool invertMouseY = false;
        [SerializeField] private bool lockCursor = true;  // ��һ�˳��������

        [Header("�ƶ�����")]
        [SerializeField] private bool limitMovement = true;
        [SerializeField] private Vector3 minPosition = new Vector3(-10, 0, -10);
        [SerializeField] private Vector3 maxPosition = new Vector3(10, 5, 10);

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
        private Vector3 originalPosition;
        private Quaternion originalRotation;
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

            // ����ԭʼ״̬
            originalPosition = transform.position;
            originalRotation = transform.rotation;
            originalFieldOfView = cameraComponent.fieldOfView;
        }

        private void Start()
        {
            // ��һ�˳�ģʽ��������굽��Ļ����
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
                HandleMouseLook();  // ��һ�˳ƣ�����Ҫ������ֱ�Ӵ�������ӽ�
            }

            if (enableZoom)
            {
                HandleZoom();
            }
        }

        #endregion

        #region ���봦��

        private void HandleInput()
        {
            // ESC���л��������״̬
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ToggleCursorLock();
            }

            // R���������
            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetCamera();
            }
        }

        private void HandleMovement()
        {
            if (!enableMovement) return;

            Vector3 movement = Vector3.zero;

            // WASD�ƶ�
            if (Input.GetKey(KeyCode.W))
                movement += transform.forward;
            if (Input.GetKey(KeyCode.S))
                movement -= transform.forward;
            if (Input.GetKey(KeyCode.A))
                movement -= transform.right;
            if (Input.GetKey(KeyCode.D))
                movement += transform.right;

            // �����ƶ�
            if (Input.GetKey(KeyCode.Q))
                movement += Vector3.up;
            if (Input.GetKey(KeyCode.E))
                movement += Vector3.down;

            // Ӧ���ƶ�
            if (movement != Vector3.zero)
            {
                Vector3 newPosition = transform.position + movement.normalized * moveSpeed * Time.deltaTime;

                // �����ƶ���Χ
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
            // ��һ�˳�ģʽ��ֻ�����������ʱ�Ŵ����ӽ�
            if (Cursor.lockState != CursorLockMode.Locked) return;

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            if (invertMouseY)
                mouseY = -mouseY;

            // ˮƽ��ת
            transform.Rotate(Vector3.up * mouseX);

            // ��ֱ��ת
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
            transform.position = originalPosition;
            transform.rotation = originalRotation;
            cameraComponent.fieldOfView = originalFieldOfView;
            currentRotationX = 0f;

            Debug.Log("��������õ���ʼ״̬");
        }

        public void FocusOnTarget(Transform target, float distance = 5f)
        {
            if (target == null) return;

            Vector3 direction = (transform.position - target.position).normalized;
            Vector3 newPosition = target.position + direction * distance;

            transform.position = newPosition;
            transform.LookAt(target);

            Debug.Log($"����۽���: {target.name}");
        }

        #endregion

        #region �����ӿ�

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

        #region ���Թ���

        [Header("����")]
        [SerializeField] private bool showDebugInfo = false;

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(Screen.width - 300, 10, 280, 200));
            GUILayout.Label("=== Camera Controller ===");
            GUILayout.Label($"λ��: {transform.position:F2}");
            GUILayout.Label($"��ת: {transform.eulerAngles:F1}");
            GUILayout.Label($"FOV: {cameraComponent.fieldOfView:F1}");
            GUILayout.Label($"�������: {(IsCursorLocked() ? "��" : "��")}");

            GUILayout.Space(10);
            GUILayout.Label("����˵��:");
            GUILayout.Label("WASD - �ƶ�");
            GUILayout.Label("QE - �����ƶ�");
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

        private void OnDrawGizmosSelected()
        {
            if (!limitMovement) return;

            // �����ƶ���Χ
            Gizmos.color = Color.yellow;
            Vector3 center = (minPosition + maxPosition) * 0.5f;
            Vector3 size = maxPosition - minPosition;
            Gizmos.DrawWireCube(center, size);
        }

        #endregion
    }
}