// UIManager.cs - 完整的简化魔法球系统
using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace BugFixerGame
{
    public class UIManager : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject gameOverPanel;

        [Header("魔法球UI设置")]
        [SerializeField] private Transform manaOrbsContainer;           // 魔法球容器
        [SerializeField] private GameObject manaOrbPrefab;              // 魔法球预制体

        [Header("魔法球布局设置")]
        [SerializeField] private float orbSpacing = 60f;               // 魔法球间距
        [SerializeField] private bool useHorizontalLayout = true;       // 是否使用水平布局
        [SerializeField] private Vector3 startPosition = Vector3.zero; // 起始位置偏移

        [Header("UI Elements")]
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button quitGameButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button returnToMenuButton;
        [SerializeField] private Button restartGameButton;

        // 魔法球管理
        private List<SimpleManaOrb> manaOrbs = new List<SimpleManaOrb>();
        private int currentMaxMana = 0;

        public static UIManager Instance { get; private set; }

        #region Unity生命周期

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeUI();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            GameManager.OnManaChanged += UpdateMana;
            GameManager.OnPauseStateChanged += TogglePauseMenu;
            GameManager.OnGameOver += ShowGameOver;
        }

        private void OnDisable()
        {
            GameManager.OnManaChanged -= UpdateMana;
            GameManager.OnPauseStateChanged -= TogglePauseMenu;
            GameManager.OnGameOver -= ShowGameOver;
        }

        #endregion

        #region UI初始化

        private void InitializeUI()
        {
            // 初始化显示面板
            SetPanel(mainMenuPanel, true);
            SetPanel(hudPanel, false);
            SetPanel(pausePanel, false);
            SetPanel(gameOverPanel, false);

            // 按钮绑定
            SetupButtons();

            Debug.Log("UIManager 初始化完成");
        }

        private void SetupButtons()
        {
            if (startGameButton)
                startGameButton.onClick.AddListener(() => GameManager.Instance.StartGame());

            if (quitGameButton)
                quitGameButton.onClick.AddListener(() => Application.Quit());

            if (resumeButton)
            {
                resumeButton.onClick.RemoveAllListeners();
                resumeButton.onClick.AddListener(() =>
                {
                    GameManager.Instance.ResumeGame();
                    // 重新锁定鼠标
                    var camCtrl = Camera.main?.GetComponent<CameraController>();
                    if (camCtrl != null)
                        camCtrl.SetCursorLocked(true);
                    else
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }
                });
            }

            if (returnToMenuButton)
                returnToMenuButton.onClick.AddListener(() => GameManager.Instance.ReturnToMainMenu());

            if (restartGameButton)
                restartGameButton.onClick.AddListener(() => {
                    SetPanel(gameOverPanel, false);
                    GameManager.Instance.StartGame();
                });
        }

        #endregion

        #region 魔法球管理

        /// <summary>
        /// 创建魔法球
        /// </summary>
        private void CreateManaOrbs(int maxMana)
        {
            // 清除现有魔法球
            ClearManaOrbs();

            if (manaOrbsContainer == null || manaOrbPrefab == null)
            {
                Debug.LogError("❌ 魔法球容器或预制体未设置！请在UIManager中配置这些引用。");
                return;
            }

            currentMaxMana = maxMana;

            for (int i = 0; i < maxMana; i++)
            {
                // 实例化魔法球
                GameObject orbGO = Instantiate(manaOrbPrefab, manaOrbsContainer);

                // 设置位置
                Vector3 position = CalculateOrbPosition(i);
                orbGO.transform.localPosition = position;

                // 获取SimpleManaOrb组件
                SimpleManaOrb orbScript = orbGO.GetComponent<SimpleManaOrb>();
                if (orbScript == null)
                {
                    Debug.LogError($"❌ 魔法球预制体 {manaOrbPrefab.name} 没有SimpleManaOrb组件！自动添加组件。");
                    orbScript = orbGO.AddComponent<SimpleManaOrb>();
                }

                // 确保魔法球处于满状态
                orbScript.ResetToFull();

                // 添加到列表
                manaOrbs.Add(orbScript);

                // 设置名称便于调试
                orbGO.name = $"ManaOrb_{i + 1}";
            }

            Debug.Log($"🔮 创建了 {maxMana} 个魔法球");
        }

        /// <summary>
        /// 计算魔法球位置
        /// </summary>
        private Vector3 CalculateOrbPosition(int index)
        {
            Vector3 position = startPosition;

            if (useHorizontalLayout)
            {
                // 水平排列
                position.x += index * orbSpacing;
            }
            else
            {
                // 垂直排列
                position.y -= index * orbSpacing;
            }

            return position;
        }

        /// <summary>
        /// 清除所有魔法球
        /// </summary>
        private void ClearManaOrbs()
        {
            foreach (var orb in manaOrbs)
            {
                if (orb != null && orb.gameObject != null)
                {
                    DestroyImmediate(orb.gameObject);
                }
            }
            manaOrbs.Clear();

            // 同时清理容器中可能残留的子对象
            if (manaOrbsContainer != null)
            {
                foreach (Transform child in manaOrbsContainer)
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            Debug.Log("🧹 清除了所有魔法球");
        }

        /// <summary>
        /// 更新魔法值显示
        /// </summary>
        private void UpdateMana(int currentMana, int maxMana)
        {
            // 如果魔法球数量不匹配，重新创建
            if (manaOrbs.Count != maxMana || currentMaxMana != maxMana)
            {
                Debug.Log($"🔄 魔法球数量不匹配，重新创建：当前{manaOrbs.Count}个，需要{maxMana}个");
                CreateManaOrbs(maxMana);
            }

            // 更新魔法球状态
            UpdateManaOrbsDisplay(currentMana, maxMana);

            Debug.Log($"📊 魔法值UI更新: {currentMana}/{maxMana}");
        }

        /// <summary>
        /// 更新魔法球显示状态
        /// </summary>
        private void UpdateManaOrbsDisplay(int currentMana, int maxMana)
        {
            for (int i = 0; i < manaOrbs.Count; i++)
            {
                if (manaOrbs[i] == null) continue;

                if (i < currentMana)
                {
                    // 这个魔法球应该是满的
                    if (manaOrbs[i].IsEmpty())
                    {
                        // 如果当前是空的，重置为满
                        manaOrbs[i].ResetToFull();
                        Debug.Log($"🔮 魔法球 {i + 1} 重置为满蓝状态");
                    }
                }
                else
                {
                    // 这个魔法球应该是空的
                    if (!manaOrbs[i].IsEmpty())
                    {
                        // 如果当前是满的，播放变空动画
                        manaOrbs[i].PlayEmptyAnimation();
                        Debug.Log($"✨ 魔法球 {i + 1} 播放变空动画");
                    }
                }
            }
        }

        #endregion

        #region UI面板管理

        private void ShowGameOver()
        {
            SetPanel(gameOverPanel, true);

            // 解锁鼠标以便点击UI
            var camCtrl = Camera.main?.GetComponent<CameraController>();
            if (camCtrl != null)
                camCtrl.SetCursorLocked(false);
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            Debug.Log("💀 显示游戏结束界面");
        }

        private void TogglePauseMenu(bool isPaused)
        {
            SetPanel(pausePanel, isPaused);
            Debug.Log($"⏸️ 暂停菜单: {(isPaused ? "显示" : "隐藏")}");
        }

        private void SetPanel(GameObject panel, bool state)
        {
            if (panel)
                panel.SetActive(state);
        }

        public void ShowHUD()
        {
            SetPanel(hudPanel, true);

            // 确保魔法值UI正确显示
            if (GameManager.Instance != null)
            {
                UpdateMana(GameManager.Instance.GetCurrentMana(), GameManager.Instance.GetMaxMana());
            }

            Debug.Log("🎮 显示游戏HUD");
        }

        /// <summary>
        /// 显示主菜单，并隐藏游戏内所有面板
        /// </summary>
        public void ShowMainMenu()
        {
            SetPanel(mainMenuPanel, true);
            SetPanel(hudPanel, false);
            SetPanel(pausePanel, false);
            SetPanel(gameOverPanel, false);

            Debug.Log("🏠 显示主菜单");
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 设置魔法球间距
        /// </summary>
        public void SetOrbSpacing(float spacing)
        {
            orbSpacing = spacing;
            // 重新计算所有魔法球位置
            for (int i = 0; i < manaOrbs.Count; i++)
            {
                if (manaOrbs[i] != null)
                {
                    Vector3 newPosition = CalculateOrbPosition(i);
                    manaOrbs[i].transform.localPosition = newPosition;
                }
            }
            Debug.Log($"📏 魔法球间距设置为: {spacing}");
        }

        /// <summary>
        /// 设置布局方向
        /// </summary>
        public void SetHorizontalLayout(bool horizontal)
        {
            useHorizontalLayout = horizontal;
            // 重新计算所有魔法球位置
            for (int i = 0; i < manaOrbs.Count; i++)
            {
                if (manaOrbs[i] != null)
                {
                    Vector3 newPosition = CalculateOrbPosition(i);
                    manaOrbs[i].transform.localPosition = newPosition;
                }
            }
            Debug.Log($"📐 布局方向设置为: {(horizontal ? "水平" : "垂直")}");
        }

        /// <summary>
        /// 手动刷新魔法球显示
        /// </summary>
        public void RefreshManaDisplay()
        {
            if (GameManager.Instance != null)
            {
                UpdateMana(GameManager.Instance.GetCurrentMana(), GameManager.Instance.GetMaxMana());
            }
        }

        /// <summary>
        /// 获取当前魔法球数量
        /// </summary>
        public int GetManaOrbCount() => manaOrbs.Count;

        /// <summary>
        /// 获取指定索引的魔法球
        /// </summary>
        public SimpleManaOrb GetManaOrb(int index)
        {
            if (index >= 0 && index < manaOrbs.Count)
                return manaOrbs[index];
            return null;
        }

        /// <summary>
        /// 获取所有魔法球的状态信息
        /// </summary>
        public string GetManaOrbsStatus()
        {
            string status = "魔法球状态:\n";
            for (int i = 0; i < manaOrbs.Count; i++)
            {
                if (manaOrbs[i] != null)
                {
                    status += $"  {i + 1}. {manaOrbs[i].GetStatusDescription()}\n";
                }
            }
            return status;
        }

        #endregion

        #region 调试功能

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(Screen.width - 350, Screen.height - 250, 330, 240));
            GUILayout.Label("=== 魔法球UI调试 ===");
            GUILayout.Label($"魔法球数量: {manaOrbs.Count}");
            GUILayout.Label($"当前间距: {orbSpacing}");
            GUILayout.Label($"布局方向: {(useHorizontalLayout ? "水平" : "垂直")}");

            if (GameManager.Instance != null)
            {
                GUILayout.Label($"当前魔法值: {GameManager.Instance.GetCurrentMana()}/{GameManager.Instance.GetMaxMana()}");
            }

            // 引用检查
            GUILayout.Label($"容器引用: {(manaOrbsContainer != null ? "✓" : "✗")}");
            GUILayout.Label($"预制体引用: {(manaOrbPrefab != null ? "✓" : "✗")}");

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("测试减少"))
            {
                TestDecreaseMana();
            }
            if (GUILayout.Button("测试重置"))
            {
                TestResetMana();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("重新创建"))
            {
                if (GameManager.Instance != null)
                {
                    CreateManaOrbs(GameManager.Instance.GetMaxMana());
                }
            }
            if (GUILayout.Button("检查状态"))
            {
                Debug.Log(GetManaOrbsStatus());
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        [ContextMenu("🔍 检查UI引用")]
        private void CheckUIReferences()
        {
            Debug.Log("=== UIManager 引用检查 ===");
            Debug.Log($"Mana Orbs Container: {(manaOrbsContainer != null ? manaOrbsContainer.name : "未设置")}");
            Debug.Log($"Mana Orb Prefab: {(manaOrbPrefab != null ? manaOrbPrefab.name : "未设置")}");

            Debug.Log($"主菜单面板: {(mainMenuPanel != null ? mainMenuPanel.name : "未设置")}");
            Debug.Log($"HUD面板: {(hudPanel != null ? hudPanel.name : "未设置")}");
            Debug.Log($"暂停面板: {(pausePanel != null ? pausePanel.name : "未设置")}");
            Debug.Log($"游戏结束面板: {(gameOverPanel != null ? gameOverPanel.name : "未设置")}");

            Debug.Log($"当前魔法球数量: {manaOrbs.Count}");
            Debug.Log($"布局参数: 间距={orbSpacing}, 水平={useHorizontalLayout}, 起始位置={startPosition}");
        }

        [ContextMenu("📊 测试减少魔法值")]
        private void TestDecreaseMana()
        {
            if (Application.isPlaying && GameManager.Instance != null)
            {
                int currentMana = GameManager.Instance.GetCurrentMana();
                int maxMana = GameManager.Instance.GetMaxMana();
                if (currentMana > 0)
                {
                    UpdateMana(currentMana - 1, maxMana);
                }
            }
        }

        [ContextMenu("🔄 测试重置魔法值")]
        private void TestResetMana()
        {
            if (Application.isPlaying && GameManager.Instance != null)
            {
                int maxMana = GameManager.Instance.GetMaxMana();
                UpdateMana(maxMana, maxMana);
            }
        }

        [ContextMenu("🔮 强制重新创建魔法球")]
        private void ForceRecreateManaOrbs()
        {
            if (Application.isPlaying && GameManager.Instance != null)
            {
                CreateManaOrbs(GameManager.Instance.GetMaxMana());
            }
        }

        #endregion
    }
}