// GameManager.cs - 完整的蓝量系统和游戏结束条件
using System;
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

        [Header("Current Game Status (Runtime)")]
        [SerializeField, ReadOnly] private bool gameEnded = false;      // 游戏是否已结束
        [SerializeField, ReadOnly] private string gameEndReason = "";   // 游戏结束原因

        private GameObject currentGameInstance;
        private bool isPaused = false;
        private RoomSystem roomSystem;

        public static GameManager Instance { get; private set; }

        // 现有事件
        public static event Action<int, int> OnManaChanged; // (currentMana, maxMana)
        public static event Action<bool> OnPauseStateChanged;
        public static event Action OnGameOver; // 魔法值耗尽时触发 - Bad End

        // 新增事件
        public static event Action OnHappyEnd; // 所有bug修复完成时触发 - Happy End
        public static event Action<string> OnGameEnded; // 游戏结束事件（包含结束原因）

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

            UIManager.Instance?.ShowHUD();

            Debug.Log($"游戏开始 - 魔法值: {currentMana}/{maxMana}");

            // 如果启用了bug修复获胜条件，显示相关信息
            if (enableBugFixWin && roomSystem != null)
            {
                Debug.Log($"🎯 获胜条件：修复所有房间中的Bug");
            }
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

            // 取消暂停并通知 UIManager 切换到主菜单
            ResumeGame();
            UIManager.Instance?.ShowMainMenu();
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

        #region 调试功能

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 10, 350, 200));
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
        }

        #endregion
    }
}