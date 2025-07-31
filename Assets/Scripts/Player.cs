using UnityEngine;
using System.Collections;

namespace BugFixerGame
{
    [RequireComponent(typeof(CharacterController))]
    public class Player : MonoBehaviour
    {
        [Header("�ƶ�����")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float jumpForce = 5f;
        [SerializeField] private float gravity = -20f;

        [Header("�������")]
        [SerializeField] private float groundCheckDistance = 0.3f;
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private LayerMask clickableMask = -1;  // ��Ϊ-1������в㼶
        [SerializeField] private float rayLength = 100f;
        [SerializeField] private bool showDebugRay = true;
        [SerializeField] private bool enableContinuousDebug = true;  // ����debug���

        [Header("�����������")]
        [SerializeField] private float holdTime = 2f;              // ����ʱ��

        private CharacterController controller;
        private Camera cam;
        private CameraController cameraController;
        private Vector3 velocity;
        private bool isGrounded;
        private GameObject currentDetectedObject;  // ��Ϊ�����������
        private BugObject currentDetectedBugObject; // ������BugObject������

        // ������ر���
        private bool isHolding = false;
        private float holdStartTime = 0f;
        private Coroutine holdCoroutine;

        // �¼�
        public static event System.Action<BugObject> OnBugObjectClicked;
        public static event System.Action<Vector3> OnEmptySpaceClicked;
        public static event System.Action<GameObject, float> OnObjectHoldProgress;  // ��ΪGameObject�ͽ���(0-1)
        public static event System.Action OnHoldCancelled;

        // �����¼���������
        public static event System.Action<GameObject, bool> OnObjectDetectionComplete; // (��������, �Ƿ������bug)

        #region Unity��������

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            cam = Camera.main;
            if (cam == null)
            {
                // ���û����������������������е����
                cam = GetComponentInChildren<Camera>();
            }

            cameraController = GetComponentInChildren<CameraController>();
        }

        private void Update()
        {
            HandleMovement();
            HandleJump();
            HandleRayDetection();
            HandleClickInput();
            HandleEscapeInput();
        }

        #endregion

        #region �ƶ�����

        private void HandleMovement()
        {
            // ������
            isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, groundCheckDistance + 0.1f, groundMask.value);

            if (isGrounded && velocity.y < 0f)
                velocity.y = -2f;

            // ��ȡ����
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");

            // �����ƶ����򣨻����������
            Vector3 forward = cam.transform.forward;
            Vector3 right = cam.transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 move = (forward * v + right * h) * moveSpeed;

            // Ӧ������
            velocity.y += gravity * Time.deltaTime;
            move.y = velocity.y;

            // �ƶ���ɫ
            controller.Move(move * Time.deltaTime);
        }

        private void HandleJump()
        {
            if (isGrounded && Input.GetKeyDown(KeyCode.Space))
            {
                velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            }
        }

        #endregion

        #region ���߼��

        private void HandleRayDetection()
        {
            Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f);
            Ray ray = cam.ScreenPointToRay(screenCenter);

            if (showDebugRay)
            {
                Color rayColor = currentDetectedObject != null ? Color.red : Color.yellow;
                Debug.DrawRay(ray.origin, ray.direction * rayLength, rayColor);
            }

            // ���֮ǰ��⵽�Ķ���
            GameObject previousObject = currentDetectedObject;
            currentDetectedObject = null;
            currentDetectedBugObject = null;

            // ���߼�� - ʹ�ö��ַ�ʽȷ���ܼ�⵽����
            RaycastHit hit;
            bool hitSomething = false;

            // ����1�����Trigger����ͨ��ײ��
            if (Physics.Raycast(ray, out hit, rayLength, clickableMask.value, QueryTriggerInteraction.Collide))
            {
                hitSomething = true;
                currentDetectedObject = hit.collider.gameObject;

                // ����debug���
                if (enableContinuousDebug)
                {
                    Debug.Log($"[���߼��] ��������: {hit.collider.name}, Layer: {hit.collider.gameObject.layer}, Distance: {hit.distance:F2}, IsTrigger: {hit.collider.isTrigger}");
                }

                // ����Ƿ���BugObject���
                BugObject bug = hit.collider.GetComponent<BugObject>();
                if (bug != null)
                {
                    currentDetectedBugObject = bug;
                    if (enableContinuousDebug)
                    {
                        Debug.Log($"[BugObject���] �ҵ�BugObject: {bug.name}, ����: {bug.GetBugType()}, ����״̬: {bug.IsBugActive()}");
                    }
                }
                else
                {
                    if (enableContinuousDebug)
                    {
                        Debug.Log($"[���߼��] ���� {hit.collider.name} û��BugObject���");
                    }
                }
            }

