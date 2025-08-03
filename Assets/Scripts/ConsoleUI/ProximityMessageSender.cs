// ProximityMessageSender.cs - 改进版：支持选择消息类型
using UnityEngine;

namespace BugFixerGame
{
    /// <summary>
    /// 消息类型枚举
    /// </summary>
    public enum ProximityMessageType
    {
        Message,    // 普通消息（持久化，置顶显示）
        Alert       // 警告消息（临时显示）
    }

    public class ProximityMessageSender : MonoBehaviour
    {
        [Header("消息类型设置")]
        [SerializeField] private ProximityMessageType messageType = ProximityMessageType.Message; // 消息类型
        [SerializeField] private string messageTitle = "提示";                                     // 消息标题（可自定义）

        [Header("消息内容")]
        [SerializeField][TextArea(3, 5)] private string messageContent = "这是一条消息";           // 消息内容

        [Header("显示时间设置")]
        [SerializeField] private bool useCustomDisplayTime = false;                               // 是否使用自定义显示时间
        [SerializeField] private float customDisplayTime = 5f;                                   // 自定义显示时间（仅当useCustomDisplayTime为true时生效）

        [Header("行为设置")]
        [SerializeField] private bool triggerOnce = true;                                        // 是否只触发一次
        [SerializeField] private bool destroyAfterTrigger = true;                                // 触发后是否销毁

        [Header("调试设置")]
        [SerializeField] private bool showDebugGizmos = true;                                    // 是否显示调试球体
        [SerializeField] private bool enableDebugLog = true;                                     // 是否启用调试日志

        // 固定参数（不在Inspector中显示）
        private const float TRIGGER_DISTANCE = 3f;        // 固定触发距离3米
        private const string TARGET_TAG = "Player";       // 固定目标标签
        private const float DESTROY_DELAY = 0.1f;         // 固定销毁延迟

        // 私有变量
        private Transform playerTransform;
        private bool hasTriggered = false;
        private bool isDestroying = false;

        // 事件
        public static event System.Action<ProximityMessageSender, GameObject> OnMessageTriggered;

        #region Unity生命周期

        private void Start()
        {
            ValidateSettings();
            FindPlayer();

            if (enableDebugLog)
            {
                string typeText = messageType == ProximityMessageType.Message ? "消息" : "警告";
                Debug.Log($"📡 ProximityMessageSender [{name}] 初始化完成 - 类型: {typeText}, 标题: '{messageTitle}', 内容: '{messageContent}'");
            }
        }

        private void Update()
        {
            if (hasTriggered && triggerOnce) return;
            if (isDestroying) return;
            if (playerTransform == null)
            {
                FindPlayer();
                return;
            }

            CheckDistance();
        }

        #endregion

        #region 核心逻辑

        /// <summary>
        /// 验证设置
        /// </summary>
        private void ValidateSettings()
        {
            if (string.IsNullOrEmpty(messageContent))
            {
                messageContent = "这是一条消息";
                Debug.LogWarning($"⚠️ ProximityMessageSender [{name}]: 消息内容为空，已设置为默认值");
            }

            if (string.IsNullOrEmpty(messageTitle))
            {
                messageTitle = messageType == ProximityMessageType.Message ? "提示" : "警告";
                Debug.LogWarning($"⚠️ ProximityMessageSender [{name}]: 消息标题为空，已设置为默认值: '{messageTitle}'");
            }

            if (useCustomDisplayTime && customDisplayTime <= 0f)
            {
                customDisplayTime = 5f;
                Debug.LogWarning($"⚠️ ProximityMessageSender [{name}]: 自定义显示时间无效，已设置为默认值: {customDisplayTime}s");
            }
        }

        /// <summary>
        /// 查找玩家对象
        /// </summary>
        private void FindPlayer()
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag(TARGET_TAG);

