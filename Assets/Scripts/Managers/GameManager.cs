using System;
using System.Collections;
using UnityEngine;

namespace BugFixerGame
{
    // 游戏状态枚举
    public enum GameState
    {
        MainMenu,           // 主菜单
        GameStart,          // 游戏开始过场
        MemoryPhase,        // 记忆阶段（观察正确房间）
        TransitionToCheck,  // 过渡到检测阶段
        CheckPhase,         // 检测阶段（寻找bug）
        RoomResult,         // 单个房间结果反馈
        GameEnd,            // 游戏结束
        Paused              // 暂停
    }

    // 房间结果类型
    public enum RoomResult
    {
        Perfect,        // 完美（正确识别bug状态）
        Wrong,          // 错误（误判或漏判）
        Timeout         // 超时（如果有时间限制）
    }

    // 游戏结局类型
    public enum GameEnding
    {
        Perfect,        // 7-10分 完美结局
        Good,           // 6分 良好结局  
        Bad             // 0-5分 坏结局
    }

    public class GameManager : MonoBehaviour
    {
        [Header("游戏设置")]
        [SerializeField] private int totalRooms = 10;           // 总房间数
        [SerializeField] private float memoryTime = 5f;         // 记忆阶段时间
        [SerializeField] private float transitionTime = 1f;     // 过渡时间

        [Header("分数设置")]
        [SerializeField] private int perfectScore = 1;          // 正确判断得分
        [SerializeField] private int wrongPenalty = -1;         // 错误判断扣分

        // 当前游戏状态
        private GameState currentState;
        private int currentRoomIndex = 0;
        private int currentScore = 0;
        private bool currentRoomHasBug = false;
        private bool playerDetectedBug = false;

        // 游戏数据
        private GameData gameData;

        // 事件系统
        public static event Action<GameState> OnGameStateChanged;
        public static event Action<int> OnScoreChanged;
        public static event Action<int, int> OnRoomProgressChanged; // (current, total)
        public static event Action<RoomResult, int> OnRoomCompleted; // (result, score)
        public static event Action<GameEnding, int> OnGameCompleted; // (ending, finalScore)

        // 单例模式
        public static GameManager Instance { get; private set; }

        #region Unity生命周期

        private void Awake()
        {
            // 单例模式实现
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeGameManager();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            ChangeState(GameState.MainMenu);
        }

        private void Update()
        {
            HandleInput();
            UpdateCurrentState();
        }

        #endregion

        #region 初始化

        private void InitializeGameManager()
        {
            gameData = new GameData();
            Debug.Log("GameManager初始化完成");
        }

        #endregion

        #region 游戏状态管理

        public void ChangeState(GameState newState)
        {
            if (currentState == newState) return;

            ExitCurrentState();
            currentState = newState;
            EnterNewState();

            OnGameStateChanged?.Invoke(currentState);
            Debug.Log($"游戏状态切换至: {currentState}");
        }

        private void ExitCurrentState()
        {
            switch (currentState)
            {
                case GameState.MemoryPhase:
                    StopAllCoroutines(); // 停止记忆阶段计时器
                    break;
                case GameState.CheckPhase:
                    // 清理检测阶段的UI状态
                    break;
            }
        }

        private void EnterNewState()
        {
            switch (currentState)
            {
                case GameState.MainMenu:
                    ResetGameData();
                    break;

                case GameState.GameStart:
                    StartCoroutine(PlayStartSequence());
                    break;

                case GameState.MemoryPhase:
                    StartMemoryPhase();
                    break;

                case GameState.TransitionToCheck:
                    StartCoroutine(TransitionToCheckPhase());
                    break;

                case GameState.CheckPhase:
                    StartCheckPhase();
                    break;

                case GameState.RoomResult:
                    ShowRoomResult();
                    break;

                case GameState.GameEnd:
                    ShowGameEndResult();
                    break;
            }
        }

        private void UpdateCurrentState()
        {
            switch (currentState)
            {
                case GameState.CheckPhase:
                    // 在检测阶段处理玩家输入
                    break;
            }
        }

        #endregion

        #region 游戏流程控制

        public void StartNewGame()
        {
            ResetGameData();
            ChangeState(GameState.GameStart);
        }

        private void ResetGameData()
        {
            currentRoomIndex = 0;
            currentScore = 0;
            playerDetectedBug = false;
            OnScoreChanged?.Invoke(currentScore);
            OnRoomProgressChanged?.Invoke(currentRoomIndex + 1, totalRooms);
        }

        private IEnumerator PlayStartSequence()
        {
            // 播放开场动画/过场
            Debug.Log("播放开场序列...");
            yield return new WaitForSeconds(2f); // 模拟开场时间

            StartNextRoom();
        }

        private void StartNextRoom()
        {
            if (currentRoomIndex >= totalRooms)
            {
                ChangeState(GameState.GameEnd);
                return;
            }

            currentRoomIndex++;
            OnRoomProgressChanged?.Invoke(currentRoomIndex, totalRooms);

            // 生成当前房间数据（包括是否有bug）
            GenerateRoomData();

            ChangeState(GameState.MemoryPhase);
        }

        private void GenerateRoomData()
        {
            // 随机决定当前房间是否有bug
            currentRoomHasBug = UnityEngine.Random.Range(0f, 1f) < 0.7f; // 70%概率有bug
            playerDetectedBug = false;

            Debug.Log($"房间 {currentRoomIndex}: {(currentRoomHasBug ? "有Bug" : "无Bug")}");

            // 这里可以通知RoomManager生成对应的房间
            // RoomManager.Instance?.GenerateRoom(currentRoomIndex, currentRoomHasBug);
        }

