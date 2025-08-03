using System.Collections.Generic;
using System.Collections;
using System;
using System.Linq;
using UnityEngine;

public class RoomSystem : MonoBehaviour
{
    [Header("房间设置")]
    public List<GameObject> roomPrefabs = new List<GameObject>(10);
    public float roomSpacing = 20f;
    public int visibleRoomCount = 10;
    public int playerCenterPosition = 4; // 玩家在可见房间中的目标位置
    public Vector3 initialRoomCenter = Vector3.zero; // 初始房间中心位置

    [Header("检测设置")]
    public float detectionInterval = 0.1f; // 检测间隔（秒）

    [Header("玩家引用管理")]
    [SerializeField] private Transform assignedPlayer = null; // 显示当前分配的玩家
    [SerializeField] private float maxWaitTimeForPlayer = 10f; // 等待玩家注册的最大时间（秒）
    [SerializeField] private bool enablePlayerWaitingLog = true; // 是否启用等待玩家的日志

    [Header("Debug绘制设置")]
    public Vector3 roomSize = new Vector3(15f, 10f, 15f);
    public Color debugLineColor = Color.green;
    public bool showDebugLines = true;

    [Header("运行时信息")]
    [SerializeField] private int currentRoomSequence = 0;
    [SerializeField] private List<RoomInstance> roomInstances = new List<RoomInstance>();

    [Header("Bug追踪信息")]
    [SerializeField] private int totalBugsInAllRooms = 0;
    [SerializeField] private int totalBugsFixed = 0;
    [SerializeField] private bool showBugTrackingInfo = true;

    [Header("当前房间Bug信息")]
    [SerializeField] private List<BugFixerGame.BugObject> currentRoomBugs = new List<BugFixerGame.BugObject>();
    [SerializeField] private int currentRoomOriginalBugCount = 0;
    [SerializeField] private int currentRoomFixedBugCount = 0;
    [SerializeField] private bool autoUpdateCurrentRoomInfo = true;

    [Header("调试信息")]
    [SerializeField] private bool enableDebugLog = true;

    private Transform player;
    private bool roomsCreated = false; // 房间是否已创建
    private bool playerRegistered = false; // 玩家是否已注册
    private bool isFullyInitialized = false; // 是否完全初始化完成
    private int lastProcessedSequence = int.MinValue;
    private float lastDetectionTime = 0f;
    private float roomCreationTime = 0f; // 房间创建完成的时间

    // 事件
    public static event System.Action OnAllBugsFixed; // 所有bug修复完成事件
    public static event System.Action OnRoomsCreated; // 房间创建完成事件
    public static event System.Action OnPlayerRegistered; // 玩家注册完成事件
    public static event System.Action OnSystemFullyInitialized; // 系统完全初始化完成事件

    [System.Serializable]
    public class RoomInstance
    {
        public GameObject gameObject;
        public int currentSequence;
        public int roomTypeIndex;
        public float worldPosition;

        [Header("Bug追踪")]
        public List<BugFixerGame.BugObject> bugsInRoom = new List<BugFixerGame.BugObject>();
        public int originalBugCount = 0;
        public int fixedBugCount = 0;

        public RoomInstance(GameObject go, int sequence, int typeIndex, float worldPos)
        {
            gameObject = go;
            currentSequence = sequence;
            roomTypeIndex = typeIndex;
            worldPosition = worldPos;
            bugsInRoom = new List<BugFixerGame.BugObject>();
            originalBugCount = 0;
            fixedBugCount = 0;
        }

        public void UpdatePosition(int newSequence, float newWorldPos, List<GameObject> roomPrefabs)
        {
            currentSequence = newSequence;
            worldPosition = newWorldPos;
            roomTypeIndex = Mathf.Abs(newSequence) % roomPrefabs.Count;

            if (gameObject != null)
            {
                gameObject.transform.position = new Vector3(newWorldPos, 0, 0);
                gameObject.name = $"Room_Seq{newSequence}_Type{roomTypeIndex + 1}";
            }
        }

        public float GetDistanceToPlayer(Vector3 playerPos)
        {
            if (gameObject == null) return float.MaxValue;
            return Vector3.Distance(playerPos, gameObject.transform.position);
        }