            if (playerObj != null)
            {
                Player playerComponent = playerObj.GetComponent<Player>();
                if (playerComponent != null)
                {
                    playerTransform = playerObj.transform;
                    if (enableDebugLog)
                    {
                        Debug.Log($"📡 ProximityMessageSender [{name}]: 找到玩家 '{playerObj.name}'");
                    }
                }
            }
        }

        /// <summary>
        /// 检查距离并触发消息
        /// </summary>
        private void CheckDistance()
        {
            float distance = Vector3.Distance(transform.position, playerTransform.position);

            if (distance <= TRIGGER_DISTANCE)
            {
                TriggerMessage();
            }
        }

        /// <summary>
        /// 触发消息显示
        /// </summary>
        private void TriggerMessage()
        {
            if (hasTriggered && triggerOnce) return;
            if (isDestroying) return;

            hasTriggered = true;

            if (enableDebugLog)
            {
                string typeText = messageType == ProximityMessageType.Message ? "消息" : "警告";
                Debug.Log($"📡 ProximityMessageSender [{name}]: 触发{typeText} - 标题: '{messageTitle}', 内容: '{messageContent}'");
            }

            // 根据消息类型调用不同的显示方法
            if (messageType == ProximityMessageType.Message)
            {
                // 显示Message类型
                if (useCustomDisplayTime)
                {
                    InfoDisplayUI.ShowMessage(messageTitle, messageContent, customDisplayTime);
                }
                else
                {
                    InfoDisplayUI.ShowMessage(messageTitle, messageContent); // 使用默认时间
                }
            }
            else
            {
                // 显示Alert类型
                if (useCustomDisplayTime)
                {
                    InfoDisplayUI.ShowAlert(messageTitle, messageContent, customDisplayTime);
                }
                else
                {
                    InfoDisplayUI.ShowAlert(messageTitle, messageContent); // 使用默认时间
                }
            }

            // 触发事件
            OnMessageTriggered?.Invoke(this, playerTransform.gameObject);

            // 如果设置了销毁，则延迟销毁
            if (destroyAfterTrigger)
            {
                DestroyWithDelay();
            }
        }

        /// <summary>
        /// 延迟销毁对象
        /// </summary>
        private void DestroyWithDelay()
        {
            if (isDestroying) return;

            isDestroying = true;

            if (enableDebugLog)
            {
                Debug.Log($"📡 ProximityMessageSender [{name}]: 将在 {DESTROY_DELAY}s 后销毁");
            }

            // 使用Invoke延迟销毁
            Invoke(nameof(DestroyObject), DESTROY_DELAY);
        }

