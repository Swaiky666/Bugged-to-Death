// UIManager.cs - 完整的UI管理系统，包含游戏结束界面和检测UI控制
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
        [SerializeField] private GameObject gameOverPanel;     // Bad End面板
        [SerializeField] private GameObject happyEndPanel;     // Happy End面板
        [SerializeField] private GameObject badEndPanel;       // 额外的Bad End面板（如果需要）

        [Header("检测UI控制")]
        [SerializeField] private GameObject crosshair;             // 十字准心对象
        [SerializeField] private GameObject magicCircle;           // 魔法圈对象
        [SerializeField] private bool enableDetectionUIControl = true;      // 是否启用检测UI控制
        [SerializeField] private bool debugDetectionUI = true;             // 调试检测UI（默认开启）

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

        [Header("Game End UI Elements")]
        [SerializeField] private Button badEndRestartButton;           // Bad End重新开始按钮
        [SerializeField] private Button badEndMenuButton;              // Bad End返回菜单按钮
        [SerializeField] private Button happyEndRestartButton;         // Happy End重新开始按钮
        [SerializeField] private Button happyEndMenuButton;            // Happy End返回菜单按钮

        [Header("Game End Settings")]
        [SerializeField] private float gameEndFadeTime = 1f;           // 游戏结束面板淡入时间
        [SerializeField] private bool unlockCursorOnGameEnd = true;     // 游戏结束时是否解锁鼠标

        // 魔法球管理
        private List<SimpleManaOrb> manaOrbs = new List<SimpleManaOrb>();
        private int currentMaxMana = 0;

        // 检测UI状态管理
        private bool originalCrosshairState = true;
        private bool originalMagicCircleState = false;
        private bool isDetectionUIActive = false;

        public static UIManager Instance { get; private set; }

        #region Unity生命周期

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeUI();
                InitializeDetectionUI();
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
            GameManager.OnGameOver += ShowBadEnd;           // 订阅Bad End事件
            GameManager.OnHappyEnd += ShowHappyEnd;         // 订阅Happy End事件
            GameManager.OnGameEnded += HandleGameEnded;     // 订阅通用游戏结束事件

            // 订阅Player的检测相关事件
            Player.OnObjectHoldProgress += HandleDetectionProgress;
            Player.OnHoldCancelled += HandleDetectionCancelled;
        }

        private void OnDisable()
        {
            GameManager.OnManaChanged -= UpdateMana;
            GameManager.OnPauseStateChanged -= TogglePauseMenu;
            GameManager.OnGameOver -= ShowBadEnd;
            GameManager.OnHappyEnd -= ShowHappyEnd;
            GameManager.OnGameEnded -= HandleGameEnded;

            // 取消订阅Player事件
            Player.OnObjectHoldProgress -= HandleDetectionProgress;
            Player.OnHoldCancelled -= HandleDetectionCancelled;

            // 恢复检测UI状态
            RestoreDetectionUIState();
        }

        #endregion

        #region 检测UI控制

        /// <summary>
        /// 初始化检测UI控制
        /// </summary>
        private void InitializeDetectionUI()
        {
            if (!enableDetectionUIControl)
            {
                Debug.Log("🎮 UIManager: 检测UI控制未启用，跳过初始化");
                return;
            }

            Debug.Log("🎮 UIManager: 开始初始化检测UI控制");

            // 记录原始状态 - 确保crosshair默认是显示的
            if (crosshair != null)
            {
                // 先确保crosshair是显示状态，再记录原始状态
                crosshair.SetActive(true);
                originalCrosshairState = true; // 强制设为true，因为crosshair应该默认显示
                Debug.Log($"🎯 UIManager: Crosshair设置为显示状态并记录原始状态 - {originalCrosshairState} (对象: {crosshair.name})");
            }
            else
            {
                Debug.LogWarning("⚠️ UIManager: Crosshair对象未设置！请在Inspector中拖入Crosshair对象。");
                originalCrosshairState = true; // 默认值
            }

            if (magicCircle != null)
            {
                // magic circle 默认应该是隐藏的
                magicCircle.SetActive(false);
                originalMagicCircleState = false; // 强制设为false，因为magic circle应该默认隐藏
                Debug.Log($"🔮 UIManager: Magic Circle设置为隐藏状态并记录原始状态 - {originalMagicCircleState} (对象: {magicCircle.name})");
            }
            else
            {
                Debug.LogWarning("⚠️ UIManager: Magic Circle对象未设置！请在Inspector中拖入Magic Circle对象。");
                originalMagicCircleState = false; // 默认值
            }

            isDetectionUIActive = false;
            Debug.Log("✅ UIManager: 检测UI控制初始化完成");
            Debug.Log($"✅ UIManager: 初始化总结 - Crosshair: {(crosshair != null ? crosshair.name : "null")} ({originalCrosshairState}), Magic Circle: {(magicCircle != null ? magicCircle.name : "null")} ({originalMagicCircleState})");
        }

        /// <summary>
        /// 处理检测进度事件
        /// </summary>
        private void HandleDetectionProgress(GameObject detectedObject, float progress)
        {
            if (!enableDetectionUIControl) return;

            // 如果是第一次接收到进度事件（progress > 0且UI未激活），则开始检测
            if (progress > 0f && !isDetectionUIActive)
            {
                StartDetectionUI();
            }
        }

        /// <summary>
        /// 处理检测取消事件
        /// </summary>
        private void HandleDetectionCancelled()
        {
            if (!enableDetectionUIControl) return;

            Debug.Log("🎮 UIManager: 收到检测取消事件");

            // 立即恢复
            EndDetectionUI();

            // 延迟恢复，确保状态正确（防止其他代码干扰）
            StartCoroutine(DelayedRestoreDetectionUI());
        }

        /// <summary>
        /// 延迟恢复检测UI状态（确保状态正确）
        /// </summary>
        private System.Collections.IEnumerator DelayedRestoreDetectionUI()
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame(); // 等待两帧确保所有更新完成

            if (!isDetectionUIActive) // 只有在不是检测状态时才恢复
            {
                Debug.Log("🔄 UIManager: 延迟恢复检测UI状态");
                ForceRestoreDetectionUIState();
            }
        }

        /// <summary>
        /// 开始检测UI状态
        /// </summary>
        private void StartDetectionUI()
        {
            if (isDetectionUIActive)
            {
                if (debugDetectionUI)
                    Debug.Log("🎮 UIManager: 检测UI已经激活，跳过");
                return;
            }

            isDetectionUIActive = true;

            Debug.Log("🎮 UIManager: 开始检测UI状态");
            Debug.Log($"🎮 UIManager: 当前状态 - Crosshair: {(crosshair != null ? crosshair.activeInHierarchy.ToString() : "null")}, Magic Circle: {(magicCircle != null ? magicCircle.activeInHierarchy.ToString() : "null")}");

            // 隐藏crosshair
            if (crosshair != null)
            {
                crosshair.SetActive(false);
                Debug.Log("🎯 UIManager: Crosshair 已隐藏");
            }
            else
            {
                Debug.LogWarning("⚠️ UIManager: Crosshair 对象为null，无法隐藏");
            }

            // 显示magic circle
            if (magicCircle != null)
            {
                magicCircle.SetActive(true);
                Debug.Log("🔮 UIManager: Magic Circle 已显示");
            }
            else
            {
                Debug.LogWarning("⚠️ UIManager: Magic Circle 对象为null，无法显示");
            }
        }

        /// <summary>
        /// 结束检测UI状态
        /// </summary>
        private void EndDetectionUI()
        {
            if (!isDetectionUIActive)
            {
                Debug.Log("🎮 UIManager: 检测UI未激活，直接恢复状态");
                // 即使UI未激活，也要确保恢复到正确状态
                ForceRestoreDetectionUIState();
                return;
            }

            isDetectionUIActive = false;

            Debug.Log("🎮 UIManager: 结束检测UI状态");

            RestoreDetectionUIState();
        }

        /// <summary>
        /// 恢复检测UI到原始状态
        /// </summary>
        private void RestoreDetectionUIState()
        {
            if (!enableDetectionUIControl)
            {
                Debug.Log("🎮 UIManager: 检测UI控制未启用，跳过恢复");
                return;
            }

            Debug.Log("🔄 UIManager: 恢复检测UI到原始状态");
            Debug.Log($"🔄 UIManager: 原始状态 - Crosshair: {originalCrosshairState}, Magic Circle: {originalMagicCircleState}");

            // 恢复crosshair
            if (crosshair != null)
            {
                crosshair.SetActive(originalCrosshairState);
                Debug.Log($"🎯 UIManager: Crosshair 已恢复为 {originalCrosshairState} (实际状态: {crosshair.activeInHierarchy})");
            }
            else
            {
                Debug.LogWarning("⚠️ UIManager: Crosshair 对象为null，无法恢复");
            }

            // 恢复magic circle
            if (magicCircle != null)
            {
                magicCircle.SetActive(originalMagicCircleState);
                Debug.Log($"🔮 UIManager: Magic Circle 已恢复为 {originalMagicCircleState} (实际状态: {magicCircle.activeInHierarchy})");
            }
            else
            {
                Debug.LogWarning("⚠️ UIManager: Magic Circle 对象为null，无法恢复");
            }

            isDetectionUIActive = false;
        }

        /// <summary>
        /// 重置检测UI到默认状态
        /// </summary>
        public void ResetDetectionUIToDefault()
        {
            Debug.Log("🔄 UIManager: 重置检测UI到默认状态");

            // 重置原始状态为期望的默认值
            originalCrosshairState = true;  // crosshair 应该默认显示
            originalMagicCircleState = false; // magic circle 应该默认隐藏

            // 设置UI到默认状态
            if (crosshair != null)
            {
                crosshair.SetActive(true);
                Debug.Log("🎯 UIManager: Crosshair 重置为显示状态");
            }

            if (magicCircle != null)
            {
                magicCircle.SetActive(false);
                Debug.Log("🔮 UIManager: Magic Circle 重置为隐藏状态");
            }

            isDetectionUIActive = false;
            Debug.Log("✅ UIManager: 检测UI已重置到默认状态");
        }

        /// <summary>
        /// 强制显示Crosshair（紧急修复用）
        /// </summary>
        public void ForceShowCrosshair()
        {
            Debug.Log("🆘 UIManager: 强制显示Crosshair");

            if (crosshair != null)
            {
                crosshair.SetActive(true);
                originalCrosshairState = true; // 同时更新原始状态
                Debug.Log("🎯 UIManager: Crosshair 已强制显示并更新原始状态");
            }
            else
            {
                Debug.LogError("❌ UIManager: Crosshair 对象为null，无法强制显示");
            }
        }

        /// <summary>
        /// 强制恢复检测UI状态（用于确保状态正确）
        /// </summary>
        private void ForceRestoreDetectionUIState()
        {
            if (!enableDetectionUIControl) return;

            Debug.Log("🔄 UIManager: 强制恢复检测UI状态");

            // 强制恢复crosshair
            if (crosshair != null)
            {
                crosshair.SetActive(originalCrosshairState);
                Debug.Log($"🎯 UIManager: Crosshair 强制恢复为 {originalCrosshairState}");
            }

            // 强制恢复magic circle
            if (magicCircle != null)
            {
                magicCircle.SetActive(originalMagicCircleState);
                Debug.Log($"🔮 UIManager: Magic Circle 强制恢复为 {originalMagicCircleState}");
            }

            isDetectionUIActive = false;
        }

        /// <summary>
        /// 手动设置检测UI状态（用于测试或外部调用）
        /// </summary>
        public void SetDetectionUIState(bool isDetecting)
        {
            if (isDetecting)
            {
                StartDetectionUI();
            }
            else
            {
                EndDetectionUI();
            }
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
            SetPanel(happyEndPanel, false);
            SetPanel(badEndPanel, false);

            // 按钮绑定
            SetupButtons();

            Debug.Log("UIManager 初始化完成");
        }

        private void SetupButtons()
        {
            // 主菜单按钮
            if (startGameButton)
                startGameButton.onClick.AddListener(() => GameManager.Instance.StartGame());

            if (quitGameButton)
                quitGameButton.onClick.AddListener(() => Application.Quit());

            // 暂停菜单按钮
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

            // 原有的游戏结束按钮（可能是Bad End用的）
            if (restartGameButton)
                restartGameButton.onClick.AddListener(() => {
                    HideAllGameEndPanels();
                    RestartGame();
                });

            // Bad End按钮
            if (badEndRestartButton)
                badEndRestartButton.onClick.AddListener(() => {
                    Debug.Log("🔄 Bad End: 重新开始游戏");
                    HideAllGameEndPanels();
                    RestartGame();
                });

            if (badEndMenuButton)
                badEndMenuButton.onClick.AddListener(() => {
                    Debug.Log("🏠 Bad End: 返回主菜单");
                    HideAllGameEndPanels();
                    GameManager.Instance.ReturnToMainMenu();
                });

            // Happy End按钮
            if (happyEndRestartButton)
                happyEndRestartButton.onClick.AddListener(() => {
                    Debug.Log("🔄 Happy End: 重新开始游戏");
                    HideAllGameEndPanels();
                    RestartGame();
                });

            if (happyEndMenuButton)
                happyEndMenuButton.onClick.AddListener(() => {
                    Debug.Log("🏠 Happy End: 返回主菜单");
                    HideAllGameEndPanels();
                    GameManager.Instance.ReturnToMainMenu();
                });
        }

        #endregion

        #region 魔法球管理

        /// <summary>
        /// 创建魔法球
        /// </summary>
        private void CreateManaOrbs(int maxMana)
        {
            Debug.Log($"🔮 开始创建魔法球UI，数量: {maxMana}");

            // 清除现有魔法球
            ClearManaOrbs();

            if (manaOrbsContainer == null || manaOrbPrefab == null)
            {
                Debug.LogError("❌ 魔法球容器或预制体未设置！请在UIManager中配置这些引用。");
                Debug.LogError($"容器: {(manaOrbsContainer != null ? "已设置" : "未设置")}, 预制体: {(manaOrbPrefab != null ? "已设置" : "未设置")}");
                return;
            }

            currentMaxMana = maxMana;
            int successCount = 0;

            for (int i = 0; i < maxMana; i++)
            {
                try
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

                    successCount++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"❌ 创建第{i + 1}个魔法球时出错: {e.Message}");
                }
            }

            Debug.Log($"🔮 魔法球创建完成，成功创建 {successCount}/{maxMana} 个魔法球");

            if (successCount != maxMana)
            {
                Debug.LogWarning($"⚠️ 期望创建 {maxMana} 个魔法球，实际创建 {successCount} 个");
            }
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
            Debug.Log($"🧹 开始清除魔法球UI，当前数量: {manaOrbs.Count}");

            int clearedCount = 0;
            foreach (var orb in manaOrbs)
            {
                if (orb != null && orb.gameObject != null)
                {
                    DestroyImmediate(orb.gameObject);
                    clearedCount++;
                }
            }
            manaOrbs.Clear();

            // 同时清理容器中可能残留的子对象
            if (manaOrbsContainer != null)
            {
                int remainingCount = manaOrbsContainer.childCount;
                if (remainingCount > 0)
                {
                    Debug.Log($"🧹 发现容器中还有 {remainingCount} 个残留子对象，正在清理...");

                    // 从后往前删除，避免索引问题
                    for (int i = manaOrbsContainer.childCount - 1; i >= 0; i--)
                    {
                        Transform child = manaOrbsContainer.GetChild(i);
                        if (child != null)
                        {
                            DestroyImmediate(child.gameObject);
                        }
                    }
                }
            }

            // 重置状态
            currentMaxMana = 0;

            Debug.Log($"🧹 魔法球清理完成，已清理 {clearedCount} 个魔法球对象");
        }

        /// <summary>
        /// 更新魔法值显示
        /// </summary>
        private void UpdateMana(int currentMana, int maxMana)
        {
            Debug.Log($"📊 UpdateMana被调用: {currentMana}/{maxMana}, 当前魔法球数量: {manaOrbs.Count}");

            // 如果魔法球数量不匹配或为空，重新创建
            if (manaOrbs.Count != maxMana || currentMaxMana != maxMana || manaOrbs.Count == 0)
            {
                Debug.Log($"🔄 魔法球数量不匹配，重新创建：当前{manaOrbs.Count}个，需要{maxMana}个");
                CreateManaOrbs(maxMana);
            }

            // 更新魔法球状态
            UpdateManaOrbsDisplay(currentMana, maxMana);

            Debug.Log($"📊 魔法值UI更新完成: {currentMana}/{maxMana}");
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

        #region 游戏结束界面管理

        /// <summary>
        /// 显示Bad End界面
        /// </summary>
        private void ShowBadEnd()
        {
            Debug.Log("💀 显示Bad End界面");

            // 注意：保持HUD显示，让玩家可以看到最终的魔法值状态
            // SetPanel(hudPanel, false);

            // 显示Bad End面板（优先使用badEndPanel，如果没有则使用gameOverPanel）
            if (badEndPanel != null)
            {
                StartCoroutine(ShowGameEndPanelWithFade(badEndPanel));
            }
            else if (gameOverPanel != null)
            {
                StartCoroutine(ShowGameEndPanelWithFade(gameOverPanel));
            }
            else
            {
                Debug.LogError("❌ 没有找到Bad End面板！请在UIManager中设置badEndPanel或gameOverPanel引用。");
            }

            // 解锁鼠标以便点击UI
            UnlockCursorForUI();
        }

        /// <summary>
        /// 显示Happy End界面
        /// </summary>
        private void ShowHappyEnd()
        {
            Debug.Log("🎉 显示Happy End界面");

            // 注意：保持HUD显示，让玩家可以看到最终的魔法值状态
            // SetPanel(hudPanel, false);

            // 显示Happy End面板
            if (happyEndPanel != null)
            {
                StartCoroutine(ShowGameEndPanelWithFade(happyEndPanel));
            }
            else
            {
                Debug.LogError("❌ 没有找到Happy End面板！请在UIManager中设置happyEndPanel引用。");
            }

            // 解锁鼠标以便点击UI
            UnlockCursorForUI();
        }

        /// <summary>
        /// 处理通用游戏结束事件
        /// </summary>
        private void HandleGameEnded(string endType)
        {
            Debug.Log($"🎮 收到游戏结束事件: {endType}");

            // 游戏结束时恢复检测UI状态
            RestoreDetectionUIState();

            switch (endType.ToLower())
            {
                case "badend":
                    // Bad End已经在ShowBadEnd中处理
                    break;
                case "happyend":
                    // Happy End已经在ShowHappyEnd中处理
                    break;
                default:
                    Debug.LogWarning($"⚠️ 未知的游戏结束类型: {endType}");
                    break;
            }
        }

        /// <summary>
        /// 带淡入效果显示游戏结束面板
        /// </summary>
        private System.Collections.IEnumerator ShowGameEndPanelWithFade(GameObject panel)
        {
            if (panel == null) yield break;

            // 立即显示面板
            panel.SetActive(true);

            // 如果有CanvasGroup组件，播放淡入动画
            CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                float elapsed = 0f;

                while (elapsed < gameEndFadeTime)
                {
                    elapsed += Time.unscaledDeltaTime; // 使用unscaledDeltaTime以防游戏被暂停
                    canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / gameEndFadeTime);
                    yield return null;
                }

                canvasGroup.alpha = 1f;
            }

            Debug.Log($"✅ 游戏结束面板 {panel.name} 显示完成");
        }

        /// <summary>
        /// 隐藏所有游戏结束面板
        /// </summary>
        private void HideAllGameEndPanels()
        {
            SetPanel(gameOverPanel, false);
            SetPanel(happyEndPanel, false);
            SetPanel(badEndPanel, false);
            Debug.Log("🙈 所有游戏结束面板已隐藏");
        }

        /// <summary>
        /// 为UI交互解锁鼠标
        /// </summary>
        private void UnlockCursorForUI()
        {
            if (!unlockCursorOnGameEnd) return;

            var camCtrl = Camera.main?.GetComponent<CameraController>();
            if (camCtrl != null)
                camCtrl.SetCursorLocked(false);
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            Debug.Log("🖱️ 鼠标已解锁用于UI交互");
        }

        #endregion

        #region UI面板管理

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

            // 强制重新生成魔法球UI
            if (GameManager.Instance != null)
            {
                int currentMana = GameManager.Instance.GetCurrentMana();
                int maxMana = GameManager.Instance.GetMaxMana();

                Debug.Log($"🎮 ShowHUD: 强制重新生成魔法球UI - {currentMana}/{maxMana}");

                // 清除现有魔法球
                ClearManaOrbs();

                // 重新创建魔法球
                CreateManaOrbs(maxMana);

                // 更新魔法球状态
                UpdateManaOrbsDisplay(currentMana, maxMana);
            }

            // 重置检测UI到默认状态（确保crosshair显示）
            ResetDetectionUIToDefault();

            Debug.Log("🎮 显示游戏HUD - 魔法球UI已重新生成，检测UI已重置到默认状态");
        }

        /// <summary>
        /// 显示主菜单，并隐藏所有游戏内面板
        /// </summary>
        public void ShowMainMenu()
        {
            SetPanel(mainMenuPanel, true);
            SetPanel(hudPanel, false);
            SetPanel(pausePanel, false);
            HideAllGameEndPanels();

            // 清理魔法球UI
            ClearManaOrbs();

            // 重置检测UI到默认状态
            ResetDetectionUIToDefault();

            Debug.Log("🏠 显示主菜单 - 魔法球UI已清理，检测UI已重置到默认状态");
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
        /// 强制重新生成魔法球UI
        /// </summary>
        public void ForceRegenerateManaOrbs()
        {
            Debug.Log("🔄 强制重新生成魔法球UI");

            if (GameManager.Instance != null)
            {
                int currentMana = GameManager.Instance.GetCurrentMana();
                int maxMana = GameManager.Instance.GetMaxMana();

                // 清除现有魔法球
                ClearManaOrbs();

                // 重新创建魔法球
                CreateManaOrbs(maxMana);

                // 更新魔法球状态
                UpdateManaOrbsDisplay(currentMana, maxMana);

                Debug.Log($"🔮 魔法球UI重新生成完成: {currentMana}/{maxMana}");
            }
            else
            {
                Debug.LogWarning("⚠️ GameManager实例不存在，无法重新生成魔法球UI");
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

        /// <summary>
        /// 检查是否有游戏结束面板正在显示
        /// </summary>
        public bool IsGameEndPanelShowing()
        {
            return (gameOverPanel != null && gameOverPanel.activeInHierarchy) ||
                   (happyEndPanel != null && happyEndPanel.activeInHierarchy) ||
                   (badEndPanel != null && badEndPanel.activeInHierarchy);
        }

        /// <summary>
        /// 手动显示Bad End面板（用于测试）
        /// </summary>
        public void ShowBadEndManual()
        {
            ShowBadEnd();
        }

        /// <summary>
        /// 手动显示Happy End面板（用于测试）
        /// </summary>
        public void ShowHappyEndManual()
        {
            ShowHappyEnd();
        }

        /// <summary>
        /// 重启游戏（确保删除旧实例）
        /// </summary>
        private void RestartGame()
        {
            Debug.Log("🔄 UIManager: 开始重启游戏流程");

            // 先清理当前UI状态
            HideAllGameEndPanels();
            ClearManaOrbs();
            RestoreDetectionUIState();

            // 确保GameManager先清理旧的游戏实例
            if (GameManager.Instance != null)
            {
                GameManager.Instance.CleanupCurrentGame();
                GameManager.Instance.StartGame();
            }
            else
            {
                Debug.LogError("❌ GameManager实例不存在，无法重启游戏");
            }
        }

        /// <summary>
        /// 设置检测UI对象引用
        /// </summary>
        public void SetDetectionUIObjects(GameObject crosshairObj, GameObject magicCircleObj)
        {
            crosshair = crosshairObj;
            magicCircle = magicCircleObj;
            InitializeDetectionUI();
            Debug.Log($"🎮 UIManager: 检测UI对象已设置 - Crosshair: {(crosshair != null ? crosshair.name : "null")}, Magic Circle: {(magicCircle != null ? magicCircle.name : "null")}");
        }

        /// <summary>
        /// 启用/禁用检测UI控制
        /// </summary>
        public void SetDetectionUIControlEnabled(bool enabled)
        {
            enableDetectionUIControl = enabled;
            Debug.Log($"🎮 UIManager: 检测UI控制 {(enabled ? "启用" : "禁用")}");

            if (!enabled)
            {
                RestoreDetectionUIState();
            }
        }

        /// <summary>
        /// 获取检测UI状态信息
        /// </summary>
        public string GetDetectionUIStatus()
        {
            return $"检测UI控制启用: {enableDetectionUIControl}\n" +
                   $"检测UI激活: {isDetectionUIActive}\n" +
                   $"Crosshair: {(crosshair != null ? (crosshair.activeInHierarchy ? "显示" : "隐藏") : "未设置")}\n" +
                   $"Magic Circle: {(magicCircle != null ? (magicCircle.activeInHierarchy ? "显示" : "隐藏") : "未设置")}";
        }

        #endregion

        #region 调试功能

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(Screen.width - 380, Screen.height - 350, 360, 340));
            GUILayout.Label("=== UIManager调试 ===");
            GUILayout.Label($"魔法球数量: {manaOrbs.Count}");
            GUILayout.Label($"当前间距: {orbSpacing}");
            GUILayout.Label($"布局方向: {(useHorizontalLayout ? "水平" : "垂直")}");

            if (GameManager.Instance != null)
            {
                GUILayout.Label($"当前魔法值: {GameManager.Instance.GetCurrentMana()}/{GameManager.Instance.GetMaxMana()}");
                GUILayout.Label($"游戏结束: {GameManager.Instance.IsGameEnded()}");
                if (GameManager.Instance.IsGameEnded())
                {
                    GUILayout.Label($"结束原因: {GameManager.Instance.GetGameEndReason()}");
                }
            }

            // 检测UI状态
            GUILayout.Space(5);
            GUILayout.Label("=== 检测UI状态 ===");
            GUILayout.Label($"UI控制启用: {enableDetectionUIControl}");
            GUILayout.Label($"检测UI激活: {isDetectionUIActive}");
            GUILayout.Label($"Crosshair: {(crosshair != null ? (crosshair.activeInHierarchy ? "显示" : "隐藏") : "未设置")}");
            GUILayout.Label($"Magic Circle: {(magicCircle != null ? (magicCircle.activeInHierarchy ? "显示" : "隐藏") : "未设置")}");

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("测试Bad End"))
            {
                ShowBadEndManual();
            }
            if (GUILayout.Button("测试Happy End"))
            {
                ShowHappyEndManual();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("隐藏结束面板"))
            {
                HideAllGameEndPanels();
            }
            if (GUILayout.Button("重新生成魔法球"))
            {
                ForceRegenerateManaOrbs();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("显示主菜单"))
            {
                ShowMainMenu();
            }
            if (GUILayout.Button("显示HUD"))
            {
                ShowHUD();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("开始检测UI"))
            {
                SetDetectionUIState(true);
            }
            if (GUILayout.Button("结束检测UI"))
            {
                SetDetectionUIState(false);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("强制恢复检测UI"))
            {
                ForceRestoreDetectionUIState();
            }
            if (GUILayout.Button("重新初始化检测UI"))
            {
                InitializeDetectionUI();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🆘 强制显示Crosshair"))
            {
                ForceShowCrosshair();
            }
            if (GUILayout.Button("🔄 重置到默认状态"))
            {
                ResetDetectionUIToDefault();
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
            Debug.Log($"Happy End面板: {(happyEndPanel != null ? happyEndPanel.name : "未设置")}");
            Debug.Log($"Bad End面板: {(badEndPanel != null ? badEndPanel.name : "未设置")}");

            Debug.Log($"Crosshair: {(crosshair != null ? crosshair.name : "未设置")}");
            Debug.Log($"Magic Circle: {(magicCircle != null ? magicCircle.name : "未设置")}");

            Debug.Log($"当前魔法球数量: {manaOrbs.Count}");
            Debug.Log($"布局参数: 间距={orbSpacing}, 水平={useHorizontalLayout}, 起始位置={startPosition}");
        }

        [ContextMenu("💀 测试Bad End")]
        private void TestBadEnd()
        {
            if (Application.isPlaying)
            {
                ShowBadEndManual();
            }
        }

        [ContextMenu("🎉 测试Happy End")]
        private void TestHappyEnd()
        {
            if (Application.isPlaying)
            {
                ShowHappyEndManual();
            }
        }

        [ContextMenu("🙈 隐藏所有结束面板")]
        private void TestHideAllGameEndPanels()
        {
            if (Application.isPlaying)
            {
                HideAllGameEndPanels();
            }
        }

        [ContextMenu("🎮 显示HUD")]
        private void TestShowHUD()
        {
            if (Application.isPlaying)
            {
                ShowHUD();
            }
        }

        [ContextMenu("🎯 测试检测UI")]
        private void TestDetectionUI()
        {
            if (Application.isPlaying)
            {
                Debug.Log("🧪 测试检测UI控制...");
                Debug.Log($"当前状态: {GetDetectionUIStatus()}");

                // 切换检测UI状态
                SetDetectionUIState(!isDetectionUIActive);
                Debug.Log($"切换后状态: {GetDetectionUIStatus()}");
            }
        }

        [ContextMenu("🆘 强制显示Crosshair")]
        private void TestForceShowCrosshair()
        {
            ForceShowCrosshair();
        }

        [ContextMenu("🔄 重置检测UI到默认状态")]
        private void TestResetDetectionUIToDefault()
        {
            ResetDetectionUIToDefault();
        }

        [ContextMenu("🔄 恢复检测UI状态")]
        private void TestRestoreDetectionUI()
        {
            if (Application.isPlaying)
            {
                Debug.Log("🧪 手动恢复检测UI状态...");
                RestoreDetectionUIState();
                Debug.Log("检测UI已恢复到原始状态");
            }
        }

        [ContextMenu("🔧 强制恢复检测UI状态")]
        private void TestForceRestoreDetectionUI()
        {
            if (Application.isPlaying)
            {
                Debug.Log("🧪 强制恢复检测UI状态...");
                ForceRestoreDetectionUIState();
                Debug.Log("检测UI已强制恢复");
            }
        }

        [ContextMenu("🔄 重新初始化检测UI")]
        private void TestReinitializeDetectionUI()
        {
            if (Application.isPlaying)
            {
                Debug.Log("🧪 重新初始化检测UI...");
                InitializeDetectionUI();
                Debug.Log("检测UI重新初始化完成");
            }
        }

        [ContextMenu("🔍 检查检测UI状态")]
        private void TestCheckDetectionUIStatus()
        {
            Debug.Log("=== 检测UI状态检查 ===");
            Debug.Log($"UI控制启用: {enableDetectionUIControl}");
            Debug.Log($"检测UI激活: {isDetectionUIActive}");
            Debug.Log($"调试模式: {debugDetectionUI}");

            if (crosshair != null)
            {
                Debug.Log($"Crosshair对象: {crosshair.name}");
                Debug.Log($"Crosshair当前状态: {crosshair.activeInHierarchy}");
                Debug.Log($"Crosshair原始状态: {originalCrosshairState}");
            }
            else
            {
                Debug.Log("Crosshair对象: null");
            }

            if (magicCircle != null)
            {
                Debug.Log($"Magic Circle对象: {magicCircle.name}");
                Debug.Log($"Magic Circle当前状态: {magicCircle.activeInHierarchy}");
                Debug.Log($"Magic Circle原始状态: {originalMagicCircleState}");
            }
            else
            {
                Debug.Log("Magic Circle对象: null");
            }
        }

        #endregion
    }
}