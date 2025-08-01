// 重构后的 GameManager.cs
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BugFixerGame
{
    public class GameManager : MonoBehaviour
    {
        [Header("设置")]
        [SerializeField] private GameObject gamePrefab;
        [SerializeField] private GameObject mainMenuCamera;
        [SerializeField] private GameObject mainMenuUI;

        [Header("分数设置")]
        [SerializeField] private int bugScore = 1;
        [SerializeField] private int wrongPenalty = -1;

        private GameObject currentGameInstance;
        private int score = 0;
        private bool isPaused = false;

        public static GameManager Instance { get; private set; }

        public static event Action<int> OnScoreChanged;
        public static event Action<bool> OnPauseStateChanged;

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

            score = 0;
            OnScoreChanged?.Invoke(score);
        }

        public void ReturnToMainMenu()
        {
            if (currentGameInstance)
                Destroy(currentGameInstance);

            if (mainMenuCamera) mainMenuCamera.SetActive(true);
            if (mainMenuUI) mainMenuUI.SetActive(true);

            ResumeGame();
        }

        private void HandleObjectDetection(GameObject obj, bool isBug)
        {
            int delta = isBug ? bugScore : wrongPenalty;
            score += delta;
            score = Mathf.Max(0, score);
            OnScoreChanged?.Invoke(score);
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

        public bool IsPaused() => isPaused;
        public int GetScore() => score;
    }
}
