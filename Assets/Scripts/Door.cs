using UnityEngine;
using BugFixerGame;

/// <summary>
/// 门控制脚本 - 旋转式开门动画
/// 只负责注册到GameManager和执行开关门动画，距离检测由GameManager统一处理
/// </summary>
public class Door : MonoBehaviour
{
    [Header("门状态设置")]
    [SerializeField] private bool isOpen = false;                   // 当前门是否打开
    [SerializeField] private float animationDuration = 1f;          // 开关门动画时长

    [Header("门旋转设置")]
    [SerializeField] private Vector3 closedRotation = Vector3.zero; // 关门旋转（相对于初始旋转）
    [SerializeField] private Vector3 openRotation = new Vector3(0, 90f, 0); // 开门旋转（相对于初始旋转）
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // 动画曲线

    [Header("调试信息")]
    [SerializeField] private bool showDebugInfo = false;           // 显示调试信息

    // 运行时状态显示（只读）
    [Header("运行时状态（只读）")]
    [SerializeField, ReadOnly] private int doorId;
    [SerializeField, ReadOnly] private bool isAnimating = false;
    [SerializeField, ReadOnly] private bool isRegistered = false;   // 是否已注册到GameManager

    // 组件引用
    private Quaternion initialRotation;
    private float animationStartTime;
    private Quaternion animationStartRot;
    private Quaternion animationTargetRot;

    // 门ID（用于调试）
    private static int nextDoorId = 1;

    #region Unity生命周期

    private void Awake()
    {
        // 分配门ID
        doorId = nextDoorId++;

        // 记录初始旋转
        initialRotation = transform.rotation;

        // 设置门的名称
        if (string.IsNullOrEmpty(gameObject.name) || gameObject.name.StartsWith("GameObject"))
        {
            gameObject.name = $"Door_{doorId}";
        }
    }

    private void Start()
    {
        // 注册到GameManager
        RegisterToGameManager();

        // 设置初始旋转
        SetDoorRotation(isOpen);

        Debug.Log($"🚪 门 {gameObject.name} 初始化完成");
    }

    private void Update()
    {
        // 只需要更新门动画
        UpdateDoorAnimation();
    }

    private void OnDestroy()
    {
        // 从GameManager注销
        UnregisterFromGameManager();
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
            SetDoorRotation(open);
        }

