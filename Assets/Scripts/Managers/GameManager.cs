using System;
using System.Collections;
using UnityEngine;

namespace BugFixerGame
{
    // ��Ϸ״̬ö��
    public enum GameState
    {
        MainMenu,           // ���˵�
        GameStart,          // ��Ϸ��ʼ����
        MemoryPhase,        // ����׶Σ��۲���ȷ���䣩
        TransitionToCheck,  // ���ɵ����׶�
        CheckPhase,         // ���׶Σ�Ѱ��bug��
        RoomResult,         // ��������������
        GameEnd,            // ��Ϸ����
        Paused              // ��ͣ
    }

    // ����������
    public enum RoomResult
    {
        Perfect,        // ��������ȷʶ��bug״̬��
        Wrong,          // �������л�©�У�
        Timeout         // ��ʱ�������ʱ�����ƣ�
    }

    // ��Ϸ�������
    public enum GameEnding
    {
        Perfect,        // 7-10�� �������
        Good,           // 6�� ���ý��  
        Bad             // 0-5�� �����
    }

    public class GameManager : MonoBehaviour
    {
        [Header("��Ϸ����")]
        [SerializeField] private int totalRooms = 10;           // �ܷ�����
        [SerializeField] private float memoryTime = 5f;         // ����׶�ʱ��
        [SerializeField] private float transitionTime = 1f;     // ����ʱ��

        [Header("��������")]
        [SerializeField] private int perfectScore = 1;          // ��ȷ�жϵ÷�
        [SerializeField] private int wrongPenalty = -1;         // �����жϿ۷�

        // ��ǰ��Ϸ״̬
        private GameState currentState;
        private int currentRoomIndex = 0;
        private int currentScore = 0;
        private bool currentRoomHasBug = false;
        private bool playerDetectedBug = false;

        // ��Ϸ����
        private GameData gameData;

        // �¼�ϵͳ
        public static event Action<GameState> OnGameStateChanged;
        public static event Action<int> OnScoreChanged;
        public static event Action<int, int> OnRoomProgressChanged; // (current, total)
        public static event Action<RoomResult, int> OnRoomCompleted; // (result, score)
        public static event Action<GameEnding, int> OnGameCompleted; // (ending, finalScore)

        // ����ģʽ
        public static GameManager Instance { get; private set; }

        #region Unity��������

        private void Awake()
        {
            // ����ģʽʵ��
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

        #region ��ʼ��

        private void InitializeGameManager()
        {
            gameData = new GameData();
            Debug.Log("GameManager��ʼ�����");
        }

        #endregion

        #region ��Ϸ״̬����

        public void ChangeState(GameState newState)
        {
            if (currentState == newState) return;

            ExitCurrentState();
            currentState = newState;
            EnterNewState();

            OnGameStateChanged?.Invoke(currentState);
            Debug.Log($"��Ϸ״̬�л���: {currentState}");
        }

        private void ExitCurrentState()
        {
            switch (currentState)
            {
                case GameState.MemoryPhase:
                    StopAllCoroutines(); // ֹͣ����׶μ�ʱ��
                    break;
                case GameState.CheckPhase:
                    // ������׶ε�UI״̬
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
                    // �ڼ��׶δ����������
                    break;
            }
        }

        #endregion

        #region ��Ϸ���̿���

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
            // ���ſ�������/����
            Debug.Log("���ſ�������...");
            yield return new WaitForSeconds(2f); // ģ�⿪��ʱ��

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

            // ���ɵ�ǰ�������ݣ������Ƿ���bug��
            GenerateRoomData();

            ChangeState(GameState.MemoryPhase);
        }

        private void GenerateRoomData()
        {
            // ���������ǰ�����Ƿ���bug
            currentRoomHasBug = UnityEngine.Random.Range(0f, 1f) < 0.7f; // 70%������bug
            playerDetectedBug = false;

            Debug.Log($"���� {currentRoomIndex}: {(currentRoomHasBug ? "��Bug" : "��Bug")}");

            // �������֪ͨRoomManager���ɶ�Ӧ�ķ���
            // RoomManager.Instance?.GenerateRoom(currentRoomIndex, currentRoomHasBug);
        }

        private void StartMemoryPhase()
        {
            Debug.Log("��ʼ����׶�");
            StartCoroutine(MemoryTimer());
        }

        private IEnumerator MemoryTimer()
        {
            yield return new WaitForSeconds(memoryTime);
            ChangeState(GameState.TransitionToCheck);
        }

        private IEnumerator TransitionToCheckPhase()
        {
            Debug.Log("���ɵ����׶�...");
            yield return new WaitForSeconds(transitionTime);
            ChangeState(GameState.CheckPhase);
        }