        /// <summary>
        /// 清理已销毁的Bug对象
        /// </summary>
        public void CleanupDestroyedBugs()
        {
            // 移除所有null引用的bug
            for (int i = bugsInRoom.Count - 1; i >= 0; i--)
            {
                if (bugsInRoom[i] == null)
                {
                    bugsInRoom.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 扫描房间内的所有BugObject
        /// </summary>
        public void ScanForBugObjects()
        {
            bugsInRoom.Clear();
            fixedBugCount = 0;

            if (gameObject != null)
            {
                // 获取房间及其所有子物体中的BugObject组件
                BugFixerGame.BugObject[] foundBugs = gameObject.GetComponentsInChildren<BugFixerGame.BugObject>();

                foreach (var bug in foundBugs)
                {
                    if (bug != null)
                    {
                        bugsInRoom.Add(bug);

                        // 检查是否已经被修复（这种情况主要发生在房间重用时）
                        if (!bug.IsBugActive() || bug.IsBeingFixed())
                        {
                            fixedBugCount++;
                        }
                    }
                }

                originalBugCount = bugsInRoom.Count;

                Debug.Log($"🔍 房间序列{currentSequence}扫描完成：找到{originalBugCount}个BugObject，其中{fixedBugCount}个已修复");
            }
        }

        /// <summary>
        /// 移除已修复的bug
        /// </summary>
        public bool RemoveFixedBug(BugFixerGame.BugObject fixedBug)
        {
            if (bugsInRoom.Contains(fixedBug))
            {
                fixedBugCount++;
                Debug.Log($"🔧 房间序列{currentSequence}：Bug {fixedBug.name} 已修复 ({fixedBugCount}/{originalBugCount})");
                return true;
            }
            return false;
        }

        /// <summary>
        /// 检查房间内所有bug是否都已修复
        /// </summary>
        public bool AreAllBugsFixed()
        {
            return originalBugCount > 0 && fixedBugCount >= originalBugCount;
        }

        /// <summary>
        /// 获取房间bug统计信息
        /// </summary>
        public string GetBugStatusInfo()
        {
            if (originalBugCount == 0)
                return "无Bug";

            return $"{fixedBugCount}/{originalBugCount} ({(float)fixedBugCount / originalBugCount:P0})";
        }
    }

    #region 玩家引用管理

    /// <summary>
    /// 供Player调用的设置玩家引用方法（这是玩家主动注册的入口）
    /// </summary>
    public void SetPlayer(Transform playerTransform)
    {
        if (playerTransform == null)
        {
            Debug.LogWarning("⚠️ 尝试设置空的玩家引用");
            return;
        }

        player = playerTransform;
        assignedPlayer = playerTransform; // 更新Inspector显示
        playerRegistered = true;

        Debug.Log($"🎮 房间系统收到玩家注册: {playerTransform.name}, 位置: {playerTransform.position}");

        // 触发玩家注册事件
        OnPlayerRegistered?.Invoke();

        // 如果房间已创建且玩家已注册，则完成完全初始化
        if (roomsCreated && playerRegistered)
        {
            CompleteFullInitialization();
        }
    }

    /// <summary>
    /// 获取当前玩家引用
    /// </summary>
    public Transform GetPlayer()
    {
        return player;
    }

    /// <summary>
    /// 检查是否有有效的玩家引用
    /// </summary>
    public bool HasValidPlayer()
    {
        return player != null && player.gameObject != null;
    }

    /// <summary>
    /// 清除玩家引用
    /// </summary>
    public void ClearPlayer()
    {
        player = null;
        assignedPlayer = null;
        playerRegistered = false;
        isFullyInitialized = false;
        Debug.Log("🎮 已清除玩家引用");
    }

    /// <summary>
    /// 检查玩家是否已注册
    /// </summary>
    public bool IsPlayerRegistered()
    {
        return playerRegistered;
    }

    /// <summary>
    /// 检查系统是否完全初始化
    /// </summary>
    public bool IsFullyInitialized()
    {
        return isFullyInitialized;
    }

    /// <summary>
    /// 检查房间是否已创建
    /// </summary>
    public bool AreRoomsCreated()
    {
        return roomsCreated;
    }

    /// <summary>
    /// 检查是否正在等待玩家注册
    /// </summary>
    public bool IsWaitingForPlayer()
    {
        return roomsCreated && !playerRegistered;
    }

    /// <summary>
    /// 显示玩家分配状态（用于调试）
    /// </summary>
    public void ShowPlayerAssignmentStatus()
    {
        Debug.Log("=== 玩家分配状态 ===");
        Debug.Log($"房间已创建: {roomsCreated}");
        Debug.Log($"玩家已注册: {playerRegistered}");
        Debug.Log($"完全初始化: {isFullyInitialized}");
        Debug.Log($"等待玩家中: {IsWaitingForPlayer()}");
        Debug.Log($"最大等待时间: {maxWaitTimeForPlayer}秒");
        Debug.Log($"当前玩家引用: {(player != null ? player.name : "无")}");
        Debug.Log($"分配的玩家显示: {(assignedPlayer != null ? assignedPlayer.name : "无")}");

        if (roomsCreated && !playerRegistered)
        {
            float waitTime = Time.time - roomCreationTime;
            Debug.Log($"已等待时间: {waitTime:F1}秒");
        }

        if (player != null)
        {
            Debug.Log($"玩家位置: {player.position}");
            if (isFullyInitialized)
            {
                Debug.Log($"计算序列: {GetPlayerSequenceFromPosition()}");
            }
        }
    }

    /// <summary>
    /// 强制重新等待玩家分配（用于调试）
    /// </summary>
    public void ForceWaitForPlayer()
    {
        if (isFullyInitialized)
        {
            Debug.LogWarning("系统已完全初始化，无法重新等待玩家");
            return;
        }

        if (!roomsCreated)
        {
            Debug.LogWarning("房间尚未创建，无法等待玩家");
            return;
        }

        Debug.Log("🕐 强制重新开始等待玩家分配...");

        // 重置玩家相关状态
        player = null;
        assignedPlayer = null;
        playerRegistered = false;
        isFullyInitialized = false;
        roomCreationTime = Time.time; // 重置等待开始时间

        if (enablePlayerWaitingLog)
        {
            StartCoroutine(LogPlayerWaitingStatus());
        }
    }

    #endregion

    [ContextMenu("显示当前房间Bug信息")]
    public void ShowCurrentRoomBugInfo()
    {
        UpdateCurrentRoomBugInfo();

        Debug.Log("=== 当前房间Bug详细信息 ===");
        Debug.Log($"房间序列: {currentRoomSequence}");
        Debug.Log($"Bug统计: {GetCurrentRoomBugStats()}");
        Debug.Log($"激活Bug数量: {GetCurrentRoomActiveBugs().Count}");
        Debug.Log($"未激活Bug数量: {GetCurrentRoomInactiveBugs().Count}");

        // 只显示非null的bug对象
        var validBugs = currentRoomBugs.Where(bug => bug != null).ToList();

        if (validBugs.Count > 0)
        {
            Debug.Log("Bug列表:");
            for (int i = 0; i < validBugs.Count; i++)
            {
                var bug = validBugs[i];
                string status = bug.IsBugActive() ? "激活" : "未激活";
                if (bug.IsBeingFixed()) status = "修复中";
                Debug.Log($"  {i + 1}. {bug.name} ({bug.GetBugType()}) - {status}");
            }
        }
        else
        {
            Debug.Log("当前房间没有有效的Bug对象");
        }
    }

    [ContextMenu("强制刷新当前房间Bug信息")]
    public void ForceRefreshCurrentRoomBugInfo()
    {
        RefreshCurrentRoomBugInfo();
        ShowCurrentRoomBugInfo();
    }

    [ContextMenu("获取当前房间激活Bug")]
    public void ShowCurrentRoomActiveBugs()
    {
        var activeBugs = GetCurrentRoomActiveBugs();
        Debug.Log($"=== 当前房间激活Bug ({activeBugs.Count}个) ===");
        for (int i = 0; i < activeBugs.Count; i++)
        {
            var bug = activeBugs[i];
            Debug.Log($"  {i + 1}. {bug.name} ({bug.GetBugType()})");
        }
    }

    [ContextMenu("切换自动更新当前房间信息")]
    public void ToggleAutoUpdateCurrentRoomInfo()
    {
        autoUpdateCurrentRoomInfo = !autoUpdateCurrentRoomInfo;
        Debug.Log($"🔄 自动更新当前房间信息: {(autoUpdateCurrentRoomInfo ? "开启" : "关闭")}");
    }

    [ContextMenu("清理所有房间的销毁对象")]
    public void CleanupAllDestroyedBugs()
    {
        Debug.Log("🧹 开始清理所有房间中已销毁的Bug对象...");

        int totalCleaned = 0;
        foreach (var room in roomInstances)
        {
            int beforeCount = room.bugsInRoom.Count;
            room.CleanupDestroyedBugs();
            int afterCount = room.bugsInRoom.Count;
            int cleaned = beforeCount - afterCount;

            if (cleaned > 0)
            {
                totalCleaned += cleaned;
                Debug.Log($"房间序列{room.currentSequence}: 清理了{cleaned}个销毁的Bug对象");
            }
        }

        if (totalCleaned > 0)
        {
            Debug.Log($"✅ 清理完成，总共清理了{totalCleaned}个销毁的Bug对象");
            // 更新当前房间信息
            UpdateCurrentRoomBugInfo();
        }
        else
        {
            Debug.Log("✅ 没有发现需要清理的销毁对象");
        }
    }

    #region Unity生命周期

    void Start()
    {
        InitializeRoomSystem();
    }

    void Update()
    {
        // 只有完全初始化后才开始Update逻辑
        if (!isFullyInitialized || player == null)
        {
            // 如果房间已创建但玩家还没注册，检查是否超时
            if (roomsCreated && !playerRegistered)
            {
                CheckPlayerRegistrationTimeout();
            }
            return;
        }

        // 定期检测玩家位置
        if (Time.time - lastDetectionTime >= detectionInterval)
        {
            CheckPlayerRoomPosition();
            lastDetectionTime = Time.time;

            // 自动更新当前房间Bug信息
            if (autoUpdateCurrentRoomInfo)
            {
                UpdateCurrentRoomBugInfo();
            }

            // 每隔一段时间清理一次销毁的对象（避免频繁清理）
            if (Time.time % 5f < detectionInterval) // 每5秒清理一次
            {
                GetCurrentRoom()?.CleanupDestroyedBugs();
            }
        }
    }

    private void OnEnable()
    {
        // 订阅Bug修复事件
        BugFixerGame.BugObject.OnBugFixed += HandleBugFixed;
    }

    private void OnDisable()
    {
        // 取消订阅
        BugFixerGame.BugObject.OnBugFixed -= HandleBugFixed;
    }

    #endregion

    #region 调试GUI界面

    [Header("调试GUI")]
    [SerializeField] private bool showDebugGUI = false;

    private void OnGUI()
    {
        if (!showDebugGUI || !Application.isPlaying) return;

        GUILayout.BeginArea(new Rect(10, Screen.height - 500, 500, 490));
        GUILayout.Label("=== RoomSystem 调试信息 ===");
        GUILayout.Label($"房间数量: {roomInstances.Count}");
        GUILayout.Label($"当前序列: {currentRoomSequence}");
        GUILayout.Label($"检测间隔: {detectionInterval:F2}s");

        // 初始化状态
        GUILayout.Label($"房间已创建: {roomsCreated}");
        GUILayout.Label($"玩家已注册: {playerRegistered}");
        GUILayout.Label($"完全初始化: {isFullyInitialized}");

        // 玩家分配状态
        GUILayout.Label($"玩家引用: {(player != null ? player.name : "无")}");
        if (roomsCreated && !playerRegistered)
        {
            float waitTime = Time.time - roomCreationTime;
            GUILayout.Label($"等待玩家注册: {waitTime:F1}s / {maxWaitTimeForPlayer:F1}s");
        }

        GUILayout.Space(5);
        GUILayout.Label("=== 全局Bug统计 ===");
        GUILayout.Label($"总Bug数: {totalBugsInAllRooms}");
        GUILayout.Label($"已修复: {totalBugsFixed}");
        GUILayout.Label($"剩余: {GetRemainingBugCount()}");

        GUILayout.Space(5);
        GUILayout.Label("=== 当前房间Bug信息 ===");
        GUILayout.Label($"房间序列: {currentRoomSequence}");
        GUILayout.Label($"Bug统计: {GetCurrentRoomBugStats()}");
        GUILayout.Label($"激活Bug: {GetCurrentRoomActiveBugs().Count}个");
        GUILayout.Label($"总Bug: {currentRoomBugs.Count}个");
        GUILayout.Label($"自动更新: {(autoUpdateCurrentRoomInfo ? "开启" : "关闭")}");

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("刷新当前房间"))
        {
            RefreshCurrentRoomBugInfo();
        }
        if (GUILayout.Button("显示详情"))
        {
            ShowCurrentRoomBugInfo();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("显示激活Bug"))
        {
            ShowCurrentRoomActiveBugs();
        }
        if (GUILayout.Button("切换自动更新"))
        {
            ToggleAutoUpdateCurrentRoomInfo();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("测试环形移动"))
        {
            TestRingMovement();
        }
        if (GUILayout.Button("重扫描Bug"))
        {
            RescanAllBugs();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("清理销毁对象"))
        {
            CleanupAllDestroyedBugs();
        }
        if (GUILayout.Button("后备查找玩家"))
        {
            FindPlayerAsFallback();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("强制完成初始化"))
        {
            if (roomsCreated && playerRegistered && !isFullyInitialized)
            {
                CompleteFullInitialization();
            }
        }
        if (GUILayout.Button("显示系统状态"))
        {
            ShowSystemStatus();
        }
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    #endregion

    #region 房间系统初始化

    /// <summary>
    /// 初始化房间系统 - 第一阶段：创建房间
    /// </summary>
    void InitializeRoomSystem()
    {
        if (roomPrefabs.Count == 0)
        {
            Debug.LogError("❌ 房间预制体列表为空！");
            return;
        }

        Debug.Log("🏠 开始房间系统初始化 - 第一阶段：创建房间");

        ClearAllRooms();

        // 第一阶段：创建房间（玩家会在这个过程中被实例化）
        CreateInitialRooms();

        roomsCreated = true;
        roomCreationTime = Time.time;

        // 触发房间创建完成事件
        OnRoomsCreated?.Invoke();

        Debug.Log($"✅ 房间创建完成，已创建 {roomInstances.Count} 个房间");
        Debug.Log("⏳ 等待玩家注册...");

        if (enablePlayerWaitingLog)
        {
            StartCoroutine(LogPlayerWaitingStatus());
        }
    }

    /// <summary>
    /// 创建初始房间（以默认位置为中心）
    /// </summary>
    void CreateInitialRooms()
    {
        if (roomPrefabs.Count == 0)
        {
            Debug.LogError("房间预制体列表为空！");
            return;
        }

        ClearAllRooms();

        int roomCount = Mathf.Min(visibleRoomCount, roomPrefabs.Count);

        int centerSequence = Mathf.RoundToInt(initialRoomCenter.x / roomSpacing);
        int startSequence = centerSequence - playerCenterPosition;

        if (enableDebugLog)
            Debug.Log($"创建房间范围: {startSequence} 到 {startSequence + roomCount - 1} (中心序列: {centerSequence})");

        for (int i = 0; i < roomCount; i++)
        {
            int sequenceNumber = startSequence + i;

            // 不再循环取模，确保每个Prefab只创建一次
            CreateRoomAtSequence(sequenceNumber, i);
        }

        currentRoomSequence = centerSequence;
        lastProcessedSequence = centerSequence;
    }

    void CreateRoomAtSequence(int sequenceNumber, int prefabIndex)
    {
        float worldPos = sequenceNumber * roomSpacing;

        GameObject roomPrefab = roomPrefabs[prefabIndex];
        if (roomPrefab == null)
        {
            Debug.LogError($"房间预制体 {prefabIndex} 为空！");
            return;
        }

        GameObject roomInstance = Instantiate(roomPrefab, transform);
        roomInstance.name = $"Room_Seq{sequenceNumber}_Type{prefabIndex + 1}";
        roomInstance.transform.position = new Vector3(worldPos, 0, 0);

        RoomInstance newRoom = new RoomInstance(roomInstance, sequenceNumber, prefabIndex, worldPos);
        roomInstances.Add(newRoom);

        if (enableDebugLog)
            Debug.Log($"🏗️ 创建房间: 序列{sequenceNumber}, 位置({worldPos:F1}, 0, 0), 类型{prefabIndex + 1}");
    }


    /// <summary>
    /// 完成完全初始化 - 第二阶段：玩家注册后的逻辑
    /// </summary>
    void CompleteFullInitialization()
    {
        if (!roomsCreated || !playerRegistered)
        {
            Debug.LogWarning("⚠️ 无法完成完全初始化：房间未创建或玩家未注册");
            return;
        }

        Debug.Log("🚀 开始房间系统完全初始化 - 第二阶段：玩家逻辑");

        // 根据玩家位置确定当前房间序列
        int playerSequence = GetPlayerSequenceFromPosition();
        currentRoomSequence = playerSequence;
        lastProcessedSequence = playerSequence;

        Debug.Log($"🎯 玩家位置: {player.position.ToString("F2")}, 确定当前序列: {playerSequence}");

        // 扫描所有房间的bug并统计
        ScanAllRoomsForBugs();

        // 初始化当前房间Bug信息
        UpdateCurrentRoomBugInfo();

        isFullyInitialized = true;

        // 触发完全初始化完成事件
        OnSystemFullyInitialized?.Invoke();

        if (enableDebugLog)
        {
            Debug.Log($"✅ 房间系统完全初始化完成！");
            Debug.Log($"🎯 总共发现 {totalBugsInAllRooms} 个Bug需要修复");
        }

        LogCurrentRoomLayout("完全初始化完成");
    }

    /// <summary>
    /// 检查玩家注册超时
    /// </summary>
    void CheckPlayerRegistrationTimeout()
    {
        if (!roomsCreated || playerRegistered) return;

        float waitTime = Time.time - roomCreationTime;
        if (waitTime >= maxWaitTimeForPlayer)
        {
            Debug.LogWarning($"⚠️ 等待玩家注册超时 ({waitTime:F1}s)，尝试后备方案");
            FindPlayerAsFallback();
        }
    }

    /// <summary>
    /// 后备方案：主动查找玩家（原有的查找方式）
    /// </summary>
    void FindPlayerAsFallback()
    {
        if (playerRegistered)
        {
            Debug.Log("玩家已注册，无需后备查找");
            return;
        }

        Debug.Log("🔍 执行玩家后备查找...");

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            SetPlayer(playerObj.transform);
            Debug.Log($"✅ 后备查找成功找到玩家: {playerObj.name}");
        }
        else
        {
            Debug.LogError("❌ 后备查找失败，场景中没有找到Tag为'Player'的对象");
            Debug.LogError("请检查：1) 玩家预制体是否正确放置在房间中 2) 玩家对象是否有'Player'标签 3) 玩家脚本是否正确执行注册逻辑");
        }
    }

    /// <summary>
    /// 记录等待玩家状态的协程
    /// </summary>
    IEnumerator LogPlayerWaitingStatus()
    {
        while (roomsCreated && !playerRegistered)
        {
            float waitTime = Time.time - roomCreationTime;
            Debug.Log($"⏳ 等待玩家注册中... ({waitTime:F1}s / {maxWaitTimeForPlayer:F1}s)");
            yield return new WaitForSeconds(2f);
        }
    }

    int GetPlayerSequenceFromPosition()
    {
        if (player == null) return 0;

        // 直接根据玩家的X坐标计算序列号
        float playerX = player.position.x;
        int sequence = Mathf.RoundToInt(playerX / roomSpacing);

        Debug.Log($"玩家X坐标: {playerX:F2}, 房间间距: {roomSpacing}, 计算序列: {sequence}");
        return sequence;
    }

    void ClearAllRooms()
    {
        foreach (var room in roomInstances)
        {
            if (room.gameObject != null)
                DestroyImmediate(room.gameObject);
        }
        roomInstances.Clear();

        // 重置状态
        totalBugsInAllRooms = 0;
        totalBugsFixed = 0;
        roomsCreated = false;
        playerRegistered = false;
        isFullyInitialized = false;
    }

    #endregion

    #region Bug追踪系统

    /// <summary>
    /// 扫描所有房间中的bug并统计
    /// </summary>
    void ScanAllRoomsForBugs()
    {
        totalBugsInAllRooms = 0;
        totalBugsFixed = 0;

        foreach (var room in roomInstances)
        {
            room.ScanForBugObjects();
            totalBugsInAllRooms += room.originalBugCount;
            totalBugsFixed += room.fixedBugCount;
        }

        Debug.Log($"🔍 Bug扫描完成：总共{totalBugsInAllRooms}个Bug，已修复{totalBugsFixed}个");
    }

    /// <summary>
    /// 处理bug修复事件
    /// </summary>
    void HandleBugFixed(BugFixerGame.BugObject fixedBug)
    {
        Debug.Log($"🔧 收到Bug修复事件: {fixedBug.name}");

        // 找到包含这个bug的房间
        RoomInstance bugRoom = FindRoomContainingBug(fixedBug);

        if (bugRoom != null)
        {
            if (bugRoom.RemoveFixedBug(fixedBug))
            {
                totalBugsFixed++;
                Debug.Log($"🎯 全局Bug统计更新: {totalBugsFixed}/{totalBugsInAllRooms} ({(float)totalBugsFixed / totalBugsInAllRooms:P0})");

                // 检查是否所有bug都已修复
                CheckForGameComplete();
            }
        }
        else
        {
            Debug.LogWarning($"⚠️ 无法找到包含Bug {fixedBug.name} 的房间");
        }
    }

    /// <summary>
    /// 找到包含指定bug的房间
    /// </summary>
    RoomInstance FindRoomContainingBug(BugFixerGame.BugObject targetBug)
    {
        foreach (var room in roomInstances)
        {
            if (room.bugsInRoom.Contains(targetBug))
            {
                return room;
            }
        }
        return null;
    }

    /// <summary>
    /// 检查游戏是否完成（所有bug都修复了）
    /// </summary>
    void CheckForGameComplete()
    {
        if (totalBugsInAllRooms > 0 && totalBugsFixed >= totalBugsInAllRooms)
        {
            Debug.Log("🎉 所有Bug都已修复！游戏胜利！");
            OnAllBugsFixed?.Invoke();
        }
    }

    /// <summary>
    /// 获取全局bug统计信息
    /// </summary>
    public string GetGlobalBugStats()
    {
        if (totalBugsInAllRooms == 0)
            return "无Bug需要修复";

        float percentage = (float)totalBugsFixed / totalBugsInAllRooms;
        return $"已修复: {totalBugsFixed}/{totalBugsInAllRooms} ({percentage:P0})";
    }

    /// <summary>
    /// 检查是否还有未修复的bug
    /// </summary>
    public bool HasUnfixedBugs()
    {
        return totalBugsFixed < totalBugsInAllRooms;
    }

    /// <summary>
    /// 获取剩余bug数量
    /// </summary>
    public int GetRemainingBugCount()
    {
        return Mathf.Max(0, totalBugsInAllRooms - totalBugsFixed);
    }

    /// <summary>
    /// 更新当前房间Bug信息
    /// </summary>
    private void UpdateCurrentRoomBugInfo()
    {
        // 清空当前列表
        currentRoomBugs.Clear();
        currentRoomOriginalBugCount = 0;
        currentRoomFixedBugCount = 0;

        // 找到当前房间
        RoomInstance currentRoom = GetCurrentRoom();
        if (currentRoom != null)
        {
            // 先清理房间中已销毁的bug对象
            currentRoom.CleanupDestroyedBugs();

            // 只复制非null的Bug对象
            foreach (var bug in currentRoom.bugsInRoom)
            {
                if (bug != null)
                {
                    currentRoomBugs.Add(bug);
                }
            }

            currentRoomOriginalBugCount = currentRoom.originalBugCount;
            currentRoomFixedBugCount = currentRoom.fixedBugCount;
        }
    }

    /// <summary>
    /// 获取当前房间实例
    /// </summary>
    public RoomInstance GetCurrentRoom()
    {
        foreach (var room in roomInstances)
        {
            if (room.currentSequence == currentRoomSequence)
            {
                return room;
            }
        }
        return null;
    }

    /// <summary>
    /// 获取当前房间的BugObject列表（外部调用）
    /// </summary>
    public List<BugFixerGame.BugObject> GetCurrentRoomBugObjects()
    {
        // 返回过滤掉null对象的副本
        return currentRoomBugs.Where(bug => bug != null).ToList();
    }

    /// <summary>
    /// 获取当前房间激活的Bug列表
    /// </summary>
    public List<BugFixerGame.BugObject> GetCurrentRoomActiveBugs()
    {
        List<BugFixerGame.BugObject> activeBugs = new List<BugFixerGame.BugObject>();
        foreach (var bug in currentRoomBugs)
        {
            if (bug != null && bug.IsBugActive())
            {
                activeBugs.Add(bug);
            }
        }
        return activeBugs;
    }

    /// <summary>
    /// 获取当前房间未激活的Bug列表
    /// </summary>
    public List<BugFixerGame.BugObject> GetCurrentRoomInactiveBugs()
    {
        List<BugFixerGame.BugObject> inactiveBugs = new List<BugFixerGame.BugObject>();
        foreach (var bug in currentRoomBugs)
        {
            if (bug != null && !bug.IsBugActive())
            {
                inactiveBugs.Add(bug);
            }
        }
        return inactiveBugs;
    }

    /// <summary>
    /// 获取当前房间Bug统计信息
    /// </summary>
    public string GetCurrentRoomBugStats()
    {
        if (currentRoomOriginalBugCount == 0)
            return "当前房间无Bug";

        float percentage = currentRoomOriginalBugCount > 0 ? (float)currentRoomFixedBugCount / currentRoomOriginalBugCount : 0f;
        return $"当前房间: {currentRoomFixedBugCount}/{currentRoomOriginalBugCount} ({percentage:P0})";
    }

    /// <summary>
    /// 获取当前房间序列号
    /// </summary>
    public int GetCurrentRoomSequence()
    {
        return currentRoomSequence;
    }

    /// <summary>
    /// 强制刷新当前房间Bug信息
    /// </summary>
    public void RefreshCurrentRoomBugInfo()
    {
        UpdateCurrentRoomBugInfo();
        Debug.Log($"🔄 当前房间Bug信息已刷新: {GetCurrentRoomBugStats()}");
    }

    /// <summary>
    /// 检查当前房间是否还有未修复的Bug
    /// </summary>
    public bool CurrentRoomHasUnfixedBugs()
    {
        return GetCurrentRoomActiveBugs().Count > 0;
    }

    /// <summary>
    /// 获取指定序列房间的Bug信息
    /// </summary>
    public List<BugFixerGame.BugObject> GetRoomBugObjects(int roomSequence)
    {
        foreach (var room in roomInstances)
        {
            if (room.currentSequence == roomSequence)
            {
                // 清理并返回有效的bug对象
                room.CleanupDestroyedBugs();
                return room.bugsInRoom.Where(bug => bug != null).ToList();
            }
        }
        return new List<BugFixerGame.BugObject>();
    }

    #endregion

    #region 玩家位置检测

    void CheckPlayerRoomPosition()
    {
        if (player == null) return;

        // 找到距离玩家最近的房间
        RoomInstance closestRoom = null;
        float closestDistance = float.MaxValue;

        foreach (var room in roomInstances)
        {
            if (room.gameObject != null)
            {
                float distance = room.GetDistanceToPlayer(player.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestRoom = room;
                }
            }
        }

        if (closestRoom != null && closestRoom.currentSequence != currentRoomSequence)
        {
            // 玩家进入了新房间
            OnPlayerEnterRoom(closestRoom.currentSequence);
        }
    }

    public void OnPlayerEnterRoom(int sequenceNumber)
    {
        // 防止重复处理相同序列
        if (sequenceNumber == lastProcessedSequence)
        {
            return;
        }

        int previousSequence = currentRoomSequence;
        currentRoomSequence = sequenceNumber;

        // 计算移动方向
        int direction = sequenceNumber - previousSequence;

        Debug.Log($"🎯 玩家从序列 {previousSequence} 移动到序列 {sequenceNumber}，方向：{(direction > 0 ? "右" : "左")}");

        // 执行环形房间移动
        if (direction > 0)
        {
            // 向右移动：将最左边的房间移动到最右边
            HandleMovementRight(sequenceNumber);
        }
        else if (direction < 0)
        {
            // 向左移动：将最右边的房间移动到最左边
            HandleMovementLeft(sequenceNumber);
        }

        lastProcessedSequence = sequenceNumber;
        LogCurrentRoomLayout($"玩家移动到序列 {sequenceNumber} 后");

        // 更新当前房间Bug信息
        UpdateCurrentRoomBugInfo();
    }

    void HandleMovementRight(int currentSeq)
    {
        // 玩家向右移动，检查是否需要将左边房间移到右边
        RoomInstance leftmostRoom = GetLeftmostRoom();
        RoomInstance rightmostRoom = GetRightmostRoom();

        if (leftmostRoom == null || rightmostRoom == null) return;

        // 检查玩家是否接近右边界，需要移动房间
        int distanceToRightEdge = rightmostRoom.currentSequence - currentSeq;

        if (distanceToRightEdge <= 2) // 距离右边界2个房间时移动
        {
            MoveLeftmostRoomToRight();
            Debug.Log($"🔄 向右移动：将最左房间(序列{leftmostRoom.currentSequence})移动到最右边");
        }
    }

    void HandleMovementLeft(int currentSeq)
    {
        // 玩家向左移动，检查是否需要将右边房间移到左边
        RoomInstance leftmostRoom = GetLeftmostRoom();
        RoomInstance rightmostRoom = GetRightmostRoom();

        if (leftmostRoom == null || rightmostRoom == null) return;

        // 检查玩家是否接近左边界，需要移动房间
        int distanceToLeftEdge = currentSeq - leftmostRoom.currentSequence;

        if (distanceToLeftEdge <= 2) // 距离左边界2个房间时移动
        {
            MoveRightmostRoomToLeft();
            Debug.Log($"🔄 向左移动：将最右房间(序列{rightmostRoom.currentSequence})移动到最左边");
        }
    }

    void MoveLeftmostRoomToRight()
    {
        RoomInstance leftmostRoom = GetLeftmostRoom();
        RoomInstance rightmostRoom = GetRightmostRoom();

        if (leftmostRoom == null || rightmostRoom == null) return;

        // 计算新位置：最右边房间的下一个位置
        int newSequence = rightmostRoom.currentSequence + 1;
        float newWorldPos = newSequence * roomSpacing;

        Debug.Log($"环形移动：序列{leftmostRoom.currentSequence}(位置{leftmostRoom.worldPosition:F1}) → 序列{newSequence}(位置{newWorldPos:F1})");

        // 更新房间位置和数据
        leftmostRoom.UpdatePosition(newSequence, newWorldPos, roomPrefabs);
        UpdateRoomAppearance(leftmostRoom);

        // 重新扫描移动后房间的bug（因为可能是新的房间类型）
        leftmostRoom.ScanForBugObjects();
    }

    void MoveRightmostRoomToLeft()
    {
        RoomInstance leftmostRoom = GetLeftmostRoom();
        RoomInstance rightmostRoom = GetRightmostRoom();

        if (leftmostRoom == null || rightmostRoom == null) return;

        // 计算新位置：最左边房间的前一个位置
        int newSequence = leftmostRoom.currentSequence - 1;
        float newWorldPos = newSequence * roomSpacing;

        Debug.Log($"环形移动：序列{rightmostRoom.currentSequence}(位置{rightmostRoom.worldPosition:F1}) → 序列{newSequence}(位置{newWorldPos:F1})");

        // 更新房间位置和数据
        rightmostRoom.UpdatePosition(newSequence, newWorldPos, roomPrefabs);
        UpdateRoomAppearance(rightmostRoom);

        // 重新扫描移动后房间的bug
        rightmostRoom.ScanForBugObjects();
    }

    void UpdateRoomAppearance(RoomInstance room)
    {
        // 保持房间原有外观，不修改颜色
        // 如果需要其他外观更新（如激活/禁用某些组件），可以在这里添加

        // 例如：更新房间名称以反映新的序列号
        if (room.gameObject != null)
        {
            room.gameObject.name = $"Room_Seq{room.currentSequence}_Type{room.roomTypeIndex + 1}";
        }
    }

    RoomInstance GetLeftmostRoom()
    {
        if (roomInstances.Count == 0) return null;

        RoomInstance leftmost = roomInstances[0];
        foreach (var room in roomInstances)
        {
            if (room.currentSequence < leftmost.currentSequence)
                leftmost = room;
        }
        return leftmost;
    }

    RoomInstance GetRightmostRoom()
    {
        if (roomInstances.Count == 0) return null;

        RoomInstance rightmost = roomInstances[0];
        foreach (var room in roomInstances)
        {
            if (room.currentSequence > rightmost.currentSequence)
                rightmost = room;
        }
        return rightmost;
    }

    #endregion

    #region 调试和日志

    void LogCurrentRoomLayout(string context)
    {
        var sortedRooms = new List<RoomInstance>(roomInstances);
        sortedRooms.Sort((a, b) => a.currentSequence.CompareTo(b.currentSequence));

        string layout = "";
        for (int i = 0; i < sortedRooms.Count; i++)
        {
            var room = sortedRooms[i];
            string roomLabel = $"S{room.currentSequence}T{room.roomTypeIndex + 1}";

            if (showBugTrackingInfo)
            {
                roomLabel += $"[{room.GetBugStatusInfo()}]";
            }

            if (room.currentSequence == currentRoomSequence)
            {
                layout += $"[{roomLabel}] ";
            }
            else
            {
                layout += $"{roomLabel} ";
            }
        }

        Debug.Log($"📍 {context}");
        Debug.Log($"房间布局: {layout}");
        Debug.Log($"玩家在序列 {currentRoomSequence}");

        if (showBugTrackingInfo)
        {
            Debug.Log($"🎯 全局Bug状态: {GetGlobalBugStats()}");
        }
    }

    void ShowSystemStatus()
    {
        Debug.Log("=== 房间系统状态报告 ===");
        Debug.Log($"房间已创建: {roomsCreated}");
        Debug.Log($"玩家已注册: {playerRegistered}");
        Debug.Log($"完全初始化: {isFullyInitialized}");
        Debug.Log($"房间数量: {roomInstances.Count}");
        Debug.Log($"当前序列: {currentRoomSequence}");

        if (roomsCreated && !playerRegistered)
        {
            float waitTime = Time.time - roomCreationTime;
            Debug.Log($"等待玩家注册时间: {waitTime:F1}s / {maxWaitTimeForPlayer:F1}s");
        }

        if (player != null)
        {
            Debug.Log($"玩家引用: {player.name}");
            Debug.Log($"玩家位置: {player.position}");
        }

        Debug.Log($"全局Bug统计: {GetGlobalBugStats()}");
        Debug.Log($"当前房间Bug: {GetCurrentRoomBugStats()}");
    }

    #endregion

    #region 编辑器方法

    [ContextMenu("重新初始化房间系统")]
    public void EditorInitializeRooms()
    {
        InitializeRoomSystem();
    }

    [ContextMenu("强制检测玩家位置")]
    public void ForceCheckPlayerPosition()
    {
        if (!isFullyInitialized)
        {
            Debug.LogWarning("系统未完全初始化，无法检测玩家位置");
            return;
        }

        if (player == null)
        {
            Debug.LogWarning("没有玩家引用");
            return;
        }

        Debug.Log("=== 强制检测玩家位置 ===");
        Debug.Log($"玩家当前世界坐标: {player.position}");

        // 找到最近的房间
        RoomInstance closestRoom = null;
        float closestDistance = float.MaxValue;

        foreach (var room in roomInstances)
        {
            if (room.gameObject != null)
            {
                float distance = room.GetDistanceToPlayer(player.position);
                Debug.Log($"房间序列{room.currentSequence}: 位置{room.gameObject.transform.position:F1}, 距离{distance:F2}, Bug状态{room.GetBugStatusInfo()}");

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestRoom = room;
                }
            }
        }

        if (closestRoom != null)
        {
            Debug.Log($"✓ 最近的房间: 序列{closestRoom.currentSequence}, 距离{closestDistance:F2}");

            if (closestRoom.currentSequence != currentRoomSequence)
            {
                Debug.Log($"⚠️ 位置不匹配！当前记录序列: {currentRoomSequence}, 最近房间序列: {closestRoom.currentSequence}");
                currentRoomSequence = closestRoom.currentSequence;
                lastProcessedSequence = closestRoom.currentSequence;
                Debug.Log($"✓ 已更正玩家位置为序列 {closestRoom.currentSequence}");
            }
            else
            {
                Debug.Log($"✅ 玩家位置正确，在序列 {closestRoom.currentSequence}");
            }
        }

        LogCurrentRoomLayout("检测后的状态");
    }

