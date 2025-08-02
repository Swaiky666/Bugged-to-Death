// InfoSystemManager.cs - 信息系统统一管理器，提供便捷的调用接口
using UnityEngine;
using System.Collections.Generic;

namespace BugFixerGame
{
    /// <summary>
    /// 信息系统管理器 - 统一管理所有信息显示功能
    /// </summary>
    public class InfoSystemManager : MonoBehaviour
    {
        [Header("背景图片资源")]
        [SerializeField] private Sprite bugBackgroundSprite;
        [SerializeField] private Sprite messageBackgroundSprite;
        [SerializeField] private Sprite alertBackgroundSprite;

        [Header("预设消息")]
        [SerializeField] private List<PresetMessage> presetMessages = new List<PresetMessage>();

        [Header("预设提醒")]
        [SerializeField] private List<PresetAlert> presetAlerts = new List<PresetAlert>();

        [Header("系统设置")]
        [SerializeField] private bool initializeOnStart = true;
        [SerializeField] private bool autoSetupTriggers = true;
        [SerializeField] private bool enableGlobalHotkeys = true;

        [Header("全局快捷键")]
        [SerializeField] private KeyCode toggleInfoPanelKey = KeyCode.Tab;
        [SerializeField] private KeyCode showBugListKey = KeyCode.B;
        [SerializeField] private KeyCode clearAllInfoKey = KeyCode.C;

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        // 单例
        public static InfoSystemManager Instance { get; private set; }

        // 统计信息
        private int totalMessagesShown = 0;
        private int totalAlertsShown = 0;
        private int totalTriggersActivated = 0;