        private void StartCheckPhase()
        {
            Debug.Log("��ʼ���׶� - ���ո�����Bug���Ҽ�������һ����");
            // ���������ʾ���׶ε�UI��ʾ
        }

        #endregion

        #region ���봦��

        private void HandleInput()
        {
            if (currentState == GameState.CheckPhase)
            {
                // �ո�� - ���Bug
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    OnPlayerDetectBug();
                }

                // �Ҽ����ض��� - ������һ����
                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Return))
                {
                    OnPlayerProceedToNextRoom();
                }
            }

            // ESC�� - ��ͣ��Ϸ
            if (Input.GetKeyDown(KeyCode.Escape) && currentState != GameState.MainMenu)
            {
                TogglePause();
            }
        }

        public void OnPlayerDetectBug()
        {
            if (currentState != GameState.CheckPhase) return;

            playerDetectedBug = true;
            Debug.Log("��Ұ��¿ո�� - �������Bug");

            // ������������
            ProcessRoomResult();
        }

        public void OnPlayerProceedToNextRoom()
        {
            if (currentState != GameState.CheckPhase) return;

            Debug.Log("���ѡ�������һ����");

            // ��������
            ProcessRoomResult();
        }

        #endregion

        #region �����ͽ��ϵͳ

        private void ProcessRoomResult()
        {
            RoomResult result;
            int scoreChange = 0;

            // �ж���ҵ���Ϊ�Ƿ���ȷ
            if ((currentRoomHasBug && playerDetectedBug) || (!currentRoomHasBug && !playerDetectedBug))
            {
                // ��ȷ�ж�
                result = RoomResult.Perfect;
                scoreChange = perfectScore;
            }
            else
            {
                // �����ж�
                result = RoomResult.Wrong;
                scoreChange = wrongPenalty;
            }

            // ���·���
            currentScore += scoreChange;
            currentScore = Mathf.Max(0, currentScore); // ȷ��������Ϊ����

            OnScoreChanged?.Invoke(currentScore);
            OnRoomCompleted?.Invoke(result, scoreChange);

            Debug.Log($"������: {result}, �����仯: {scoreChange}, ��ǰ�ܷ�: {currentScore}");

            ChangeState(GameState.RoomResult);
        }

        private void ShowRoomResult()
        {
            // ��ʾ��������Ľ������
            StartCoroutine(RoomResultSequence());
        }

        private IEnumerator RoomResultSequence()
        {
            // ��ʾ���UI
            yield return new WaitForSeconds(2f); // ��ʾ�����ʱ��

            // ������һ������������Ϸ
            StartNextRoom();
        }

        private void ShowGameEndResult()
        {
            GameEnding ending = DetermineGameEnding(currentScore);
            OnGameCompleted?.Invoke(ending, currentScore);

            Debug.Log($"��Ϸ����! ���շ���: {currentScore}, ���: {ending}");
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

        #region ��ͣϵͳ

        public void TogglePause()
        {
            if (currentState == GameState.Paused)
            {
                // �ָ���Ϸ
                Time.timeScale = 1f;
                ChangeState(gameData.stateBeforePause);
            }
            else
            {
                // ��ͣ��Ϸ
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

        #region �����ӿ�

        // ��ȡ��ǰ��Ϸ״̬
        public GameState GetCurrentState() => currentState;

        // ��ȡ��ǰ����
        public int GetCurrentScore() => currentScore;

        // ��ȡ��ǰ�������
        public (int current, int total) GetRoomProgress() => (currentRoomIndex, totalRooms);

        // ǿ�ƽ�����Ϸ
        public void ForceEndGame()
        {
            ChangeState(GameState.GameEnd);
        }

        // ������Ϸ
        public void RestartGame()
        {
            StartNewGame();
        }

        // �������˵�
        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f; // ȷ��ʱ��ָ�����
            ChangeState(GameState.MainMenu);
        }

        #endregion

        #region ���Թ���

        [Header("���Թ���")]
        [SerializeField] private bool enableDebugKeys = true;

        private void OnGUI()
        {
            if (!enableDebugKeys) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label($"״̬: {currentState}");
            GUILayout.Label($"����: {currentRoomIndex}/{totalRooms}");
            GUILayout.Label($"����: {currentScore}");
            GUILayout.Label($"��ǰ������Bug: {currentRoomHasBug}");

            if (GUILayout.Button("ǿ����һ����"))
            {
                StartNextRoom();
            }

            if (GUILayout.Button("��ӷ���"))
            {
                currentScore++;
                OnScoreChanged?.Invoke(currentScore);
            }

            GUILayout.EndArea();
        }

        #endregion
    }

    // ��Ϸ������
    [System.Serializable]
    public class GameData
    {
        public GameState stateBeforePause;

        // ������Ӹ�����Ҫ�������Ϸ����
        public int highScore;
        public int gamesPlayed;
    }
}