    [ContextMenu("重新扫描所有房间的Bug")]
    public void RescanAllBugs()
    {
        Debug.Log("🔍 开始重新扫描所有房间的Bug...");
        ScanAllRoomsForBugs();
        LogCurrentRoomLayout("Bug重新扫描完成");
    }

    [ContextMenu("显示详细Bug信息")]
    public void ShowDetailedBugInfo()
    {
        Debug.Log("=== 详细Bug追踪信息 ===");
        Debug.Log($"全局统计: {GetGlobalBugStats()}");
        Debug.Log($"剩余Bug数量: {GetRemainingBugCount()}");
        Debug.Log($"是否还有未修复Bug: {HasUnfixedBugs()}");

        foreach (var room in roomInstances)
        {
            // 清理已销毁的bug对象
            room.CleanupDestroyedBugs();

            var validBugs = room.bugsInRoom.Where(bug => bug != null).ToList();

            Debug.Log($"房间序列{room.currentSequence}: {room.GetBugStatusInfo()} - 有效Bug数量: {validBugs.Count}");

            if (validBugs.Count > 0)
            {
                Debug.Log("Bug列表:");
                for (int i = 0; i < validBugs.Count; i++)
                {
                    var bug = validBugs[i];
                    string status = bug.IsBugActive() ? "激活" : "未激活";
                    if (bug.IsBeingFixed()) status = "修复中";
                    Debug.Log($"  {i + 1}. {bug.name} ({bug.GetBugType()}) - {status}");
                }
            }
            else
            {
                Debug.Log("  该房间没有有效的Bug对象");
            }
        }
    }

