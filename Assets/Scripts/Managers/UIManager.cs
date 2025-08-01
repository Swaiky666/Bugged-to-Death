// 重构后的 UIManager.cs
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BugFixerGame
{
    public class UIManager : MonoBehaviour
    {
        [Header("UI面板")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject pausePanel;

        [Header("UI元素")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button quitGameButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button returnToMenuButton;

        public static UIManager Instance { get; private set; }

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
            GameManager.OnScoreChanged += UpdateScore;
            GameManager.OnPauseStateChanged += TogglePauseMenu;
        }

        private void OnDisable()
        {
            GameManager.OnScoreChanged -= UpdateScore;
            GameManager.OnPauseStateChanged -= TogglePauseMenu;
        }

        private void InitializeUI()
        {
            SetPanel(mainMenuPanel, true);
            SetPanel(hudPanel, false);
            SetPanel(pausePanel, false);

            if (startGameButton)
                startGameButton.onClick.AddListener(() => GameManager.Instance.StartGame());
            if (quitGameButton)
                quitGameButton.onClick.AddListener(() => Application.Quit());
            if (resumeButton)
                resumeButton.onClick.AddListener(() => GameManager.Instance.ResumeGame());
            if (returnToMenuButton)
                returnToMenuButton.onClick.AddListener(() => GameManager.Instance.ReturnToMainMenu());
        }

        public void UpdateScore(int newScore)
        {
            if (scoreText)
                scoreText.text = "分数: " + newScore;
        }

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
        }

        public void ShowMainMenu()
        {
            SetPanel(mainMenuPanel, true);
        }
    }
}