        private void StartMemoryPhase()
        {
            Debug.Log("开始记忆阶段");
            StartCoroutine(MemoryTimer());
        }

        private IEnumerator MemoryTimer()
        {
            yield return new WaitForSeconds(memoryTime);
            ChangeState(GameState.TransitionToCheck);
        }

        private IEnumerator TransitionToCheckPhase()
        {
            Debug.Log("过渡到检测阶段...");
            yield return new WaitForSeconds(transitionTime);
            ChangeState(GameState.CheckPhase);
        }

        private void StartCheckPhase()
        {
            Debug.Log("开始检测阶段 - 按空格键清除Bug，右键进入下一房间");
            // 这里可以显示检测阶段的UI提示
        }

        #endregion

        #region 输入处理

        private void HandleInput()
        {
            if (currentState == GameState.CheckPhase)
            {
                // 空格键 - 清除Bug
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    OnPlayerDetectBug();
                }

                // 右键或特定键 - 进入下一房间
                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Return))
                {
                    OnPlayerProceedToNextRoom();
                }
            }

            // ESC键 - 暂停游戏
            if (Input.GetKeyDown(KeyCode.Escape) && currentState != GameState.MainMenu)
            {
                TogglePause();
            }
        }

        public void OnPlayerDetectBug()
        {
            if (currentState != GameState.CheckPhase) return;

            playerDetectedBug = true;
            Debug.Log("玩家按下空格键 - 尝试清除Bug");

            // 立即处理房间结果
            ProcessRoomResult();
        }

        public void OnPlayerProceedToNextRoom()
        {
            if (currentState != GameState.CheckPhase) return;

            Debug.Log("玩家选择进入下一房间");

            // 处理房间结果
            ProcessRoomResult();
        }

        #endregion

        #region 分数和结果系统

        private void ProcessRoomResult()
        {
            RoomResult result;
            int scoreChange = 0;

            // 判断玩家的行为是否正确
            if ((currentRoomHasBug && playerDetectedBug) || (!currentRoomHasBug && !playerDetectedBug))
            {
                // 正确判断
                result = RoomResult.Perfect;
                scoreChange = perfectScore;
            }
            else
            {
                // 错误判断
                result = RoomResult.Wrong;
                scoreChange = wrongPenalty;
            }

            // 更新分数
            currentScore += scoreChange;
            currentScore = Mathf.Max(0, currentScore); // 确保分数不为负数

            OnScoreChanged?.Invoke(currentScore);
            OnRoomCompleted?.Invoke(result, scoreChange);

            Debug.Log($"房间结果: {result}, 分数变化: {scoreChange}, 当前总分: {currentScore}");

            ChangeState(GameState.RoomResult);
        }

        private void ShowRoomResult()
        {
            // 显示单个房间的结果反馈
            StartCoroutine(RoomResultSequence());
        }

        private IEnumerator RoomResultSequence()
        {
            // 显示结果UI
            yield return new WaitForSeconds(2f); // 显示结果的时间

            // 继续下一个房间或结束游戏
            StartNextRoom();
        }

        private void ShowGameEndResult()
        {
            GameEnding ending = DetermineGameEnding(currentScore);
            OnGameCompleted?.Invoke(ending, currentScore);

            Debug.Log($"游戏结束! 最终分数: {currentScore}, 结局: {ending}");
        }

        private GameEnding DetermineGameEnding(int finalScore)
        {
            if (finalScore >= 7)
                return GameEnding.Perfect;
            else if (finalScore >= 6)
                return GameEnding.Good;
            else
                return GameEnding.Bad;
        }

        #endregion

        #region 暂停系统

        public void TogglePause()
        {
            if (currentState == GameState.Paused)
            {
                // 恢复游戏
                Time.timeScale = 1f;
                ChangeState(gameData.stateBeforePause);
            }
            else
            {
                // 暂停游戏
                gameData.stateBeforePause = currentState;
                Time.timeScale = 0f;
                ChangeState(GameState.Paused);
            }
        }

        public void ResumeGame()
        {
            if (currentState == GameState.Paused)
            {
                Time.timeScale = 1f;
                ChangeState(gameData.stateBeforePause);
            }
        }

        #endregion

        #region 公共接口

        // 获取当前游戏状态
        public GameState GetCurrentState() => currentState;

        // 获取当前分数
        public int GetCurrentScore() => currentScore;

        // 获取当前房间进度
        public (int current, int total) GetRoomProgress() => (currentRoomIndex, totalRooms);

        // 强制结束游戏
        public void ForceEndGame()
        {
            ChangeState(GameState.GameEnd);
        }

        // 重启游戏
        public void RestartGame()
        {
            StartNewGame();
        }

        // 返回主菜单
        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f; // 确保时间恢复正常
            ChangeState(GameState.MainMenu);
        }

        #endregion

        #region 调试功能

        [Header("调试功能")]
        [SerializeField] private bool enableDebugKeys = true;

        private void OnGUI()
        {
            if (!enableDebugKeys) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label($"状态: {currentState}");
            GUILayout.Label($"房间: {currentRoomIndex}/{totalRooms}");
            GUILayout.Label($"分数: {currentScore}");
            GUILayout.Label($"当前房间有Bug: {currentRoomHasBug}");

            if (GUILayout.Button("强制下一房间"))
            {
                StartNextRoom();
            }

            if (GUILayout.Button("添加分数"))
            {
                currentScore++;
                OnScoreChanged?.Invoke(currentScore);
            }

            GUILayout.EndArea();
        }

        #endregion
    }

    // 游戏数据类
    [System.Serializable]
    public class GameData
    {
        public GameState stateBeforePause;

        // 可以添加更多需要保存的游戏数据
        public int highScore;
        public int gamesPlayed;
    }
}