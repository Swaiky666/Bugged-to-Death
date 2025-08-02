// MessageTrigger.cs - 消息触发器组件，当玩家接触特殊tag的碰撞器时显示消息
using UnityEngine;
using System.Collections;

namespace BugFixerGame
{
    /// <summary>
    /// 触发模式枚举
    /// </summary>
    public enum TriggerMode
    {
        OnEnter,        // 进入时触发
        OnExit,         // 离开时触发
        OnEnterAndExit, // 进入和离开时都触发
        OnStay          // 停留时持续触发
    }

    /// <summary>
    /// 消息触发器组件
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class MessageTrigger : MonoBehaviour
    {
        [Header("触发设置")]
        [SerializeField] private TriggerMode triggerMode = TriggerMode.OnEnter;
        [SerializeField] private string targetTag = "Player";           // 目标标签
        [SerializeField] private bool requirePlayerComponent = true;    // 是否需要Player组件

        [Header("消息内容")]
        [SerializeField] private string messageTitle = "消息标题";
        [SerializeField, TextArea(3, 6)]
        private string messageDescription = "这是一个触发器消息的描述内容";
        [SerializeField] private float displayTime = 5f;               // 显示时间（0为永久显示）

        [Header("进入时消息")]
        [SerializeField] private bool useCustomEnterMessage = false;
        [SerializeField] private string enterMessageTitle = "进入区域";
        [SerializeField, TextArea(2, 4)]
        private string enterMessageDescription = "你进入了特殊区域";

        [Header("离开时消息")]
        [SerializeField] private bool useCustomExitMessage = false;
        [SerializeField] private string exitMessageTitle = "离开区域";
        [SerializeField, TextArea(2, 4)]
        private string exitMessageDescription = "你离开了特殊区域";

        [Header("停留消息设置")]
        [SerializeField] private float stayTriggerInterval = 2f;        // 停留触发间隔
        [SerializeField] private bool showStayProgress = false;         // 显示停留进度

        [Header("触发限制")]
        [SerializeField] private bool canTriggerMultipleTimes = true;   // 可以多次触发
        [SerializeField] private float cooldownTime = 1f;              // 冷却时间
        [SerializeField] private int maxTriggerCount = -1;              // 最大触发次数（-1为无限制）

        [Header("特效设置")]
        [SerializeField] private GameObject triggerEffect;              // 触发特效
        [SerializeField] private AudioClip triggerSound;               // 触发音效
        [SerializeField] private bool destroyAfterTrigger = false;     // 触发后销毁

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private bool debugLogTriggers = true;

        // 状态变量
        private bool isPlayerInside = false;
        private bool hasTriggered = false;
        private float lastTriggerTime = 0f;
        private int triggerCount = 0;
        private Coroutine stayCoroutine;
        private Collider triggerCollider;
        private AudioSource audioSource;

        // 事件
        public static event System.Action<MessageTrigger, GameObject> OnTriggerActivated;
        public static event System.Action<MessageTrigger, GameObject> OnPlayerEnterTrigger;
        public static event System.Action<MessageTrigger, GameObject> OnPlayerExitTrigger;

        #region Unity生命周期

        private void Awake()
        {
            // 获取碰撞器组件
            triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
            else
            {
                Debug.LogError($"❌ MessageTrigger: {gameObject.name} 没有找到Collider组件！");
            }

            // 获取音频源组件
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null && triggerSound != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }

        private void Start()
        {
            ValidateSettings();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsValidTarget(other.gameObject))
            {
                HandlePlayerEnter(other.gameObject);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsValidTarget(other.gameObject))
            {
                HandlePlayerExit(other.gameObject);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (triggerMode == TriggerMode.OnStay && IsValidTarget(other.gameObject))
            {
                HandlePlayerStay(other.gameObject);
            }
        }

        private void OnDestroy()
        {
            if (stayCoroutine != null)
            {
                StopCoroutine(stayCoroutine);
            }
        }

        #endregion

        #region 触发处理

        /// <summary>
        /// 处理玩家进入
        /// </summary>
        private void HandlePlayerEnter(GameObject player)
        {
            if (isPlayerInside) return;

            isPlayerInside = true;
            OnPlayerEnterTrigger?.Invoke(this, player);

            if (debugLogTriggers)
                Debug.Log($"🚪 玩家进入触发器: {gameObject.name}");

            switch (triggerMode)
            {
                case TriggerMode.OnEnter:
                    TriggerMessage(player, GetEnterMessage());
                    break;

                case TriggerMode.OnEnterAndExit:
                    TriggerMessage(player, GetEnterMessage());
                    break;

                case TriggerMode.OnStay:
                    StartStayTrigger(player);
                    break;
            }
        }

        /// <summary>
        /// 处理玩家离开
        /// </summary>
        private void HandlePlayerExit(GameObject player)
        {
            if (!isPlayerInside) return;

            isPlayerInside = false;
            OnPlayerExitTrigger?.Invoke(this, player);

            if (debugLogTriggers)
                Debug.Log($"🚪 玩家离开触发器: {gameObject.name}");

            // 停止停留触发
            if (stayCoroutine != null)
            {
                StopCoroutine(stayCoroutine);
                stayCoroutine = null;
            }

            switch (triggerMode)
            {
                case TriggerMode.OnExit:
                    TriggerMessage(player, GetExitMessage());
                    break;

                case TriggerMode.OnEnterAndExit:
                    TriggerMessage(player, GetExitMessage());
                    break;
            }
        }

        /// <summary>
        /// 处理玩家停留
        /// </summary>
        private void HandlePlayerStay(GameObject player)
        {
            // OnStay的逻辑在StartStayTrigger中通过协程处理
        }

        /// <summary>
        /// 开始停留触发
        /// </summary>
        private void StartStayTrigger(GameObject player)
        {
            if (stayCoroutine != null)
                StopCoroutine(stayCoroutine);

            stayCoroutine = StartCoroutine(StayTriggerCoroutine(player));
        }

        /// <summary>
        /// 停留触发协程
        /// </summary>
        private System.Collections.IEnumerator StayTriggerCoroutine(GameObject player)
        {
            float elapsed = 0f;

            while (isPlayerInside)
            {
                elapsed += Time.deltaTime;

                if (showStayProgress && elapsed < stayTriggerInterval)
                {
                    float progress = elapsed / stayTriggerInterval;
                    // 可以在这里显示进度条或其他UI反馈
                    if (debugLogTriggers && Mathf.FloorToInt(elapsed * 2) != Mathf.FloorToInt((elapsed - Time.deltaTime) * 2))
                    {
                        Debug.Log($"⏱️ 停留进度: {progress:P0}");
                    }
                }

                if (elapsed >= stayTriggerInterval)
                {
                    TriggerMessage(player, GetStayMessage());
                    elapsed = 0f; // 重置计时器以便重复触发
                }

                yield return null;
            }
        }

        #endregion

        #region 消息触发

        /// <summary>
        /// 触发消息显示
        /// </summary>
        private void TriggerMessage(GameObject player, (string title, string description) message)
        {
            if (!CanTrigger()) return;

            // 更新触发状态
            hasTriggered = true;
            lastTriggerTime = Time.time;
            triggerCount++;

            // 显示消息
            InfoDisplayUI.ShowMessage(message.title, message.description, displayTime);

            // 播放音效
            PlayTriggerSound();

            // 播放特效
            ShowTriggerEffect();

            // 触发事件
            OnTriggerActivated?.Invoke(this, player);

            if (debugLogTriggers)
                Debug.Log($"💬 触发消息: {message.title} - 触发次数: {triggerCount}");

            // 检查是否需要销毁
            if (destroyAfterTrigger || (maxTriggerCount > 0 && triggerCount >= maxTriggerCount))
            {
                StartCoroutine(DestroyAfterDelay(1f));
            }
        }

        /// <summary>
        /// 检查是否可以触发
        /// </summary>
        private bool CanTrigger()
        {
            // 检查是否可以多次触发
            if (!canTriggerMultipleTimes && hasTriggered)
                return false;

            // 检查冷却时间
            if (Time.time - lastTriggerTime < cooldownTime)
                return false;

            // 检查最大触发次数
            if (maxTriggerCount > 0 && triggerCount >= maxTriggerCount)
                return false;

            return true;
        }

        #endregion

        #region 消息内容获取

        /// <summary>
        /// 获取进入消息
        /// </summary>
        private (string title, string description) GetEnterMessage()
        {
            if (useCustomEnterMessage)
                return (enterMessageTitle, enterMessageDescription);
            else
                return (messageTitle, messageDescription);
        }

        /// <summary>
        /// 获取离开消息
        /// </summary>
        private (string title, string description) GetExitMessage()
        {
            if (useCustomExitMessage)
                return (exitMessageTitle, exitMessageDescription);
            else
                return (messageTitle + " (离开)", messageDescription);
        }

        /// <summary>
        /// 获取停留消息
        /// </summary>
        private (string title, string description) GetStayMessage()
        {
            return (messageTitle + " (停留)", messageDescription);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 检查是否是有效目标
        /// </summary>
        private bool IsValidTarget(GameObject target)
        {
            if (target == null) return false;

            // 检查标签
            if (!target.CompareTag(targetTag))
                return false;

            // 检查是否需要Player组件
            if (requirePlayerComponent && target.GetComponent<Player>() == null)
                return false;

            return true;
        }

        /// <summary>
        /// 播放触发音效
        /// </summary>
        private void PlayTriggerSound()
        {
            if (audioSource != null && triggerSound != null)
            {
                audioSource.clip = triggerSound;
                audioSource.Play();
            }
        }

        /// <summary>
        /// 显示触发特效
        /// </summary>
        private void ShowTriggerEffect()
        {
            if (triggerEffect != null)
            {
                GameObject effectInstance = Instantiate(triggerEffect, transform.position, transform.rotation);

                // 如果特效有粒子系统，在播放完成后销毁
                ParticleSystem particles = effectInstance.GetComponent<ParticleSystem>();
                if (particles != null)
                {
                    StartCoroutine(DestroyEffectAfterPlay(effectInstance, particles.main.duration + particles.main.startLifetime.constantMax));
                }
                else
                {
                    // 默认3秒后销毁特效
                    StartCoroutine(DestroyEffectAfterPlay(effectInstance, 3f));
                }
            }
        }

        /// <summary>
        /// 销毁特效
        /// </summary>
        private System.Collections.IEnumerator DestroyEffectAfterPlay(GameObject effect, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (effect != null)
                Destroy(effect);
        }

        /// <summary>
        /// 延迟销毁触发器
        /// </summary>
        private System.Collections.IEnumerator DestroyAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (debugLogTriggers)
                Debug.Log($"🗑️ 销毁触发器: {gameObject.name}");
            Destroy(gameObject);
        }

        /// <summary>
        /// 验证设置
        /// </summary>
        private void ValidateSettings()
        {
            bool hasWarnings = false;

            if (string.IsNullOrEmpty(messageTitle))
            {
                Debug.LogWarning($"⚠️ MessageTrigger: {gameObject.name} 消息标题为空");
                hasWarnings = true;
            }

            if (string.IsNullOrEmpty(messageDescription))
            {
                Debug.LogWarning($"⚠️ MessageTrigger: {gameObject.name} 消息描述为空");
                hasWarnings = true;
            }

            if (triggerCollider != null && !triggerCollider.isTrigger)
            {
                Debug.LogWarning($"⚠️ MessageTrigger: {gameObject.name} 碰撞器不是触发器，已自动设置为触发器");
                triggerCollider.isTrigger = true;
            }

            if (displayTime < 0)
            {
                Debug.LogWarning($"⚠️ MessageTrigger: {gameObject.name} 显示时间为负数，已重置为0（永久显示）");
                displayTime = 0f;
            }

            if (!hasWarnings && debugLogTriggers)
            {
                Debug.Log($"✅ MessageTrigger: {gameObject.name} 设置验证通过");
            }
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 手动触发消息
        /// </summary>
        public void ManualTrigger(GameObject target = null)
        {
            if (target == null)
            {
                // 尝试找到Player
                Player player = FindObjectOfType<Player>();
                target = player?.gameObject;
            }

            if (target != null)
            {
                TriggerMessage(target, (messageTitle, messageDescription));
            }
            else
            {
                Debug.LogWarning($"⚠️ MessageTrigger: {gameObject.name} 手动触发失败，未找到有效目标");
            }
        }

        /// <summary>
        /// 重置触发状态
        /// </summary>
        public void ResetTrigger()
        {
            hasTriggered = false;
            triggerCount = 0;
            lastTriggerTime = 0f;
            isPlayerInside = false;

            if (stayCoroutine != null)
            {
                StopCoroutine(stayCoroutine);
                stayCoroutine = null;
            }

            if (debugLogTriggers)
                Debug.Log($"🔄 重置触发器状态: {gameObject.name}");
        }

        /// <summary>
        /// 设置消息内容
        /// </summary>
        public void SetMessage(string title, string description, float time = 0f)
        {
            messageTitle = title;
            messageDescription = description;
            if (time >= 0f)
                displayTime = time;
        }

        /// <summary>
        /// 设置触发限制
        /// </summary>
        public void SetTriggerLimits(bool multipleTime, float cooldown, int maxCount = -1)
        {
            canTriggerMultipleTimes = multipleTime;
            cooldownTime = cooldown;
            maxTriggerCount = maxCount;
        }

        /// <summary>
        /// 获取触发状态
        /// </summary>
        public bool HasTriggered() => hasTriggered;
        public int GetTriggerCount() => triggerCount;
        public bool IsPlayerInside() => isPlayerInside;
        public bool CanTriggerNow() => CanTrigger();

        #endregion

        #region 调试功能

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            Vector3 screenPos = Camera.main?.WorldToScreenPoint(transform.position) ?? Vector3.zero;
            if (screenPos.z > 0 && screenPos.x > 0 && screenPos.x < Screen.width &&
                screenPos.y > 0 && screenPos.y < Screen.height)
            {
                Vector2 guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y);
                string status = $"触发器: {gameObject.name}\n模式: {triggerMode}\n玩家在内: {isPlayerInside}\n触发次数: {triggerCount}";

                if (isPlayerInside && triggerMode == TriggerMode.OnStay)
                {
                    status += $"\n停留中...";
                }

                GUI.Box(new Rect(guiPos.x - 80, guiPos.y - 40, 160, 80), status);
            }
        }