            // �����һ�ַ���û��⵽�����Ժ���Trigger�ļ��
            if (!hitSomething && Physics.Raycast(ray, out hit, rayLength, clickableMask.value, QueryTriggerInteraction.Ignore))
            {
                hitSomething = true;
                currentDetectedObject = hit.collider.gameObject;

                // ���BugObject���
                BugObject bug = hit.collider.GetComponent<BugObject>();
                if (bug != null)
                {
                    currentDetectedBugObject = bug;
                }

                if (enableContinuousDebug)
                {
                    Debug.Log($"[���߼��-����Trigger] ��������: {hit.collider.name}, Layer: {hit.collider.gameObject.layer}");
                }
            }

            // ���ʲô��û��⵽
            if (!hitSomething && enableContinuousDebug)
            {
                Debug.Log($"[���߼��] δ�����κ����� - Ray���: {ray.origin:F2}, ����: {ray.direction:F2}, ����: {rayLength}, LayerMask: {clickableMask.value}");
            }

            // ���֮ǰ�ж�������û�У�ȡ����������
            if (previousObject != null && currentDetectedObject == null && isHolding)
            {
                Debug.Log("[�������] ʧȥ����Ŀ�꣬ȡ����������");
                CancelHold();
            }
            // �����⵽��ͬ�Ķ���Ҳȡ����ǰ����
            else if (previousObject != currentDetectedObject && isHolding)
            {
                Debug.Log("[�������] ��⵽�¶���ȡ����ǰ��������");
                CancelHold();
            }
        }

        #endregion

        #region ������봦��

        private void HandleClickInput()
        {
            // ��갴�¿�ʼ����
            if (Input.GetMouseButtonDown(0))
            {
                StartHold();
            }

            // ����ɿ�ȡ������
            if (Input.GetMouseButtonUp(0))
            {
                if (isHolding)
                {
                    CancelHold();
                }
            }
        }

        private void HandleEscapeInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (cameraController != null)
                {
                    cameraController.ToggleCursorLock();
                }
            }
        }

        #endregion

        #region �������

        private void StartHold()
        {
            if (isHolding) return;

            // û�м�⵽�κ�����ʱ����հ״�
            if (currentDetectedObject == null)
            {
                Debug.Log("[�������] û�м�⵽���壬����հ׵��");
                HandleEmptyClick();
                return;
            }

            isHolding = true;
            holdStartTime = Time.time;

            Debug.Log($"[�������] ��ʼ�����������: {currentDetectedObject.name}");

            // ��ʼ����Э��
            if (holdCoroutine != null)
                StopCoroutine(holdCoroutine);
            holdCoroutine = StartCoroutine(HoldCoroutine());
        }

        private IEnumerator HoldCoroutine()
        {
            float elapsed = 0f;
            float lastLogTime = 0f;

            Debug.Log("[�������] ����Э�̿�ʼ");

            while (elapsed < holdTime && isHolding)
            {
                elapsed = Time.time - holdStartTime;
                float progress = elapsed / holdTime;

                // ÿ0.5�����һ�ν���
                if (elapsed - lastLogTime >= 0.5f)
                {
                    Debug.Log($"[�������] ��������: {progress:P0} ({elapsed:F1}s / {holdTime:F1}s)");
                    lastLogTime = elapsed;
                }

                // ���ͽ����¼�
                if (currentDetectedObject != null)
                {
                    OnObjectHoldProgress?.Invoke(currentDetectedObject, progress);
                }

                yield return null;
            }

            // �������
            if (isHolding && currentDetectedObject != null)
            {
                Debug.Log("[�������] ������ɣ���ʼ�������");
                CompleteObjectDetection();
            }
            else
            {
                Debug.Log("[�������] ����Э�̽����������������㣨���ܱ�ȡ���ˣ�");
            }
        }

        private void CompleteObjectDetection()
        {
            if (currentDetectedObject == null)
            {
                Debug.LogError("[�������] CompleteObjectDetection: currentDetectedObjectΪnull");
                return;
            }

            Debug.Log($"[�������] ������ɣ���ʼ�������: {currentDetectedObject.name}");

            // �жϼ��������Ƿ������bug
            bool isActualBug = currentDetectedBugObject != null && currentDetectedBugObject.IsBugActive();

            Debug.Log($"[�����] ����: {currentDetectedObject.name}, �Ƿ�Ϊ��bug: {isActualBug}");

            // ���ͼ������¼���GameManager��������
            OnObjectDetectionComplete?.Invoke(currentDetectedObject, isActualBug);

            // �����⵽���bug������bug�޸�
            if (isActualBug)
            {
                Debug.Log("[�����] ��⵽��bug�������޸�����");
                OnBugObjectClicked?.Invoke(currentDetectedBugObject);
                currentDetectedBugObject.OnClickedByPlayer();
            }
            else
            {
                Debug.Log("[�����] ��⵽�Ĳ���bug��û�м����bug");
            }

            // ���ó���״̬
            isHolding = false;
            holdCoroutine = null;

            Debug.Log("[�������] ������������");
        }

        private void CancelHold()
        {
            if (!isHolding) return;

            Debug.Log($"[�������] ������ȡ�� - ��ǰ����: {GetHoldProgress():P0}");

            isHolding = false;

            if (holdCoroutine != null)
            {
                StopCoroutine(holdCoroutine);
                holdCoroutine = null;
            }

            // ����ȡ���¼�
            OnHoldCancelled?.Invoke();
        }

        private void HandleEmptyClick()
        {
            Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f);
            Ray ray = cam.ScreenPointToRay(screenCenter);

            // ����ʹ��QueryTriggerInteraction.Collide����֤һ����
            if (Physics.Raycast(ray, out RaycastHit hit, rayLength, clickableMask.value, QueryTriggerInteraction.Collide))
            {
                OnEmptySpaceClicked?.Invoke(hit.point);
                Debug.Log($"����˿հ�����: {hit.collider.name}");
            }
            else
            {
                OnEmptySpaceClicked?.Invoke(ray.origin + ray.direction * rayLength);
                Debug.Log("�������ȫ�հ׵�����");
            }
        }

        #endregion

        #region �����ӿ�

        public GameObject GetCurrentDetectedObject() => currentDetectedObject;

        public BugObject GetCurrentDetectedBug() => currentDetectedBugObject;

        public bool IsHoldingObject() => isHolding;

        public float GetHoldProgress()
        {
            if (!isHolding) return 0f;
            float elapsed = Time.time - holdStartTime;
            return Mathf.Clamp01(elapsed / holdTime);
        }

        public void SetHoldTime(float time)
        {
            holdTime = Mathf.Max(0.1f, time);
        }

        public void ForceStopHold()
        {
            CancelHold();
        }

        #endregion

        #region ���Թ���

        [Header("����")]
        [SerializeField] private bool showDebugGUI = true;

        private void OnGUI()
        {
            if (!showDebugGUI) return;

            GUILayout.BeginArea(new Rect(10, Screen.height - 250, 400, 240));
            GUILayout.Label("=== Player Debug ===");

            GUILayout.Label($"����״̬: {(isGrounded ? "�ŵ�" : "����")}");
            GUILayout.Label($"�ƶ��ٶ�: {controller.velocity.magnitude:F2}");
            GUILayout.Label($"��⵽����: {(currentDetectedObject ? currentDetectedObject.name : "��")}");
            GUILayout.Label($"��⵽Bug: {(currentDetectedBugObject ? currentDetectedBugObject.name : "��")}");
            GUILayout.Label($"Bug����״̬: {(currentDetectedBugObject ? currentDetectedBugObject.IsBugActive().ToString() : "N/A")}");
            GUILayout.Label($"����״̬: {(isHolding ? "������" : "δ����")}");

            if (isHolding)
            {
                float progress = GetHoldProgress();
                GUILayout.Label($"��������: {progress:P0}");

                // ��ʾ������
                Rect progressRect = GUILayoutUtility.GetRect(200, 20);
                GUI.Box(progressRect, "");
                Rect fillRect = new Rect(progressRect.x + 2, progressRect.y + 2,
                                       (progressRect.width - 4) * progress, progressRect.height - 4);
                GUI.Box(fillRect, "", GUI.skin.button);
            }

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("ǿ�Ƽ�⵱ǰ����"))
            {
                if (currentDetectedObject != null)
                {
                    CompleteObjectDetection();
                }
            }
            if (GUILayout.Button("ȡ������"))
            {
                ForceStopHold();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void OnDrawGizmosSelected()
        {
            // ���Ƶ���������
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Vector3 start = transform.position + Vector3.up * 0.1f;
            Vector3 end = start + Vector3.down * (groundCheckDistance + 0.1f);
            Gizmos.DrawLine(start, end);

            // ���Ƶ�ǰ�������
            if (cam != null)
            {
                Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f);
                Ray ray = cam.ScreenPointToRay(screenCenter);
                Gizmos.color = currentDetectedObject != null ? Color.red : Color.yellow;
                Gizmos.DrawRay(ray.origin, ray.direction * rayLength);
            }
        }

        #endregion
    }
}