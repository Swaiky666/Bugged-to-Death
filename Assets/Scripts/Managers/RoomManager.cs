using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BugFixerGame
{
    // Bug类型枚举（简化版）
    public enum BugType
    {
        None,
        ObjectMissing,      // 物品缺失
        ObjectAdded,        // 额外物品
        ObjectMoved,        // 物品位置改变
        MaterialMissing,    // 材质丢失（紫色）
        CodeEffect,         // 代码特效乱飞
        ClippingBug,        // 穿模bug（床卡在地里）
        ExtraEyes          // 窗外多眼睛
    }

    // 房间配置数据（预设配置）
    [System.Serializable]
    public class RoomConfig
    {
        public int roomId;
        public GameObject normalRoomPrefab;     // 正常房间预设
        public GameObject buggyRoomPrefab;      // 有Bug的房间预设（可选）
        public bool hasBug;                     // 这个房间是否固定有Bug
        public BugType bugType;                 // 固定的Bug类型

        [TextArea(2, 4)]
        public string bugDescription;           // Bug描述（用于调试）
    }

    public class RoomManager : MonoBehaviour
    {
        [Header("房间容器")]
        [SerializeField] private Transform roomContainer;           // 房间容器
        [SerializeField] private Transform originalRoomParent;      // 原始房间父对象
        [SerializeField] private Transform currentRoomParent;       // 当前房间父对象

        [Header("房间配置")]
        [SerializeField] private RoomConfig[] roomConfigs;          // 房间配置数组

        [Header("特效预设")]
        [SerializeField] private Material missingTextureMaterial;   // 紫色材质
        [SerializeField] private GameObject codeEffectPrefab;       // 代码特效预设
        [SerializeField] private GameObject extraEyesPrefab;        // 额外眼睛预设

        // 当前房间状态
        private RoomConfig currentRoomConfig;
        private GameObject currentOriginalRoom;
        private GameObject currentDisplayRoom;
        private int currentRoomIndex = -1;

        // 单例
        public static RoomManager Instance { get; private set; }

        // 事件
        public static event Action<int> OnRoomGenerated;            // 房间生成完成
        public static event Action<bool, BugType> OnRoomLoaded;     // 房间加载完成 (hasBug, bugType)

        #region Unity生命周期

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeRoomManager();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            // 订阅GameManager事件
            GameManager.OnGameStateChanged += HandleGameStateChanged;
        }

        private void OnDisable()
        {
            // 取消订阅
            GameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        #endregion

        #region 初始化

        private void InitializeRoomManager()
        {
            // 初始化容器
            SetupContainers();

            Debug.Log("RoomManager初始化完成");
        }

        private void SetupContainers()
        {
            if (roomContainer == null)
            {
                GameObject container = new GameObject("RoomContainer");
                roomContainer = container.transform;
            }

            if (originalRoomParent == null)
            {
                GameObject original = new GameObject("OriginalRoom");
                original.transform.SetParent(roomContainer);
                originalRoomParent = original.transform;
            }

            if (currentRoomParent == null)
            {
                GameObject current = new GameObject("CurrentRoom");
                current.transform.SetParent(roomContainer);
                currentRoomParent = current.transform;
            }
        }

        #endregion

        #region GameManager事件处理

        private void HandleGameStateChanged(GameState newState)
        {
            switch (newState)
            {
                case GameState.MemoryPhase:
                    ShowOriginalRoom();
                    break;

                case GameState.TransitionToCheck:
                    HideAllRooms();
                    break;

                case GameState.CheckPhase:
                    ShowCurrentRoom();
                    break;

                case GameState.RoomResult:
                    // 保持当前显示，可以在这里高亮Bug位置
                    break;
            }
        }

        #endregion

        #region 房间生成和加载

        public void LoadRoom(int roomIndex, bool shouldShowBugVersion)
        {
            if (roomIndex < 0 || roomIndex >= roomConfigs.Length)
            {
                Debug.LogError($"房间索引超出范围: {roomIndex}");
                return;
            }

            currentRoomIndex = roomIndex;
            currentRoomConfig = roomConfigs[roomIndex];

            Debug.Log($"加载房间 {roomIndex}: {currentRoomConfig.bugDescription}");

            // 清理之前的房间
            ClearCurrentRooms();

            // 加载正常版本房间（用于记忆阶段）
            LoadOriginalRoom();

            // 加载当前房间（根据shouldShowBugVersion决定显示哪个版本）
            LoadCurrentRoom(shouldShowBugVersion);

            // 通知其他系统
            OnRoomGenerated?.Invoke(roomIndex);
            OnRoomLoaded?.Invoke(shouldShowBugVersion && currentRoomConfig.hasBug,
                               shouldShowBugVersion ? currentRoomConfig.bugType : BugType.None);
        }

        private void LoadOriginalRoom()
        {
            if (currentRoomConfig.normalRoomPrefab == null)
            {
                Debug.LogError($"房间 {currentRoomIndex} 缺少正常版本预设");
                return;
            }

            // 实例化正常房间
            currentOriginalRoom = Instantiate(currentRoomConfig.normalRoomPrefab, originalRoomParent);
            currentOriginalRoom.name = $"OriginalRoom_{currentRoomIndex}";
            currentOriginalRoom.SetActive(false); // 初始隐藏
        }

        private void LoadCurrentRoom(bool shouldShowBugVersion)
        {
            GameObject prefabToUse;

            // 决定使用哪个预设
            if (shouldShowBugVersion && currentRoomConfig.hasBug && currentRoomConfig.buggyRoomPrefab != null)
            {
                // 使用预制的Bug版本
                prefabToUse = currentRoomConfig.buggyRoomPrefab;
                Debug.Log($"使用Bug版本房间，Bug类型: {currentRoomConfig.bugType}");
            }
            else
            {
                // 使用正常版本
                prefabToUse = currentRoomConfig.normalRoomPrefab;
                Debug.Log("使用正常版本房间");
            }

            // 实例化当前房间
            currentDisplayRoom = Instantiate(prefabToUse, currentRoomParent);
            currentDisplayRoom.name = $"CurrentRoom_{currentRoomIndex}_{(shouldShowBugVersion ? "Bug" : "Normal")}";
            currentDisplayRoom.SetActive(false); // 初始隐藏
        }

        #endregion

        #region 房间显示控制

        private void ShowOriginalRoom()
        {
            HideAllRooms();

            if (currentOriginalRoom != null)
            {
                currentOriginalRoom.SetActive(true);
                Debug.Log("显示原始房间（记忆阶段）");
            }
        }

        private void ShowCurrentRoom()
        {
            HideAllRooms();

            if (currentDisplayRoom != null)
            {
                currentDisplayRoom.SetActive(true);
                Debug.Log("显示当前房间（检测阶段）");
            }
        }

        private void HideAllRooms()
        {
            if (currentOriginalRoom != null)
                currentOriginalRoom.SetActive(false);

            if (currentDisplayRoom != null)
                currentDisplayRoom.SetActive(false);
        }

        #endregion

        #region Bug检测和处理

        public bool CurrentRoomHasBug()
        {
            if (currentRoomConfig == null) return false;

            // 只有当前显示的是Bug版本，且配置确实有Bug时才返回true
            return currentRoomConfig.hasBug && IsShowingBugVersion();
        }

        private bool IsShowingBugVersion()
        {
            // 通过房间名称判断当前显示的是否是Bug版本
            return currentDisplayRoom != null && currentDisplayRoom.name.Contains("Bug");
        }

        public BugType GetCurrentBugType()
        {
            if (currentRoomConfig == null || !CurrentRoomHasBug())
                return BugType.None;

            return currentRoomConfig.bugType;
        }

        public void ClearCurrentRoomBugs()
        {
            if (!CurrentRoomHasBug()) return;

            Debug.Log($"清除房间Bug: {currentRoomConfig.bugType}");

            // 销毁当前Bug版本房间
            if (currentDisplayRoom != null)
            {
                DestroyImmediate(currentDisplayRoom);
            }

            // 重新加载正常版本
            currentDisplayRoom = Instantiate(currentRoomConfig.normalRoomPrefab, currentRoomParent);
            currentDisplayRoom.name = $"CurrentRoom_{currentRoomIndex}_Fixed";
            currentDisplayRoom.SetActive(true);

            Debug.Log("Bug已清除，显示正常版本房间");
        }

        #endregion

        #region 房间信息获取

        public int GetCurrentRoomIndex()
        {
            return currentRoomIndex;
        }

        public RoomConfig GetCurrentRoomConfig()
        {
            return currentRoomConfig;
        }

        public string GetCurrentRoomBugDescription()
        {
            return currentRoomConfig?.bugDescription ?? "无Bug";
        }

        public int GetTotalRoomCount()
        {
            return roomConfigs?.Length ?? 0;
        }

        #endregion

        #region 清理

        private void ClearCurrentRooms()
        {
            if (currentOriginalRoom != null)
            {
                DestroyImmediate(currentOriginalRoom);
                currentOriginalRoom = null;
            }

            if (currentDisplayRoom != null)
            {
                DestroyImmediate(currentDisplayRoom);
                currentDisplayRoom = null;
            }
        }

        #endregion

        #region 调试功能

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = true;

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 250, 500, 300));
            GUILayout.Label("=== Room Manager Debug ===");

            if (currentRoomConfig != null)
            {
                GUILayout.Label($"当前房间: {currentRoomIndex}");
                GUILayout.Label($"房间有Bug: {currentRoomConfig.hasBug}");
                GUILayout.Label($"Bug类型: {currentRoomConfig.bugType}");
                GUILayout.Label($"Bug描述: {currentRoomConfig.bugDescription}");
                GUILayout.Label($"当前显示Bug版本: {IsShowingBugVersion()}");

                GUILayout.Space(10);

                if (GUILayout.Button("显示原始房间"))
                {
                    ShowOriginalRoom();
                }

                if (GUILayout.Button("显示当前房间"))
                {
                    ShowCurrentRoom();
                }

                if (GUILayout.Button("清除Bug"))
                {
                    ClearCurrentRoomBugs();
                }

                GUILayout.Space(10);
                GUILayout.Label("快速测试房间:");
                GUILayout.BeginHorizontal();
                for (int i = 0; i < Mathf.Min(roomConfigs.Length, 5); i++)
                {
                    if (GUILayout.Button($"房间{i}"))
                    {
                        LoadRoom(i, roomConfigs[i].hasBug);
                    }
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("没有加载房间");

                if (roomConfigs != null && roomConfigs.Length > 0)
                {
                    if (GUILayout.Button("加载第一个房间"))
                    {
                        LoadRoom(0, roomConfigs[0].hasBug);
                    }
                }
            }

            GUILayout.EndArea();
        }

        // 验证房间配置的完整性
        [ContextMenu("验证房间配置")]
        private void ValidateRoomConfigs()
        {
            if (roomConfigs == null || roomConfigs.Length == 0)
            {
                Debug.LogWarning("没有配置任何房间！");
                return;
            }

            for (int i = 0; i < roomConfigs.Length; i++)
            {
                RoomConfig config = roomConfigs[i];

                if (config.normalRoomPrefab == null)
                {
                    Debug.LogError($"房间 {i} 缺少正常版本预设！");
                }

                if (config.hasBug && config.buggyRoomPrefab == null)
                {
                    Debug.LogWarning($"房间 {i} 标记有Bug但没有Bug版本预设，将使用正常版本！");
                }

                if (config.hasBug && string.IsNullOrEmpty(config.bugDescription))
                {
                    Debug.LogWarning($"房间 {i} 有Bug但没有描述！");
                }
            }

            Debug.Log($"房间配置验证完成，共 {roomConfigs.Length} 个房间");
        }

        #endregion
    }
}