        /// <summary>
        /// 销毁对象
        /// </summary>
        private void DestroyObject()
        {
            if (enableDebugLog)
            {
                Debug.Log($"📡 ProximityMessageSender [{name}]: 正在销毁对象");
            }

            Destroy(gameObject);
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 手动触发消息（忽略距离检测）
        /// </summary>
        public void ManualTrigger()
        {
            if (enableDebugLog)
            {
                Debug.Log($"📡 ProximityMessageSender [{name}]: 手动触发消息");
            }

            TriggerMessage();
        }

        /// <summary>
        /// 重置触发状态（允许再次触发）
        /// </summary>
        public void ResetTrigger()
        {
            hasTriggered = false;
            isDestroying = false;

            if (enableDebugLog)
            {
                Debug.Log($"📡 ProximityMessageSender [{name}]: 触发状态已重置");
            }
        }

        /// <summary>
        /// 设置消息类型
        /// </summary>
        public void SetMessageType(ProximityMessageType type)
        {
            messageType = type;

            if (enableDebugLog)
            {
                string typeText = type == ProximityMessageType.Message ? "消息" : "警告";
                Debug.Log($"📡 ProximityMessageSender [{name}]: 消息类型已更新为 {typeText}");
            }
        }

        /// <summary>
        /// 设置消息标题
        /// </summary>
        public void SetMessageTitle(string title)
        {
            messageTitle = string.IsNullOrEmpty(title) ? "提示" : title;

            if (enableDebugLog)
            {
                Debug.Log($"📡 ProximityMessageSender [{name}]: 消息标题已更新为 '{messageTitle}'");
            }
        }

        /// <summary>
        /// 设置消息内容
        /// </summary>
        public void SetMessageContent(string content)
        {
            messageContent = string.IsNullOrEmpty(content) ? "这是一条消息" : content;

            if (enableDebugLog)
            {
                Debug.Log($"📡 ProximityMessageSender [{name}]: 消息内容已更新为 '{messageContent}'");
            }
        }

        /// <summary>
        /// 设置自定义显示时间
        /// </summary>
        public void SetCustomDisplayTime(float time, bool enable = true)
        {
            useCustomDisplayTime = enable;
            customDisplayTime = Mathf.Max(0.1f, time);

            if (enableDebugLog)
            {
                Debug.Log($"📡 ProximityMessageSender [{name}]: 自定义显示时间设置为 {customDisplayTime}s，启用: {enable}");
            }
        }

        /// <summary>
        /// 使用默认显示时间
        /// </summary>
        public void UseDefaultDisplayTime()
        {
            useCustomDisplayTime = false;

            if (enableDebugLog)
            {
                Debug.Log($"📡 ProximityMessageSender [{name}]: 已切换为使用默认显示时间");
            }
        }

        /// <summary>
        /// 检查是否已触发
        /// </summary>
        public bool HasTriggered() => hasTriggered;

        /// <summary>
        /// 检查是否正在销毁
        /// </summary>
        public bool IsDestroying() => isDestroying;

        /// <summary>
        /// 获取消息类型
        /// </summary>
        public ProximityMessageType GetMessageType() => messageType;

        /// <summary>
        /// 获取消息标题
        /// </summary>
        public string GetMessageTitle() => messageTitle;

        /// <summary>
        /// 获取消息内容
        /// </summary>
        public string GetMessageContent() => messageContent;

        /// <summary>
        /// 获取当前与玩家的距离
        /// </summary>
        public float GetDistanceToPlayer()
        {
            if (playerTransform == null) return float.MaxValue;
            return Vector3.Distance(transform.position, playerTransform.position);
        }

        /// <summary>
        /// 强制销毁对象
        /// </summary>
        public void ForceDestroy()
        {
            if (enableDebugLog)
            {
                Debug.Log($"📡 ProximityMessageSender [{name}]: 强制销毁");
            }

            CancelInvoke(); // 取消所有Invoke调用
            DestroyObject();
        }

        #endregion

        #region 调试功能

        /// <summary>
        /// 绘制调试信息
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;

            // 根据消息类型选择颜色
            Color baseColor = messageType == ProximityMessageType.Message ? Color.cyan : Color.yellow;
            Color gizmoColor = hasTriggered ? Color.red : baseColor;
            Gizmos.color = gizmoColor;

            // 绘制触发距离球体
            Gizmos.DrawWireSphere(transform.position, TRIGGER_DISTANCE);

            // 如果已触发，绘制实心球体
            if (hasTriggered)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
                Gizmos.DrawSphere(transform.position, TRIGGER_DISTANCE);
            }

