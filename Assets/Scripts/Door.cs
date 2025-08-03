using UnityEngine;
using BugFixerGame;

/// <summary>
/// 门控制脚本 - 检测玩家距离并控制门的开关
/// 自动注册到GameManager进行全局门状态管理
/// </summary>
public class Door : MonoBehaviour
{
    [Header("距离检测设置")]
    [SerializeField] private float detectionDistance = 3f;          // 玩家检测距离
    [SerializeField] private float detectionInterval = 0.1f;        // 检测间隔（秒）

    [Header("门状态设置")]
    [SerializeField] private bool isOpen = false;                   // 当前门是否打开
    [SerializeField] private float animationDuration = 1f;          // 开关门动画时长

    [Header("门动画设置")]
    [SerializeField] private Vector3 closedPosition = Vector3.zero; // 关门位置（相对于初始位置）
    [SerializeField] private Vector3 openPosition = new Vector3(0, 3f, 0); // 开门位置（相对于初始位置）
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // 动画曲线

    [Header("调试信息")]
    [SerializeField] private bool showDebugInfo = false;           // 显示调试信息
    [SerializeField, ReadOnly] private float currentPlayerDistance = float.MaxValue; // 当前玩家距离
    [SerializeField, ReadOnly] private bool isPlayerNearby = false; // 玩家是否在附近

    // 组件引用
    private Transform playerTransform;
    private Vector3 initialPosition;
    private bool isAnimating = false;
    private float animationStartTime;
    private Vector3 animationStartPos;
    private Vector3 animationTargetPos;

    // 检测计时器
    private float lastDetectionTime;

    // 门ID（用于调试）
    [SerializeField, ReadOnly] private int doorId;
    private static int nextDoorId = 1;

    #region Unity生命周期

    private void Awake()
    {
        // 分配门ID
        doorId = nextDoorId++;

        // 记录初始位置
        initialPosition = transform.position;

        // 设置门的名称
        if (string.IsNullOrEmpty(gameObject.name) || gameObject.name.StartsWith("GameObject"))
        {
            gameObject.name = $"Door_{doorId}";
        }
    }

    private void Start()
    {
        // 查找玩家
        FindPlayer();

        // 注册到GameManager
        RegisterToGameManager();

        // 设置初始位置
        SetDoorPosition(isOpen);

        Debug.Log($"🚪 门 {gameObject.name} 初始化完成，检测距离: {detectionDistance}m");
    }

    private void Update()
    {
        // 定期检测玩家距离
        if (Time.time - lastDetectionTime >= detectionInterval)
        {
            CheckPlayerDistance();
            lastDetectionTime = Time.time;
        }

        // 更新门动画
        UpdateDoorAnimation();
    }

    private void OnDestroy()
    {
        // 从GameManager注销
        UnregisterFromGameManager();
    }

    #endregion

    #region 玩家检测

