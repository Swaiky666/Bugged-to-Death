using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BugFixerGame
{
    // Bug����ö�٣��򻯰棩
    public enum BugType
    {
        None,
        ObjectMissing,      // ��Ʒȱʧ
        ObjectAdded,        // ������Ʒ
        ObjectMoved,        // ��Ʒλ�øı�
        MaterialMissing,    // ���ʶ�ʧ����ɫ��
        CodeEffect,         // ������Ч�ҷ�
        ClippingBug,        // ��ģbug�������ڵ��
        ExtraEyes          // ������۾�
    }

    // �����������ݣ�Ԥ�����ã�
    [System.Serializable]
    public class RoomConfig
    {
        public int roomId;
        public GameObject normalRoomPrefab;     // ��������Ԥ��
        public GameObject buggyRoomPrefab;      // ��Bug�ķ���Ԥ�裨��ѡ��
        public bool hasBug;                     // ��������Ƿ�̶���Bug
        public BugType bugType;                 // �̶���Bug����

        [TextArea(2, 4)]
        public string bugDescription;           // Bug���������ڵ��ԣ�
    }

    public class RoomManager : MonoBehaviour
    {
        [Header("��������")]
        [SerializeField] private Transform roomContainer;           // ��������
        [SerializeField] private Transform originalRoomParent;      // ԭʼ���丸����
        [SerializeField] private Transform currentRoomParent;       // ��ǰ���丸����

        [Header("��������")]
        [SerializeField] private RoomConfig[] roomConfigs;          // ������������

        [Header("��ЧԤ��")]
        [SerializeField] private Material missingTextureMaterial;   // ��ɫ����
        [SerializeField] private GameObject codeEffectPrefab;       // ������ЧԤ��
        [SerializeField] private GameObject extraEyesPrefab;        // �����۾�Ԥ��

        // ��ǰ����״̬
        private RoomConfig currentRoomConfig;
        private GameObject currentOriginalRoom;
        private GameObject currentDisplayRoom;
        private int currentRoomIndex = -1;

        // ����
        public static RoomManager Instance { get; private set; }

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
            // ��ʼ������
            SetupContainers();

            Debug.Log("RoomManager��ʼ�����");
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

                case GameState.RoomResult:
                    // ���ֵ�ǰ��ʾ���������������Bugλ��
                    break;
            }
        }

        #endregion

        #region �������ɺͼ���

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

            // ���ص�ǰ���䣨����shouldShowBugVersion������ʾ�ĸ��汾��
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
        }

        private void LoadCurrentRoom(bool shouldShowBugVersion)
        {
            GameObject prefabToUse;

            // ����ʹ���ĸ�Ԥ��
            if (shouldShowBugVersion && currentRoomConfig.hasBug && currentRoomConfig.buggyRoomPrefab != null)
            {
                // ʹ��Ԥ�Ƶ�Bug�汾
                prefabToUse = currentRoomConfig.buggyRoomPrefab;
                Debug.Log($"ʹ��Bug�汾���䣬Bug����: {currentRoomConfig.bugType}");
            }
            else
            {
                // ʹ�������汾
                prefabToUse = currentRoomConfig.normalRoomPrefab;
                Debug.Log("ʹ�������汾����");
            }

            // ʵ������ǰ����
            currentDisplayRoom = Instantiate(prefabToUse, currentRoomParent);
            currentDisplayRoom.name = $"CurrentRoom_{currentRoomIndex}_{(shouldShowBugVersion ? "Bug" : "Normal")}";
            currentDisplayRoom.SetActive(false); // ��ʼ����
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

            // ֻ�е�ǰ��ʾ����Bug�汾��������ȷʵ��Bugʱ�ŷ���true
            return currentRoomConfig.hasBug && IsShowingBugVersion();
        }

        private bool IsShowingBugVersion()
        {
            // ͨ�����������жϵ�ǰ��ʾ���Ƿ���Bug�汾
            return currentDisplayRoom != null && currentDisplayRoom.name.Contains("Bug");
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

            // ���ٵ�ǰBug�汾����
            if (currentDisplayRoom != null)
            {
                DestroyImmediate(currentDisplayRoom);
            }

            // ���¼��������汾
            currentDisplayRoom = Instantiate(currentRoomConfig.normalRoomPrefab, currentRoomParent);
            currentDisplayRoom.name = $"CurrentRoom_{currentRoomIndex}_Fixed";
            currentDisplayRoom.SetActive(true);

            Debug.Log("Bug���������ʾ�����汾����");
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

        #endregion

        #region ����

        private void ClearCurrentRooms()
        {
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

            GUILayout.BeginArea(new Rect(10, 250, 500, 300));
            GUILayout.Label("=== Room Manager Debug ===");

            if (currentRoomConfig != null)
            {
                GUILayout.Label($"��ǰ����: {currentRoomIndex}");
                GUILayout.Label($"������Bug: {currentRoomConfig.hasBug}");
                GUILayout.Label($"Bug����: {currentRoomConfig.bugType}");
                GUILayout.Label($"Bug����: {currentRoomConfig.bugDescription}");
                GUILayout.Label($"��ǰ��ʾBug�汾: {IsShowingBugVersion()}");

                GUILayout.Space(10);

                if (GUILayout.Button("��ʾԭʼ����"))
                {
                    ShowOriginalRoom();
                }

                if (GUILayout.Button("��ʾ��ǰ����"))
                {
                    ShowCurrentRoom();
                }

                if (GUILayout.Button("���Bug"))
                {
                    ClearCurrentRoomBugs();
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

        // ��֤�������õ�������
        [ContextMenu("��֤��������")]
        private void ValidateRoomConfigs()
        {
            if (roomConfigs == null || roomConfigs.Length == 0)
            {
                Debug.LogWarning("û�������κη��䣡");
                return;
            }

            for (int i = 0; i < roomConfigs.Length; i++)
            {
                RoomConfig config = roomConfigs[i];

                if (config.normalRoomPrefab == null)
                {
                    Debug.LogError($"���� {i} ȱ�������汾Ԥ�裡");
                }

                if (config.hasBug && config.buggyRoomPrefab == null)
                {
                    Debug.LogWarning($"���� {i} �����Bug��û��Bug�汾Ԥ�裬��ʹ�������汾��");
                }

                if (config.hasBug && string.IsNullOrEmpty(config.bugDescription))
                {
                    Debug.LogWarning($"���� {i} ��Bug��û��������");
                }
            }

            Debug.Log($"����������֤��ɣ��� {roomConfigs.Length} ������");
        }

        #endregion
    }
}