        [ContextMenu("💬 手动触发")]
        private void TestManualTrigger()
        {
            if (Application.isPlaying)
            {
                ManualTrigger();
            }
        }

        [ContextMenu("🔄 重置状态")]
        private void TestResetTrigger()
        {
            if (Application.isPlaying)
            {
                ResetTrigger();
            }
        }

        [ContextMenu("🔍 检查设置")]
        private void CheckSettings()
        {
            Debug.Log("=== MessageTrigger 设置检查 ===");
            Debug.Log($"触发器名称: {gameObject.name}");
            Debug.Log($"触发模式: {triggerMode}");
            Debug.Log($"目标标签: {targetTag}");
            Debug.Log($"需要Player组件: {requirePlayerComponent}");
            Debug.Log($"消息标题: {messageTitle}");
            Debug.Log($"消息描述: {messageDescription}");
            Debug.Log($"显示时间: {displayTime}s");
            Debug.Log($"多次触发: {canTriggerMultipleTimes}");
            Debug.Log($"冷却时间: {cooldownTime}s");
            Debug.Log($"最大触发次数: {(maxTriggerCount > 0 ? maxTriggerCount.ToString() : "无限制")}");
            Debug.Log($"触发音效: {(triggerSound != null ? triggerSound.name : "无")}");
            Debug.Log($"触发特效: {(triggerEffect != null ? triggerEffect.name : "无")}");
            Debug.Log($"碰撞器是触发器: {(triggerCollider != null ? triggerCollider.isTrigger.ToString() : "无碰撞器")}");
            Debug.Log($"当前状态 - 已触发: {hasTriggered}, 触发次数: {triggerCount}, 玩家在内: {isPlayerInside}");
        }

        [ContextMenu("🎯 测试所有消息")]
        private void TestAllMessages()
        {
            if (Application.isPlaying)
            {
                Debug.Log("测试进入消息...");
                var enterMsg = GetEnterMessage();
                InfoDisplayUI.ShowMessage(enterMsg.title, enterMsg.description, 2f);

                StartCoroutine(TestMessagesSequence());
            }
        }

        private System.Collections.IEnumerator TestMessagesSequence()
        {
            yield return new WaitForSeconds(2.5f);

            Debug.Log("测试离开消息...");
            var exitMsg = GetExitMessage();
            InfoDisplayUI.ShowMessage(exitMsg.title, exitMsg.description, 2f);

            yield return new WaitForSeconds(2.5f);

            Debug.Log("测试停留消息...");
            var stayMsg = GetStayMessage();
            InfoDisplayUI.ShowMessage(stayMsg.title, stayMsg.description, 2f);
        }

        #endregion
    }
}