// InfoDisplayUI.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
            string description,
            Sprite backgroundImage,
            float displayTime,
            bool isTemporary,
            string roomId
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

        [Header("References")]
        [SerializeField] private GameObject infoPanel;
        [SerializeField] private GameObject infoItemPrefab;
        [SerializeField] private RectTransform contentParent;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private TMP_Text roomLabel;
        [SerializeField] private TMP_Text titleText;

        [Header("Behavior")]
        [SerializeField] private bool enableRoomFiltering = true;
        [SerializeField] private bool showBugCount = true;
        [SerializeField] private bool enableScrollWheel = true;
        [SerializeField] private float scrollSpeed = 20f;

        [Header("Default Durations")]
        [SerializeField] private float defaultMessageTime = 5f;
        [SerializeField] private float defaultAlertTime = 3f;

        [Header("Background Sprites")]
        [SerializeField] private Sprite bugSprite;
        [SerializeField] private Sprite messageSprite;
        [SerializeField] private Sprite alertSprite;

        private readonly List<InfoData> currentInfos = new List<InfoData>();
        private readonly List<GameObject> infoItems = new List<GameObject>();

        private string currentRoomId, lastKnownRoomId;

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
            if (enableScrollWheel && scrollRect != null)
            {
                float d = Input.GetAxis("Mouse ScrollWheel") * scrollSpeed;
                if (Mathf.Abs(d) > 0.01f)
                {
                    var pos = scrollRect.content.anchoredPosition;
                    pos.y += d;
                    scrollRect.content.anchoredPosition = pos;
                }
            }

            if (enableRoomFiltering)
            {
                var nr = DetermineCurrentRoomId();
                if (nr != lastKnownRoomId)
                {
                    UpdateCurrentRoom();
                    RefreshBugInfo();
                }
            }
        }

        public void TogglePanel()
        {
            if (infoPanel == null) return;
            infoPanel.SetActive(!infoPanel.activeSelf);
            if (infoPanel.activeSelf) RefreshBugInfo();
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

        public void RefreshBugInfo()
        {
            // clear existing
            foreach (var go in infoItems) Destroy(go);
            infoItems.Clear();
            currentInfos.Clear();

            // fetch bugs - 使用新的API获取过滤后的Bug列表
            var rs = FindObjectOfType<RoomSystem>();
            var bugs = rs != null
                ? rs.GetCurrentRoomBugObjects() // 这个方法已经过滤了null对象
                : new List<BugObject>(FindObjectsOfType<BugObject>().Where(b => b != null));

            // instantiate items - 只为有效的Bug创建UI
            foreach (var b in bugs)
            {
                // 双重检查确保bug对象有效
                if (b == null || !b.ShouldShowInInfoPanel()) continue;

                var data = new InfoData(
                    InfoType.Bug,
                    b.GetBugTitle(),
                    b.GetBugType().ToString(),
                    bugSprite,
                    0f,
                    false,
                    currentRoomId
                );
                currentInfos.Add(data);

                var go = Instantiate(infoItemPrefab, contentParent);
                infoItems.Add(go);
                go.GetComponent<InfoItemUI>()
                  .SetupInfo(data, bugSprite);
            }

            // force layout rebuild and scroll to top
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent);
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 1f;

            UpdateTitle();
        }

        private void OnBugFixed(BugObject bug)
        {
            // 检查bug对象是否仍然有效
            if (bug == null) return;

            // find and remove matching entry
            int idx = currentInfos.FindIndex(i => i.Title == bug.GetBugTitle());
            if (idx < 0) return;

            currentInfos.RemoveAt(idx);
            if (idx < infoItems.Count && infoItems[idx] != null)
            {
                Destroy(infoItems[idx]);
                infoItems.RemoveAt(idx);
            }

            // rebuild layout
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent);
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 1f;

            UpdateTitle();
        }

        private void HandleTriggerActivated(MessageTrigger t, GameObject _)
        {
            ShowMessage(t.MessageTitle, t.MessageDescription, t.DisplayTime);
        }

        public static void ShowMessage(string title, string desc, float time = 0f)
        {
            if (Instance == null) return;
            if (time <= 0f) time = Instance.defaultMessageTime;

            var data = new InfoData(
                InfoType.Message,
                title,
                desc,
                Instance.messageSprite,
                time,
                true,
                Instance.currentRoomId
            );
            Instance.currentInfos.Add(data);

            var go = Instantiate(Instance.infoItemPrefab, Instance.contentParent);
            Instance.infoItems.Add(go);
            go.GetComponent<InfoItemUI>().SetupInfo(data, Instance.messageSprite);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(Instance.contentParent);
            Instance.scrollRect.verticalNormalizedPosition = 1f;

            Instance.StartCoroutine(Instance.AutoHide(data));
        }

        public static void ShowAlert(string title, string desc, float time = 0f)
        {
            if (Instance == null) return;
            if (time <= 0f) time = Instance.defaultAlertTime;

            var data = new InfoData(
                InfoType.Alert,
                title,
                desc,
                Instance.alertSprite,
                time,
                true,
                Instance.currentRoomId
            );
            Instance.currentInfos.Add(data);

            var go = Instantiate(Instance.infoItemPrefab, Instance.contentParent);
            Instance.infoItems.Add(go);
            go.GetComponent<InfoItemUI>().SetupInfo(data, Instance.alertSprite);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(Instance.contentParent);
            Instance.scrollRect.verticalNormalizedPosition = 1f;

            Instance.StartCoroutine(Instance.AutoHide(data));
        }

        private IEnumerator AutoHide(InfoData info)
        {
            yield return new WaitForSeconds(info.DisplayTime);

            int idx = currentInfos.IndexOf(info);
            if (idx >= 0)
            {
                currentInfos.RemoveAt(idx);
                if (idx < infoItems.Count && infoItems[idx] != null)
                {
                    Destroy(infoItems[idx]);
                    infoItems.RemoveAt(idx);
                }

                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent);
                if (scrollRect != null)
                    scrollRect.verticalNormalizedPosition = 1f;

                UpdateTitle();
            }
        }

        private void UpdateTitle()
        {
            if (!showBugCount || titleText == null) return;
            
            // 只计算有效的Bug信息数量
            int validBugCount = currentInfos.Count(info => info.Type == InfoType.Bug);
            titleText.text = $"Info [{currentRoomId}] (Bug: {validBugCount})";
        }

        public void SetBackgroundSprites(Sprite bugBg, Sprite msgBg, Sprite alertBg)
        {
            bugSprite = bugBg;
            messageSprite = msgBg;
            alertSprite = alertBg;
        }
    }
}