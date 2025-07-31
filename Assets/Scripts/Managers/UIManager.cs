using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BugFixerGame
{
    public class UIManager : MonoBehaviour
    {
        [Header("主要UI面板")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject gameHUDPanel;
        [SerializeField] private GameObject memoryPhasePanel;
        [SerializeField] private GameObject checkPhasePanel;
        [SerializeField] private GameObject roomResultPanel;
        [SerializeField] private GameObject gameEndPanel;
        [SerializeField] private GameObject pauseMenuPanel;

        [Header("游戏HUD元素")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI roomProgressText;
        [SerializeField] private Slider roomProgressSlider;

        [Header("记忆阶段UI")]
        [SerializeField] private TextMeshProUGUI memoryInstructionText;
        [SerializeField] private TextMeshProUGUI memoryTimerText;
        [SerializeField] private Image memoryTimerFill;

        [Header("检测阶段UI")]
        [SerializeField] private TextMeshProUGUI checkInstructionText;
        [SerializeField] private Button clearBugButton;
        [SerializeField] private Button nextRoomButton;

        [Header("房间结果UI")]
        [SerializeField] private TextMeshProUGUI resultTitleText;
        [SerializeField] private TextMeshProUGUI resultDescriptionText;
        [SerializeField] private TextMeshProUGUI scoreChangeText;
        [SerializeField] private Image resultIcon;
        [SerializeField] private Sprite correctIcon;
        [SerializeField] private Sprite wrongIcon;

        [Header("游戏结束UI")]
        [SerializeField] private TextMeshProUGUI finalScoreText;
        [SerializeField] private TextMeshProUGUI endingTitleText;
        [SerializeField] private TextMeshProUGUI endingDescriptionText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;

        [Header("主菜单UI")]
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button quitGameButton;
        [SerializeField] private TextMeshProUGUI gameTitle;

        [Header("暂停菜单UI")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseMainMenuButton;
        [SerializeField] private Button pauseRestartButton;

        [Header("动画设置")]
        [SerializeField] private float panelFadeTime = 0.3f;
        [SerializeField] private float resultDisplayTime = 2f;

        // UI状态
        private Coroutine memoryTimerCoroutine;
        private Coroutine resultDisplayCoroutine;

        // 单例
        public static UIManager Instance { get; private set; }

        // 事件
        public static event Action OnStartGameClicked;
        public static event Action OnClearBugClicked;
        public static event Action OnNextRoomClicked;
        public static event Action OnRestartClicked;
        public static event Action OnMainMenuClicked;
        public static event Action OnResumeClicked;
        public static event Action OnQuitGameClicked;

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
            // 订阅GameManager事件
            GameManager.OnGameStateChanged += HandleGameStateChanged;
            GameManager.OnScoreChanged += UpdateScore;
            GameManager.OnRoomProgressChanged += UpdateRoomProgress;
            GameManager.OnRoomCompleted += ShowRoomResult;
            GameManager.OnGameCompleted += ShowGameEndResult;

            // 订阅RoomManager事件
            RoomManager.OnRoomLoaded += HandleRoomLoaded;
        }

        private void OnDisable()
        {
            // 取消订阅GameManager事件
            GameManager.OnGameStateChanged -= HandleGameStateChanged;
            GameManager.OnScoreChanged -= UpdateScore;
            GameManager.OnRoomProgressChanged -= UpdateRoomProgress;
            GameManager.OnRoomCompleted -= ShowRoomResult;
            GameManager.OnGameCompleted -= ShowGameEndResult;

            // 取消订阅RoomManager事件
            RoomManager.OnRoomLoaded -= HandleRoomLoaded;
        }

        #endregion

        #region 初始化

        private void InitializeUI()
        {
            SetupButtons();
            InitializeTexts();
            HideAllPanels();

            Debug.Log("UIManager初始化完成");
        }

        private void SetupButtons()
        {
            // 主菜单按钮
            if (startGameButton != null)
                startGameButton.onClick.AddListener(() => OnStartGameClicked?.Invoke());
            if (quitGameButton != null)
                quitGameButton.onClick.AddListener(() => OnQuitGameClicked?.Invoke());

            // 游戏内按钮
            if (clearBugButton != null)
                clearBugButton.onClick.AddListener(() => OnClearBugClicked?.Invoke());
            if (nextRoomButton != null)
                nextRoomButton.onClick.AddListener(() => OnNextRoomClicked?.Invoke());

            // 结束界面按钮
            if (restartButton != null)
                restartButton.onClick.AddListener(() => OnRestartClicked?.Invoke());
            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(() => OnMainMenuClicked?.Invoke());

            // 暂停菜单按钮
            if (resumeButton != null)
                resumeButton.onClick.AddListener(() => OnResumeClicked?.Invoke());
            if (pauseMainMenuButton != null)
                pauseMainMenuButton.onClick.AddListener(() => OnMainMenuClicked?.Invoke());
            if (pauseRestartButton != null)
                pauseRestartButton.onClick.AddListener(() => OnRestartClicked?.Invoke());
        }

        private void InitializeTexts()
        {
            // 设置默认文本
            if (gameTitle != null)
                gameTitle.text = "Bug修复师";

            if (memoryInstructionText != null)
                memoryInstructionText.text = "仔细观察房间，记住所有细节！";

            if (checkInstructionText != null)
                checkInstructionText.text = "寻找Bug并按空格键清除，或右键进入下一房间";

            // 初始化分数和进度
            UpdateScore(0);
            UpdateRoomProgress(1, 10);
        }

        private void HideAllPanels()
        {
            SetPanelActive(mainMenuPanel, false);
            SetPanelActive(gameHUDPanel, false);
            SetPanelActive(memoryPhasePanel, false);
            SetPanelActive(checkPhasePanel, false);
            SetPanelActive(roomResultPanel, false);
            SetPanelActive(gameEndPanel, false);
            SetPanelActive(pauseMenuPanel, false);
        }

        #endregion

        #region 游戏状态处理

        private void HandleGameStateChanged(GameState newState)
        {
            Debug.Log($"UI响应状态变化: {newState}");

            // 先隐藏所有面板
            HideAllGameplayPanels();

            switch (newState)
            {
                case GameState.MainMenu:
                    ShowMainMenu();
                    break;

                case GameState.GameStart:
                    ShowGameHUD();
                    break;

                case GameState.MemoryPhase:
                    ShowMemoryPhase();
                    break;

                case GameState.CheckPhase:
                    ShowCheckPhase();
                    break;

                case GameState.RoomResult:
                    // 房间结果通过事件单独处理
                    break;

                case GameState.GameEnd:
                    // 游戏结束通过事件单独处理
                    break;

                case GameState.Paused:
                    ShowPauseMenu();
                    break;
            }
        }

        private void HandleRoomLoaded(bool hasBug, BugType bugType)
        {
            Debug.Log($"UI响应房间加载: hasBug={hasBug}, bugType={bugType}");

            // 可以根据房间信息更新UI提示
            if (checkInstructionText != null)
            {
                checkInstructionText.text = "寻找Bug并按空格键清除，或右键进入下一房间";
            }
        }

        #endregion

        #region 面板显示控制

        private void ShowMainMenu()
        {
            HideAllPanels();
            SetPanelActive(mainMenuPanel, true);
            StartCoroutine(FadeInPanel(mainMenuPanel));
        }

        private void ShowGameHUD()
        {
            SetPanelActive(gameHUDPanel, true);
        }

        private void ShowMemoryPhase()
        {
            SetPanelActive(memoryPhasePanel, true);
            StartCoroutine(FadeInPanel(memoryPhasePanel));

            // 开始记忆阶段计时器
            if (memoryTimerCoroutine != null)
                StopCoroutine(memoryTimerCoroutine);
            memoryTimerCoroutine = StartCoroutine(MemoryTimerCountdown(5f)); // 5秒记忆时间
        }

        private void ShowCheckPhase()
        {
            SetPanelActive(memoryPhasePanel, false);
            SetPanelActive(checkPhasePanel, true);
            StartCoroutine(FadeInPanel(checkPhasePanel));
        }

        private void ShowPauseMenu()
        {
            SetPanelActive(pauseMenuPanel, true);
            StartCoroutine(FadeInPanel(pauseMenuPanel));
        }

        private void HideAllGameplayPanels()
        {
            SetPanelActive(memoryPhasePanel, false);
            SetPanelActive(checkPhasePanel, false);
            SetPanelActive(roomResultPanel, false);
            SetPanelActive(pauseMenuPanel, false);
        }

        #endregion

        #region 数据更新

        private void UpdateScore(int newScore)
        {
            if (scoreText != null)
            {
                scoreText.text = $"分数: {newScore}";

                // 添加分数变化动画
                StartCoroutine(ScoreChangeAnimation());
            }
        }

        private void UpdateRoomProgress(int currentRoom, int totalRooms)
        {
            if (roomProgressText != null)
            {
                roomProgressText.text = $"房间: {currentRoom}/{totalRooms}";
            }

            if (roomProgressSlider != null)
            {
                roomProgressSlider.maxValue = totalRooms;
                roomProgressSlider.value = currentRoom;
            }
        }

        #endregion

        #region 结果显示

        private void ShowRoomResult(RoomResult result, int scoreChange)
        {
            if (roomResultPanel == null) return;

            // 设置结果内容
            SetupRoomResultContent(result, scoreChange);

            // 显示结果面板
            SetPanelActive(roomResultPanel, true);
            StartCoroutine(FadeInPanel(roomResultPanel));

            // 自动隐藏结果面板
            if (resultDisplayCoroutine != null)
                StopCoroutine(resultDisplayCoroutine);
            resultDisplayCoroutine = StartCoroutine(HideResultAfterDelay());
        }

        private void SetupRoomResultContent(RoomResult result, int scoreChange)
        {
            switch (result)
            {
                case RoomResult.Perfect:
                    if (resultTitleText != null) resultTitleText.text = "完美！";
                    if (resultDescriptionText != null) resultDescriptionText.text = "你正确识别了房间状态";
                    if (resultIcon != null) resultIcon.sprite = correctIcon;
                    break;

                case RoomResult.Wrong:
                    if (resultTitleText != null) resultTitleText.text = "错误";
                    if (resultDescriptionText != null)
                    {
                        string description = scoreChange < 0 ? "你错误地判断了房间状态" : "判断有误";
                        resultDescriptionText.text = description;
                    }
                    if (resultIcon != null) resultIcon.sprite = wrongIcon;
                    break;
            }

            if (scoreChangeText != null)
            {
                string sign = scoreChange > 0 ? "+" : "";
                scoreChangeText.text = $"{sign}{scoreChange}";
                scoreChangeText.color = scoreChange > 0 ? Color.green : Color.red;
            }
        }

        private void ShowGameEndResult(GameEnding ending, int finalScore)
        {
            if (gameEndPanel == null) return;

            HideAllPanels();
            SetupGameEndContent(ending, finalScore);
            SetPanelActive(gameEndPanel, true);
            StartCoroutine(FadeInPanel(gameEndPanel));
        }

        private void SetupGameEndContent(GameEnding ending, int finalScore)
        {
            if (finalScoreText != null)
                finalScoreText.text = $"最终分数: {finalScore}";

            switch (ending)
            {
                case GameEnding.Perfect:
                    if (endingTitleText != null) endingTitleText.text = "完美结局！";
                    if (endingDescriptionText != null)
                        endingDescriptionText.text = "你是一位出色的Bug修复师！游戏开发者可以安心睡觉了。";
                    break;

                case GameEnding.Good:
                    if (endingTitleText != null) endingTitleText.text = "良好结局";
                    if (endingDescriptionText != null)
                        endingDescriptionText.text = "做得不错！游戏基本稳定，但还有改进空间。";
                    break;

                case GameEnding.Bad:
                    if (endingTitleText != null) endingTitleText.text = "糟糕结局";
                    if (endingDescriptionText != null)
                        endingDescriptionText.text = "游戏充满了Bug...开发者醒来后会很头疼的。";
                    break;
            }
        }

        #endregion

        #region 动画效果

        private IEnumerator FadeInPanel(GameObject panel)
        {
            if (panel == null) yield break;

            CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = panel.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 0f;
            float elapsedTime = 0f;

            while (elapsedTime < panelFadeTime)
            {
                elapsedTime += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsedTime / panelFadeTime);
                yield return null;
            }

            canvasGroup.alpha = 1f;
        }

        private IEnumerator ScoreChangeAnimation()
        {
            if (scoreText == null) yield break;

            Vector3 originalScale = scoreText.transform.localScale;
            Vector3 targetScale = originalScale * 1.2f;

            // 放大
            float elapsedTime = 0f;
            float animTime = 0.1f;

            while (elapsedTime < animTime)
            {
                elapsedTime += Time.unscaledDeltaTime;
                scoreText.transform.localScale = Vector3.Lerp(originalScale, targetScale, elapsedTime / animTime);
                yield return null;
            }

            // 缩小回原来大小
            elapsedTime = 0f;
            while (elapsedTime < animTime)
            {
                elapsedTime += Time.unscaledDeltaTime;
                scoreText.transform.localScale = Vector3.Lerp(targetScale, originalScale, elapsedTime / animTime);
                yield return null;
            }

            scoreText.transform.localScale = originalScale;
        }

        private IEnumerator MemoryTimerCountdown(float duration)
        {
            float remainingTime = duration;

            while (remainingTime > 0)
            {
                // 更新计时器文本
                if (memoryTimerText != null)
                {
                    int seconds = Mathf.CeilToInt(remainingTime);
                    memoryTimerText.text = $"剩余时间: {seconds}秒";
                }

                // 更新计时器填充
                if (memoryTimerFill != null)
                {
                    memoryTimerFill.fillAmount = remainingTime / duration;
                }

                remainingTime -= Time.deltaTime;
                yield return null;
            }

            // 时间结束
            if (memoryTimerText != null)
                memoryTimerText.text = "观察完毕！";
        }

        private IEnumerator HideResultAfterDelay()
        {
            yield return new WaitForSeconds(resultDisplayTime);

            if (roomResultPanel != null)
            {
                SetPanelActive(roomResultPanel, false);
            }
        }

        #endregion

        #region 工具方法

        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
                panel.SetActive(active);
        }

        #endregion

        #region 公共接口

        public void ShowNotification(string message, float duration = 2f)
        {
            StartCoroutine(ShowNotificationCoroutine(message, duration));
        }

        private IEnumerator ShowNotificationCoroutine(string message, float duration)
        {
            // 这里可以实现一个简单的通知显示
            Debug.Log($"通知: {message}");
            yield return new WaitForSeconds(duration);
        }

        public void SetMemoryPhaseTime(float time)
        {
            if (memoryTimerCoroutine != null)
            {
                StopCoroutine(memoryTimerCoroutine);
                memoryTimerCoroutine = StartCoroutine(MemoryTimerCountdown(time));
            }
        }

        // 强制刷新UI状态
        public void RefreshUI()
        {
            if (GameManager.Instance != null)
            {
                UpdateScore(GameManager.Instance.GetCurrentScore());
                var progress = GameManager.Instance.GetRoomProgress();
                UpdateRoomProgress(progress.current, progress.total);
            }
        }

        #endregion

        #region 调试功能

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = true;

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 560, 400, 200));
            GUILayout.Label("=== UI Manager Debug ===");

            GameState currentState = GameManager.Instance?.GetCurrentState() ?? GameState.MainMenu;
            GUILayout.Label($"当前游戏状态: {currentState}");

            GUILayout.Label("当前活动面板:");
            if (mainMenuPanel && mainMenuPanel.activeInHierarchy) GUILayout.Label("- 主菜单");
            if (gameHUDPanel && gameHUDPanel.activeInHierarchy) GUILayout.Label("- 游戏HUD");
            if (memoryPhasePanel && memoryPhasePanel.activeInHierarchy) GUILayout.Label("- 记忆阶段");
            if (checkPhasePanel && checkPhasePanel.activeInHierarchy) GUILayout.Label("- 检测阶段");
            if (roomResultPanel && roomResultPanel.activeInHierarchy) GUILayout.Label("- 房间结果");
            if (gameEndPanel && gameEndPanel.activeInHierarchy) GUILayout.Label("- 游戏结束");
            if (pauseMenuPanel && pauseMenuPanel.activeInHierarchy) GUILayout.Label("- 暂停菜单");

            if (GUILayout.Button("刷新UI"))
            {
                RefreshUI();
            }

            if (GUILayout.Button("测试通知"))
            {
                ShowNotification("这是一个测试通知", 3f);
            }

            GUILayout.EndArea();
        }

        #endregion
    }
}