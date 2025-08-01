// UIManager.cs
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BugFixerGame
{
    public class UIManager : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject pausePanel;

        [Header("UI Elements")]
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
            // 初始只显示主菜单
            SetPanel(mainMenuPanel, true);
            SetPanel(hudPanel, false);
            SetPanel(pausePanel, false);

            // 按钮绑定
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
                    // 重锁鼠标
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
        }

        public void UpdateScore(int newScore)
        {
            if (scoreText)
                scoreText.text = "Score: " + newScore;
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

        /// <summary>
        /// 显示主菜单，并隐藏游戏内所有面板
        /// </summary>
        public void ShowMainMenu()
        {
            SetPanel(mainMenuPanel, true);
            SetPanel(hudPanel, false);
            SetPanel(pausePanel, false);
        }
    }
}
