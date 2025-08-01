using UnityEngine;
using System.Collections;

namespace BugFixerGame
{
    [RequireComponent(typeof(CharacterController))]
    public class Player : MonoBehaviour
    {
        [Header("移动参数")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float jumpForce = 5f;
        [SerializeField] private float gravity = -20f;

        [Header("检测设置")]
        [SerializeField] private float groundCheckDistance = 0.3f;
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private LayerMask clickableMask = -1;  // 设为-1检测所有层级
        [SerializeField] private float rayLength = 100f;
        [SerializeField] private bool showDebugRay = true;
        [SerializeField] private bool enableContinuousDebug = true;  // 持续debug输出

        [Header("长按点击设置")]
        [SerializeField] private float holdTime = 2f;              // 长按时间

        private CharacterController controller;
        private Camera cam;
        private CameraController cameraController;
        private Vector3 velocity;
        private bool isGrounded;
        private GameObject currentDetectedObject;  // 改为检测所有物体
        private BugObject currentDetectedBugObject; // 保留对BugObject的引用

        // 长按相关变量
        private bool isHolding = false;
        private float holdStartTime = 0f;
        private Coroutine holdCoroutine;

        // 事件
        public static event System.Action<BugObject> OnBugObjectClicked;
        public static event System.Action<Vector3> OnEmptySpaceClicked;
        public static event System.Action<GameObject, float> OnObjectHoldProgress;  // 改为GameObject和进度(0-1)
        public static event System.Action OnHoldCancelled;

        // 新增事件：检测完成
        public static event System.Action<GameObject, bool> OnObjectDetectionComplete; // (检测的物体, 是否是真的bug)

        #region Unity生命周期

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            cam = Camera.main;
            if (cam == null)
            {
                // 如果没有主相机，尝试找子物体中的相机
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

        #region 移动控制

        private void HandleMovement()
        {
            // 地面检测
            isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, groundCheckDistance + 0.1f, groundMask.value);

            if (isGrounded && velocity.y < 0f)
                velocity.y = -2f;

            // 获取输入
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");

            // 计算移动方向（基于相机朝向）
            Vector3 forward = cam.transform.forward;
            Vector3 right = cam.transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 move = (forward * v + right * h) * moveSpeed;

            // 应用重力
            velocity.y += gravity * Time.deltaTime;
            move.y = velocity.y;

            // 移动角色
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

        #region 射线检测

        private void HandleRayDetection()
        {
            Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f);
            Ray ray = cam.ScreenPointToRay(screenCenter);

            if (showDebugRay)
            {
                Color rayColor = currentDetectedObject != null ? Color.red : Color.yellow;
                Debug.DrawRay(ray.origin, ray.direction * rayLength, rayColor);
            }

            // 清除之前检测到的对象
            GameObject previousObject = currentDetectedObject;
            currentDetectedObject = null;
            currentDetectedBugObject = null;

            // 射线检测 - 使用多种方式确保能检测到物体
            RaycastHit hit;
            bool hitSomething = false;

            // 方法1：检测Trigger和普通碰撞体
            if (Physics.Raycast(ray, out hit, rayLength, clickableMask.value, QueryTriggerInteraction.Collide))
            {
                hitSomething = true;
                currentDetectedObject = hit.collider.gameObject;

                // 持续debug输出
                if (enableContinuousDebug)
                {
                    Debug.Log($"[射线检测] 命中物体: {hit.collider.name}, Layer: {hit.collider.gameObject.layer}, Distance: {hit.distance:F2}, IsTrigger: {hit.collider.isTrigger}");
                }

                // 检查是否有BugObject组件
                BugObject bug = hit.collider.GetComponent<BugObject>();
                if (bug != null)
                {
                    currentDetectedBugObject = bug;
                    if (enableContinuousDebug)
                    {
                        Debug.Log($"[BugObject检测] 找到BugObject: {bug.name}, 类型: {bug.GetBugType()}, 激活状态: {bug.IsBugActive()}");
                    }
                }
                else
                {
                    if (enableContinuousDebug)
                    {
                        Debug.Log($"[射线检测] 物体 {hit.collider.name} 没有BugObject组件");
                    }
                }
            }

            // 如果第一种方法没检测到，尝试忽略Trigger的检测
            if (!hitSomething && Physics.Raycast(ray, out hit, rayLength, clickableMask.value, QueryTriggerInteraction.Ignore))
            {
                hitSomething = true;
                currentDetectedObject = hit.collider.gameObject;

                // 检查BugObject组件
                BugObject bug = hit.collider.GetComponent<BugObject>();
                if (bug != null)
                {
                    currentDetectedBugObject = bug;
                }

                if (enableContinuousDebug)
                {
                    Debug.Log($"[射线检测-忽略Trigger] 命中物体: {hit.collider.name}, Layer: {hit.collider.gameObject.layer}");
                }
            }

            // 如果什么都没检测到
            if (!hitSomething && enableContinuousDebug)
            {
                Debug.Log($"[射线检测] 未命中任何物体 - Ray起点: {ray.origin:F2}, 方向: {ray.direction:F2}, 距离: {rayLength}, LayerMask: {clickableMask.value}");
            }

            // 如果之前有对象但现在没有，取消长按操作
            if (previousObject != null && currentDetectedObject == null && isHolding)
            {
                Debug.Log("[长按相关] 失去对象目标，取消长按操作");
                CancelHold();
            }
            // 如果检测到不同的对象，也取消当前长按
            else if (previousObject != currentDetectedObject && isHolding)
            {
                Debug.Log("[长按相关] 检测到新对象，取消当前长按操作");
                CancelHold();
            }
        }

        #endregion

        #region 点击输入处理

        private void HandleClickInput()
        {
            // 鼠标按下开始长按
            if (Input.GetMouseButtonDown(0))
            {
                StartHold();
            }

            // 鼠标松开取消长按
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

        #region 长按相关

        private void StartHold()
        {
            if (isHolding) return;

            // 没有检测到任何物体时点击空白处
            if (currentDetectedObject == null)
            {
                Debug.Log("[长按相关] 没有检测到物体，处理空白点击");
                HandleEmptyClick();
                return;
            }

            isHolding = true;
            holdStartTime = Time.time;

            Debug.Log($"[长按相关] 开始长按检测物体: {currentDetectedObject.name}");

            // 开始长按协程
            if (holdCoroutine != null)
                StopCoroutine(holdCoroutine);
            holdCoroutine = StartCoroutine(HoldCoroutine());
        }

        private IEnumerator HoldCoroutine()
        {
            float elapsed = 0f;
            float lastLogTime = 0f;

            Debug.Log("[长按相关] 长按协程开始");

            while (elapsed < holdTime && isHolding)
            {
                elapsed = Time.time - holdStartTime;
                float progress = elapsed / holdTime;

                // 每0.5秒输出一次进度
                if (elapsed - lastLogTime >= 0.5f)
                {
                    Debug.Log($"[长按相关] 长按进度: {progress:P0} ({elapsed:F1}s / {holdTime:F1}s)");
                    lastLogTime = elapsed;
                }

                // 发送进度事件
                if (currentDetectedObject != null)
                {
                    OnObjectHoldProgress?.Invoke(currentDetectedObject, progress);
                }

                yield return null;
            }

            // 长按完成
            if (isHolding && currentDetectedObject != null)
            {
                Debug.Log("[长按相关] 长按完成！开始检测物体");
                CompleteObjectDetection();
            }
            else
            {
                Debug.Log("[长按相关] 长按协程结束，但条件不满足（可能被取消了）");
            }
        }

        private void CompleteObjectDetection()
        {
            if (currentDetectedObject == null)
            {
                Debug.LogError("[长按相关] CompleteObjectDetection: currentDetectedObject为null");
                return;
            }

            Debug.Log($"[长按相关] 长按完成，开始检测物体: {currentDetectedObject.name}");

            // 判断检测的物体是否真的是bug
            bool isActualBug = currentDetectedBugObject != null && currentDetectedBugObject.IsBugActive();

            Debug.Log($"[检测结果] 物体: {currentDetectedObject.name}, 是否为真bug: {isActualBug}");

            // 发送检测完成事件给GameManager处理评分
            OnObjectDetectionComplete?.Invoke(currentDetectedObject, isActualBug);

            // 如果检测到真的bug，触发bug修复
            if (isActualBug)
            {
                Debug.Log("[检测结果] 检测到真bug，触发修复流程");
                OnBugObjectClicked?.Invoke(currentDetectedBugObject);
                currentDetectedBugObject.OnClickedByPlayer();
            }
            else
            {
                Debug.Log("[检测结果] 检测到的不是bug或没有激活的bug");
            }

            // 重置长按状态
            isHolding = false;
            holdCoroutine = null;

            Debug.Log("[长按相关] 物体检测调用完成");
        }

        private void CancelHold()
        {
            if (!isHolding) return;

            Debug.Log($"[长按相关] 长按被取消 - 当前进度: {GetHoldProgress():P0}");

            isHolding = false;

            if (holdCoroutine != null)
            {
                StopCoroutine(holdCoroutine);
                holdCoroutine = null;
            }

            // 发送取消事件
            OnHoldCancelled?.Invoke();
        }

        private void HandleEmptyClick()
        {
            Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f);
            Ray ray = cam.ScreenPointToRay(screenCenter);

            // 尝试使用QueryTriggerInteraction.Collide来保证一致性
            if (Physics.Raycast(ray, out RaycastHit hit, rayLength, clickableMask.value, QueryTriggerInteraction.Collide))
            {
                OnEmptySpaceClicked?.Invoke(hit.point);
                Debug.Log($"点击了空白区域: {hit.collider.name}");
            }
            else
            {
                OnEmptySpaceClicked?.Invoke(ray.origin + ray.direction * rayLength);
                Debug.Log("点击了完全空白的区域");
            }
        }

        #endregion

        #region 公共接口

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

        #region 调试功能

        [Header("调试")]
        [SerializeField] private bool showDebugGUI = true;

        private void OnGUI()
        {
            if (!showDebugGUI) return;

            GUILayout.BeginArea(new Rect(10, Screen.height - 250, 400, 240));
            GUILayout.Label("=== Player Debug ===");

            GUILayout.Label($"地面状态: {(isGrounded ? "着地" : "空中")}");
            GUILayout.Label($"移动速度: {controller.velocity.magnitude:F2}");
            GUILayout.Label($"检测到物体: {(currentDetectedObject ? currentDetectedObject.name : "无")}");
            GUILayout.Label($"检测到Bug: {(currentDetectedBugObject ? currentDetectedBugObject.name : "无")}");
            GUILayout.Label($"Bug激活状态: {(currentDetectedBugObject ? currentDetectedBugObject.IsBugActive().ToString() : "N/A")}");
            GUILayout.Label($"长按状态: {(isHolding ? "长按中" : "未按下")}");

            if (isHolding)
            {
                float progress = GetHoldProgress();
                GUILayout.Label($"长按进度: {progress:P0}");

                // 显示进度条
                Rect progressRect = GUILayoutUtility.GetRect(200, 20);
                GUI.Box(progressRect, "");
                Rect fillRect = new Rect(progressRect.x + 2, progressRect.y + 2,
                                       (progressRect.width - 4) * progress, progressRect.height - 4);
                GUI.Box(fillRect, "", GUI.skin.button);
            }

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("强制检测当前物体"))
            {
                if (currentDetectedObject != null)
                {
                    CompleteObjectDetection();
                }
            }
            if (GUILayout.Button("取消长按"))
            {
                ForceStopHold();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void OnDrawGizmosSelected()
        {
            // 绘制地面检测射线
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Vector3 start = transform.position + Vector3.up * 0.1f;
            Vector3 end = start + Vector3.down * (groundCheckDistance + 0.1f);
            Gizmos.DrawLine(start, end);

            // 绘制当前检测射线
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