            // 如果找到了玩家，绘制连线
            if (playerTransform != null)
            {
                Gizmos.color = messageType == ProximityMessageType.Message ? Color.green : new Color(1f, 0.5f, 0f); // orange color
                Gizmos.DrawLine(transform.position, playerTransform.position);
            }
        }

        /// <summary>
        /// 绘制选中时的调试信息
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos) return;

            // 绘制中心点，根据消息类型选择形状
            Gizmos.color = Color.white;
            if (messageType == ProximityMessageType.Message)
            {
                Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
            }
            else
            {
                Gizmos.DrawSphere(transform.position, 0.25f);
            }
        }

        #endregion

        #region Context Menu（Inspector右键菜单）

        [ContextMenu("🧪 手动触发消息")]
        private void DebugManualTrigger()
        {
            if (Application.isPlaying)
            {
                ManualTrigger();
            }
            else
            {
                Debug.Log("📡 请在运行时使用此功能");
            }
        }

        [ContextMenu("🔄 重置触发状态")]
        private void DebugResetTrigger()
        {
            if (Application.isPlaying)
            {
                ResetTrigger();
            }
            else
            {
                hasTriggered = false;
                isDestroying = false;
                Debug.Log("📡 触发状态已重置（编辑器模式）");
            }
        }

        [ContextMenu("🔍 查找玩家")]
        private void DebugFindPlayer()
        {
            if (Application.isPlaying)
            {
                FindPlayer();
                if (playerTransform != null)
                {
                    Debug.Log($"📡 找到玩家: {playerTransform.name}, 距离: {GetDistanceToPlayer():F1}m");
                }
                else
                {
                    Debug.Log("📡 未找到玩家");
                }
            }
            else
            {
                Debug.Log("📡 请在运行时使用此功能");
            }
        }

        [ContextMenu("💥 强制销毁")]
        private void DebugForceDestroy()
        {
            if (Application.isPlaying)
            {
                ForceDestroy();
            }
            else
            {
                Debug.Log("📡 请在运行时使用此功能");
            }
        }

        [ContextMenu("🔄 切换消息类型")]
        private void DebugToggleMessageType()
        {
            if (messageType == ProximityMessageType.Message)
            {
                SetMessageType(ProximityMessageType.Alert);
            }
            else
            {
                SetMessageType(ProximityMessageType.Message);
            }

            Debug.Log($"📡 消息类型已切换为: {messageType}");
        }

        [ContextMenu("📊 显示详细状态")]
        private void DebugShowDetailedStatus()
        {
            Debug.Log("=== ProximityMessageSender 详细状态 ===");
            Debug.Log($"对象名称: {name}");
            Debug.Log($"消息类型: {messageType}");
            Debug.Log($"消息标题: '{messageTitle}'");
            Debug.Log($"消息内容: '{messageContent}'");
            Debug.Log($"触发距离: {TRIGGER_DISTANCE}m (固定)");
            Debug.Log($"只触发一次: {triggerOnce}");
            Debug.Log($"触发后销毁: {destroyAfterTrigger}");
            Debug.Log($"使用自定义显示时间: {useCustomDisplayTime}");
            if (useCustomDisplayTime)
            {
                Debug.Log($"自定义显示时间: {customDisplayTime}s");
            }
            Debug.Log($"已触发: {hasTriggered}");
            Debug.Log($"正在销毁: {isDestroying}");
            Debug.Log($"玩家引用: {(playerTransform != null ? playerTransform.name : "null")}");

            if (Application.isPlaying && playerTransform != null)
            {
                Debug.Log($"当前距离: {GetDistanceToPlayer():F1}m");
            }
        }

        [ContextMenu("⚡ 测试消息类型")]
        private void DebugTestMessageType()
        {
            if (!Application.isPlaying)
            {
                Debug.Log("📡 请在运行时使用此功能");
                return;
            }

            Debug.Log("📡 测试不同消息类型:");

            // 保存当前设置
            var originalType = messageType;
            var originalTitle = messageTitle;
            var originalContent = messageContent;

            // 测试Message类型
            SetMessageType(ProximityMessageType.Message);
            SetMessageTitle("测试消息");
            SetMessageContent("这是一条测试消息，会置顶显示并持久化");
            InfoDisplayUI.ShowMessage(messageTitle, messageContent);

            // 等待一秒后测试Alert类型
            Invoke(nameof(TestAlertType), 1f);

            // 恢复原始设置
            messageType = originalType;
            messageTitle = originalTitle;
            messageContent = originalContent;
        }

        private void TestAlertType()
        {
            InfoDisplayUI.ShowAlert("测试警告", "这是一条测试警告消息，会临时显示");
            Debug.Log("📡 消息类型测试完成");
        }

        #endregion
    }
}