        if (showDebugInfo)
        {
            Debug.Log($"🚪 门 {gameObject.name}: {(open ? "开启" : "关闭")} (动画: {animate})");
        }
    }

    /// <summary>
    /// 立即设置门的旋转
    /// </summary>
    private void SetDoorRotation(bool open)
    {
        Vector3 targetEuler = open ? (initialRotation.eulerAngles + openRotation) : (initialRotation.eulerAngles + closedRotation);
        transform.rotation = Quaternion.Euler(targetEuler);
        isAnimating = false;
    }

    /// <summary>
    /// 开始门的动画
    /// </summary>
    private void StartDoorAnimation(bool open)
    {
        animationStartTime = Time.time;
        animationStartRot = transform.rotation;

        Vector3 targetEuler = open ? (initialRotation.eulerAngles + openRotation) : (initialRotation.eulerAngles + closedRotation);
        animationTargetRot = Quaternion.Euler(targetEuler);

        isAnimating = true;

        if (showDebugInfo)
        {
            Debug.Log($"🎬 门 {gameObject.name}: 开始旋转动画 {animationStartRot.eulerAngles} → {animationTargetRot.eulerAngles}");
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
            transform.rotation = animationTargetRot;
            isAnimating = false;

            if (showDebugInfo)
            {
                Debug.Log($"🎬 门 {gameObject.name}: 旋转动画完成，最终角度: {transform.rotation.eulerAngles}");
            }
        }
        else
        {
            // 应用动画曲线进行平滑旋转
            float curveValue = animationCurve.Evaluate(progress);
            transform.rotation = Quaternion.Slerp(animationStartRot, animationTargetRot, curveValue);
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
            isRegistered = true;
            Debug.Log($"✅ 门 {gameObject.name} 已注册到 GameManager");
        }
        else
        {
            Debug.LogWarning($"⚠️ 门 {gameObject.name} 找不到 GameManager，无法注册");
            isRegistered = false;
        }
    }

    /// <summary>
    /// 从GameManager注销
    /// </summary>
    private void UnregisterFromGameManager()
    {
        if (GameManager.Instance != null && isRegistered)
        {
            GameManager.Instance.UnregisterDoor(this);
            isRegistered = false;
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
    /// 获取门ID
    /// </summary>
    public int GetDoorId() => doorId;

    /// <summary>
    /// 是否正在播放动画
    /// </summary>
    public bool IsAnimating() => isAnimating;

    /// <summary>
    /// 是否已注册到GameManager
    /// </summary>
    public bool IsRegistered() => isRegistered;

    /// <summary>
    /// 获取门的位置（供GameManager距离计算使用）
    /// </summary>
    public Vector3 GetPosition() => transform.position;

    /// <summary>
    /// 设置开门和关门的旋转角度
    /// </summary>
    public void SetRotationAngles(Vector3 closedRot, Vector3 openRot)
    {
        closedRotation = closedRot;
        openRotation = openRot;

        Debug.Log($"🔧 门 {gameObject.name}: 旋转角度设置 - 关门: {closedRot}, 开门: {openRot}");

        // 如果不在动画中，立即应用当前状态的旋转
        if (!isAnimating)
        {
            SetDoorRotation(isOpen);
        }
    }

    /// <summary>
    /// 获取当前旋转相对于初始旋转的偏移
    /// </summary>
    public Vector3 GetCurrentRotationOffset()
    {
        return (transform.rotation * Quaternion.Inverse(initialRotation)).eulerAngles;
    }

    /// <summary>
    /// 设置动画时长
    /// </summary>
    public void SetAnimationDuration(float duration)
    {
        animationDuration = Mathf.Max(0.1f, duration);
        Debug.Log($"🔧 门 {gameObject.name}: 动画时长设置为 {animationDuration}秒");
    }

    #endregion

    #region 调试功能

    private void OnDrawGizmos()
    {
        if (!showDebugInfo) return;

        // 绘制门的旋转中心点
        Gizmos.color = isRegistered ? Color.green : Color.red;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.3f);

        // 绘制开门方向指示器
        Vector3 forwardClosed = transform.position + (Quaternion.Euler(initialRotation.eulerAngles + closedRotation) * Vector3.forward * 1.5f);
        Vector3 forwardOpen = transform.position + (Quaternion.Euler(initialRotation.eulerAngles + openRotation) * Vector3.forward * 1.5f);

        // 关门方向（红色）
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, forwardClosed);
        Gizmos.DrawWireCube(forwardClosed, Vector3.one * 0.1f);

        // 开门方向（绿色）
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, forwardOpen);
        Gizmos.DrawWireCube(forwardOpen, Vector3.one * 0.1f);

        // 当前门朝向
        Gizmos.color = isOpen ? Color.green : Color.red;
        Vector3 currentForward = transform.position + transform.forward * 1.2f;
        Gizmos.DrawLine(transform.position, currentForward);

        // 绘制旋转扇形区域
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        DrawRotationArc();
    }

    private void DrawRotationArc()
    {
        // 绘制从关门到开门的弧形区域
        float closedAngle = (initialRotation.eulerAngles + closedRotation).y;
        float openAngle = (initialRotation.eulerAngles + openRotation).y;

        // 确保角度在0-360范围内
        closedAngle = closedAngle % 360f;
        openAngle = openAngle % 360f;

        float arcAngle = Mathf.DeltaAngle(closedAngle, openAngle);
        int segments = 20;
        float angleStep = arcAngle / segments;

        Vector3 center = transform.position;
        float radius = 1.0f;

        for (int i = 0; i < segments; i++)
        {
            float angle1 = closedAngle + angleStep * i;
            float angle2 = closedAngle + angleStep * (i + 1);

            Vector3 point1 = center + Quaternion.Euler(0, angle1, 0) * Vector3.forward * radius;
            Vector3 point2 = center + Quaternion.Euler(0, angle2, 0) * Vector3.forward * radius;

            Gizmos.DrawLine(point1, point2);
            if (i == 0) Gizmos.DrawLine(center, point1);
            if (i == segments - 1) Gizmos.DrawLine(center, point2);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // 绘制详细的旋转信息
#if UNITY_EDITOR
        Vector3 labelPos = transform.position + Vector3.up * 2f;
        string infoText = $"Door {doorId}\n" +
                         $"状态: {(isOpen ? "开启" : "关闭")}\n" +
                         $"已注册: {(isRegistered ? "是" : "否")}\n" +
                         $"正在动画: {(isAnimating ? "是" : "否")}\n" +
                         $"当前角度: {transform.rotation.eulerAngles.y:F1}°\n" +
                         $"目标角度: {(isOpen ? openRotation.y : closedRotation.y):F1}°";

        UnityEditor.Handles.Label(labelPos, infoText);
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
        Debug.Log($"已注册: {(isRegistered ? "是" : "否")}");
        Debug.Log($"正在动画: {(isAnimating ? "是" : "否")}");
        Debug.Log($"位置: {transform.position}");
        Debug.Log($"初始旋转: {initialRotation.eulerAngles}");
        Debug.Log($"当前旋转: {transform.rotation.eulerAngles}");
        Debug.Log($"关门旋转: {initialRotation.eulerAngles + closedRotation}");
        Debug.Log($"开门旋转: {initialRotation.eulerAngles + openRotation}");
        Debug.Log($"动画时长: {animationDuration}秒");
    }

    [ContextMenu("重置到初始旋转")]
    private void ResetToInitialRotation()
    {
        if (Application.isPlaying)
        {
            transform.rotation = initialRotation;
            isAnimating = false;
            isOpen = false;
            Debug.Log($"🔄 门 {gameObject.name}: 已重置到初始旋转");
        }
    }

    [ContextMenu("设置为标准门（0°→90°）")]
    private void SetStandardDoorRotation()
    {
        closedRotation = Vector3.zero;
        openRotation = new Vector3(0, 90f, 0);

        if (Application.isPlaying && !isAnimating)
        {
            SetDoorRotation(isOpen);
        }

        Debug.Log($"🚪 门 {gameObject.name}: 设置为标准旋转 (0° → 90°)");
    }

    [ContextMenu("设置为反向门（0°→-90°）")]
    private void SetReverseDoorRotation()
    {
        closedRotation = Vector3.zero;
        openRotation = new Vector3(0, -90f, 0);

        if (Application.isPlaying && !isAnimating)
        {
            SetDoorRotation(isOpen);
        }

        Debug.Log($"🚪 门 {gameObject.name}: 设置为反向旋转 (0° → -90°)");
    }

    [ContextMenu("测试快速开关门")]
    private void TestQuickToggle()
    {
        if (Application.isPlaying)
        {
            Debug.Log("🧪 测试快速开关门动画");
            SetDoorState(!isOpen, true);
        }
    }

    [ContextMenu("测试无动画切换")]
    private void TestInstantToggle()
    {
        if (Application.isPlaying)
        {
            Debug.Log("🧪 测试无动画状态切换");
            SetDoorState(!isOpen, false);
        }
    }

    #endregion
}