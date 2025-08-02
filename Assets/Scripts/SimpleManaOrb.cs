// SimpleManaOrb.cs - 代码控制动画的魔法球组件
using UnityEngine;

namespace BugFixerGame
{
    /// <summary>
    /// 简化的魔法球组件 - 直接用代码控制动画
    /// 扣除蓝量时播放动画，播放一次后停留在最后一帧
    /// </summary>
    public class SimpleManaOrb : MonoBehaviour
    {
        [Header("魔法球设置")]
        [SerializeField] private Animation animationComponent;      // Animation组件
        [SerializeField] private string emptyAnimationName = "ManaOrb_Empty"; // 变空动画名称

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        private bool isEmpty = false;
        private bool hasPlayedAnimation = false;
        private AnimationClip emptyClip;

        #region Unity生命周期

        private void Awake()
        {
            // 获取Animation组件
            if (animationComponent == null)
                animationComponent = GetComponent<Animation>();

            if (animationComponent == null)
            {
                Debug.LogError($"魔法球 {gameObject.name} 没有找到Animation组件！请确保预制体包含Animation组件。");
                return;
            }

            // 获取动画片段
            if (!string.IsNullOrEmpty(emptyAnimationName))
            {
                emptyClip = animationComponent.GetClip(emptyAnimationName);
                if (emptyClip == null)
                {
                    Debug.LogError($"魔法球 {gameObject.name} 没有找到动画 '{emptyAnimationName}'！");
                }
                else
                {
                    // 设置动画不循环
                    emptyClip.wrapMode = WrapMode.ClampForever;
                    Debug.Log($"魔法球 {gameObject.name} 动画设置完成：{emptyAnimationName}");
                }
            }
        }

        private void Start()
        {
            // 确保一开始处于满蓝状态
            ResetToFull();
        }

        #endregion

        #region 魔法球控制

        /// <summary>
        /// 重置到满蓝状态
        /// </summary>
        public void ResetToFull()
        {
            isEmpty = false;
            hasPlayedAnimation = false;

            if (animationComponent != null)
            {
                // 停止所有动画
                animationComponent.Stop();

                // 重置到动画的第一帧（满蓝状态）
                if (emptyClip != null)
                {
                    animationComponent[emptyAnimationName].normalizedTime = 0f;
                    animationComponent[emptyAnimationName].enabled = true;
                    animationComponent.Sample(); // 采样到第一帧
                    animationComponent[emptyAnimationName].enabled = false;
                }
            }

            if (showDebugInfo)
                Debug.Log($"🔮 魔法球 {gameObject.name} 重置为满蓝状态");
        }

        /// <summary>
        /// 播放变空动画（从满蓝变成空蓝）
        /// </summary>
        public void PlayEmptyAnimation()
        {
            if (isEmpty || hasPlayedAnimation)
            {
                if (showDebugInfo)
                    Debug.Log($"⚠️ 魔法球 {gameObject.name} 已经是空状态或已播放动画，跳过");
                return;
            }

            if (animationComponent == null || emptyClip == null)
            {
                Debug.LogError($"❌ 魔法球 {gameObject.name} 动画组件或动画片段未设置");
                return;
            }

            isEmpty = true;
            hasPlayedAnimation = true;

            // 播放动画
            animationComponent.Stop(); // 先停止所有动画
            animationComponent.Play(emptyAnimationName);

            if (showDebugInfo)
                Debug.Log($"✨ 魔法球 {gameObject.name} 开始播放变空动画");
        }

