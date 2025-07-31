using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BugFixerGame
{
    public class SimplifiedRoomManager : MonoBehaviour
    {
        [Header("房间容器")]
        [SerializeField] private Transform roomContainer;           // 房间容器
        [SerializeField] private Transform originalRoomParent;      // 原始房间父对象
        [SerializeField] private Transform currentRoomParent;       // 当前房间父对象

        [Header("房间配置")]
        [SerializeField] private RoomConfig[] roomConfigs;          // 房间配置数组

        // 当前房间状态
        private RoomConfig currentRoomConfig;
        private GameObject currentOriginalRoom;
        private GameObject currentDisplayRoom;
        private int currentRoomIndex = -1;

        // Bug对象管理
        private List<BugObject> currentBugObjects = new List<BugObject>();

        // 单例
        public static SimplifiedRoomManager Instance { get; private set; }

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
            SetupContainers();
            Debug.Log("SimplifiedRoomManager初始化完成");
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
            }
        }

        #endregion

        #region 房间加载

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

            // 加载当前房间并设置Bug状态
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

            // 确保原始房间的所有BugObject都是关闭状态
            SetAllBugObjectsInRoom(currentOriginalRoom, false);
        }

        private void LoadCurrentRoom(bool shouldShowBugVersion)
        {
            // 总是使用正常版本作为基础
            GameObject prefabToUse = currentRoomConfig.normalRoomPrefab;

            // 实例化当前房间
            currentDisplayRoom = Instantiate(prefabToUse, currentRoomParent);
            currentDisplayRoom.name = $"CurrentRoom_{currentRoomIndex}";
            currentDisplayRoom.SetActive(false); // 初始隐藏

            // 收集房间内的所有BugObject
            CollectBugObjects();

            // 根据需要激活Bug效果
            if (shouldShowBugVersion && currentRoomConfig.hasBug)
            {
                ActivateRoomBugs();
                Debug.Log($"激活房间Bug: {currentRoomConfig.bugType}");
            }
            else
            {
                DeactivateRoomBugs();
                Debug.Log("房间无Bug或显示正常版本");
            }
        }

        #endregion

        #region BugObject管理

        private void CollectBugObjects()
        {
            currentBugObjects.Clear();

            if (currentDisplayRoom != null)
            {
                BugObject[] bugObjects = currentDisplayRoom.GetComponentsInChildren<BugObject>();
                currentBugObjects.AddRange(bugObjects);

                Debug.Log($"找到 {currentBugObjects.Count} 个BugObject");
            }
        }

        private void ActivateRoomBugs()
        {
            foreach (BugObject bugObj in currentBugObjects)
            {
                // 只激活与当前房间Bug类型匹配的BugObject
                if (bugObj.GetBugType() == currentRoomConfig.bugType)
                {
                    bugObj.ActivateBug();
                }
            }
        }

        private void DeactivateRoomBugs()
        {
            foreach (BugObject bugObj in currentBugObjects)
            {
                bugObj.DeactivateBug();
            }
        }

        private void SetAllBugObjectsInRoom(GameObject room, bool active)
        {
            BugObject[] bugObjects = room.GetComponentsInChildren<BugObject>();
            foreach (BugObject bugObj in bugObjects)
            {
                if (active)
                    bugObj.ActivateBug();
                else
                    bugObj.DeactivateBug();
            }
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

            // 检查是否有激活的BugObject
            foreach (BugObject bugObj in currentBugObjects)
            {
                if (bugObj.IsBugActive())
                    return true;
            }

            return false;
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

            // 停用所有激活的BugObject
            DeactivateRoomBugs();

            Debug.Log("所有Bug已清除");
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

        public List<BugObject> GetCurrentBugObjects()
        {
            return new List<BugObject>(currentBugObjects);
        }

        #endregion

        #region 清理

        private void ClearCurrentRooms()
        {
            currentBugObjects.Clear();

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

            GUILayout.BeginArea(new Rect(10, 250, 500, 400));
            GUILayout.Label("=== Simplified Room Manager Debug ===");

            if (currentRoomConfig != null)
            {
                GUILayout.Label($"当前房间: {currentRoomIndex}");
                GUILayout.Label($"房间有Bug: {currentRoomConfig.hasBug}");
                GUILayout.Label($"Bug类型: {currentRoomConfig.bugType}");
                GUILayout.Label($"Bug描述: {currentRoomConfig.bugDescription}");
                GUILayout.Label($"BugObject数量: {currentBugObjects.Count}");

                // 显示当前激活的BugObject
                int activeBugs = 0;
                foreach (BugObject bugObj in currentBugObjects)
                {
                    if (bugObj.IsBugActive())
                        activeBugs++;
                }
                GUILayout.Label($"激活的Bug: {activeBugs}");

                GUILayout.Space(10);

                if (GUILayout.Button("显示原始房间"))
                {
                    ShowOriginalRoom();
                }

                if (GUILayout.Button("显示当前房间"))
                {
                    ShowCurrentRoom();
                }

                if (GUILayout.Button("激活所有Bug"))
                {
                    ActivateRoomBugs();
                }

                if (GUILayout.Button("清除所有Bug"))
                {
                    ClearCurrentRoomBugs();
                }

                GUILayout.Space(10);

                // 显示每个BugObject的状态
                if (currentBugObjects.Count > 0)
                {
                    GUILayout.Label("BugObject列表:");
                    foreach (BugObject bugObj in currentBugObjects)
                    {
                        string status = bugObj.IsBugActive() ? "ON" : "OFF";
                        GUILayout.Label($"- {bugObj.name}: {bugObj.GetBugType()} [{status}]");
                    }
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

        #endregion
    }
}