    [ContextMenu("测试游戏完成")]
    public void TestGameComplete()
    {
        Debug.Log("🧪 模拟所有Bug修复完成...");
        totalBugsFixed = totalBugsInAllRooms;
        CheckForGameComplete();
    }

    [ContextMenu("验证房间位置")]
    public void VerifyRoomPositions()
    {
        Debug.Log("=== 验证房间位置 ===");
        if (player != null)
        {
            Debug.Log($"玩家当前位置: {player.position}");
            float playerX = player.position.x;
            int expectedSequence = Mathf.RoundToInt(playerX / roomSpacing);
            Debug.Log($"根据位置计算的期望序列: {expectedSequence}");
            Debug.Log($"当前记录的序列: {currentRoomSequence}");
            Debug.Log($"匹配状态: {(expectedSequence == currentRoomSequence ? "✅ 匹配" : "❌ 不匹配")}");
            Debug.Log("---");
        }

        foreach (var room in roomInstances)
        {
            if (room.gameObject != null)
            {
                float expectedPos = room.currentSequence * roomSpacing;
                float actualPos = room.gameObject.transform.position.x;
                bool isCorrect = Mathf.Abs(expectedPos - actualPos) < 0.01f;

                Debug.Log($"序列{room.currentSequence}: 期望位置{expectedPos:F1}, 实际位置{actualPos:F1} {(isCorrect ? "✓" : "❌")}");
            }
        }
    }

