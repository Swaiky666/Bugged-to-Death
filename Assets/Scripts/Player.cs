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

        [Header("游戏结束控制")]
        [SerializeField] private bool disableControlsOnGameEnd = true;  // 游戏结束时是否禁用控制

        [Header("房间系统集成")]
        [SerializeField] private bool autoRegisterToRoomSystem = true;  // 是否自动注册到房间系统
        [SerializeField] private string roomSystemObjectName = "RoomSystem(Clone)";  // 房间系统对象名称
        [SerializeField] private Transform assignedRoomSystem = null;  // Inspector中显示当前连接的房间系统

        private CharacterController controller;
        private Camera cam;
        private CameraController cameraController;
        private Vector3 velocity;
        private bool isGrounded;
        private GameObject currentDetectedObject;  // 改为检测所有物体
        private BugObject currentDetectedBugObject; // 保留对BugObject的引用

        // 房间系统引用
        private RoomSystem connectedRoomSystem = null;

        // 长按相关变量
        private bool isHolding = false;
        private float holdStartTime = 0f;
        private Coroutine holdCoroutine;

        // 控制状态
        private bool controlsEnabled = true;

        // 事件
        public static event System.Action<BugObject> OnBugObjectClicked;
        public static event System.Action<Vector3> OnEmptySpaceClicked;
        public static event System.Action<GameObject, float> OnObjectHoldProgress;  // 改为GameObject和进度(0-1)
        public static event System.Action OnHoldCancelled;

        // 新增事件：检测完成
        public static event System.Action<GameObject, bool> OnObjectDetectionComplete; // (检测的物体, 是否是真的bug)

        // 新增事件：检测结果UI显示
        public static event System.Action<bool> OnDetectionResult; // (是否检测成功)

        #region Unity生命周期

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            // 尝试获取相机引用
            cam = Camera.main;
            if (cam == null)
            {
                // 如果没有主相机，尝试找子物体中的相机
                cam = GetComponentInChildren<Camera>();
            }

            // 如果还是找不到，记录警告但不报错
            if (cam == null)
            {
                Debug.LogWarning("🎥 Player Awake: 未找到相机引用，将在Start时重试");
            }

            cameraController = GetComponentInChildren<CameraController>();

            // 新增：自动注册到房间系统
            if (autoRegisterToRoomSystem)
            {
                RegisterToRoomSystem();
            }

            Debug.Log($"🎮 Player Awake完成 - 相机: {(cam != null ? cam.name : "未找到")}, 控制器: {(cameraController != null ? "有效" : "无效")}");


        }

        private void Start()
        {
            // 确保相机引用在游戏开始时是有效的
            ValidateCameraReference();
            Debug.Log($"🎮 Player Start: 相机引用状态 - {(cam != null ? $"有效 ({cam.name})" : "无效")}");

            RegisterToGameManager();
            RegisterToRoomSystem();

        }

        private void OnEnable()
        {
            // 订阅游戏结束事件
            GameManager.OnGameOver += OnGameEnded;
            GameManager.OnHappyEnd += OnGameEnded;
            GameManager.OnGameEnded += OnGameEndedWithReason;
        }

        private void OnDisable()
        {
            // 取消订阅
            GameManager.OnGameOver -= OnGameEnded;
            GameManager.OnHappyEnd -= OnGameEnded;
            GameManager.OnGameEnded -= OnGameEndedWithReason;
        }

        private void Update()
        {
            // 检查相机引用是否有效
            if (!ValidateCameraReference())
            {
                return; // 如果相机无效，跳过这一帧的处理
            }

            // 检查控制是否启用
            UpdateControlsState();

            if (controlsEnabled)
            {
                HandleMovement();
                HandleJump();
                HandleRayDetection();
                HandleClickInput();
            }
            else
            {
                // 游戏结束时，取消任何进行中的长按操作
                if (isHolding)
                {
                    CancelHold();
                }
            }

            // ESC键始终可用（用于暂停菜单）
            HandleEscapeInput();
        }

        private void OnDestroy()
        {
            // 清理引用，避免内存泄漏
            cam = null;
            cameraController = null;
            connectedRoomSystem = null;
            assignedRoomSystem = null;

            // 停止所有协程
            if (holdCoroutine != null)
            {
                StopCoroutine(holdCoroutine);
                holdCoroutine = null;
            }

            Debug.Log("🧹 Player: OnDestroy - 清理完成");
        }

        #endregion

        #region 房间系统集成



        private void RegisterToGameManager()
        {
            GameManager gameManager = FindObjectOfType<GameManager>();
            if (gameManager != null)
            {
                gameManager.RegisterPlayer(transform);
                Debug.Log($"✅ Player {gameObject.name} 已注册到 GameManager");
            }
            else
            {
                Debug.LogWarning("⚠️ Player 找不到 GameManager，无法注册");
            }
        }


        /// <summary>
        /// 注册到房间系统
        /// </summary>
        private void RegisterToRoomSystem()
        {
            RoomSystem roomSystem = FindObjectOfType<RoomSystem>();
            if (roomSystem != null)
            {
                roomSystem.SetPlayer(transform);
                Debug.Log($"✅ Player {gameObject.name} 已注册到 RoomSystem");
            }
            else
            {
                Debug.LogWarning("⚠️ Player 找不到 RoomSystem，无法注册");
            }
        }


        /// <summary>
        /// 手动设置房间系统引用
        /// </summary>
        public void SetRoomSystem(RoomSystem roomSystem)
        {
            if (roomSystem != null)
            {
                connectedRoomSystem = roomSystem;
                assignedRoomSystem = roomSystem.transform;
                Debug.Log($"🔗 手动设置房间系统引用: {roomSystem.name}");
            }
            else
            {
                connectedRoomSystem = null;
                assignedRoomSystem = null;
                Debug.Log("🔗 清除房间系统引用");
            }
        }

        /// <summary>
        /// 获取连接的房间系统
        /// </summary>
        public RoomSystem GetConnectedRoomSystem()
        {
            return connectedRoomSystem;
        }

        /// <summary>
        /// 检查是否连接到房间系统
        /// </summary>
        public bool IsConnectedToRoomSystem()
        {
            return connectedRoomSystem != null && connectedRoomSystem.gameObject != null;
        }

        /// <summary>
        /// 强制重新注册到房间系统
        /// </summary>
        public void ForceRegisterToRoomSystem()
        {
            connectedRoomSystem = null;
            assignedRoomSystem = null;
            RegisterToRoomSystem();
        }

        #endregion

        #region 调试Context菜单

        [ContextMenu("🎥 验证相机引用")]
        private void DebugValidateCameraReference()
        {
            bool result = ValidateCameraReference();
            Debug.Log($"🎥 相机引用验证结果: {(result ? "成功" : "失败")}");
            if (result)
            {
                Debug.Log($"当前相机: {cam.name}");
                Debug.Log($"相机控制器: {(cameraController != null ? cameraController.name : "无")}");
            }
        }

        [ContextMenu("🔄 强制重新获取相机")]
        private void DebugForceReacquireCamera()
        {
            cam = null;
            cameraController = null;
            bool result = ValidateCameraReference();
            Debug.Log($"🔄 强制重新获取相机结果: {(result ? "成功" : "失败")}");
        }

        [ContextMenu("🔗 强制重新注册房间系统")]
        private void DebugForceRegisterRoomSystem()
        {
            ForceRegisterToRoomSystem();
        }

        [ContextMenu("🏠 显示房间系统连接状态")]
        private void DebugShowRoomSystemConnection()
        {
            Debug.Log("=== 房间系统连接状态 ===");
            Debug.Log($"自动注册: {autoRegisterToRoomSystem}");
            Debug.Log($"目标对象名: {roomSystemObjectName}");
            Debug.Log($"连接状态: {(IsConnectedToRoomSystem() ? "已连接" : "未连接")}");
            if (connectedRoomSystem != null)
            {
                Debug.Log($"连接的房间系统: {connectedRoomSystem.name}");
                Debug.Log($"房间系统初始化状态: {connectedRoomSystem.transform.GetComponent<RoomSystem>()}");
            }
        }

        [ContextMenu("🎮 显示控制状态")]
        private void DebugShowControlState()
        {
            Debug.Log("=== Player控制状态 ===");
            Debug.Log($"控制启用: {controlsEnabled}");
            Debug.Log($"游戏结束时禁用: {disableControlsOnGameEnd}");
            Debug.Log($"正在长按: {isHolding}");
            Debug.Log($"相机引用: {(cam != null ? cam.name : "无效")}");
            Debug.Log($"相机控制器: {(cameraController != null ? "有效" : "无效")}");
            Debug.Log($"房间系统连接: {(IsConnectedToRoomSystem() ? "已连接" : "未连接")}");

            if (GameManager.Instance != null)
            {
                Debug.Log($"游戏结束状态: {GameManager.Instance.IsGameEnded()}");
                if (GameManager.Instance.IsGameEnded())
                {
                    Debug.Log($"结束原因: {GameManager.Instance.GetGameEndReason()}");
                }
            }
        }

        [ContextMenu("⚠️ 测试相机丢失情况")]
        private void DebugTestCameraLoss()
        {
            if (Application.isPlaying)
            {
                Debug.Log("🧪 模拟相机引用丢失...");
                cam = null;
                cameraController = null;
                Debug.Log("相机引用已清空，下一帧Update将尝试重新获取");
            }
        }

        [ContextMenu("🎮 测试长按完成行为")]
        private void DebugTestHoldCompletion()
        {
            if (Application.isPlaying && currentDetectedObject != null)
            {
                Debug.Log("🧪 模拟长按完成...");
                CompleteObjectDetection();
            }
            else
            {
                Debug.Log("请先将视线对准一个物体，然后运行此测试");
            }
        }

        [ContextMenu("🔄 测试长按重启逻辑")]
        private void DebugTestHoldRestart()
        {
            if (Application.isPlaying)
            {
                Debug.Log("=== 🧪 测试长按重启逻辑 ===");
                Debug.Log($"当前长按状态: {isHolding}");
                Debug.Log($"协程状态: {(holdCoroutine != null ? "运行中" : "空闲")}");
                Debug.Log($"当前检测物体: {(currentDetectedObject != null ? currentDetectedObject.name : "无")}");

                if (isHolding)
                {
                    Debug.Log("🛑 强制完成当前长按...");
                    CompleteObjectDetection();
                }

                Debug.Log("现在尝试开始新的长按 - 这应该会被允许");
                if (currentDetectedObject != null)
                {
                    StartHold();
                }
            }
        }

        #endregion

        #region 控制状态管理

        /// <summary>
        /// 验证相机引用是否有效，如果无效则尝试重新获取
        /// </summary>
        private bool ValidateCameraReference()
        {
            // 检查当前相机引用是否有效
            if (cam != null && cam.gameObject != null)
            {
                return true;
            }

            // 相机引用无效，尝试重新获取
            Debug.LogWarning("🎥 Player: 相机引用无效，尝试重新获取...");

            // 首先尝试获取主相机
            cam = Camera.main;

            // 如果主相机不存在，尝试从子物体中获取
            if (cam == null)
            {
                cam = GetComponentInChildren<Camera>();
            }

            // 如果还是找不到，尝试在场景中查找Camera组件
            if (cam == null)
            {
                cam = FindObjectOfType<Camera>();
            }

            if (cam != null)
            {
                Debug.Log($"🎥 Player: 成功重新获取相机引用: {cam.name}");

                // 同时更新CameraController引用
                if (cameraController == null)
                {
                    cameraController = cam.GetComponent<CameraController>();
                    if (cameraController == null)
                    {
                        cameraController = GetComponentInChildren<CameraController>();
                    }
                }

                return true;
            }
            else
            {
                // Debug.LogWarning("🎥 Player: 无法找到有效的相机引用");
                return false;
            }
        }

        /// <summary>
        /// 更新控制状态
        /// </summary>
        private void UpdateControlsState()
        {
            bool shouldEnableControls = true;

            if (disableControlsOnGameEnd && GameManager.Instance != null)
            {
                // 如果游戏结束且启用了游戏结束禁用控制，则禁用控制
                if (GameManager.Instance.IsGameEnded())
                {
                    shouldEnableControls = false;
                }
            }

            // 如果控制状态发生变化，记录日志
            if (controlsEnabled != shouldEnableControls)
            {
                controlsEnabled = shouldEnableControls;
                Debug.Log($"🎮 玩家控制状态更新: {(controlsEnabled ? "启用" : "禁用")}");

                if (!controlsEnabled)
                {
                    // 禁用控制时，停止所有移动
                    velocity.x = 0f;
                    velocity.z = 0f;
                }
            }
        }

        /// <summary>
        /// 游戏结束事件处理
        /// </summary>
        private void OnGameEnded()
        {
            Debug.Log("🛑 Player: 收到游戏结束事件，准备禁用控制");
            UpdateControlsState();
        }

        /// <summary>
        /// 带原因的游戏结束事件处理
        /// </summary>
        private void OnGameEndedWithReason(string reason)
        {
            Debug.Log($"🛑 Player: 游戏结束 - {reason}，控制已禁用");
            UpdateControlsState();
        }

        /// <summary>
        /// 手动设置控制启用状态
        /// </summary>
        public void SetControlsEnabled(bool enabled)
        {
            controlsEnabled = enabled;
            Debug.Log($"🎮 手动设置玩家控制: {(enabled ? "启用" : "禁用")}");

            if (!enabled && isHolding)
            {
                CancelHold();
            }
        }

        /// <summary>
        /// 检查控制是否启用
        /// </summary>
        public bool AreControlsEnabled()
        {
            return controlsEnabled;
        }

        #endregion

        #region 移动控制

        private void HandleMovement()
        {
            // 确保相机引用有效
            if (cam == null)
            {
                return;
            }

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
            // 确保相机引用有效
            if (cam == null)
            {
                return;
            }

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
                BugObject bug = hit.collider.GetComponentInParent<BugObject>();

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
            // 鼠标按下开始长按（只有在没有进行长按时才能开始新的长按）
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
            // 如果已经在进行长按，则不开始新的长按
            if (isHolding)
            {
                Debug.Log("[长按相关] 已经在进行长按，忽略新的长按请求");
                return;
            }

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

            while (elapsed < holdTime && isHolding && controlsEnabled)
            {
                elapsed = Time.time - holdStartTime;
                float progress = elapsed / holdTime;

                // 每0.5秒输出一次进度
                if (elapsed - lastLogTime >= 0.5f)
                {
                    Debug.Log($"[长按相关] 长按进度: {progress:P0} ({elapsed:F1}s / {holdTime:F1}s)");
                    lastLogTime = elapsed;
                }

                // 发送进度事件 (UIManager会监听这个事件来控制UI)
                if (currentDetectedObject != null)
                {
                    OnObjectHoldProgress?.Invoke(currentDetectedObject, progress);
                }

                yield return null;
            }

            // 长按完成
            if (isHolding && currentDetectedObject != null && controlsEnabled)
            {
                Debug.Log("[长按相关] 长按完成！开始检测物体");
                CompleteObjectDetection();
            }
            else
            {
                Debug.Log("[长按相关] 长按协程结束，但条件不满足（可能被取消了或控制被禁用）");

                // 如果是因为控制被禁用而结束，也要发送取消事件
                if (!controlsEnabled)
                {
                    OnHoldCancelled?.Invoke();
                }
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

            // 触发检测结果UI显示事件
            OnDetectionResult?.Invoke(isActualBug);
            Debug.Log($"[检测结果] 触发UI显示事件: {(isActualBug ? "成功" : "失败")}");

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

            // 长按检测完成后，立即重置状态并发送取消事件
            Debug.Log("[长按相关] 检测完成，重置状态");
            isHolding = false;
            holdCoroutine = null;

            // 发送取消事件来隐藏进度UI和恢复检测UI状态 (UIManager会处理)
            OnHoldCancelled?.Invoke();

            Debug.Log("[长按相关] 物体检测调用完成，事件已发送");
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

            // 发送取消事件 (UIManager会监听这个事件来恢复UI状态)
            OnHoldCancelled?.Invoke();

            Debug.Log("[长按相关] 长按取消完成，事件已发送");
        }

        private void HandleEmptyClick()
        {
            // 确保相机引用有效
            if (cam == null)
            {
                Debug.LogWarning("HandleEmptyClick: 相机引用无效，无法处理空白点击");
                return;
            }

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

        /// <summary>
        /// 设置是否在游戏结束时禁用控制
        /// </summary>
        public void SetDisableControlsOnGameEnd(bool disable)
        {
            disableControlsOnGameEnd = disable;
            UpdateControlsState();
            Debug.Log($"🎮 设置游戏结束时禁用控制: {disable}");
        }

        #endregion

        #region 调试功能

        [Header("调试")]
        [SerializeField] private bool showDebugGUI = true;

        private void OnGUI()
        {
            if (!showDebugGUI) return;

            GUILayout.BeginArea(new Rect(10, Screen.height - 400, 450, 390));
            GUILayout.Label("=== Player Debug ===");

            GUILayout.Label($"地面状态: {(isGrounded ? "着地" : "空中")}");
            GUILayout.Label($"移动速度: {controller.velocity.magnitude:F2}");
            GUILayout.Label($"控制启用: {(controlsEnabled ? "是" : "否")}");
            GUILayout.Label($"相机引用: {(cam != null ? cam.name : "无效")}");
            GUILayout.Label($"相机控制器: {(cameraController != null ? "有效" : "无效")}");

            // 房间系统连接状态
            GUILayout.Label($"房间系统连接: {(IsConnectedToRoomSystem() ? "已连接" : "未连接")}");
            if (connectedRoomSystem != null)
            {
                GUILayout.Label($"连接的系统: {connectedRoomSystem.name}");
            }

            if (GameManager.Instance != null)
            {
                GUILayout.Label($"游戏结束: {GameManager.Instance.IsGameEnded()}");
                if (GameManager.Instance.IsGameEnded())
                {
                    GUILayout.Label($"结束原因: {GameManager.Instance.GetGameEndReason()}");
                }
            }

            GUILayout.Label($"检测到物体: {(currentDetectedObject ? currentDetectedObject.name : "无")}");
            GUILayout.Label($"检测到Bug: {(currentDetectedBugObject ? currentDetectedBugObject.name : "无")}");
            GUILayout.Label($"Bug激活状态: {(currentDetectedBugObject ? currentDetectedBugObject.IsBugActive().ToString() : "N/A")}");
            GUILayout.Label($"长按状态: {(isHolding ? "长按中" : "未按下")}");
            GUILayout.Label($"长按协程: {(holdCoroutine != null ? "运行中" : "空闲")}");

            if (isHolding)
            {
                float progress = GetHoldProgress();
                GUILayout.Label($"长按进度: {progress:P0}");
                GUILayout.Label($"剩余时间: {(holdTime - (Time.time - holdStartTime)):F1}s");

                // 显示进度条
                Rect progressRect = GUILayoutUtility.GetRect(200, 20);
                GUI.Box(progressRect, "");
                Rect fillRect = new Rect(progressRect.x + 2, progressRect.y + 2,
                                       (progressRect.width - 4) * progress, progressRect.height - 4);
                GUI.Box(fillRect, "", GUI.skin.button);
            }

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("强制完成长按"))
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

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("开始新长按"))
            {
                if (currentDetectedObject != null && !isHolding)
                {
                    StartHold();
                }
            }
            if (GUILayout.Button("重新获取相机"))
            {
                cam = null; // 强制重新获取
                ValidateCameraReference();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("切换控制状态"))
            {
                SetControlsEnabled(!controlsEnabled);
            }
            if (GUILayout.Button("测试相机引用"))
            {
                bool isValid = ValidateCameraReference();
                Debug.Log($"🎥 相机引用测试结果: {(isValid ? "有效" : "无效")}");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("重置控制"))
            {
                SetControlsEnabled(true);
                disableControlsOnGameEnd = true;
                UpdateControlsState();
            }
            if (GUILayout.Button("重新注册房间系统"))
            {
                ForceRegisterToRoomSystem();
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