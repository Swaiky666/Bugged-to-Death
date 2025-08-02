// GameManager.cs - 完整的蓝量系统
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

        private GameObject currentGameInstance;
        private bool isPaused = false;

        public static GameManager Instance { get; private set; }

        public static event Action<int, int> OnManaChanged; // (currentMana, maxMana)
        public static event Action<bool> OnPauseStateChanged;
        public static event Action OnGameOver; // 魔法值耗尽时触发

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
        }

        private void OnDisable()
        {
            Player.OnObjectDetectionComplete -= HandleObjectDetection;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePause();
            }
        }

        public void StartGame()
        {
            if (mainMenuCamera) mainMenuCamera.SetActive(false);
            if (mainMenuUI) mainMenuUI.SetActive(false);

            if (gamePrefab)
            {
                currentGameInstance = Instantiate(gamePrefab);
            }

            // 重置魔法值
            currentMana = maxMana;
            OnManaChanged?.Invoke(currentMana, maxMana);
            UIManager.Instance?.ShowHUD();

            Debug.Log($"游戏开始 - 魔法值: {currentMana}/{maxMana}");
        }

        public void ReturnToMainMenu()
        {
            if (currentGameInstance)
            {
                Destroy(currentGameInstance);
                currentGameInstance = null;
            }

            if (mainMenuCamera) mainMenuCamera.SetActive(true);
            if (mainMenuUI) mainMenuUI.SetActive(true);

            // 取消暂停并通知 UIManager 切换到主菜单
            ResumeGame();
            UIManager.Instance?.ShowMainMenu();
        }

        private void HandleObjectDetection(GameObject obj, bool isBug)
        {
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
                if (currentMana <= 0)
                {
                    HandleGameOver();
                }
            }
        }

        private void HandleGameOver()
        {
            Debug.Log("💀 魔法值耗尽！游戏结束");
            OnGameOver?.Invoke();

            // 可以在这里添加游戏结束的处理逻辑
            // 比如显示游戏结束界面、统计等
        }

        public void RestoreMana(int amount)
        {
            currentMana = Mathf.Min(maxMana, currentMana + amount);
            OnManaChanged?.Invoke(currentMana, maxMana);
            Debug.Log($"🔮 魔法值恢复 +{amount}，当前: {currentMana}/{maxMana}");
        }

        public void TogglePause()
        {
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
        public int GetCurrentMana() => currentMana;
        public int GetMaxMana() => maxMana;
        public float GetManaPercentage() => maxMana > 0 ? (float)currentMana / maxMana : 0f;
        public bool HasMana() => currentMana > 0;

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

        #region 调试功能

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 150));
            GUILayout.Label("=== GameManager 调试 ===");
            GUILayout.Label($"当前魔法值: {currentMana}/{maxMana}");
            GUILayout.Label($"魔法值百分比: {GetManaPercentage():P0}");
            GUILayout.Label($"游戏暂停: {isPaused}");
            GUILayout.Label($"失败惩罚: {failurePenalty}");

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

            if (GUILayout.Button("触发游戏结束"))
            {
                currentMana = 0;
                OnManaChanged?.Invoke(currentMana, maxMana);
                HandleGameOver();
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

        #endregion
    }
}