    [ContextMenu("显示距离信息")]
    public void ShowDistanceInfo()
    {
        if (player == null)
        {
            Debug.LogWarning("找不到玩家对象");
            return;
        }

        Debug.Log("=== 房间距离信息 ===");
        Debug.Log($"玩家位置: {player.position}");

        var sortedByDistance = new List<RoomInstance>(roomInstances);
        sortedByDistance.Sort((a, b) => a.GetDistanceToPlayer(player.position).CompareTo(b.GetDistanceToPlayer(player.position)));

        for (int i = 0; i < sortedByDistance.Count; i++)
        {
            var room = sortedByDistance[i];
            if (room.gameObject != null)
            {
                float distance = room.GetDistanceToPlayer(player.position);
                string marker = i == 0 ? "🎯" : "  ";
                Debug.Log($"{marker} 排名{i + 1}: 序列{room.currentSequence}, 位置{room.gameObject.transform.position:F1}, 距离{distance:F2}");
            }
        }
    }

    [ContextMenu("模拟向右移动")]
    public void TestMoveRight()
    {
        Debug.Log("🧪 模拟玩家向右移动");
        OnPlayerEnterRoom(currentRoomSequence + 1);
    }

    [ContextMenu("模拟向左移动")]
    public void TestMoveLeft()
    {
        Debug.Log("🧪 模拟玩家向左移动");
        OnPlayerEnterRoom(currentRoomSequence - 1);
    }

