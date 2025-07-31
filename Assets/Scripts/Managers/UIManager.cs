using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BugFixerGame
{
    public class UIManager : MonoBehaviour
    {
        [Header("��ҪUI���")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject gameHUDPanel;
        [SerializeField] private GameObject memoryPhasePanel;
        [SerializeField] private GameObject checkPhasePanel;
        [SerializeField] private GameObject roomResultPanel;
        [SerializeField] private GameObject gameEndPanel;
        [SerializeField] private GameObject pauseMenuPanel;

        [Header("��ϷHUDԪ��")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI roomProgressText;
        [SerializeField] private Slider roomProgressSlider;

        [Header("����׶�UI")]
        [SerializeField] private TextMeshProUGUI memoryInstructionText;
        [SerializeField] private TextMeshProUGUI memoryTimerText;
        [SerializeField] private Image memoryTimerFill;

        [Header("���׶�UI")]
        [SerializeField] private TextMeshProUGUI checkInstructionText;
        [SerializeField] private Button clearBugButton;
        [SerializeField] private Button nextRoomButton;

        [Header("������UI")]
        [SerializeField] private TextMeshProUGUI resultTitleText;
        [SerializeField] private TextMeshProUGUI resultDescriptionText;
        [SerializeField] private TextMeshProUGUI scoreChangeText;
        [SerializeField] private Image resultIcon;
        [SerializeField] private Sprite correctIcon;
        [SerializeField] private Sprite wrongIcon;

        [Header("��Ϸ����UI")]
        [SerializeField] private TextMeshProUGUI finalScoreText;
        [SerializeField] private TextMeshProUGUI endingTitleText;
        [SerializeField] private TextMeshProUGUI endingDescriptionText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;

        [Header("���˵�UI")]
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button quitGameButton;
        [SerializeField] private TextMeshProUGUI gameTitle;

        [Header("��ͣ�˵�UI")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseMainMenuButton;
        [SerializeField] private Button pauseRestartButton;

        [Header("��������")]
        [SerializeField] private float panelFadeTime = 0.3f;
        [SerializeField] private float resultDisplayTime = 2f;

        // UI״̬
        private Coroutine memoryTimerCoroutine;
        private Coroutine resultDisplayCoroutine;

        // ����
        public static UIManager Instance { get; private set; }

        // �¼�
        public static event Action OnStartGameClicked;
        public static event Action OnClearBugClicked;
        public static event Action OnNextRoomClicked;
        public static event Action OnRestartClicked;
        public static event Action OnMainMenuClicked;
        public static event Action OnResumeClicked;
        public static event Action OnQuitGameClicked;

        #region Unity��������

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
            // ����GameManager�¼�
            GameManager.OnGameStateChanged += HandleGameStateChanged;
            GameManager.OnScoreChanged += UpdateScore;
            GameManager.OnRoomProgressChanged += UpdateRoomProgress;
            GameManager.OnRoomCompleted += ShowRoomResult;
            GameManager.OnGameCompleted += ShowGameEndResult;

            // ����RoomManager�¼�
            RoomManager.OnRoomLoaded += HandleRoomLoaded;
        }

        private void OnDisable()
        {
            // ȡ������GameManager�¼�
            GameManager.OnGameStateChanged -= HandleGameStateChanged;
            GameManager.OnScoreChanged -= UpdateScore;
            GameManager.OnRoomProgressChanged -= UpdateRoomProgress;
            GameManager.OnRoomCompleted -= ShowRoomResult;
            GameManager.OnGameCompleted -= ShowGameEndResult;

            // ȡ������RoomManager�¼�
            RoomManager.OnRoomLoaded -= HandleRoomLoaded;
        }

        #endregion

        #region ��ʼ��

        private void InitializeUI()
        {
            SetupButtons();
            InitializeTexts();
            HideAllPanels();

            Debug.Log("UIManager��ʼ�����");
        }

        private void SetupButtons()
        {
            // ���˵���ť
            if (startGameButton != null)
                startGameButton.onClick.AddListener(() => OnStartGameClicked?.Invoke());
            if (quitGameButton != null)
                quitGameButton.onClick.AddListener(() => OnQuitGameClicked?.Invoke());

            // ��Ϸ�ڰ�ť
            if (clearBugButton != null)
                clearBugButton.onClick.AddListener(() => OnClearBugClicked?.Invoke());
            if (nextRoomButton != null)
                nextRoomButton.onClick.AddListener(() => OnNextRoomClicked?.Invoke());

            // �������水ť
            if (restartButton != null)
                restartButton.onClick.AddListener(() => OnRestartClicked?.Invoke());
            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(() => OnMainMenuClicked?.Invoke());

            // ��ͣ�˵���ť
            if (resumeButton != null)
                resumeButton.onClick.AddListener(() => OnResumeClicked?.Invoke());
            if (pauseMainMenuButton != null)
                pauseMainMenuButton.onClick.AddListener(() => OnMainMenuClicked?.Invoke());
            if (pauseRestartButton != null)
                pauseRestartButton.onClick.AddListener(() => OnRestartClicked?.Invoke());
        }

        private void InitializeTexts()
        {
            // ����Ĭ���ı�
            if (gameTitle != null)
                gameTitle.text = "Bug�޸�ʦ";

            if (memoryInstructionText != null)
                memoryInstructionText.text = "��ϸ�۲췿�䣬��ס����ϸ�ڣ�";

            if (checkInstructionText != null)
                checkInstructionText.text = "Ѱ��Bug�����ո����������Ҽ�������һ����";

            // ��ʼ�������ͽ���
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

        #region ��Ϸ״̬����

        private void HandleGameStateChanged(GameState newState)
        {
            Debug.Log($"UI��Ӧ״̬�仯: {newState}");

            // �������������
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
                    // ������ͨ���¼���������
                    break;

                case GameState.GameEnd:
                    // ��Ϸ����ͨ���¼���������
                    break;

                case GameState.Paused:
                    ShowPauseMenu();
                    break;
            }
        }

        private void HandleRoomLoaded(bool hasBug, BugType bugType)
        {
            Debug.Log($"UI��Ӧ�������: hasBug={hasBug}, bugType={bugType}");

            // ���Ը��ݷ�����Ϣ����UI��ʾ
            if (checkInstructionText != null)
            {
                checkInstructionText.text = "Ѱ��Bug�����ո����������Ҽ�������һ����";
            }
        }

        #endregion

        #region �����ʾ����

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

            // ��ʼ����׶μ�ʱ��
            if (memoryTimerCoroutine != null)
                StopCoroutine(memoryTimerCoroutine);
            memoryTimerCoroutine = StartCoroutine(MemoryTimerCountdown(5f)); // 5�����ʱ��
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

        #region ���ݸ���

        private void UpdateScore(int newScore)
        {
            if (scoreText != null)
            {
                scoreText.text = $"����: {newScore}";

                // ��ӷ����仯����
                StartCoroutine(ScoreChangeAnimation());
            }
        }

        private void UpdateRoomProgress(int currentRoom, int totalRooms)
        {
            if (roomProgressText != null)
            {
                roomProgressText.text = $"����: {currentRoom}/{totalRooms}";
            }

            if (roomProgressSlider != null)
            {
                roomProgressSlider.maxValue = totalRooms;
                roomProgressSlider.value = currentRoom;
            }
        }

        #endregion

        #region �����ʾ

        private void ShowRoomResult(RoomResult result, int scoreChange)
        {
            if (roomResultPanel == null) return;

            // ���ý������
            SetupRoomResultContent(result, scoreChange);

            // ��ʾ������
            SetPanelActive(roomResultPanel, true);
            StartCoroutine(FadeInPanel(roomResultPanel));

            // �Զ����ؽ�����
            if (resultDisplayCoroutine != null)
                StopCoroutine(resultDisplayCoroutine);
            resultDisplayCoroutine = StartCoroutine(HideResultAfterDelay());
        }

        private void SetupRoomResultContent(RoomResult result, int scoreChange)
        {
            switch (result)
            {
                case RoomResult.Perfect:
                    if (resultTitleText != null) resultTitleText.text = "������";
                    if (resultDescriptionText != null) resultDescriptionText.text = "����ȷʶ���˷���״̬";
                    if (resultIcon != null) resultIcon.sprite = correctIcon;
                    break;

                case RoomResult.Wrong:
                    if (resultTitleText != null) resultTitleText.text = "����";
                    if (resultDescriptionText != null)
                    {
                        string description = scoreChange < 0 ? "�������ж��˷���״̬" : "�ж�����";
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
                finalScoreText.text = $"���շ���: {finalScore}";

            switch (ending)
            {
                case GameEnding.Perfect:
                    if (endingTitleText != null) endingTitleText.text = "������֣�";
                    if (endingDescriptionText != null)
                        endingDescriptionText.text = "����һλ��ɫ��Bug�޸�ʦ����Ϸ�����߿��԰���˯���ˡ�";
                    break;

                case GameEnding.Good:
                    if (endingTitleText != null) endingTitleText.text = "���ý��";
                    if (endingDescriptionText != null)
                        endingDescriptionText.text = "���ò�����Ϸ�����ȶ��������иĽ��ռ䡣";
                    break;

                case GameEnding.Bad:
                    if (endingTitleText != null) endingTitleText.text = "�����";
                    if (endingDescriptionText != null)
                        endingDescriptionText.text = "��Ϸ������Bug...��������������ͷ�۵ġ�";
                    break;
            }
        }

        #endregion

        #region ����Ч��

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

            // �Ŵ�
            float elapsedTime = 0f;
            float animTime = 0.1f;

            while (elapsedTime < animTime)
            {
                elapsedTime += Time.unscaledDeltaTime;
                scoreText.transform.localScale = Vector3.Lerp(originalScale, targetScale, elapsedTime / animTime);
                yield return null;
            }

            // ��С��ԭ����С
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
                // ���¼�ʱ���ı�
                if (memoryTimerText != null)
                {
                    int seconds = Mathf.CeilToInt(remainingTime);
                    memoryTimerText.text = $"ʣ��ʱ��: {seconds}��";
                }

                // ���¼�ʱ�����
                if (memoryTimerFill != null)
                {
                    memoryTimerFill.fillAmount = remainingTime / duration;
                }

                remainingTime -= Time.deltaTime;
                yield return null;
            }

            // ʱ�����
            if (memoryTimerText != null)
                memoryTimerText.text = "�۲���ϣ�";
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

        #region ���߷���

        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
                panel.SetActive(active);
        }

        #endregion

        #region �����ӿ�

        public void ShowNotification(string message, float duration = 2f)
        {
            StartCoroutine(ShowNotificationCoroutine(message, duration));
        }

        private IEnumerator ShowNotificationCoroutine(string message, float duration)
        {
            // �������ʵ��һ���򵥵�֪ͨ��ʾ
            Debug.Log($"֪ͨ: {message}");
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

        // ǿ��ˢ��UI״̬
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

        #region ���Թ���

        [Header("����")]
        [SerializeField] private bool showDebugInfo = true;

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 560, 400, 200));
            GUILayout.Label("=== UI Manager Debug ===");

            GameState currentState = GameManager.Instance?.GetCurrentState() ?? GameState.MainMenu;
            GUILayout.Label($"��ǰ��Ϸ״̬: {currentState}");

            GUILayout.Label("��ǰ����:");
            if (mainMenuPanel && mainMenuPanel.activeInHierarchy) GUILayout.Label("- ���˵�");
            if (gameHUDPanel && gameHUDPanel.activeInHierarchy) GUILayout.Label("- ��ϷHUD");
            if (memoryPhasePanel && memoryPhasePanel.activeInHierarchy) GUILayout.Label("- ����׶�");
            if (checkPhasePanel && checkPhasePanel.activeInHierarchy) GUILayout.Label("- ���׶�");
            if (roomResultPanel && roomResultPanel.activeInHierarchy) GUILayout.Label("- ������");
            if (gameEndPanel && gameEndPanel.activeInHierarchy) GUILayout.Label("- ��Ϸ����");
            if (pauseMenuPanel && pauseMenuPanel.activeInHierarchy) GUILayout.Label("- ��ͣ�˵�");

            if (GUILayout.Button("ˢ��UI"))
            {
                RefreshUI();
            }

            if (GUILayout.Button("����֪ͨ"))
            {
                ShowNotification("����һ������֪ͨ", 3f);
            }

            GUILayout.EndArea();
        }

        #endregion
    }
}