    /// <summary>
    /// 查找玩家对象
    /// </summary>
    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            Debug.Log($"🎮 门 {gameObject.name} 找到玩家: {playerObj.name}");
        }
        else
        {
            Debug.LogWarning($"⚠️ 门 {gameObject.name} 找不到标签为'Player'的玩家对象");
        }
    }

    /// <summary>
    /// 检测玩家距离
    /// </summary>
    private void CheckPlayerDistance()
    {
        if (playerTransform == null)
        {
            // 尝试重新查找玩家
            FindPlayer();
            return;
        }

        // 计算距离
        currentPlayerDistance = Vector3.Distance(transform.position, playerTransform.position);
        bool wasNearby = isPlayerNearby;
        isPlayerNearby = currentPlayerDistance <= detectionDistance;

        // 检测状态改变
        if (isPlayerNearby != wasNearby)
        {
            OnPlayerProximityChanged(isPlayerNearby);
        }
    }

    /// <summary>
    /// 玩家接近状态改变时调用
    /// </summary>
    private void OnPlayerProximityChanged(bool playerNearby)
    {
        if (showDebugInfo)
        {
            Debug.Log($"🚪 门 {gameObject.name}: 玩家{(playerNearby ? "进入" : "离开")}检测范围 (距离: {currentPlayerDistance:F2}m)");
        }

        // 通知GameManager更新全局门状态
        if (GameManager.Instance != null)
        {
            GameManager.Instance.UpdateGlobalDoorState(playerNearby);
        }
    }

    #endregion

    #region 门状态控制

    /// <summary>
    /// 设置门的开关状态（由GameManager调用）
    /// </summary>
    public void SetDoorState(bool open, bool animate = true)
    {
        if (isOpen == open && !isAnimating) return; // 状态相同且不在动画中，直接返回

        isOpen = open;

        if (animate && gameObject.activeInHierarchy)
        {
            StartDoorAnimation(open);
        }
        else
        {
            SetDoorPosition(open);
        }

        if (showDebugInfo)
        {
            Debug.Log($"🚪 门 {gameObject.name}: {(open ? "开启" : "关闭")} (动画: {animate})");
        }
    }

    /// <summary>
    /// 立即设置门的位置
    /// </summary>
    private void SetDoorPosition(bool open)
    {
        Vector3 targetPos = open ? (initialPosition + openPosition) : (initialPosition + closedPosition);
        transform.position = targetPos;
        isAnimating = false;
    }

    /// <summary>
    /// 开始门的动画
    /// </summary>
    private void StartDoorAnimation(bool open)
    {
        animationStartTime = Time.time;
        animationStartPos = transform.position;
        animationTargetPos = open ? (initialPosition + openPosition) : (initialPosition + closedPosition);
        isAnimating = true;

        if (showDebugInfo)
        {
            Debug.Log($"🎬 门 {gameObject.name}: 开始动画 {animationStartPos} → {animationTargetPos}");
        }
    }

    /// <summary>
    /// 更新门的动画
    /// </summary>
    private void UpdateDoorAnimation()
    {
        if (!isAnimating) return;

        float elapsed = Time.time - animationStartTime;
        float progress = elapsed / animationDuration;

        if (progress >= 1f)
        {
            // 动画完成
            transform.position = animationTargetPos;
            isAnimating = false;

            if (showDebugInfo)
            {
                Debug.Log($"🎬 门 {gameObject.name}: 动画完成，最终位置: {transform.position}");
            }
        }
        else
        {
            // 应用动画曲线
            float curveValue = animationCurve.Evaluate(progress);
            transform.position = Vector3.Lerp(animationStartPos, animationTargetPos, curveValue);
        }
    }

    #endregion

    #region GameManager注册

    /// <summary>
    /// 注册到GameManager
    /// </summary>
    private void RegisterToGameManager()
    {
        // 查找GameManager
        GameManager gameManager = FindGameManager();
        if (gameManager != null)
        {
            gameManager.RegisterDoor(this);
            Debug.Log($"✅ 门 {gameObject.name} 已注册到 GameManager");
        }
        else
        {
            Debug.LogWarning($"⚠️ 门 {gameObject.name} 找不到 GameManager，无法注册");
        }
    }

    /// <summary>
    /// 从GameManager注销
    /// </summary>
    private void UnregisterFromGameManager()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.UnregisterDoor(this);
            Debug.Log($"❌ 门 {gameObject.name} 已从 GameManager 注销");
        }
    }

    /// <summary>
    /// 查找场景中的GameManager
    /// </summary>
    private GameManager FindGameManager()
    {
        // 首先尝试通过单例获取
        if (GameManager.Instance != null)
        {
            return GameManager.Instance;
        }

        // 尝试通过名称查找GameObject
        GameObject gameManagerObj = GameObject.Find("GameManager");
        if (gameManagerObj != null)
        {
            GameManager gameManager = gameManagerObj.GetComponent<GameManager>();
            if (gameManager != null)
            {
                return gameManager;
            }
        }

        // 最后尝试通过类型查找
        return FindObjectOfType<GameManager>();
    }

    #endregion

    #region 公共接口

    /// <summary>
    /// 获取当前门状态
    /// </summary>
    public bool IsOpen() => isOpen;

    /// <summary>
    /// 获取玩家是否在附近
    /// </summary>
    public bool IsPlayerNearby() => isPlayerNearby;

    /// <summary>
    /// 获取当前玩家距离
    /// </summary>
    public float GetPlayerDistance() => currentPlayerDistance;

    /// <summary>
    /// 获取检测距离
    /// </summary>
    public float GetDetectionDistance() => detectionDistance;

    /// <summary>
    /// 设置检测距离
    /// </summary>
    public void SetDetectionDistance(float distance)
    {
        detectionDistance = Mathf.Max(0.1f, distance);
        Debug.Log($"🔧 门 {gameObject.name}: 检测距离设置为 {detectionDistance}m");
    }

    /// <summary>
    /// 获取门ID
    /// </summary>
    public int GetDoorId() => doorId;

    /// <summary>
    /// 是否正在播放动画
    /// </summary>
    public bool IsAnimating() => isAnimating;

    #endregion

    #region 调试功能

    private void OnDrawGizmos()
    {
        if (!showDebugInfo) return;

        // 绘制检测范围
        Gizmos.color = isPlayerNearby ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionDistance);

        // 绘制门的开关位置
        Vector3 basePos = Application.isPlaying ? initialPosition : transform.position;

        // 关门位置
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(basePos + closedPosition, Vector3.one * 0.5f);

        // 开门位置
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(basePos + openPosition, Vector3.one * 0.5f);

        // 当前位置
        Gizmos.color = isOpen ? Color.green : Color.red;
        Gizmos.DrawCube(transform.position, Vector3.one * 0.3f);

        // 玩家连线
        if (playerTransform != null)
        {
            Gizmos.color = isPlayerNearby ? Color.green : Color.gray;
            Gizmos.DrawLine(transform.position, playerTransform.position);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // 绘制动画路径
        Vector3 basePos = Application.isPlaying ? initialPosition : transform.position;
        Vector3 closedPos = basePos + closedPosition;
        Vector3 openPos = basePos + openPosition;

        Gizmos.color = Color.white;
        Gizmos.DrawLine(closedPos, openPos);

        // 绘制标签
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f,
            $"Door {doorId}\n距离: {currentPlayerDistance:F2}m\n状态: {(isOpen ? "开启" : "关闭")}\n玩家附近: {(isPlayerNearby ? "是" : "否")}");