    [ContextMenu("显示当前布局")]
    public void LogCurrentLayout()
    {
        LogCurrentRoomLayout("手动查看当前布局");
    }

    [ContextMenu("显示边界房间信息")]
    public void ShowBoundaryRooms()
    {
        Debug.Log("=== 边界房间信息 ===");

        var leftmost = GetLeftmostRoom();
        var rightmost = GetRightmostRoom();

        if (leftmost != null)
        {
            Debug.Log($"最左房间: 序列{leftmost.currentSequence}, 位置{leftmost.worldPosition:F1}");
        }
        else
        {
            Debug.Log("找不到最左房间");
        }

        if (rightmost != null)
        {
            Debug.Log($"最右房间: 序列{rightmost.currentSequence}, 位置{rightmost.worldPosition:F1}");
        }
        else
        {
            Debug.Log("找不到最右房间");
        }

        if (leftmost != null && rightmost != null)
        {
            int totalSpan = rightmost.currentSequence - leftmost.currentSequence + 1;
            Debug.Log($"房间跨度: {totalSpan} (序列{leftmost.currentSequence}到{rightmost.currentSequence})");
        }
    }

    [ContextMenu("测试环形移动")]
    public void TestRingMovement()
    {
        if (!isFullyInitialized)
        {
            Debug.LogWarning("房间系统未完全初始化");
            return;
        }

        Debug.Log("=== 🧪 测试环形移动功能 ===");

        // 记录移动前状态
        Debug.Log("移动前状态:");
        LogCurrentRoomLayout("测试前");
        ShowBoundaryRooms();

        // 模拟向右移动
        Debug.Log("\n🚀 模拟向右移动，应该将最左房间移到最右...");
        int newSequence = currentRoomSequence + 3; // 移动3步，确保触发房间移动
        OnPlayerEnterRoom(newSequence);

        Debug.Log("移动后状态:");
        LogCurrentRoomLayout("测试后");
        ShowBoundaryRooms();

        Debug.Log("=== 环形移动测试完成 ===");
    }

