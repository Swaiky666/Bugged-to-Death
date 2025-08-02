// InfoDisplayUI.cs - 房间信息显示UI系统，支持滚轮显示和多种信息类型
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace BugFixerGame
{
    /// <summary>
    /// 信息类型枚举
    /// </summary>
    public enum InfoType
    {
        Bug,        // Bug信息
        Message,    // 消息（触发器触发）
        Alert       // 提醒（代码调用）
    }

    /// <summary>
    /// 信息数据结构
    /// </summary>
    [System.Serializable]
    public class InfoData
    {
        public InfoType type;
        public string title;
        [TextArea(3, 6)]
        public string description;
        public Sprite backgroundImage;
        public float displayTime = 0f; // 0表示不自动隐藏
        public bool isTemporary = false; // 是否是临时信息（消息和提醒通常是临时的）

        public InfoData(InfoType infoType, string infoTitle, string infoDescription, Sprite background = null, float time = 0f, bool temporary = false)
        {
            type = infoType;
            title = infoTitle;
            description = infoDescription;
            backgroundImage = background;
            displayTime = time;
            isTemporary = temporary;
        }
    }

    /// <summary>
    /// 信息显示UI管理器
    /// </summary>
    public class InfoDisplayUI : MonoBehaviour
    {
        [Header("UI组件")]
        [SerializeField] private GameObject infoPanel;              // 信息面板
        [SerializeField] private ScrollRect scrollRect;             // 滚动视图
        [SerializeField] private Transform contentParent;           // 内容父对象
        [SerializeField] private GameObject infoItemPrefab;         // 信息项预制体
        [SerializeField] private Button toggleButton;               // 切换显示按钮
        [SerializeField] private Button closeButton;                // 关闭按钮
        [SerializeField] private TextMeshProUGUI titleText;         // 标题文本（显示Bug数量）

        [Header("背景图片")]
        [SerializeField] private Sprite bugBackgroundSprite;        // Bug信息背景
        [SerializeField] private Sprite messageBackgroundSprite;    // 消息背景
        [SerializeField] private Sprite alertBackgroundSprite;      // 提醒背景

        [Header("显示设置")]
        [SerializeField] private bool startVisible = false;         // 启动时是否可见
        [SerializeField] private bool autoRefreshBugInfo = true;    // 自动刷新Bug信息
        [SerializeField] private float bugRefreshInterval = 2f;     // Bug刷新间隔
        [SerializeField] private bool showBugCount = true;          // 在标题中显示Bug数量

        [Header("动画设置")]
        [SerializeField] private float fadeTime = 0.3f;            // 淡入淡出时间
        [SerializeField] private bool useScaleAnimation = true;     // 使用缩放动画
        [SerializeField] private Vector3 hiddenScale = new Vector3(0.8f, 0.8f, 1f);
        [SerializeField] private Vector3 visibleScale = Vector3.one;

        [Header("临时信息设置")]
        [SerializeField] private float defaultMessageTime = 5f;     // 默认消息显示时间
        [SerializeField] private float defaultAlertTime = 3f;       // 默认提醒显示时间
        [SerializeField] private int maxTemporaryInfos = 10;        // 最大临时信息数量

        [Header("键盘快捷键")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;   // 切换显示的快捷键

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        // UI状态
        private bool isVisible = false;
        private CanvasGroup canvasGroup;
        private Coroutine fadeCoroutine;
        private Coroutine refreshCoroutine;

        // 信息管理
        private List<InfoData> currentInfos = new List<InfoData>();
        private List<GameObject> infoItems = new List<GameObject>();
        private Dictionary<InfoData, Coroutine> temporaryInfoCoroutines = new Dictionary<InfoData, Coroutine>();

        // 单例
        public static InfoDisplayUI Instance { get; private set; }

        // 事件
        public static event Action<bool> OnVisibilityChanged;

        #region Unity生命周期

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

        private void Start()
        {
            if (startVisible)
            {
                ShowPanel();
            }
            else
            {
                HidePanel();
            }

            if (autoRefreshBugInfo)
            {
                StartBugRefresh();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                TogglePanel();
            }
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }

        #endregion

        #region 初始化

        private void InitializeUI()
        {
            // 获取CanvasGroup组件
            canvasGroup = infoPanel?.GetComponent<CanvasGroup>();
            if (canvasGroup == null && infoPanel != null)
            {
                canvasGroup = infoPanel.AddComponent<CanvasGroup>();
            }

            // 设置按钮事件
            if (toggleButton != null)
                toggleButton.onClick.AddListener(TogglePanel);

            if (closeButton != null)
                closeButton.onClick.AddListener(HidePanel);

            // 检查必要组件
            if (infoPanel == null)
                Debug.LogError("❌ InfoDisplayUI: infoPanel未设置！");

            if (scrollRect == null)
                Debug.LogError("❌ InfoDisplayUI: scrollRect未设置！");

            if (contentParent == null)
                Debug.LogError("❌ InfoDisplayUI: contentParent未设置！");

            if (infoItemPrefab == null)
                Debug.LogError("❌ InfoDisplayUI: infoItemPrefab未设置！");

            Debug.Log("✅ InfoDisplayUI 初始化完成");
        }

        #endregion

        #region 面板显示控制

        /// <summary>
        /// 显示面板
        /// </summary>
        public void ShowPanel()
        {
            if (isVisible) return;

            isVisible = true;
            OnVisibilityChanged?.Invoke(true);

            if (infoPanel != null)
                infoPanel.SetActive(true);

            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            fadeCoroutine = StartCoroutine(FadeIn());

            // 刷新Bug信息
            RefreshBugInfo();

            Debug.Log("📱 信息面板已显示");
        }

        /// <summary>
        /// 隐藏面板
        /// </summary>
        public void HidePanel()
        {
            if (!isVisible) return;

            isVisible = false;
            OnVisibilityChanged?.Invoke(false);

            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            fadeCoroutine = StartCoroutine(FadeOut());

            Debug.Log("📱 信息面板已隐藏");
        }

        /// <summary>
        /// 切换面板显示状态
        /// </summary>
        public void TogglePanel()
        {
            if (isVisible)
                HidePanel();
            else
                ShowPanel();
        }

        #endregion

        #region 动画

        private IEnumerator FadeIn()
        {
            if (canvasGroup == null) yield break;

            // 设置初始状态
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            if (useScaleAnimation)
                infoPanel.transform.localScale = hiddenScale;

            float elapsed = 0f;

            while (elapsed < fadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / fadeTime;

                canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);

                if (useScaleAnimation)
                    infoPanel.transform.localScale = Vector3.Lerp(hiddenScale, visibleScale, t);

                yield return null;
            }

            // 最终状态
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            if (useScaleAnimation)
                infoPanel.transform.localScale = visibleScale;
        }

        private IEnumerator FadeOut()
        {
            if (canvasGroup == null) yield break;

            float startAlpha = canvasGroup.alpha;
            Vector3 startScale = infoPanel.transform.localScale;

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            float elapsed = 0f;

            while (elapsed < fadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / fadeTime;

                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);

                if (useScaleAnimation)
                    infoPanel.transform.localScale = Vector3.Lerp(startScale, hiddenScale, t);

                yield return null;
            }

            // 最终状态
            canvasGroup.alpha = 0f;
            if (useScaleAnimation)
                infoPanel.transform.localScale = hiddenScale;

            if (infoPanel != null)
                infoPanel.SetActive(false);
        }

        #endregion

        #region Bug信息管理

        /// <summary>
        /// 刷新Bug信息
        /// </summary>
        public void RefreshBugInfo()
        {
            // 清除现有的Bug信息
            RemoveInfosByType(InfoType.Bug);

            // 查找场景中所有的BugObject
            BugObject[] bugObjects = FindObjectsOfType<BugObject>();
            List<BugInfo> bugInfos = new List<BugInfo>();

            foreach (var bugObj in bugObjects)
            {
                if (bugObj.ShouldShowInInfoPanel())
                {
                    bugInfos.Add(bugObj.GetBugInfo());
                }
            }

            // 添加Bug信息到列表
            foreach (var bugInfo in bugInfos)
            {
                InfoData infoData = new InfoData(
                    InfoType.Bug,
                    "", // 不使用title
                    bugInfo.description, // 只显示描述
                    bugBackgroundSprite,
                    0f, // Bug信息不自动隐藏
                    false
                );

                AddInfo(infoData, false); // 不立即刷新UI
            }

            // 一次性刷新UI
            RefreshUI();

            // 更新标题
            UpdateTitle();

            Debug.Log($"🔄 刷新Bug信息完成，找到 {bugInfos.Count} 个Bug");
        }

        /// <summary>
        /// 开始自动刷新Bug信息
        /// </summary>
        private void StartBugRefresh()
        {
            if (refreshCoroutine != null)
                StopCoroutine(refreshCoroutine);

            refreshCoroutine = StartCoroutine(BugRefreshCoroutine());
        }

        /// <summary>
        /// 停止自动刷新Bug信息
        /// </summary>
        private void StopBugRefresh()
        {
            if (refreshCoroutine != null)
            {
                StopCoroutine(refreshCoroutine);
                refreshCoroutine = null;
            }
        }

        private IEnumerator BugRefreshCoroutine()
        {
            while (autoRefreshBugInfo)
            {
                yield return new WaitForSeconds(bugRefreshInterval);

                if (isVisible)
                {
                    RefreshBugInfo();
                }
            }
        }

        #endregion

        #region 信息管理

        /// <summary>
        /// 添加信息
        /// </summary>
        public void AddInfo(InfoData info, bool refreshUI = true)
        {
            if (info == null) return;

            // 如果是临时信息且数量超限，移除最旧的临时信息
            if (info.isTemporary)
            {
                int temporaryCount = 0;
                for (int i = currentInfos.Count - 1; i >= 0; i--)
                {
                    if (currentInfos[i].isTemporary)
                    {
                        temporaryCount++;
                        if (temporaryCount >= maxTemporaryInfos)
                        {
                            RemoveInfo(currentInfos[i], false);
                        }
                    }
                }
            }

            currentInfos.Add(info);

            if (refreshUI)
                RefreshUI();

            // 更新标题
            UpdateTitle();

            // 如果是临时信息且有显示时间，启动自动移除
            if (info.isTemporary && info.displayTime > 0)
            {
                var removeCoroutine = StartCoroutine(RemoveInfoAfterDelay(info, info.displayTime));
                temporaryInfoCoroutines[info] = removeCoroutine;
            }

            // 更新标题
            UpdateTitle();

            Debug.Log($"📝 添加{info.type}信息: {info.description}");
        }

        /// <summary>
        /// 移除信息
        /// </summary>
        public void RemoveInfo(InfoData info, bool refreshUI = true)
        {
            if (info == null) return;

            currentInfos.Remove(info);

            // 停止自动移除协程
            if (temporaryInfoCoroutines.ContainsKey(info))
            {
                if (temporaryInfoCoroutines[info] != null)
                    StopCoroutine(temporaryInfoCoroutines[info]);
                temporaryInfoCoroutines.Remove(info);
            }

            if (refreshUI)
                RefreshUI();

            // 更新标题
            UpdateTitle();

            Debug.Log($"🗑️ 移除{info.type}信息: {info.description}");
        }

        /// <summary>
        /// 根据类型移除信息
        /// </summary>
        public void RemoveInfosByType(InfoType type, bool refreshUI = true)
        {
            for (int i = currentInfos.Count - 1; i >= 0; i--)
            {
                if (currentInfos[i].type == type)
                {
                    RemoveInfo(currentInfos[i], false);
                }
            }

            if (refreshUI)
                RefreshUI();
        }

        /// <summary>
        /// 清除所有信息
        /// </summary>
        public void ClearAllInfo(bool refreshUI = true)
        {
            currentInfos.Clear();

            // 停止所有自动移除协程
            foreach (var coroutine in temporaryInfoCoroutines.Values)
            {
                if (coroutine != null)
                    StopCoroutine(coroutine);
            }
            temporaryInfoCoroutines.Clear();

            if (refreshUI)
                RefreshUI();

            // 更新标题
            UpdateTitle();

            Debug.Log("🧹 清除所有信息");
        }

        private IEnumerator RemoveInfoAfterDelay(InfoData info, float delay)
        {
            yield return new WaitForSeconds(delay);
            RemoveInfo(info);
        }

        #endregion

        #region UI刷新

        /// <summary>
        /// 刷新UI显示
        /// </summary>
        private void RefreshUI()
        {
            if (contentParent == null || infoItemPrefab == null) return;

            // 清除现有UI项目
            ClearUIItems();

            // 创建新的UI项目
            foreach (var info in currentInfos)
            {
                CreateInfoItem(info);
            }

            // 重新计算滚动区域大小
            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 1f; // 滚动到顶部
            }
        }

        /// <summary>
        /// 清除UI项目
        /// </summary>
        private void ClearUIItems()
        {
            foreach (var item in infoItems)
            {
                if (item != null)
                    DestroyImmediate(item);
            }
            infoItems.Clear();
        }

        /// <summary>
        /// 创建信息项目UI
        /// </summary>
        private void CreateInfoItem(InfoData info)
        {
            GameObject itemGO = Instantiate(infoItemPrefab, contentParent);
            infoItems.Add(itemGO);

            // 获取UI组件
            InfoItemUI itemUI = itemGO.GetComponent<InfoItemUI>();
            if (itemUI == null)
            {
                itemUI = itemGO.AddComponent<InfoItemUI>();
            }

            // 设置背景图片
            Sprite backgroundSprite = GetBackgroundSprite(info.type);

            // 设置信息内容
            itemUI.SetupInfo(info, backgroundSprite);
        }

        /// <summary>
        /// 根据类型获取背景图片
        /// </summary>
        private Sprite GetBackgroundSprite(InfoType type)
        {
            switch (type)
            {
                case InfoType.Bug:
                    return bugBackgroundSprite;
                case InfoType.Message:
                    return messageBackgroundSprite;
                case InfoType.Alert:
                    return alertBackgroundSprite;
                default:
                    return null;
            }
        }

        #endregion

        #region 标题更新

        /// <summary>
        /// 更新标题显示
        /// </summary>
        private void UpdateTitle()
        {
            if (!showBugCount || titleText == null) return;

            int bugCount = GetBugInfoCount();
            int totalCount = GetInfoCount();

            if (bugCount > 0)
            {
                titleText.text = $"房间信息 (Bug: {bugCount}/{totalCount})";
            }
            else
            {
                titleText.text = $"房间信息 ({totalCount})";
            }
        }

        #endregion

        #region 外部调用接口

        /// <summary>
        /// 显示消息（触发器触发）
        /// </summary>
        public static void ShowMessage(string title, string description, float displayTime = 0f)
        {
            if (Instance == null) return;

            if (displayTime <= 0f)
                displayTime = Instance.defaultMessageTime;

            InfoData messageInfo = new InfoData(
                InfoType.Message,
                "", // 不使用title
                description, // 只使用description
                Instance.messageBackgroundSprite,
                displayTime,
                true
            );

            Instance.AddInfo(messageInfo);

            // 如果面板未显示，自动显示
            if (!Instance.isVisible)
            {
                Instance.ShowPanel();
            }
        }

        /// <summary>
        /// 显示提醒（代码调用）
        /// </summary>
        public static void ShowAlert(string title, string description, float displayTime = 0f)
        {
            if (Instance == null) return;

            if (displayTime <= 0f)
                displayTime = Instance.defaultAlertTime;

            InfoData alertInfo = new InfoData(
                InfoType.Alert,
                "", // 不使用title
                description, // 只使用description
                Instance.alertBackgroundSprite,
                displayTime,
                true
            );

            Instance.AddInfo(alertInfo);

            // 如果面板未显示，自动显示
            if (!Instance.isVisible)
            {
                Instance.ShowPanel();
            }
        }

        /// <summary>
        /// 设置背景图片
        /// </summary>
        public void SetBackgroundSprites(Sprite bugBg, Sprite messageBg, Sprite alertBg)
        {
            bugBackgroundSprite = bugBg;
            messageBackgroundSprite = messageBg;
            alertBackgroundSprite = alertBg;
        }

        /// <summary>
        /// 设置自动刷新
        /// </summary>
        public void SetAutoRefresh(bool enable, float interval = 2f)
        {
            autoRefreshBugInfo = enable;
            bugRefreshInterval = interval;

            if (enable)
                StartBugRefresh();
            else
                StopBugRefresh();
        }

        #endregion

        #region 公共接口

        public bool IsVisible() => isVisible;
        public int GetInfoCount() => currentInfos.Count;
        public int GetBugInfoCount() => currentInfos.FindAll(info => info.type == InfoType.Bug).Count;
        public int GetMessageCount() => currentInfos.FindAll(info => info.type == InfoType.Message).Count;
        public int GetAlertCount() => currentInfos.FindAll(info => info.type == InfoType.Alert).Count;

        #endregion

        #region 调试功能

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 250));
            GUILayout.Label("=== Info Display UI Debug ===");
            GUILayout.Label($"面板可见: {isVisible}");
            GUILayout.Label($"总信息数: {GetInfoCount()}");
            GUILayout.Label($"Bug信息: {GetBugInfoCount()}");
            GUILayout.Label($"消息数: {GetMessageCount()}");
            GUILayout.Label($"提醒数: {GetAlertCount()}");
            GUILayout.Label($"自动刷新Bug: {autoRefreshBugInfo}");
            GUILayout.Label($"快捷键: {toggleKey}");

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("显示面板"))
            {
                ShowPanel();
            }
            if (GUILayout.Button("隐藏面板"))
            {
                HidePanel();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("刷新Bug"))
            {
                RefreshBugInfo();
            }
            if (GUILayout.Button("清除所有"))
            {
                ClearAllInfo();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("测试消息"))
            {
                ShowMessage("测试消息", "这是一个测试消息，将在5秒后自动消失", 5f);
            }
            if (GUILayout.Button("测试提醒"))
            {
                ShowAlert("测试提醒", "这是一个测试提醒，将在3秒后自动消失", 3f);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        [ContextMenu("📱 显示面板")]
        private void TestShowPanel()
        {
            if (Application.isPlaying)
                ShowPanel();
        }

        [ContextMenu("🙈 隐藏面板")]
        private void TestHidePanel()
        {
            if (Application.isPlaying)
                HidePanel();
        }

        [ContextMenu("🔄 刷新Bug信息")]
        private void TestRefreshBugInfo()
        {
            if (Application.isPlaying)
                RefreshBugInfo();
        }

        [ContextMenu("💬 测试消息")]
        private void TestShowMessage()
        {
            if (Application.isPlaying)
                ShowMessage("测试消息", "这是一个来自Context菜单的测试消息", 5f);
        }

        [ContextMenu("⚠️ 测试提醒")]
        private void TestShowAlert()
        {
            if (Application.isPlaying)
                ShowAlert("测试提醒", "这是一个来自Context菜单的测试提醒", 3f);
        }

        [ContextMenu("🔍 检查组件设置")]
        private void CheckComponentSetup()
        {
            Debug.Log("=== InfoDisplayUI 组件检查 ===");
            Debug.Log($"信息面板: {(infoPanel != null ? infoPanel.name : "未设置")}");
            Debug.Log($"滚动视图: {(scrollRect != null ? scrollRect.name : "未设置")}");
            Debug.Log($"内容父对象: {(contentParent != null ? contentParent.name : "未设置")}");
            Debug.Log($"信息项预制体: {(infoItemPrefab != null ? infoItemPrefab.name : "未设置")}");
            Debug.Log($"标题文本: {(titleText != null ? titleText.name : "未设置")}");
            Debug.Log($"Bug背景: {(bugBackgroundSprite != null ? bugBackgroundSprite.name : "未设置")}");
            Debug.Log($"消息背景: {(messageBackgroundSprite != null ? messageBackgroundSprite.name : "未设置")}");
            Debug.Log($"提醒背景: {(alertBackgroundSprite != null ? alertBackgroundSprite.name : "未设置")}");
            Debug.Log($"当前信息数量: {currentInfos.Count}");
            Debug.Log($"UI项目数量: {infoItems.Count}");
        }

        #endregion
    }
}