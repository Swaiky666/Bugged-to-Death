// ProximityMessageSender.cs
using UnityEngine;

namespace BugFixerGame
{
    public class ProximityMessageSender : MonoBehaviour
    {
        [Header("消息内容")]
        [SerializeField][TextArea(3, 5)] private string messageContent = "这是一条消息"; // 消息内容

        [Header("行为设置")]
        [SerializeField] private bool triggerOnce = true;              // 是否只触发一次
        [SerializeField] private bool destroyAfterTrigger = true;      // 触发后是否销毁

        [Header("调试设置")]
        [SerializeField] private bool showDebugGizmos = true;          // 是否显示调试球体
        [SerializeField] private bool enableDebugLog = true;           // 是否启用调试日志

        // 固定参数（不在Inspector中显示）
        private const float TRIGGER_DISTANCE = 3f;        // 固定触发距离3米
        private const string TARGET_TAG = "Player";       // 固定目标标签
        private const string MESSAGE_TITLE = "提示";      // 固定消息标题
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
                Debug.Log($"📡 ProximityMessageSender [{name}] 初始化完成 - 内容: '{messageContent}'");
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
                Debug.Log($"📡 ProximityMessageSender [{name}]: 触发消息 - '{messageContent}'");
            }

            // 显示消息（使用默认显示时间）
            InfoDisplayUI.ShowMessage(MESSAGE_TITLE, messageContent);

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
        /// 设置消息内容
        /// </summary>
        public void SetMessageContent(string content)
        {
            messageContent = content;

            if (enableDebugLog)
            {
                Debug.Log($"📡 ProximityMessageSender [{name}]: 消息内容已更新为 '{content}'");
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

            // 选择颜色
            Color gizmoColor = hasTriggered ? Color.red : Color.cyan;
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
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, playerTransform.position);
            }
        }

        /// <summary>
        /// 绘制选中时的调试信息
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos) return;

            // 绘制中心点
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
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

        [ContextMenu("📊 显示状态信息")]
        private void DebugShowStatus()
        {
            Debug.Log("=== ProximityMessageSender 状态 ===");
            Debug.Log($"对象名称: {name}");
            Debug.Log($"触发距离: {TRIGGER_DISTANCE}m (固定)");
            Debug.Log($"消息内容: '{messageContent}'");
            Debug.Log($"只触发一次: {triggerOnce}");
            Debug.Log($"触发后销毁: {destroyAfterTrigger}");
            Debug.Log($"已触发: {hasTriggered}");
            Debug.Log($"正在销毁: {isDestroying}");
            Debug.Log($"玩家引用: {(playerTransform != null ? playerTransform.name : "null")}");

            if (Application.isPlaying && playerTransform != null)
            {
                Debug.Log($"当前距离: {GetDistanceToPlayer():F1}m");
            }
        }

        #endregion
    }
}