#endif
    }

    [ContextMenu("测试开门")]
    private void TestOpenDoor()
    {
        if (Application.isPlaying)
        {
            SetDoorState(true, true);
        }
    }

    [ContextMenu("测试关门")]
    private void TestCloseDoor()
    {
        if (Application.isPlaying)
        {
            SetDoorState(false, true);
        }
    }

    [ContextMenu("强制查找玩家")]
    private void ForceeFindPlayer()
    {
        FindPlayer();
    }

    [ContextMenu("重新注册到GameManager")]
    private void ReregisterToGameManager()
    {
        if (Application.isPlaying)
        {
            UnregisterFromGameManager();
            RegisterToGameManager();
        }
    }

    [ContextMenu("显示门信息")]
    private void ShowDoorInfo()
    {
        Debug.Log($"=== 门 {gameObject.name} 信息 ===");
        Debug.Log($"门ID: {doorId}");
        Debug.Log($"当前状态: {(isOpen ? "开启" : "关闭")}");
        Debug.Log($"玩家距离: {currentPlayerDistance:F2}m");
        Debug.Log($"检测距离: {detectionDistance}m");
        Debug.Log($"玩家在附近: {(isPlayerNearby ? "是" : "否")}");
        Debug.Log($"正在动画: {(isAnimating ? "是" : "否")}");
        Debug.Log($"初始位置: {initialPosition}");
        Debug.Log($"当前位置: {transform.position}");
        Debug.Log($"关门位置: {initialPosition + closedPosition}");
        Debug.Log($"开门位置: {initialPosition + openPosition}");
    }

    #endregion
}