        #region Unity生命周期

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
                return;
            }
        }

        private void Start()
        {
            if (initializeOnStart)
            {
                Initialize();
            }
        }

        private void Update()
        {
            if (enableGlobalHotkeys)
            {
                HandleGlobalHotkeys();
            }
        }

        private void OnEnable()
        {
            // 订阅触发器事件
            MessageTrigger.OnTriggerActivated += OnTriggerActivated;
        }

        private void OnDisable()
        {
            // 取消订阅
            MessageTrigger.OnTriggerActivated -= OnTriggerActivated;
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化信息系统
        /// </summary>
        public void Initialize()
        {
            Debug.Log("🚀 InfoSystemManager: 开始初始化信息系统");

            // 设置InfoDisplayUI的背景图片
            if (InfoDisplayUI.Instance != null)
            {
                InfoDisplayUI.Instance.SetBackgroundSprites(
                    bugBackgroundSprite,
                    messageBackgroundSprite,
                    alertBackgroundSprite
                );
                Debug.Log("✅ 设置InfoDisplayUI背景图片完成");
            }
            else
            {
                Debug.LogWarning("⚠️ 未找到InfoDisplayUI实例");
            }

            // 自动设置场景中的触发器
            if (autoSetupTriggers)
            {
                SetupAllTriggers();
            }

            Debug.Log("✅ InfoSystemManager: 信息系统初始化完成");
        }

        /// <summary>
        /// 设置场景中所有触发器
        /// </summary>
        private void SetupAllTriggers()
        {
            MessageTrigger[] triggers = FindObjectsOfType<MessageTrigger>();
            Debug.Log($"🔧 找到 {triggers.Length} 个消息触发器，开始设置...");

            foreach (var trigger in triggers)
            {
                // 这里可以添加统一的触发器设置逻辑
                Debug.Log($"  - 设置触发器: {trigger.gameObject.name}");
            }
        }

        #endregion

        #region 快捷键处理

        /// <summary>
        /// 处理全局快捷键
        /// </summary>
        private void HandleGlobalHotkeys()
        {
            if (Input.GetKeyDown(toggleInfoPanelKey))
            {
                ToggleInfoPanel();
            }

            if (Input.GetKeyDown(showBugListKey))
            {
                ShowBugList();
            }

            if (Input.GetKeyDown(clearAllInfoKey))
            {
                ClearAllInfo();
            }
        }

        #endregion

        #region 便捷接口 - 消息显示

        /// <summary>
        /// 显示预设消息
        /// </summary>
        public void ShowPresetMessage(string messageId)
        {
            PresetMessage preset = presetMessages.Find(m => m.id == messageId);
            if (preset != null)
            {
                ShowMessage(preset.title, preset.description, preset.displayTime);
            }
            else
            {
                Debug.LogWarning($"⚠️ 未找到ID为 '{messageId}' 的预设消息");
            }
        }

        /// <summary>
        /// 显示消息
        /// </summary>
        public void ShowMessage(string title, string description, float displayTime = 5f)
        {
            InfoDisplayUI.ShowMessage(title, description, displayTime);
            totalMessagesShown++;
            Debug.Log($"💬 显示消息: {description} (总计: {totalMessagesShown})");
        }

        /// <summary>
        /// 显示预设提醒
        /// </summary>
        public void ShowPresetAlert(string alertId)
        {
            PresetAlert preset = presetAlerts.Find(a => a.id == alertId);
            if (preset != null)
            {
                ShowAlert(preset.title, preset.description, preset.displayTime);
            }
            else
            {
                Debug.LogWarning($"⚠️ 未找到ID为 '{alertId}' 的预设提醒");
            }
        }

        /// <summary>
        /// 显示提醒
        /// </summary>
        public void ShowAlert(string title, string description, float displayTime = 3f)
        {
            InfoDisplayUI.ShowAlert(title, description, displayTime);
            totalAlertsShown++;
            Debug.Log($"⚠️ 显示提醒: {description} (总计: {totalAlertsShown})");
        }

        /// <summary>
        /// 显示重要提醒（自动显示信息面板）
        /// </summary>
        public void ShowImportantAlert(string title, string description, float displayTime = 5f)
        {
            // 确保信息面板可见
            if (InfoDisplayUI.Instance != null && !InfoDisplayUI.Instance.IsVisible())
            {
                InfoDisplayUI.Instance.ShowPanel();
            }

            ShowAlert(title, description, displayTime);
        }

        #endregion

        #region 便捷接口 - 面板控制

        /// <summary>
        /// 切换信息面板显示状态
        /// </summary>
        public void ToggleInfoPanel()
        {
            if (InfoDisplayUI.Instance != null)
            {
                InfoDisplayUI.Instance.TogglePanel();
            }
        }

        /// <summary>
        /// 显示信息面板
        /// </summary>
        public void ShowInfoPanel()
        {
            if (InfoDisplayUI.Instance != null)
            {
                InfoDisplayUI.Instance.ShowPanel();
            }
        }

        /// <summary>
        /// 隐藏信息面板
        /// </summary>
        public void HideInfoPanel()
        {
            if (InfoDisplayUI.Instance != null)
            {
                InfoDisplayUI.Instance.HidePanel();
            }
        }

        /// <summary>
        /// 显示Bug列表
        /// </summary>
        public void ShowBugList()
        {
            if (InfoDisplayUI.Instance != null)
            {
                InfoDisplayUI.Instance.ShowPanel();
                InfoDisplayUI.Instance.RefreshBugInfo();
                Debug.Log("📋 显示Bug列表");
            }
        }

        /// <summary>
        /// 清除所有信息
        /// </summary>
        public void ClearAllInfo()
        {
            if (InfoDisplayUI.Instance != null)
            {
                InfoDisplayUI.Instance.ClearAllInfo();
                Debug.Log("🧹 清除所有信息");
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 触发器激活事件处理
        /// </summary>
        private void OnTriggerActivated(MessageTrigger trigger, GameObject player)
        {
            totalTriggersActivated++;
            Debug.Log($"🎯 触发器激活: {trigger.gameObject.name} (总计: {totalTriggersActivated})");
        }

        #endregion

        #region 游戏事件响应

        /// <summary>
        /// 响应游戏开始
        /// </summary>
        public void OnGameStart()
        {
            ShowAlert("游戏开始", "开始寻找房间中的Bug吧！按Tab键查看信息面板", 4f);
        }

        /// <summary>
        /// 响应Bug修复
        /// </summary>
        public void OnBugFixed(BugObject bug)
        {
            string message = $"成功修复了 {bug.GetBugTitle()}！";
            ShowAlert("Bug修复成功", message, 3f);
        }

        /// <summary>
        /// 响应错误检测
        /// </summary>
        public void OnWrongDetection(GameObject obj)
        {
            ShowAlert("检测错误", $"{obj.name} 不是Bug，魔法值减少了！", 3f);
        }

        /// <summary>
        /// 响应魔法值不足
        /// </summary>
        public void OnManaEmpty()
        {
            ShowImportantAlert("魔法值耗尽", "你的魔法值已经用完了！游戏即将结束。", 5f);
        }

        /// <summary>
        /// 响应所有Bug修复完成
        /// </summary>
        public void OnAllBugsFixed()
        {
            ShowImportantAlert("恭喜！", "你已经修复了所有Bug！房间现在完美无瑕！", 6f);
        }

        #endregion

        #region 实用工具方法

        /// <summary>
        /// 根据Bug类型显示相应提醒
        /// </summary>
        public void ShowBugTypeAlert(BugType bugType)
        {
            string title = $"发现 {GameStatsHelper.GetBugTypeDisplayName(bugType)}";
            string description = GetBugTypeDescription(bugType);
            ShowAlert(title, description, 4f);
        }

        /// <summary>
        /// 获取Bug类型描述
        /// </summary>
        private string GetBugTypeDescription(BugType bugType)
        {
            switch (bugType)
            {
                case BugType.ObjectFlickering:
                    return "物体正在不正常地闪烁，这可能是渲染问题导致的。";
                case BugType.CollisionMissing:
                    return "碰撞检测出现问题，物体可能无法正常交互。";
                case BugType.WrongOrMissingMaterial:
                    return "物体的材质不正确或者丢失了。";
                case BugType.WrongObject:
                    return "这里出现了错误的物体，应该替换为正确的物体。";
                case BugType.MissingObject:
                    return "这里缺少了一个物体，需要将其恢复。";
                case BugType.ObjectShaking:
                    return "物体正在不正常地震动，可能是物理系统的问题。";
                case BugType.ObjectMovedOrClipping:
                    return "物体的位置不正确或者发生了穿模现象。";
                default:
                    return "发现了一个未知类型的Bug。";
            }
        }

        /// <summary>
        /// 显示帮助信息
        /// </summary>
        public void ShowHelpInfo()
        {
            string helpText = $"快捷键说明：\n" +
                            $"• {toggleInfoPanelKey} - 切换信息面板\n" +
                            $"• {showBugListKey} - 显示Bug列表\n" +
                            $"• {clearAllInfoKey} - 清除所有信息\n\n" +
                            $"长按鼠标左键检测Bug";

            ShowMessage("帮助信息", helpText, 8f);
        }

        /// <summary>
        /// 显示统计信息
        /// </summary>
        public void ShowStatistics()
        {
            string statsText = $"当前统计：\n" +
                             $"• 显示的消息数：{totalMessagesShown}\n" +
                             $"• 显示的提醒数：{totalAlertsShown}\n" +
                             $"• 触发器激活次数：{totalTriggersActivated}";

            if (InfoDisplayUI.Instance != null)
            {
                statsText += $"\n• 当前信息数：{InfoDisplayUI.Instance.GetInfoCount()}";
                statsText += $"\n• Bug信息数：{InfoDisplayUI.Instance.GetBugInfoCount()}";
            }

            ShowMessage("统计信息", statsText, 6f);
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 设置背景图片
        /// </summary>
        public void SetBackgroundSprites(Sprite bug, Sprite message, Sprite alert)
        {
            bugBackgroundSprite = bug;
            messageBackgroundSprite = message;
            alertBackgroundSprite = alert;

            if (InfoDisplayUI.Instance != null)
            {
                InfoDisplayUI.Instance.SetBackgroundSprites(bug, message, alert);
            }
        }

        /// <summary>
        /// 添加预设消息
        /// </summary>
        public void AddPresetMessage(string id, string title, string description, float displayTime = 5f)
        {
            PresetMessage newMessage = new PresetMessage
            {
                id = id,
                title = title,
                description = description,
                displayTime = displayTime
            };

            presetMessages.Add(newMessage);
        }

        /// <summary>
        /// 添加预设提醒
        /// </summary>
        public void AddPresetAlert(string id, string title, string description, float displayTime = 3f)
        {
            PresetAlert newAlert = new PresetAlert
            {
                id = id,
                title = title,
                description = description,
                displayTime = displayTime
            };

            presetAlerts.Add(newAlert);
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public (int messages, int alerts, int triggers) GetStatistics()
        {
            return (totalMessagesShown, totalAlertsShown, totalTriggersActivated);
        }

        /// <summary>
        /// 重置统计信息
        /// </summary>
        public void ResetStatistics()
        {
            totalMessagesShown = 0;
            totalAlertsShown = 0;
            totalTriggersActivated = 0;
            Debug.Log("📊 统计信息已重置");
        }

        #endregion

        #region 调试功能

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(Screen.width - 320, 10, 300, 300));
            GUILayout.Label("=== Info System Manager ===");
            GUILayout.Label($"消息总数: {totalMessagesShown}");
            GUILayout.Label($"提醒总数: {totalAlertsShown}");
            GUILayout.Label($"触发器激活: {totalTriggersActivated}");

            if (InfoDisplayUI.Instance != null)
            {
                GUILayout.Label($"面板可见: {InfoDisplayUI.Instance.IsVisible()}");
                GUILayout.Label($"当前信息: {InfoDisplayUI.Instance.GetInfoCount()}");
            }

            GUILayout.Space(10);
            GUILayout.Label("快捷键:");
            GUILayout.Label($"{toggleInfoPanelKey} - 切换面板");
            GUILayout.Label($"{showBugListKey} - Bug列表");
            GUILayout.Label($"{clearAllInfoKey} - 清除信息");

            GUILayout.Space(10);

            if (GUILayout.Button("测试消息"))
            {
                ShowMessage("测试消息", "这是一个测试消息", 3f);
            }

            if (GUILayout.Button("测试提醒"))
            {
                ShowAlert("测试提醒", "这是一个测试提醒", 3f);
            }

            if (GUILayout.Button("显示帮助"))
            {
                ShowHelpInfo();
            }

            if (GUILayout.Button("显示统计"))
            {
                ShowStatistics();
            }

            if (GUILayout.Button("重置统计"))
            {
                ResetStatistics();
            }

            GUILayout.EndArea();
        }

        [ContextMenu("🚀 初始化系统")]
        private void TestInitialize()
        {
            if (Application.isPlaying)
            {
                Initialize();
            }
        }

        [ContextMenu("💬 测试预设消息")]
        private void TestPresetMessage()
        {
            if (Application.isPlaying && presetMessages.Count > 0)
            {
                ShowPresetMessage(presetMessages[0].id);
            }
            else
            {
                ShowMessage("测试消息", "这是一个通过InfoSystemManager显示的测试消息", 3f);
            }
        }

        [ContextMenu("⚠️ 测试预设提醒")]
        private void TestPresetAlert()
        {
            if (Application.isPlaying && presetAlerts.Count > 0)
            {
                ShowPresetAlert(presetAlerts[0].id);
            }
            else
            {
                ShowAlert("测试提醒", "这是一个通过InfoSystemManager显示的测试提醒", 3f);
            }
        }

        [ContextMenu("📋 显示Bug列表")]
        private void TestShowBugList()
        {
            if (Application.isPlaying)
            {
                ShowBugList();
            }
        }

        [ContextMenu("📊 显示统计信息")]
        private void TestShowStatistics()
        {
            if (Application.isPlaying)
            {
                ShowStatistics();
            }
        }

        [ContextMenu("🔍 检查系统状态")]
        private void CheckSystemStatus()
        {
            Debug.Log("=== InfoSystemManager 系统状态 ===");
            Debug.Log($"实例状态: {(Instance == this ? "主实例" : "非主实例")}");
            Debug.Log($"InfoDisplayUI: {(InfoDisplayUI.Instance != null ? "已连接" : "未找到")}");
            Debug.Log($"预设消息数量: {presetMessages.Count}");
            Debug.Log($"预设提醒数量: {presetAlerts.Count}");
            Debug.Log($"背景图片设置: Bug={bugBackgroundSprite != null}, Message={messageBackgroundSprite != null}, Alert={alertBackgroundSprite != null}");
            Debug.Log($"全局快捷键: {(enableGlobalHotkeys ? "启用" : "禁用")}");
            Debug.Log($"统计信息: 消息={totalMessagesShown}, 提醒={totalAlertsShown}, 触发器={totalTriggersActivated}");

            MessageTrigger[] triggers = FindObjectsOfType<MessageTrigger>();
            Debug.Log($"场景中的触发器数量: {triggers.Length}");
        }

        #endregion
    }

    #region 数据结构

    /// <summary>
    /// 预设消息数据
    /// </summary>
    [System.Serializable]
    public class PresetMessage
    {
        public string id;
        public string title;
        [TextArea(2, 4)]
        public string description;
        public float displayTime = 5f;
    }

    /// <summary>
    /// 预设提醒数据
    /// </summary>
    [System.Serializable]
    public class PresetAlert
    {
        public string id;
        public string title;
        [TextArea(2, 4)]
        public string description;
        public float displayTime = 3f;
    }

    #endregion
}