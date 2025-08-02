// UIManager.cs - 直接在主菜单和暂停面板放音频slider
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

        [Header("主菜单音频控制")]
        [SerializeField] private Slider mainMenuMusicSlider;     // 主菜单音乐音量滑条
        [SerializeField] private Slider mainMenuSFXSlider;       // 主菜单音效音量滑条

        [Header("暂停菜单音频控制")]
        [SerializeField] private Slider pauseMusicSlider;       // 暂停菜单音乐音量滑条
        [SerializeField] private Slider pauseSFXSlider;         // 暂停菜单音效音量滑条

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

            // 隐藏crosshair
            if (crosshair != null)
            {
                crosshair.SetActive(false);
                Debug.Log("🎯 UIManager: Crosshair 已隐藏");
            }

            // 显示magic circle
            if (magicCircle != null)
            {
                magicCircle.SetActive(true);
                Debug.Log("🔮 UIManager: Magic Circle 已显示");
            }
        }

        /// <summary>
        /// 结束检测UI状态
        /// </summary>
        private void EndDetectionUI()
        {
            if (!isDetectionUIActive)
            {
                ForceRestoreDetectionUIState();
                return;
            }

            isDetectionUIActive = false;
            RestoreDetectionUIState();
        }

        /// <summary>
        /// 恢复检测UI到原始状态
        /// </summary>
        private void RestoreDetectionUIState()
        {
            if (!enableDetectionUIControl) return;

            Debug.Log("🔄 UIManager: 恢复检测UI到原始状态");

            // 恢复crosshair
            if (crosshair != null)
            {
                crosshair.SetActive(originalCrosshairState);
            }

            // 恢复magic circle
            if (magicCircle != null)
            {
                magicCircle.SetActive(originalMagicCircleState);
            }

            isDetectionUIActive = false;
        }

        /// <summary>
        /// 强制恢复检测UI状态（用于确保状态正确）
        /// </summary>
        private void ForceRestoreDetectionUIState()
        {
            if (!enableDetectionUIControl) return;

            // 强制恢复crosshair
            if (crosshair != null)
            {
                crosshair.SetActive(originalCrosshairState);
            }

            // 强制恢复magic circle
            if (magicCircle != null)
            {
                magicCircle.SetActive(originalMagicCircleState);
            }

            isDetectionUIActive = false;
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

            // 初始化音频slider
            InitializeAudioSliders();

            Debug.Log("UIManager 初始化完成");
        }

        private void SetupButtons()
        {
            // 主菜单按钮
            if (startGameButton)
            {
                startGameButton.onClick.AddListener(() => {
                    AudioManager.Instance?.PlayButtonClickSound();
                    GameManager.Instance.StartGame();
                });
            }

            if (quitGameButton)
            {
                quitGameButton.onClick.AddListener(() => {
                    AudioManager.Instance?.PlayButtonClickSound();
                    Application.Quit();
                });
            }

            // 暂停菜单按钮
            if (resumeButton)
            {
                resumeButton.onClick.RemoveAllListeners();
                resumeButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlayButtonClickSound();
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
                returnToMenuButton.onClick.AddListener(() => {
                    AudioManager.Instance?.PlayButtonClickSound();
                    GameManager.Instance.ReturnToMainMenu();
                });

            // 游戏结束按钮
            if (restartGameButton)
                restartGameButton.onClick.AddListener(() => {
                    HideAllGameEndPanels();
                    RestartGame();
                });

            if (badEndRestartButton)
                badEndRestartButton.onClick.AddListener(() => {
                    HideAllGameEndPanels();
                    RestartGame();
                });

            if (badEndMenuButton)
                badEndMenuButton.onClick.AddListener(() => {
                    HideAllGameEndPanels();
                    GameManager.Instance.ReturnToMainMenu();
                });

            if (happyEndRestartButton)
                happyEndRestartButton.onClick.AddListener(() => {
                    HideAllGameEndPanels();
                    RestartGame();
                });

            if (happyEndMenuButton)
                happyEndMenuButton.onClick.AddListener(() => {
                    HideAllGameEndPanels();
                    GameManager.Instance.ReturnToMainMenu();
                });
        }

        #endregion

        #region 音频Slider管理

        /// <summary>
        /// 初始化音频Slider
        /// </summary>
        private void InitializeAudioSliders()
        {
            // 从PlayerPrefs加载保存的音量设置
            float savedMusicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
            float savedSFXVolume = PlayerPrefs.GetFloat("SFXVolume", 0.8f);

            // 设置AudioManager的音量
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetMusicVolume(savedMusicVolume);
                AudioManager.Instance.SetSFXVolume(savedSFXVolume);
            }

            // 设置所有slider的属性和初始值
            SetupSlider(mainMenuMusicSlider, savedMusicVolume, OnMusicVolumeChanged);
            SetupSlider(mainMenuSFXSlider, savedSFXVolume, OnSFXVolumeChanged);
            SetupSlider(pauseMusicSlider, savedMusicVolume, OnMusicVolumeChanged);
            SetupSlider(pauseSFXSlider, savedSFXVolume, OnSFXVolumeChanged);

            Debug.Log($"🔊 音频Slider初始化完成 - 音乐: {savedMusicVolume:F2}, 音效: {savedSFXVolume:F2}");
        }

        /// <summary>
        /// 设置单个Slider的属性
        /// </summary>
        private void SetupSlider(Slider slider, float initialValue, UnityEngine.Events.UnityAction<float> callback)
        {
            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.value = initialValue;
                slider.onValueChanged.AddListener(callback);
            }
        }

        /// <summary>
        /// 音乐音量改变回调
        /// </summary>
        private void OnMusicVolumeChanged(float value)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetMusicVolume(value);
            }

            // 同步更新所有音乐slider的值
            UpdateSliderValue(mainMenuMusicSlider, value);
            UpdateSliderValue(pauseMusicSlider, value);

            // 保存设置
            PlayerPrefs.SetFloat("MusicVolume", value);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 音效音量改变回调
        /// </summary>
        private void OnSFXVolumeChanged(float value)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetSFXVolume(value);
                // 播放测试音效
                AudioManager.Instance.PlayButtonClickSound();
            }

            // 同步更新所有音效slider的值
            UpdateSliderValue(mainMenuSFXSlider, value);
            UpdateSliderValue(pauseSFXSlider, value);

            // 保存设置
            PlayerPrefs.SetFloat("SFXVolume", value);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 更新slider的值（不触发回调）
        /// </summary>
        private void UpdateSliderValue(Slider slider, float value)
        {
            if (slider != null && !Mathf.Approximately(slider.value, value))
            {
                // 临时移除监听器，更新值，然后重新添加
                slider.onValueChanged.RemoveAllListeners();
                slider.value = value;

                // 重新添加对应的监听器
                if (slider == mainMenuMusicSlider || slider == pauseMusicSlider)
                {
                    slider.onValueChanged.AddListener(OnMusicVolumeChanged);
                }
                else if (slider == mainMenuSFXSlider || slider == pauseSFXSlider)
                {
                    slider.onValueChanged.AddListener(OnSFXVolumeChanged);
                }
            }
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
                Debug.LogError("❌ 魔法球容器或预制体未设置！");
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
                    orbScript = orbGO.AddComponent<SimpleManaOrb>();
                }

                // 确保魔法球处于满状态
                orbScript.ResetToFull();

                // 添加到列表
                manaOrbs.Add(orbScript);

                // 设置名称便于调试
                orbGO.name = $"ManaOrb_{i + 1}";
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
                for (int i = manaOrbsContainer.childCount - 1; i >= 0; i--)
                {
                    Transform child = manaOrbsContainer.GetChild(i);
                    if (child != null)
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }

            currentMaxMana = 0;
        }

        /// <summary>
        /// 更新魔法值显示
        /// </summary>
        private void UpdateMana(int currentMana, int maxMana)
        {
            // 如果魔法球数量不匹配或为空，重新创建
            if (manaOrbs.Count != maxMana || currentMaxMana != maxMana || manaOrbs.Count == 0)
            {
                CreateManaOrbs(maxMana);
            }

            // 更新魔法球状态
            UpdateManaOrbsDisplay(currentMana, maxMana);
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
                    }
                }
                else
                {
                    // 这个魔法球应该是空的
                    if (!manaOrbs[i].IsEmpty())
                    {
                        // 如果当前是满的，播放变空动画
                        manaOrbs[i].PlayEmptyAnimation();
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
            // 显示Bad End面板
            if (badEndPanel != null)
            {
                StartCoroutine(ShowGameEndPanelWithFade(badEndPanel));
            }
            else if (gameOverPanel != null)
            {
                StartCoroutine(ShowGameEndPanelWithFade(gameOverPanel));
            }

            // 解锁鼠标以便点击UI
            UnlockCursorForUI();
        }

        /// <summary>
        /// 显示Happy End界面
        /// </summary>
        private void ShowHappyEnd()
        {
            // 显示Happy End面板
            if (happyEndPanel != null)
            {
                StartCoroutine(ShowGameEndPanelWithFade(happyEndPanel));
            }

            // 解锁鼠标以便点击UI
            UnlockCursorForUI();
        }

        /// <summary>
        /// 处理通用游戏结束事件
        /// </summary>
        private void HandleGameEnded(string endType)
        {
            // 游戏结束时恢复检测UI状态
            RestoreDetectionUIState();
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
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / gameEndFadeTime);
                    yield return null;
                }

                canvasGroup.alpha = 1f;
            }
        }

        /// <summary>
        /// 隐藏所有游戏结束面板
        /// </summary>
        private void HideAllGameEndPanels()
        {
            SetPanel(gameOverPanel, false);
            SetPanel(happyEndPanel, false);
            SetPanel(badEndPanel, false);
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
        }

        #endregion

        #region UI面板管理

        private void TogglePauseMenu(bool isPaused)
        {
            SetPanel(pausePanel, isPaused);
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

                // 清除现有魔法球
                ClearManaOrbs();

                // 重新创建魔法球
                CreateManaOrbs(maxMana);

                // 更新魔法球状态
                UpdateManaOrbsDisplay(currentMana, maxMana);
            }

            // 重置检测UI到默认状态
            if (crosshair != null) crosshair.SetActive(true);
            if (magicCircle != null) magicCircle.SetActive(false);
            isDetectionUIActive = false;

            //切换到游戏音乐
            AudioManager.Instance?.OnShowGameHUD();
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
            if (crosshair != null) crosshair.SetActive(true);
            if (magicCircle != null) magicCircle.SetActive(false);
            isDetectionUIActive = false;

            // 切换到主菜单音乐
            AudioManager.Instance?.OnShowMainMenu();
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
        }

        #endregion

        #region 重启游戏

        /// <summary>
        /// 重启游戏（确保删除旧实例）
        /// </summary>
        private void RestartGame()
        {
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
        }

        #endregion
    }
}