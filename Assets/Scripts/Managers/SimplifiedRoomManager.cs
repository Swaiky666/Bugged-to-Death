using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BugFixerGame
{
    public class SimplifiedRoomManager : MonoBehaviour
    {
        [Header("��������")]
        [SerializeField] private Transform roomContainer;           // ��������
        [SerializeField] private Transform originalRoomParent;      // ԭʼ���丸����
        [SerializeField] private Transform currentRoomParent;       // ��ǰ���丸����

        [Header("��������")]
        [SerializeField] private RoomConfig[] roomConfigs;          // ������������

        // ��ǰ����״̬
        private RoomConfig currentRoomConfig;
        private GameObject currentOriginalRoom;
        private GameObject currentDisplayRoom;
        private int currentRoomIndex = -1;

        // Bug�������
        private List<BugObject> currentBugObjects = new List<BugObject>();

        // ����
        public static SimplifiedRoomManager Instance { get; private set; }

        // �¼�
        public static event Action<int> OnRoomGenerated;            // �����������
        public static event Action<bool, BugType> OnRoomLoaded;     // ���������� (hasBug, bugType)

        #region Unity��������

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeRoomManager();
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
        }

        private void OnDisable()
        {
            // ȡ������
            GameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        #endregion

        #region ��ʼ��

        private void InitializeRoomManager()
        {
            SetupContainers();
            Debug.Log("SimplifiedRoomManager��ʼ�����");
        }

        private void SetupContainers()
        {
            if (roomContainer == null)
            {
                GameObject container = new GameObject("RoomContainer");
                roomContainer = container.transform;
            }

            if (originalRoomParent == null)
            {
                GameObject original = new GameObject("OriginalRoom");
                original.transform.SetParent(roomContainer);
                originalRoomParent = original.transform;
            }

            if (currentRoomParent == null)
            {
                GameObject current = new GameObject("CurrentRoom");
                current.transform.SetParent(roomContainer);
                currentRoomParent = current.transform;
            }
        }

        #endregion

        #region GameManager�¼�����

        private void HandleGameStateChanged(GameState newState)
        {
            switch (newState)
            {
                case GameState.MemoryPhase:
                    ShowOriginalRoom();
                    break;

                case GameState.TransitionToCheck:
                    HideAllRooms();
                    break;

                case GameState.CheckPhase:
                    ShowCurrentRoom();
                    break;
            }
        }

        #endregion

        #region �������

        public void LoadRoom(int roomIndex, bool shouldShowBugVersion)
        {
            if (roomIndex < 0 || roomIndex >= roomConfigs.Length)
            {
                Debug.LogError($"��������������Χ: {roomIndex}");
                return;
            }

            currentRoomIndex = roomIndex;
            currentRoomConfig = roomConfigs[roomIndex];

            Debug.Log($"���ط��� {roomIndex}: {currentRoomConfig.bugDescription}");

            // ����֮ǰ�ķ���
            ClearCurrentRooms();

            // ���������汾���䣨���ڼ���׶Σ�
            LoadOriginalRoom();

            // ���ص�ǰ���䲢����Bug״̬
            LoadCurrentRoom(shouldShowBugVersion);

            // ֪ͨ����ϵͳ
            OnRoomGenerated?.Invoke(roomIndex);
            OnRoomLoaded?.Invoke(shouldShowBugVersion && currentRoomConfig.hasBug,
                               shouldShowBugVersion ? currentRoomConfig.bugType : BugType.None);
        }

        private void LoadOriginalRoom()
        {
            if (currentRoomConfig.normalRoomPrefab == null)
            {
                Debug.LogError($"���� {currentRoomIndex} ȱ�������汾Ԥ��");
                return;
            }

            // ʵ������������
            currentOriginalRoom = Instantiate(currentRoomConfig.normalRoomPrefab, originalRoomParent);
            currentOriginalRoom.name = $"OriginalRoom_{currentRoomIndex}";
            currentOriginalRoom.SetActive(false); // ��ʼ����

            // ȷ��ԭʼ���������BugObject���ǹر�״̬
            SetAllBugObjectsInRoom(currentOriginalRoom, false);
        }

        private void LoadCurrentRoom(bool shouldShowBugVersion)
        {
            // ����ʹ�������汾��Ϊ����
            GameObject prefabToUse = currentRoomConfig.normalRoomPrefab;

            // ʵ������ǰ����
            currentDisplayRoom = Instantiate(prefabToUse, currentRoomParent);
            currentDisplayRoom.name = $"CurrentRoom_{currentRoomIndex}";
            currentDisplayRoom.SetActive(false); // ��ʼ����

            // �ռ������ڵ�����BugObject
            CollectBugObjects();

            // ������Ҫ����BugЧ��
            if (shouldShowBugVersion && currentRoomConfig.hasBug)
            {
                ActivateRoomBugs();
                Debug.Log($"�����Bug: {currentRoomConfig.bugType}");
            }
            else
            {
                DeactivateRoomBugs();
                Debug.Log("������Bug����ʾ�����汾");
            }
        }

        #endregion

        #region BugObject����

        private void CollectBugObjects()
        {
            currentBugObjects.Clear();

            if (currentDisplayRoom != null)
            {
                BugObject[] bugObjects = currentDisplayRoom.GetComponentsInChildren<BugObject>();
                currentBugObjects.AddRange(bugObjects);

                Debug.Log($"�ҵ� {currentBugObjects.Count} ��BugObject");
            }
        }

        private void ActivateRoomBugs()
        {
            foreach (BugObject bugObj in currentBugObjects)
            {
                // ֻ�����뵱ǰ����Bug����ƥ���BugObject
                if (bugObj.GetBugType() == currentRoomConfig.bugType)
                {
                    bugObj.ActivateBug();
                }
            }
        }

        private void DeactivateRoomBugs()
        {
            foreach (BugObject bugObj in currentBugObjects)
            {
                bugObj.DeactivateBug();
            }
        }

        private void SetAllBugObjectsInRoom(GameObject room, bool active)
        {
            BugObject[] bugObjects = room.GetComponentsInChildren<BugObject>();
            foreach (BugObject bugObj in bugObjects)
            {
                if (active)
                    bugObj.ActivateBug();
                else
                    bugObj.DeactivateBug();
            }
        }

        #endregion

        #region ������ʾ����

        private void ShowOriginalRoom()
        {
            HideAllRooms();

            if (currentOriginalRoom != null)
            {
                currentOriginalRoom.SetActive(true);
                Debug.Log("��ʾԭʼ���䣨����׶Σ�");
            }
        }

        private void ShowCurrentRoom()
        {
            HideAllRooms();

            if (currentDisplayRoom != null)
            {
                currentDisplayRoom.SetActive(true);
                Debug.Log("��ʾ��ǰ���䣨���׶Σ�");
            }
        }

        private void HideAllRooms()
        {
            if (currentOriginalRoom != null)
                currentOriginalRoom.SetActive(false);

            if (currentDisplayRoom != null)
                currentDisplayRoom.SetActive(false);
        }

        #endregion

        #region Bug���ʹ���

        public bool CurrentRoomHasBug()
        {
            if (currentRoomConfig == null) return false;

            // ����Ƿ��м����BugObject
            foreach (BugObject bugObj in currentBugObjects)
            {
                if (bugObj.IsBugActive())
                    return true;
            }

            return false;
        }

        public BugType GetCurrentBugType()
        {
            if (currentRoomConfig == null || !CurrentRoomHasBug())
                return BugType.None;

            return currentRoomConfig.bugType;
        }

        public void ClearCurrentRoomBugs()
        {
            if (!CurrentRoomHasBug()) return;

            Debug.Log($"�������Bug: {currentRoomConfig.bugType}");

            // ͣ�����м����BugObject
            DeactivateRoomBugs();

            Debug.Log("����Bug�����");
        }

        #endregion

        #region ������Ϣ��ȡ

        public int GetCurrentRoomIndex()
        {
            return currentRoomIndex;
        }

        public RoomConfig GetCurrentRoomConfig()
        {
            return currentRoomConfig;
        }

        public string GetCurrentRoomBugDescription()
        {
            return currentRoomConfig?.bugDescription ?? "��Bug";
        }

        public int GetTotalRoomCount()
        {
            return roomConfigs?.Length ?? 0;
        }

        public List<BugObject> GetCurrentBugObjects()
        {
            return new List<BugObject>(currentBugObjects);
        }

        #endregion

        #region ����

        private void ClearCurrentRooms()
        {
            currentBugObjects.Clear();

            if (currentOriginalRoom != null)
            {
                DestroyImmediate(currentOriginalRoom);
                currentOriginalRoom = null;
            }

            if (currentDisplayRoom != null)
            {
                DestroyImmediate(currentDisplayRoom);
                currentDisplayRoom = null;
            }
        }

        #endregion

        #region ���Թ���

        [Header("����")]
        [SerializeField] private bool showDebugInfo = true;

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 250, 500, 400));
            GUILayout.Label("=== Simplified Room Manager Debug ===");

            if (currentRoomConfig != null)
            {
                GUILayout.Label($"��ǰ����: {currentRoomIndex}");
                GUILayout.Label($"������Bug: {currentRoomConfig.hasBug}");
                GUILayout.Label($"Bug����: {currentRoomConfig.bugType}");
                GUILayout.Label($"Bug����: {currentRoomConfig.bugDescription}");
                GUILayout.Label($"BugObject����: {currentBugObjects.Count}");

                // ��ʾ��ǰ�����BugObject
                int activeBugs = 0;
                foreach (BugObject bugObj in currentBugObjects)
                {
                    if (bugObj.IsBugActive())
                        activeBugs++;
                }
                GUILayout.Label($"�����Bug: {activeBugs}");

                GUILayout.Space(10);

                if (GUILayout.Button("��ʾԭʼ����"))
                {
                    ShowOriginalRoom();
                }

                if (GUILayout.Button("��ʾ��ǰ����"))
                {
                    ShowCurrentRoom();
                }

                if (GUILayout.Button("��������Bug"))
                {
                    ActivateRoomBugs();
                }

                if (GUILayout.Button("�������Bug"))
                {
                    ClearCurrentRoomBugs();
                }

                GUILayout.Space(10);

                // ��ʾÿ��BugObject��״̬
                if (currentBugObjects.Count > 0)
                {
                    GUILayout.Label("BugObject�б�:");
                    foreach (BugObject bugObj in currentBugObjects)
                    {
                        string status = bugObj.IsBugActive() ? "ON" : "OFF";
                        GUILayout.Label($"- {bugObj.name}: {bugObj.GetBugType()} [{status}]");
                    }
                }

                GUILayout.Space(10);
                GUILayout.Label("���ٲ��Է���:");
                GUILayout.BeginHorizontal();
                for (int i = 0; i < Mathf.Min(roomConfigs.Length, 5); i++)
                {
                    if (GUILayout.Button($"����{i}"))
                    {
                        LoadRoom(i, roomConfigs[i].hasBug);
                    }
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("û�м��ط���");

                if (roomConfigs != null && roomConfigs.Length > 0)
                {
                    if (GUILayout.Button("���ص�һ������"))
                    {
                        LoadRoom(0, roomConfigs[0].hasBug);
                    }
                }
            }

            GUILayout.EndArea();
        }

        #endregion
    }
}