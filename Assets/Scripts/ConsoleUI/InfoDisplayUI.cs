// InfoDisplayUI.cs - 修改版本，支持message置顶和跨房间持久化
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

        // 新增：用于跟踪message和alert的剩余时间
        [System.NonSerialized]
        public float StartTime;
        [System.NonSerialized]
        public bool IsPersistent; // 是否跨房间持久化

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
            StartTime = Time.time;
            IsPersistent = (type == InfoType.Message); // message类型跨房间持久化
        }

        // 检查是否过期
        public bool IsExpired()
        {
            if (!IsTemporary) return false;
            return Time.time - StartTime >= DisplayTime;
        }

        // 获取剩余时间
        public float GetRemainingTime()
        {
            if (!IsTemporary) return float.MaxValue;
            return Mathf.Max(0f, DisplayTime - (Time.time - StartTime));
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

        [Header("Message Behavior")]
        [SerializeField] private bool enableMessagePersistence = true; // 是否启用message持久化
        [SerializeField] private bool enableDebugLog = true; // 调试日志

        private readonly List<InfoData> currentInfos = new List<InfoData>();
        private readonly List<GameObject> infoItems = new List<GameObject>();

        // 新增：用于跟踪持久化的message和alert
        private readonly List<InfoData> persistentInfos = new List<InfoData>();
        private readonly Dictionary<InfoData, Coroutine> autoHideCoroutines = new Dictionary<InfoData, Coroutine>();

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

            // 清理所有协程
            foreach (var coroutine in autoHideCoroutines.Values)
            {
                if (coroutine != null)
                    StopCoroutine(coroutine);
            }
            autoHideCoroutines.Clear();
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

            // 定期清理过期的持久化信息
            CleanupExpiredPersistentInfos();
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

            if (enableDebugLog)
            {
                Debug.Log($"🏠 InfoDisplayUI: 房间切换到 {currentRoomId}");
            }
        }

        private string DetermineCurrentRoomId()
        {
            var rs = FindObjectOfType<RoomSystem>();
            var room = rs?.GetCurrentRoom();
            return room != null ? room.currentSequence.ToString() : "Unknown";
        }

        public void RefreshBugInfo()
        {
            if (enableDebugLog)
            {
                Debug.Log($"🔄 InfoDisplayUI: 开始刷新信息面板 - 当前持久化信息数量: {persistentInfos.Count}");
            }

            // 保存还在显示时间内的持久化信息（message和alert）
            SaveActivePersistentInfos();

            // 清除现有UI
            ClearAllInfoItems();

            // 重新构建信息列表（按优先级排序）
            RebuildInfoList();

            // 重新创建UI元素
            CreateInfoItemsUI();

            // 强制布局重建并滚动到顶部
            ForceLayoutRebuild();

            UpdateTitle();

            if (enableDebugLog)
            {
                Debug.Log($"✅ InfoDisplayUI: 信息面板刷新完成 - 总信息数: {currentInfos.Count}");
            }
        }

        /// <summary>
        /// 保存还在显示的持久化信息
        /// </summary>
        private void SaveActivePersistentInfos()
        {
            // 从当前信息中保存还未过期的持久化信息
            var activeMessages = currentInfos.Where(info =>
                info.IsPersistent && info.IsTemporary && !info.IsExpired()).ToList();

            if (enableDebugLog && activeMessages.Count > 0)
            {
                Debug.Log($"💾 InfoDisplayUI: 保存 {activeMessages.Count} 个活跃的持久化信息");
                foreach (var msg in activeMessages)
                {
                    Debug.Log($"  - {msg.Title}: 剩余时间 {msg.GetRemainingTime():F1}s");
                }
            }

            // 更新持久化信息列表
            persistentInfos.Clear();
            persistentInfos.AddRange(activeMessages);
        }

        /// <summary>
        /// 清除所有UI元素
        /// </summary>
        private void ClearAllInfoItems()
        {
            foreach (var go in infoItems)
            {
                if (go != null)
                    Destroy(go);
            }
            infoItems.Clear();
            currentInfos.Clear();
        }

        /// <summary>
        /// 重新构建信息列表（按优先级排序：Message -> Bug -> Alert）
        /// </summary>
        private void RebuildInfoList()
        {
            // 1. 首先添加持久化的message信息（置顶）
            var activeMessages = persistentInfos.Where(info =>
                info.Type == InfoType.Message && !info.IsExpired()).ToList();
            currentInfos.AddRange(activeMessages);

            // 2. 然后添加当前房间的Bug信息
            AddCurrentRoomBugs();

            // 3. 最后添加持久化的alert信息
            var activeAlerts = persistentInfos.Where(info =>
                info.Type == InfoType.Alert && !info.IsExpired()).ToList();
            currentInfos.AddRange(activeAlerts);

            if (enableDebugLog)
            {
                Debug.Log($"📋 InfoDisplayUI: 重建信息列表 - Messages: {activeMessages.Count}, Bugs: {currentInfos.Count(i => i.Type == InfoType.Bug)}, Alerts: {activeAlerts.Count}");
            }
        }

        /// <summary>
        /// 添加当前房间的Bug信息
        /// </summary>
        private void AddCurrentRoomBugs()
        {
            var rs = FindObjectOfType<RoomSystem>();
            var bugs = rs != null
                ? rs.GetCurrentRoomBugObjects()
                : new List<BugObject>(FindObjectsOfType<BugObject>().Where(b => b != null));

            foreach (var b in bugs)
            {
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
            }
        }

        /// <summary>
        /// 创建UI元素
        /// </summary>
        private void CreateInfoItemsUI()
        {
            foreach (var info in currentInfos)
            {
                var go = Instantiate(infoItemPrefab, contentParent);
                infoItems.Add(go);

                // 根据信息类型选择背景
                Sprite sprite = info.Type switch
                {
                    InfoType.Bug => bugSprite,
                    InfoType.Message => messageSprite,
                    InfoType.Alert => alertSprite,
                    _ => bugSprite
                };

                go.GetComponent<InfoItemUI>().SetupInfo(info, sprite);

                // 为持久化的临时信息重新启动自动隐藏协程
                if (info.IsTemporary && info.IsPersistent && !autoHideCoroutines.ContainsKey(info))
                {
                    float remainingTime = info.GetRemainingTime();
                    if (remainingTime > 0f)
                    {
                        var coroutine = StartCoroutine(AutoHideWithRemainingTime(info, remainingTime));
                        autoHideCoroutines[info] = coroutine;

                        if (enableDebugLog)
                        {
                            Debug.Log($"⏰ InfoDisplayUI: 为 '{info.Title}' 重新启动自动隐藏协程，剩余时间: {remainingTime:F1}s");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 强制布局重建
        /// </summary>
        private void ForceLayoutRebuild()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent);
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 1f;
        }

        /// <summary>
        /// 清理过期的持久化信息
        /// </summary>
        private void CleanupExpiredPersistentInfos()
        {
            for (int i = persistentInfos.Count - 1; i >= 0; i--)
            {
                if (persistentInfos[i].IsExpired())
                {
                    if (enableDebugLog)
                    {
                        Debug.Log($"🧹 InfoDisplayUI: 清理过期的持久化信息: {persistentInfos[i].Title}");
                    }
                    persistentInfos.RemoveAt(i);
                }
            }
        }

        private void OnBugFixed(BugObject bug)
        {
            if (bug == null) return;

            // 找到并移除匹配的Bug信息
            int idx = currentInfos.FindIndex(i => i.Type == InfoType.Bug && i.Title == bug.GetBugTitle());
            if (idx < 0) return;

            currentInfos.RemoveAt(idx);
            if (idx < infoItems.Count && infoItems[idx] != null)
            {
                Destroy(infoItems[idx]);
                infoItems.RemoveAt(idx);
            }

            ForceLayoutRebuild();
            UpdateTitle();

            if (enableDebugLog)
            {
                Debug.Log($"🐛 InfoDisplayUI: Bug已修复并从列表中移除: {bug.GetBugTitle()}");
            }
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

            // 添加到当前信息列表的开头（置顶）
            Instance.currentInfos.Insert(0, data);

            // 如果启用持久化，也添加到持久化列表
            if (Instance.enableMessagePersistence)
            {
                Instance.persistentInfos.Add(data);
            }

            // 创建UI元素（插入到最前面）
            var go = Instantiate(Instance.infoItemPrefab, Instance.contentParent);
            go.transform.SetAsFirstSibling(); // 确保显示在最上方
            Instance.infoItems.Insert(0, go);
            go.GetComponent<InfoItemUI>().SetupInfo(data, Instance.messageSprite);

            Instance.ForceLayoutRebuild();

            // 启动自动隐藏协程
            var coroutine = Instance.StartCoroutine(Instance.AutoHide(data));
            Instance.autoHideCoroutines[data] = coroutine;

            if (Instance.enableDebugLog)
            {
                Debug.Log($"📨 InfoDisplayUI: 显示Message - '{title}': '{desc}', 显示时间: {time}s, 持久化: {Instance.enableMessagePersistence}");
            }
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

            Instance.ForceLayoutRebuild();

            var coroutine = Instance.StartCoroutine(Instance.AutoHide(data));
            Instance.autoHideCoroutines[data] = coroutine;

            if (Instance.enableDebugLog)
            {
                Debug.Log($"⚠️ InfoDisplayUI: 显示Alert - '{title}': '{desc}', 显示时间: {time}s");
            }
        }

        private IEnumerator AutoHide(InfoData info)
        {
            yield return new WaitForSeconds(info.DisplayTime);
            RemoveInfo(info);
        }

        private IEnumerator AutoHideWithRemainingTime(InfoData info, float remainingTime)
        {
            yield return new WaitForSeconds(remainingTime);
            RemoveInfo(info);
        }

        private void RemoveInfo(InfoData info)
        {
            // 从协程字典中移除
            if (autoHideCoroutines.ContainsKey(info))
            {
                autoHideCoroutines.Remove(info);
            }

            // 从持久化列表中移除
            persistentInfos.Remove(info);

            // 从当前信息列表中移除
            int idx = currentInfos.IndexOf(info);
            if (idx >= 0)
            {
                currentInfos.RemoveAt(idx);
                if (idx < infoItems.Count && infoItems[idx] != null)
                {
                    Destroy(infoItems[idx]);
                    infoItems.RemoveAt(idx);
                }

                ForceLayoutRebuild();
                UpdateTitle();

                if (enableDebugLog)
                {
                    Debug.Log($"⏰ InfoDisplayUI: 信息已过期并移除: '{info.Title}' ({info.Type})");
                }
            }
        }

        private void UpdateTitle()
        {
            if (!showBugCount || titleText == null) return;

            int validBugCount = currentInfos.Count(info => info.Type == InfoType.Bug);
            int messageCount = currentInfos.Count(info => info.Type == InfoType.Message);
            int alertCount = currentInfos.Count(info => info.Type == InfoType.Alert);

            titleText.text = $"Info [{currentRoomId}] (Bug: {validBugCount}, Msg: {messageCount}, Alert: {alertCount})";
        }

        public void SetBackgroundSprites(Sprite bugBg, Sprite msgBg, Sprite alertBg)
        {
            bugSprite = bugBg;
            messageSprite = msgBg;
            alertSprite = alertBg;
        }

        /// <summary>
        /// 设置message持久化功能开关
        /// </summary>
        public void SetMessagePersistenceEnabled(bool enabled)
        {
            enableMessagePersistence = enabled;
            if (enableDebugLog)
            {
                Debug.Log($"🔧 InfoDisplayUI: Message持久化功能 {(enabled ? "启用" : "禁用")}");
            }
        }

        /// <summary>
        /// 手动清理所有持久化信息
        /// </summary>
        public void ClearAllPersistentInfos()
        {
            persistentInfos.Clear();

            // 停止所有自动隐藏协程
            foreach (var coroutine in autoHideCoroutines.Values)
            {
                if (coroutine != null)
                    StopCoroutine(coroutine);
            }
            autoHideCoroutines.Clear();

            RefreshBugInfo();

            if (enableDebugLog)
            {
                Debug.Log("🧹 InfoDisplayUI: 所有持久化信息已清理");
            }
        }

        /// <summary>
        /// 获取当前持久化信息状态
        /// </summary>
        public string GetPersistentInfoStatus()
        {
            var status = "=== 持久化信息状态 ===\n";
            status += $"持久化功能: {(enableMessagePersistence ? "启用" : "禁用")}\n";
            status += $"持久化信息数量: {persistentInfos.Count}\n";
            status += $"活跃协程数量: {autoHideCoroutines.Count}\n";

            foreach (var info in persistentInfos)
            {
                status += $"- {info.Type} '{info.Title}': 剩余 {info.GetRemainingTime():F1}s\n";
            }

            return status;
        }

        #region Context Menu调试

        [ContextMenu("📊 显示持久化信息状态")]
        private void DebugShowPersistentStatus()
        {
            Debug.Log(GetPersistentInfoStatus());
        }

        [ContextMenu("🧹 清理所有持久化信息")]
        private void DebugClearPersistentInfos()
        {
            ClearAllPersistentInfos();
        }

        [ContextMenu("📨 测试Message显示")]
        private void DebugTestMessage()
        {
            if (Application.isPlaying)
            {
                ShowMessage("测试消息", "这是一条测试消息，应该会置顶显示并跨房间持久化", 10f);
            }
        }

        [ContextMenu("⚠️ 测试Alert显示")]
        private void DebugTestAlert()
        {
            if (Application.isPlaying)
            {
                ShowAlert("测试警告", "这是一条测试警告消息", 5f);
            }
        }

        [ContextMenu("🔄 强制刷新信息面板")]
        private void DebugRefreshInfo()
        {
            if (Application.isPlaying)
            {
                RefreshBugInfo();
            }
        }

        #endregion
    }
}