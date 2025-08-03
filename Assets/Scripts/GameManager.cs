// GameManager.cs - 完整的蓝量系统、游戏结束条件和门系统
using System;
using System.Collections.Generic;
using UnityEngine;

// 自定义ReadOnly属性，用于在Inspector中显示只读字段
public class ReadOnlyAttribute : PropertyAttribute { }

#if UNITY_EDITOR
[UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : UnityEditor.PropertyDrawer
{
    public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false;
        UnityEditor.EditorGUI.PropertyField(position, property, label, true);
        GUI.enabled = true;
    }
}
#endif

namespace BugFixerGame
{
    public class GameManager : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private GameObject gamePrefab;
        [SerializeField] private GameObject mainMenuCamera;
        [SerializeField] private GameObject mainMenuUI;

        [Header("Mana Settings")]
        [SerializeField] private int maxMana = 5;
        [SerializeField] private int failurePenalty = 1;

        [Header("Current Mana Status (Runtime)")]
        [SerializeField, ReadOnly] private int currentMana = 5;  // 只读显示当前蓝量

        [Header("Game End Settings")]
        [SerializeField] private bool enableManaGameOver = true;        // 启用蓝量耗尽结束
        [SerializeField] private bool enableBugFixWin = true;           // 启用修复所有bug获胜
        [SerializeField] private float gameEndDelay = 2f;               // 游戏结束延迟时间

        [Header("Door System Settings")]
        [SerializeField] private bool enableDoorSystem = true;          // 启用门系统
        [SerializeField] private bool globalDoorState = false;          // 全局门状态（开启/关闭）
        [SerializeField] private bool showDoorDebugInfo = false;        // 显示门调试信息

        [Header("Current Game Status (Runtime)")]
        [SerializeField, ReadOnly] private bool gameEnded = false;      // 游戏是否已结束
        [SerializeField, ReadOnly] private string gameEndReason = "";   // 游戏结束原因

        [Header("Door System Status (Runtime)")]
        [SerializeField, ReadOnly] private int registeredDoorsCount = 0;    // 已注册门数量
        [SerializeField, ReadOnly] private int doorsNearPlayer = 0;         // 玩家附近的门数量
        [SerializeField, ReadOnly] private bool anyPlayerNearDoor = false;  // 是否有玩家在任何门附近

        private GameObject currentGameInstance;
        private bool isPaused = false;
        private RoomSystem roomSystem;

        // 门系统管理
        private List<Door> registeredDoors = new List<Door>();
        private bool lastPlayerNearDoorState = false;

        public static GameManager Instance { get; private set; }

        // 现有事件
        public static event Action<int, int> OnManaChanged; // (currentMana, maxMana)
        public static event Action<bool> OnPauseStateChanged;
        public static event Action OnGameOver; // 魔法值耗尽时触发 - Bad End

        // 新增事件
        public static event Action OnHappyEnd; // 所有bug修复完成时触发 - Happy End
        public static event Action<string> OnGameEnded; // 游戏结束事件（包含结束原因）
        public static event Action OnGameInstanceCreated; // 🆕 游戏实例创建完成事件

