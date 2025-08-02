// InfoDisplayUI.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BugFixerGame
{
    public enum InfoType { Bug, Message, Alert }

    [System.Serializable]
    public class InfoData
    {
        public InfoType Type;
        public string Title;
        public string Description;
        public Sprite BackgroundImage;
        public float DisplayTime;
        public bool IsTemporary;
        public string RoomId;

        public InfoData(
            InfoType type,
            string title,
            string description = null,
            Sprite backgroundImage = null,
            float displayTime = 0f,
            bool isTemporary = false,
            string roomId = ""
        )
        {
            Type = type;
            Title = title;
            Description = description;
            BackgroundImage = backgroundImage;
            DisplayTime = displayTime;
            IsTemporary = isTemporary;
            RoomId = roomId;
        }
    }

    public class InfoDisplayUI : MonoBehaviour
    {
        public static InfoDisplayUI Instance { get; private set; }

        [Header("Panel & Prefab")]
        [SerializeField] private GameObject infoPanel;
        [SerializeField] private GameObject infoItemPrefab;
        [SerializeField] private RectTransform contentParent;

        [Header("Layout Settings")]
        [SerializeField] private float itemHeight = 100f;
        [SerializeField] private float itemSpacing = 10f;

        [Header("Scroll Settings")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private bool enableScrollWheel = true;
        [SerializeField] private float scrollSpeed = 20f;

        [Header("Room & Title")]
        [SerializeField] private TMP_Text roomLabel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private bool enableRoomFiltering = true;
        [SerializeField] private bool showBugCount = true;

        [Header("Background Sprites")]
        [SerializeField] private Sprite bugSprite;
        [SerializeField] private Sprite messageSprite;
        [SerializeField] private Sprite alertSprite;

        [Header("Default Durations")]
        [SerializeField] private float defaultMessageTime = 5f;
        [SerializeField] private float defaultAlertTime = 3f;

        private readonly List<InfoData> currentInfos = new List<InfoData>();
        private readonly List<GameObject> infoItems = new List<GameObject>();

        private string currentRoomId;
        private string lastKnownRoomId;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            UpdateCurrentRoom();
            RefreshBugInfo();
        }

        private void OnEnable()
        {
            MessageTrigger.OnTriggerActivated += HandleTriggerActivated;
            BugObject.OnBugFixed += OnBugFixed;
        }

        private void OnDisable()
        {
            MessageTrigger.OnTriggerActivated -= HandleTriggerActivated;
            BugObject.OnBugFixed -= OnBugFixed;
        }

        private void Update()
        {
            if (enableScrollWheel) HandleScroll();
            if (enableRoomFiltering)
                CheckRoomChange();
        }

        private void HandleScroll()
        {
            if (scrollRect == null) return;
            float delta = Input.GetAxis("Mouse ScrollWheel") * scrollSpeed;
            if (Mathf.Abs(delta) > 0.01f)
            {
                var pos = scrollRect.content.anchoredPosition;
                pos.y += delta;
                scrollRect.content.anchoredPosition = pos;
            }
        }

        public void TogglePanel()
        {
            if (infoPanel == null) return;
            bool show = !infoPanel.activeSelf;
            infoPanel.SetActive(show);
            if (show) RefreshBugInfo();
        }

        private void CheckRoomChange()
        {
            string newRoom = DetermineCurrentRoomId();
            if (newRoom != lastKnownRoomId)
            {
                OnRoomChanged(lastKnownRoomId, newRoom);
                UpdateCurrentRoom();
            }
        }

        private void UpdateCurrentRoom()
        {
            currentRoomId = DetermineCurrentRoomId();
            lastKnownRoomId = currentRoomId;
            if (roomLabel != null)
                roomLabel.text = "Room: " + currentRoomId;
        }

        private string DetermineCurrentRoomId()
        {
            var rs = FindObjectOfType<RoomSystem>();
            var room = rs?.GetCurrentRoom();
            return room != null ? room.currentSequence.ToString() : "Unknown";
        }

        private void OnRoomChanged(string oldRoom, string newRoom)
        {
            // Refresh bug list for the new room and remove old entries
            RefreshBugInfo();
        }

        /// <summary>
        /// Fetches and displays only the bugs in the current room.
        /// </summary>
        public void RefreshBugInfo()
        {
            currentInfos.RemoveAll(i => i.Type == InfoType.Bug);

            var rs = FindObjectOfType<RoomSystem>();
            List<BugObject> bugs = rs != null
                ? rs.GetCurrentRoomBugObjects()
                : new List<BugObject>(FindObjectsOfType<BugObject>());

            foreach (var b in bugs)
            {
                if (!b.ShouldShowInInfoPanel()) continue;
                currentInfos.Add(new InfoData(
                    InfoType.Bug,
                    b.GetBugTitle(),
                    b.GetBugType().ToString(),
                    bugSprite,
                    0f,
                    false,
                    currentRoomId
                ));
            }

            RenderUI();
            UpdateTitle();
        }

        private void OnBugFixed(BugObject bug)
        {
            for (int i = currentInfos.Count - 1; i >= 0; i--)
            {
                if (currentInfos[i].Type == InfoType.Bug &&
                    currentInfos[i].Title == bug.GetBugTitle())
                {
                    currentInfos.RemoveAt(i);
                }
            }
            RenderUI();
            UpdateTitle();
        }

        private void RenderUI()
        {
            foreach (var go in infoItems) Destroy(go);
            infoItems.Clear();

            float totalHeight = currentInfos.Count * (itemHeight + itemSpacing) - itemSpacing;
            contentParent.sizeDelta = new Vector2(
                contentParent.sizeDelta.x,
                Mathf.Max(totalHeight, 0)
            );

            for (int i = 0; i < currentInfos.Count; i++)
            {
                var info = currentInfos[i];
                var go = Instantiate(infoItemPrefab, contentParent);
                infoItems.Add(go);
                var ui = go.GetComponent<InfoItemUI>();
                ui.SetupInfo(info, GetBackgroundSprite(info.Type));

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1);
                rt.sizeDelta = new Vector2(0, itemHeight);
                float yOffset = i * (itemHeight + itemSpacing);
                rt.anchoredPosition = new Vector2(0, -yOffset);
            }

            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 1f;
        }

        private Sprite GetBackgroundSprite(InfoType type)
        {
            switch (type)
            {
                case InfoType.Bug: return bugSprite;
                case InfoType.Message: return messageSprite;
                case InfoType.Alert: return alertSprite;
                default: return null;
            }
        }

        private void UpdateTitle()
        {
            if (!showBugCount || titleText == null) return;

            int bugCount = 0;
            foreach (var info in currentInfos)
                if (info.Type == InfoType.Bug) bugCount++;

            int total = currentInfos.Count;
            string tag = enableRoomFiltering ? $" [{currentRoomId}]" : "";
            titleText.text = $"Info{tag} (Bug: {bugCount}/{total})";
        }

        private void HandleTriggerActivated(MessageTrigger trigger, GameObject player)
        {
            ShowMessage(
                trigger.MessageTitle,
                trigger.MessageDescription,
                trigger.DisplayTime,
                trigger.RoomId
            );
        }

        public static void ShowMessage(string title, string description, float displayTime = 0f, string roomId = "")
        {
            if (Instance == null) return;
            if (displayTime <= 0f) displayTime = Instance.defaultMessageTime;
            var info = new InfoData(
                InfoType.Message,
                title,
                description,
                Instance.messageSprite,
                displayTime,
                true,
                Instance.enableRoomFiltering ? roomId : ""
            );
            Instance.currentInfos.Add(info);
            Instance.RenderUI();
            Instance.StartCoroutine(Instance.AutoHide(info));
        }

        public static void ShowAlert(string title, string description, float displayTime = 0f)
        {
            if (Instance == null) return;
            if (displayTime <= 0f) displayTime = Instance.defaultAlertTime;
            var info = new InfoData(
                InfoType.Alert,
                title,
                description,
                Instance.alertSprite,
                displayTime,
                true,
                Instance.enableRoomFiltering ? Instance.currentRoomId : ""
            );
            Instance.currentInfos.Add(info);
            Instance.RenderUI();
            Instance.StartCoroutine(Instance.AutoHide(info));
        }

        private IEnumerator AutoHide(InfoData info)
        {
            yield return new WaitForSeconds(info.DisplayTime);
            Instance.currentInfos.Remove(info);
            Instance.RenderUI();
            Instance.UpdateTitle();
        }

        public void SetBackgroundSprites(Sprite bugBg, Sprite msgBg, Sprite alertBg)
        {
            bugSprite = bugBg;
            messageSprite = msgBg;
            alertSprite = alertBg;
        }
    }
}