    [ContextMenu("显示系统状态")]
    public void ShowSystemStatusDebug()
    {
        ShowSystemStatus();
    }

    #endregion

    #region Gizmos绘制

    void OnDrawGizmos()
    {
        if (!showDebugLines) return;

        // 显示系统状态
        Gizmos.color = roomsCreated ? Color.green : Color.yellow;
        if (transform.position != Vector3.zero)
        {
            Gizmos.DrawWireSphere(transform.position, roomsCreated ? 1f : 2f);
        }

        if (!roomsCreated) return;

        Gizmos.color = debugLineColor;

        foreach (var room in roomInstances)
        {
            if (room.gameObject != null)
            {
                Vector3 center = room.gameObject.transform.position;
                DrawRoomBounds(center, roomSize);

                // 绘制序列号和Bug信息
#if UNITY_EDITOR
                Vector3 labelPos = center + Vector3.up * (roomSize.y * 0.5f + 2f);
                string label = $"Seq{room.currentSequence}\nPos{center.x:F1}";

                if (showBugTrackingInfo)
                {
                    label += $"\nBugs:{room.GetBugStatusInfo()}";
                }

                if (player != null)
                {
                    float distance = room.GetDistanceToPlayer(player.position);
                    label += $"\nDist{distance:F1}";
                }

                UnityEditor.Handles.Label(labelPos, label);
#endif

                if (room.currentSequence == currentRoomSequence)
                {
                    Gizmos.color = Color.yellow;
                    DrawRoomBounds(center, roomSize * 1.1f);
                    Gizmos.color = debugLineColor;
                }
            }
        }

        // 绘制玩家位置
        if (player != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(player.position, 1f);
        }
    }