        // 门系统事件
        public static event Action<bool> OnGlobalDoorStateChanged; // 全局门状态改变事件 (isOpen)

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            Player.OnObjectDetectionComplete += HandleObjectDetection;
            RoomSystem.OnAllBugsFixed += HandleAllBugsFixed;
        }

        private void OnDisable()
        {
            Player.OnObjectDetectionComplete -= HandleObjectDetection;
            RoomSystem.OnAllBugsFixed -= HandleAllBugsFixed;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && !gameEnded)
            {
                TogglePause();
            }
        }

        public void StartGame()
        {
            // 先清理现有游戏实例
            CleanupCurrentGame();

            // 短暂延迟确保清理完成
            StartCoroutine(StartGameDelayed());

            //播放游戏开始音效并切换音乐
            AudioManager.Instance?.OnGameStart();
        }

        private System.Collections.IEnumerator StartGameDelayed()
        {
            // 等待一帧确保旧实例完全销毁
            yield return new WaitForEndOfFrame();

            if (mainMenuCamera) mainMenuCamera.SetActive(false);
            if (mainMenuUI) mainMenuUI.SetActive(false);

            if (gamePrefab)
            {
                currentGameInstance = Instantiate(gamePrefab);
                Debug.Log($"🎮 创建新的游戏实例: {currentGameInstance.name}");
            }

            // 重置游戏状态
            gameEnded = false;
            gameEndReason = "";

            // 重置魔法值
            currentMana = maxMana;
            OnManaChanged?.Invoke(currentMana, maxMana);

            // 查找房间系统
            FindRoomSystem();

            // 初始化门系统
            InitializeDoorSystem();

            // 🆕 等待额外一帧确保所有初始化完成
            yield return new WaitForEndOfFrame();

            Debug.Log($"游戏开始 - 魔法值: {currentMana}/{maxMana}");

            // 如果启用了bug修复获胜条件，显示相关信息
            if (enableBugFixWin && roomSystem != null)
            {
                Debug.Log($"🎯 获胜条件：修复所有房间中的Bug");
            }

            // 🆕 触发游戏实例创建完成事件
            OnGameInstanceCreated?.Invoke();
            Debug.Log("🎮 游戏实例创建完成事件已触发");
        }

        public void ReturnToMainMenu()
        {
            CleanupCurrentGame();

            if (mainMenuCamera) mainMenuCamera.SetActive(true);
            if (mainMenuUI) mainMenuUI.SetActive(true);

            // 重置游戏状态
            gameEnded = false;
            gameEndReason = "";
            roomSystem = null;

            // 清理门系统
            CleanupDoorSystem();

            // 取消暂停并通知 UIManager 切换到主菜单
            ResumeGame();
            UIManager.Instance?.ShowMainMenu();

            // 返回主菜单音乐
            AudioManager.Instance?.OnReturnToMainMenu();
        }

        /// <summary>
        /// 清理当前游戏实例
        /// </summary>
        public void CleanupCurrentGame()
        {
            if (currentGameInstance != null)
            {
                Debug.Log($"🧹 开始清理旧的游戏实例: {currentGameInstance.name}");

                // 获取实例中的所有Player组件，提前通知它们即将被销毁
                Player[] players = currentGameInstance.GetComponentsInChildren<Player>();
                foreach (var player in players)
                {
                    if (player != null)
                    {
                        Debug.Log($"🎮 通知Player组件即将销毁: {player.name}");
                        player.SetControlsEnabled(false); // 提前禁用控制
                    }
                }

                Destroy(currentGameInstance);
                currentGameInstance = null;
                Debug.Log("🧹 旧游戏实例清理完成");
            }
            else
            {
                Debug.Log("🧹 没有需要清理的游戏实例");
            }
        }

        private void FindRoomSystem()
        {
            roomSystem = FindObjectOfType<RoomSystem>();
            if (roomSystem == null)
            {
                Debug.LogWarning("⚠️ 未找到RoomSystem组件，无法启用Bug修复获胜条件");
                enableBugFixWin = false;
            }
            else
            {
                Debug.Log("✅ 找到RoomSystem，Bug修复获胜条件已启用");
            }
        }

        private void HandleObjectDetection(GameObject obj, bool isBug)
        {
            if (gameEnded) return; // 游戏已结束，不再处理

            if (isBug)
            {
                // 修复成功，不扣除魔法值
                Debug.Log($"✅ 修复成功！物体: {obj.name}，魔法值保持: {currentMana}/{maxMana}");
            }
            else
            {
                // 修复失败，扣除魔法值
                currentMana -= failurePenalty;
                currentMana = Mathf.Max(0, currentMana);

                Debug.Log($"❌ 修复失败！物体: {obj.name}，魔法值减少: -{failurePenalty}，当前: {currentMana}/{maxMana}");

                OnManaChanged?.Invoke(currentMana, maxMana);

                // 检查是否魔法值耗尽
                if (enableManaGameOver && currentMana <= 0)
                {
                    StartCoroutine(HandleGameOverDelayed());
                }
            }
        }

        private void HandleAllBugsFixed()
        {
            if (gameEnded) return; // 避免重复触发

            if (enableBugFixWin)
            {
                Debug.Log("🎉 所有Bug修复完成！触发Happy End！");
                StartCoroutine(HandleHappyEndDelayed());
            }
            else
            {
                Debug.Log("🎉 所有Bug修复完成！但Happy End条件未启用");
            }
        }

        private System.Collections.IEnumerator HandleGameOverDelayed()
        {
            if (gameEnded) yield break;

            gameEnded = true;
            gameEndReason = "魔法值耗尽";

            Debug.Log("💀 魔法值耗尽！准备显示Bad End...");

            // 等待延迟时间
            yield return new WaitForSeconds(gameEndDelay);

            // 触发Bad End事件
            OnGameOver?.Invoke();
            OnGameEnded?.Invoke("BadEnd");

            Debug.Log("💀 Bad End触发完成");
        }

        private System.Collections.IEnumerator HandleHappyEndDelayed()
        {
            if (gameEnded) yield break;

            gameEnded = true;
            gameEndReason = "所有Bug修复完成";

            Debug.Log("🎉 所有Bug修复完成！准备显示Happy End...");

            // 等待延迟时间
            yield return new WaitForSeconds(gameEndDelay);

            // 触发Happy End事件
            OnHappyEnd?.Invoke();
            OnGameEnded?.Invoke("HappyEnd");

            Debug.Log("🎉 Happy End触发完成");
        }

        public void RestoreMana(int amount)
        {
            if (gameEnded) return;

            currentMana = Mathf.Min(maxMana, currentMana + amount);
            OnManaChanged?.Invoke(currentMana, maxMana);
            Debug.Log($"🔮 魔法值恢复 +{amount}，当前: {currentMana}/{maxMana}");
        }

        public void TogglePause()
        {
            if (gameEnded) return;

            isPaused = !isPaused;
            Time.timeScale = isPaused ? 0 : 1;
            OnPauseStateChanged?.Invoke(isPaused);
        }

        public void ResumeGame()
        {
            isPaused = false;
            Time.timeScale = 1;
            OnPauseStateChanged?.Invoke(false);
        }

        // 公共接口
        public bool IsPaused() => isPaused;
        public bool IsGameEnded() => gameEnded;
        public string GetGameEndReason() => gameEndReason;
        public int GetCurrentMana() => currentMana;
        public int GetMaxMana() => maxMana;
        public float GetManaPercentage() => maxMana > 0 ? (float)currentMana / maxMana : 0f;
        public bool HasMana() => currentMana > 0;

        // 获取房间系统相关信息
        public bool HasRoomSystem() => roomSystem != null;
        public string GetBugStats()
        {
            if (roomSystem == null) return "无房间系统";
            return roomSystem.GetGlobalBugStats();
        }
        public int GetRemainingBugCount()
        {
            if (roomSystem == null) return 0;
            return roomSystem.GetRemainingBugCount();
        }
        public bool HasUnfixedBugs()
        {
            if (roomSystem == null) return false;
            return roomSystem.HasUnfixedBugs();
        }

        // 设置器（用于调试或特殊情况）
        public void SetMaxMana(int newMaxMana)
        {
            maxMana = Mathf.Max(1, newMaxMana);
            currentMana = Mathf.Min(currentMana, maxMana);
            OnManaChanged?.Invoke(currentMana, maxMana);
            Debug.Log($"🔧 最大魔法值设置为: {maxMana}");
        }

        public void SetFailurePenalty(int penalty)
        {
            failurePenalty = Mathf.Max(1, penalty);
            Debug.Log($"🔧 失败惩罚设置为: {failurePenalty}");
        }

        public void SetGameEndConditions(bool manaGameOver, bool bugFixWin)
        {
            enableManaGameOver = manaGameOver;
            enableBugFixWin = bugFixWin;
            Debug.Log($"🔧 游戏结束条件设置 - 蓝量耗尽: {manaGameOver}, Bug修复获胜: {bugFixWin}");
        }

        #region 门系统管理

        /// <summary>
        /// 初始化门系统
        /// </summary>
        private void InitializeDoorSystem()
        {
            if (!enableDoorSystem) return;

            // 清空现有门列表
            registeredDoors.Clear();

            // 重置门状态
            globalDoorState = false;
            anyPlayerNearDoor = false;
            doorsNearPlayer = 0;
            registeredDoorsCount = 0;
            lastPlayerNearDoorState = false;

            Debug.Log("🚪 门系统初始化完成");
        }

        /// <summary>
        /// 清理门系统
        /// </summary>
        private void CleanupDoorSystem()
        {
            if (!enableDoorSystem) return;

            // 清空门列表
            registeredDoors.Clear();

            // 重置状态
            globalDoorState = false;
            anyPlayerNearDoor = false;
            doorsNearPlayer = 0;
            registeredDoorsCount = 0;
            lastPlayerNearDoorState = false;

            Debug.Log("🚪 门系统已清理");
        }

        /// <summary>
        /// 注册门到管理器
        /// </summary>
        public void RegisterDoor(Door door)
        {
            if (!enableDoorSystem)
            {
                Debug.LogWarning("⚠️ 门系统未启用，无法注册门");
                return;
            }

            if (door == null)
            {
                Debug.LogWarning("⚠️ 尝试注册空门对象");
                return;
            }

            if (!registeredDoors.Contains(door))
            {
                registeredDoors.Add(door);
                registeredDoorsCount = registeredDoors.Count;

                // 设置门的初始状态
                door.SetDoorState(globalDoorState, false);

                Debug.Log($"✅ 门 {door.gameObject.name} 已注册到GameManager (总数: {registeredDoorsCount})");
            }
            else
            {
                Debug.LogWarning($"⚠️ 门 {door.gameObject.name} 已经注册过了");
            }
        }

        /// <summary>
        /// 从管理器注销门
        /// </summary>
        public void UnregisterDoor(Door door)
        {
            if (door == null) return;

            if (registeredDoors.Remove(door))
            {
                registeredDoorsCount = registeredDoors.Count;
                Debug.Log($"❌ 门 {door.gameObject.name} 已从GameManager注销 (剩余: {registeredDoorsCount})");

                // 重新计算玩家附近门的数量
                UpdatePlayerNearDoorCount();
            }
        }

        /// <summary>
        /// 更新全局门状态（由门对象调用）
        /// </summary>
        public void UpdateGlobalDoorState(bool playerNearDoor)
        {
            if (!enableDoorSystem) return;

            // 更新玩家附近门的计数
            UpdatePlayerNearDoorCount();

            // 检查是否需要改变全局门状态
            bool shouldDoorsBeOpen = anyPlayerNearDoor;

            if (shouldDoorsBeOpen != globalDoorState)
            {
                globalDoorState = shouldDoorsBeOpen;

                // 更新所有门的状态
                foreach (var door in registeredDoors)
                {
                    if (door != null)
                    {
                        door.SetDoorState(globalDoorState, true);
                    }
                }

                // 触发全局门状态改变事件
                OnGlobalDoorStateChanged?.Invoke(globalDoorState);

                if (showDoorDebugInfo)
                {
                    Debug.Log($"🚪 全局门状态改变: {(globalDoorState ? "全部开启" : "全部关闭")} " +
                             $"(玩家附近门数量: {doorsNearPlayer}/{registeredDoorsCount})");
                }
            }
        }

        /// <summary>
        /// 更新玩家附近门的数量
        /// </summary>
        private void UpdatePlayerNearDoorCount()
        {
            doorsNearPlayer = 0;

            foreach (var door in registeredDoors)
            {
                if (door != null && door.IsPlayerNearby())
                {
                    doorsNearPlayer++;
                }
            }

            bool newAnyPlayerNearDoor = doorsNearPlayer > 0;

            if (newAnyPlayerNearDoor != anyPlayerNearDoor)
            {
                anyPlayerNearDoor = newAnyPlayerNearDoor;

                if (showDoorDebugInfo)
                {
                    Debug.Log($"🎯 玩家门检测状态改变: {(anyPlayerNearDoor ? $"检测到{doorsNearPlayer}个门" : "没有门在附近")}");
                }
            }
        }

        /// <summary>
        /// 强制设置所有门的状态
        /// </summary>
        public void ForceSetAllDoorsState(bool open, bool animate = true)
        {
            if (!enableDoorSystem) return;

            globalDoorState = open;

            foreach (var door in registeredDoors)
            {
                if (door != null)
                {
                    door.SetDoorState(open, animate);
                }
            }

            OnGlobalDoorStateChanged?.Invoke(globalDoorState);

            Debug.Log($"🔧 强制设置所有门状态: {(open ? "开启" : "关闭")} (动画: {animate})");
        }

        /// <summary>
        /// 获取门系统信息
        /// </summary>
        public string GetDoorSystemInfo()
        {
            if (!enableDoorSystem) return "门系统未启用";

            return $"门系统: {registeredDoorsCount}个门已注册, " +
                   $"{doorsNearPlayer}个门附近有玩家, " +
                   $"全局状态: {(globalDoorState ? "开启" : "关闭")}";
        }

        /// <summary>
        /// 清理已销毁的门对象
        /// </summary>
        public void CleanupDestroyedDoors()
        {
            int removedCount = registeredDoors.RemoveAll(door => door == null);

            if (removedCount > 0)
            {
                registeredDoorsCount = registeredDoors.Count;
                Debug.Log($"🧹 清理了{removedCount}个已销毁的门对象，剩余: {registeredDoorsCount}");

                UpdatePlayerNearDoorCount();
            }
        }

        // 门系统公共接口
        public bool IsDoorSystemEnabled() => enableDoorSystem;
        public bool GetGlobalDoorState() => globalDoorState;
        public int GetRegisteredDoorsCount() => registeredDoorsCount;
        public int GetDoorsNearPlayerCount() => doorsNearPlayer;
        public bool IsAnyPlayerNearDoor() => anyPlayerNearDoor;
        public List<Door> GetRegisteredDoors() => new List<Door>(registeredDoors); // 返回副本

        /// <summary>
        /// 设置门系统启用状态
        /// </summary>
        public void SetDoorSystemEnabled(bool enabled)
        {
            enableDoorSystem = enabled;

            if (!enabled)
            {
                CleanupDoorSystem();
            }
            else
            {
                InitializeDoorSystem();
            }

            Debug.Log($"🔧 门系统{(enabled ? "启用" : "禁用")}");
        }

        #endregion

        #region 调试功能

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 10, 350, 300));
            GUILayout.Label("=== GameManager 调试 ===");
            GUILayout.Label($"当前魔法值: {currentMana}/{maxMana}");
            GUILayout.Label($"魔法值百分比: {GetManaPercentage():P0}");
            GUILayout.Label($"游戏暂停: {isPaused}");
            GUILayout.Label($"游戏结束: {gameEnded}");
            GUILayout.Label($"结束原因: {gameEndReason}");
            GUILayout.Label($"失败惩罚: {failurePenalty}");

            if (roomSystem != null)
            {
                GUILayout.Label($"Bug统计: {GetBugStats()}");
                GUILayout.Label($"剩余Bug: {GetRemainingBugCount()}");
            }

            // 门系统调试信息
            if (enableDoorSystem)
            {
                GUILayout.Label("--- 门系统 ---");
                GUILayout.Label($"已注册门: {registeredDoorsCount}");
                GUILayout.Label($"附近门数: {doorsNearPlayer}");
                GUILayout.Label($"全局状态: {(globalDoorState ? "开启" : "关闭")}");
            }

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("减少魔法值"))
            {
                HandleObjectDetection(gameObject, false);
            }
            if (GUILayout.Button("恢复魔法值"))
            {
                RestoreMana(1);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("触发Bad End"))
            {
                if (!gameEnded)
                {
                    currentMana = 0;
                    OnManaChanged?.Invoke(currentMana, maxMana);
                    StartCoroutine(HandleGameOverDelayed());
                }
            }
            if (GUILayout.Button("触发Happy End"))
            {
                if (!gameEnded && roomSystem != null)
                {
                    HandleAllBugsFixed();
                }
            }
            GUILayout.EndHorizontal();

            // 门系统测试按钮
            if (enableDoorSystem)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("强制开门"))
                {
                    ForceSetAllDoorsState(true, true);
                }
                if (GUILayout.Button("强制关门"))
                {
                    ForceSetAllDoorsState(false, true);
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("清理门列表"))
                {
                    CleanupDestroyedDoors();
                }
                if (GUILayout.Button("门系统信息"))
                {
                    Debug.Log(GetDoorSystemInfo());
                }
                GUILayout.EndHorizontal();
            }

            if (GUILayout.Button("重置游戏状态"))
            {
                gameEnded = false;
                gameEndReason = "";
                currentMana = maxMana;
                OnManaChanged?.Invoke(currentMana, maxMana);
            }

            GUILayout.EndArea();
        }

        [ContextMenu("测试减少魔法值")]
        private void TestDecreaseMana()
        {
            if (Application.isPlaying)
            {
                HandleObjectDetection(gameObject, false);
            }
        }

        [ContextMenu("测试恢复魔法值")]
        private void TestRestoreMana()
        {
            if (Application.isPlaying)
            {
                RestoreMana(1);
            }
        }

        [ContextMenu("重置魔法值")]
        private void TestResetMana()
        {
            if (Application.isPlaying)
            {
                currentMana = maxMana;
                OnManaChanged?.Invoke(currentMana, maxMana);
            }
        }

        [ContextMenu("测试Bad End")]
        private void TestBadEnd()
        {
            if (Application.isPlaying && !gameEnded)
            {
                currentMana = 0;
                OnManaChanged?.Invoke(currentMana, maxMana);
                StartCoroutine(HandleGameOverDelayed());
            }
        }

        [ContextMenu("测试Happy End")]
        private void TestHappyEnd()
        {
            if (Application.isPlaying && !gameEnded)
            {
                HandleAllBugsFixed();
            }
        }

        [ContextMenu("重置游戏状态")]
        private void TestResetGameState()
        {
            if (Application.isPlaying)
            {
                gameEnded = false;
                gameEndReason = "";
                currentMana = maxMana;
                OnManaChanged?.Invoke(currentMana, maxMana);
                Debug.Log("🔄 游戏状态已重置");
            }
        }

        [ContextMenu("显示游戏统计")]
        private void ShowGameStats()
        {
            Debug.Log("=== 游戏统计信息 ===");
            Debug.Log($"魔法值: {currentMana}/{maxMana} ({GetManaPercentage():P0})");
            Debug.Log($"游戏状态: {(gameEnded ? $"已结束 - {gameEndReason}" : "进行中")}");
            Debug.Log($"房间系统: {(roomSystem != null ? "已连接" : "未找到")}");

            if (roomSystem != null)
            {
                Debug.Log($"Bug状态: {GetBugStats()}");
                Debug.Log($"剩余Bug数量: {GetRemainingBugCount()}");
                Debug.Log($"是否还有未修复Bug: {HasUnfixedBugs()}");
            }

            Debug.Log($"游戏结束条件: 蓝量耗尽={enableManaGameOver}, Bug修复获胜={enableBugFixWin}");

            // 门系统统计
            if (enableDoorSystem)
            {
                Debug.Log($"门系统: {GetDoorSystemInfo()}");
            }
            else
            {
                Debug.Log("门系统: 未启用");
            }
        }

        // 门系统调试方法
        [ContextMenu("强制开启所有门")]
        private void TestOpenAllDoors()
        {
            if (Application.isPlaying && enableDoorSystem)
            {
                ForceSetAllDoorsState(true, true);
            }
        }

        [ContextMenu("强制关闭所有门")]
        private void TestCloseAllDoors()
        {
            if (Application.isPlaying && enableDoorSystem)
            {
                ForceSetAllDoorsState(false, true);
            }
        }

        [ContextMenu("显示门系统信息")]
        private void ShowDoorSystemInfo()
        {
            if (Application.isPlaying)
            {
                Debug.Log("=== 门系统信息 ===");
                Debug.Log(GetDoorSystemInfo());

                if (registeredDoors.Count > 0)
                {
                    Debug.Log("已注册的门:");
                    for (int i = 0; i < registeredDoors.Count; i++)
                    {
                        var door = registeredDoors[i];
                        if (door != null)
                        {
                            Debug.Log($"  {i + 1}. {door.gameObject.name} - " +
                                     $"状态: {(door.IsOpen() ? "开启" : "关闭")}, " +
                                     $"玩家距离: {door.GetPlayerDistance():F2}m, " +
                                     $"检测距离: {door.GetDetectionDistance():F2}m, " +
                                     $"玩家附近: {(door.IsPlayerNearby() ? "是" : "否")}");
                        }
                        else
                        {
                            Debug.Log($"  {i + 1}. [已销毁的门对象]");
                        }
                    }
                }
                else
                {
                    Debug.Log("没有已注册的门");
                }
            }
        }

        [ContextMenu("清理已销毁的门")]
        private void TestCleanupDoors()
        {
            if (Application.isPlaying)
            {
                CleanupDestroyedDoors();
            }
        }

        [ContextMenu("重新初始化门系统")]
        private void TestReinitializeDoorSystem()
        {
            if (Application.isPlaying)
            {
                CleanupDoorSystem();
                InitializeDoorSystem();
                Debug.Log("🔄 门系统已重新初始化");
            }
        }

        [ContextMenu("切换门系统启用状态")]
        private void TestToggleDoorSystem()
        {
            if (Application.isPlaying)
            {
                SetDoorSystemEnabled(!enableDoorSystem);
            }
        }

        #endregion
    }
}