        /// <summary>
        /// 直接设置为空状态（不播放动画）
        /// </summary>
        public void SetEmptyImmediate()
        {
            isEmpty = true;
            hasPlayedAnimation = true;

            if (animationComponent != null && emptyClip != null)
            {
                // 停止动画并跳到最后一帧
                animationComponent.Stop();
                animationComponent[emptyAnimationName].normalizedTime = 1f;
                animationComponent[emptyAnimationName].enabled = true;
                animationComponent.Sample(); // 采样到最后一帧
                animationComponent[emptyAnimationName].enabled = false;
            }

            if (showDebugInfo)
                Debug.Log($"⚡ 魔法球 {gameObject.name} 直接设置为空状态");
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 检查是否为空状态
        /// </summary>
        public bool IsEmpty() => isEmpty;

        /// <summary>
        /// 检查是否已播放过动画
        /// </summary>
        public bool HasPlayedAnimation() => hasPlayedAnimation;

        /// <summary>
        /// 设置动画名称
        /// </summary>
        public void SetEmptyAnimationName(string animationName)
        {
            emptyAnimationName = animationName;
            if (animationComponent != null)
            {
                emptyClip = animationComponent.GetClip(emptyAnimationName);
                if (emptyClip != null)
                {
                    emptyClip.wrapMode = WrapMode.ClampForever;
                }
            }
        }

        /// <summary>
        /// 设置Animation组件
        /// </summary>
        public void SetAnimationComponent(Animation animation)
        {
            animationComponent = animation;
        }

        /// <summary>
        /// 检查动画是否正在播放
        /// </summary>
        public bool IsAnimationPlaying()
        {
            return animationComponent != null && animationComponent.IsPlaying(emptyAnimationName);
        }

        /// <summary>
        /// 获取动画播放进度 (0-1)
        /// </summary>
        public float GetAnimationProgress()
        {
            if (animationComponent != null && animationComponent[emptyAnimationName] != null)
            {
                return animationComponent[emptyAnimationName].normalizedTime;
            }
            return 0f;
        }

        /// <summary>
        /// 获取当前状态描述
        /// </summary>
        public string GetStatusDescription()
        {
            if (isEmpty)
            {
                if (IsAnimationPlaying())
                    return $"变空中({GetAnimationProgress():P0})";
                else
                    return hasPlayedAnimation ? "空蓝(已播放动画)" : "空蓝(直接设置)";
            }
            else
                return "满蓝";
        }

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
                string status = GetStatusDescription();
                string animInfo = animationComponent != null ? emptyAnimationName : "无Animation";

                GUI.Box(new Rect(guiPos.x - 60, guiPos.y - 40, 120, 80),
                    $"魔法球\n{status}\n{animInfo}\n进度:{GetAnimationProgress():P0}");
            }
        }

        [ContextMenu("🎬 测试变空动画")]
        private void TestEmptyAnimation()
        {
            if (Application.isPlaying)
            {
                PlayEmptyAnimation();
            }
            else
            {
                Debug.Log("请在运行时测试动画");
            }
        }

        [ContextMenu("🔄 重置为满状态")]
        private void TestResetToFull()
        {
            if (Application.isPlaying)
            {
                ResetToFull();
            }
            else
            {
                Debug.Log("请在运行时测试重置");
            }
        }

        [ContextMenu("⚡ 直接设为空状态")]
        private void TestSetEmptyImmediate()
        {
            if (Application.isPlaying)
            {
                SetEmptyImmediate();
            }
            else
            {
                Debug.Log("请在运行时测试设置");
            }
        }

        [ContextMenu("🔍 检查组件状态")]
        private void CheckComponentStatus()
        {
            Debug.Log($"=== 魔法球组件状态检查 ===");
            Debug.Log($"GameObject: {gameObject.name}");
            Debug.Log($"Animation组件: {(animationComponent != null ? "已设置" : "未设置")}");
            Debug.Log($"动画名称: {emptyAnimationName}");
            Debug.Log($"动画片段: {(emptyClip != null ? emptyClip.name : "未找到")}");
            Debug.Log($"当前状态: {GetStatusDescription()}");
            Debug.Log($"动画进度: {GetAnimationProgress():P1}");
            Debug.Log($"正在播放: {IsAnimationPlaying()}");

            if (animationComponent != null)
            {
                Debug.Log($"Animation组件动画数量: {animationComponent.GetClipCount()}");
                foreach (AnimationState state in animationComponent)
                {
                    Debug.Log($"  - {state.clip.name}: WrapMode={state.clip.wrapMode}");
                }
            }
        }

        [ContextMenu("🎯 测试动画进度")]
        private void TestAnimationProgress()
        {
            if (Application.isPlaying && animationComponent != null && emptyClip != null)
            {
                StartCoroutine(MonitorAnimationProgress());
            }
        }

        private System.Collections.IEnumerator MonitorAnimationProgress()
        {
            Debug.Log("开始监控动画播放进度...");
            PlayEmptyAnimation();

            while (IsAnimationPlaying())
            {
                Debug.Log($"动画进度: {GetAnimationProgress():P1}");
                yield return new WaitForSeconds(0.2f);
            }

            Debug.Log($"动画播放结束，最终进度: {GetAnimationProgress():P1}");
        }

        #endregion
    }
}