    void DrawRoomBounds(Vector3 center, Vector3 size)
    {
        Vector3 halfSize = size * 0.5f;

        // 绘制立方体框架
        Vector3[] corners = new Vector3[8];
        corners[0] = center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);
        corners[1] = center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z);
        corners[2] = center + new Vector3(halfSize.x, -halfSize.y, halfSize.z);
        corners[3] = center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z);
        corners[4] = center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z);
        corners[5] = center + new Vector3(halfSize.x, halfSize.y, -halfSize.z);
        corners[6] = center + new Vector3(halfSize.x, halfSize.y, halfSize.z);
        corners[7] = center + new Vector3(-halfSize.x, halfSize.y, halfSize.z);

        // 底面
        Gizmos.DrawLine(corners[0], corners[1]);
        Gizmos.DrawLine(corners[1], corners[2]);
        Gizmos.DrawLine(corners[2], corners[3]);
        Gizmos.DrawLine(corners[3], corners[0]);

        // 顶面
        Gizmos.DrawLine(corners[4], corners[5]);
        Gizmos.DrawLine(corners[5], corners[6]);
        Gizmos.DrawLine(corners[6], corners[7]);
        Gizmos.DrawLine(corners[7], corners[4]);

        // 竖直边
        Gizmos.DrawLine(corners[0], corners[4]);
        Gizmos.DrawLine(corners[1], corners[5]);
        Gizmos.DrawLine(corners[2], corners[6]);
        Gizmos.DrawLine(corners[3], corners[7]);
    }

    #endregion
}