using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace BugFixerGame
{
    [RequireComponent(typeof(RawImage))]
    public class SimpleManaOrb : MonoBehaviour
    {
        [Header("魔法球图片设置")]
        [Tooltip("魔法球状态图片列表（3张图片：满蓝、中间帧、空蓝）")]
        [SerializeField] private List<Texture2D> orbTextures = new List<Texture2D>();

        [Header("动画设置")]
        [Tooltip("动画播放间隔时间（秒）")]
        [SerializeField] private float animationInterval = 0.1f;
        [Tooltip("是否循环播放动画")]
        [SerializeField] private bool loopAnimation = false;

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        private RawImage rawImage;
        private bool isEmpty = false;
        private bool hasPlayedAnimation = false;
        private bool isAnimationPlaying = false;
        private int currentFrameIndex = 0;
        private Coroutine animationCoroutine;

        private void Awake()
        {
            // 获取RawImage组件
            rawImage = GetComponent<RawImage>();

            // 校验组件
            if (rawImage == null)
            {
                Debug.LogError($"[SimpleManaOrb] 缺少 RawImage 组件于 {name}，脚本已禁用");
                enabled = false;
                return;
            }

            // 校验图片列表
            ValidateTextures();
        }

        private void Start()
        {
            // 游戏一开始重置为满蓝
            ResetToFull();
        }

        /// <summary>
        /// 验证图片设置
        /// </summary>
        private void ValidateTextures()
        {
            if (orbTextures.Count < 3)
            {
                Debug.LogError($"[SimpleManaOrb] {name} 需要至少3张图片（满蓝、中间帧、空蓝），当前只有 {orbTextures.Count} 张");
                return;
            }

            // 检查是否有空的图片引用
            for (int i = 0; i < orbTextures.Count; i++)
            {
                if (orbTextures[i] == null)
                {
                    Debug.LogError($"[SimpleManaOrb] {name} 第 {i + 1} 张图片为空，请设置正确的Texture2D引用");
                }
            }

            if (showDebugInfo)
            {
                Debug.Log($"[SimpleManaOrb] {name} 图片验证完成，共 {orbTextures.Count} 张图片");
            }
        }

        /// <summary>
        /// 设置指定帧的图片
        /// </summary>
        private void SetFrame(int frameIndex)
        {
            if (rawImage == null || orbTextures.Count == 0) return;

            // 确保索引在有效范围内
            frameIndex = Mathf.Clamp(frameIndex, 0, orbTextures.Count - 1);

            if (orbTextures[frameIndex] != null)
            {
                rawImage.texture = orbTextures[frameIndex];
                currentFrameIndex = frameIndex;

                if (showDebugInfo)
                    Debug.Log($"[SimpleManaOrb] {name} 设置为第 {frameIndex + 1} 帧");
            }
            else
            {
                Debug.LogError($"[SimpleManaOrb] {name} 第 {frameIndex + 1} 帧图片为空");
            }
        }

        /// <summary>
        /// 重置到满蓝状态（显示第一张图片）
        /// </summary>
        public void ResetToFull()
        {
            // 停止任何正在进行的动画
            StopAnimation();

            isEmpty = false;
            hasPlayedAnimation = false;
            isAnimationPlaying = false;

            // 设置为第一帧（满蓝）
            SetFrame(0);

            if (showDebugInfo)
                Debug.Log($"[SimpleManaOrb] {name} 重置为满蓝");
        }

        /// <summary>
        /// 播放扣蓝动画
        /// </summary>
        public void PlayEmptyAnimation()
        {
            if (isEmpty || hasPlayedAnimation)
            {
                if (showDebugInfo)
                    Debug.Log($"[SimpleManaOrb] {name} 已播放或已为空，跳过");
                return;
            }

            if (orbTextures.Count < 3)
            {
                Debug.LogError($"[SimpleManaOrb] {name} 图片数量不足，无法播放动画");
                return;
            }

            isEmpty = true;
            hasPlayedAnimation = true;

            // 停止之前的动画
            StopAnimation();

            // 开始播放动画
            animationCoroutine = StartCoroutine(PlayAnimationCoroutine());

            if (showDebugInfo)
                Debug.Log($"[SimpleManaOrb] {name} 开始播放扣蓝动画");
        }

        /// <summary>
        /// 动画播放协程
        /// </summary>
        private IEnumerator PlayAnimationCoroutine()
        {
            isAnimationPlaying = true;

            // 播放所有帧
            for (int i = 0; i < orbTextures.Count; i++)
            {
                SetFrame(i);
                yield return new WaitForSeconds(animationInterval);
            }

            // 动画播放完毕
            isAnimationPlaying = false;

            // 确保停在最后一帧
            SetFrame(orbTextures.Count - 1);

            if (showDebugInfo)
                Debug.Log($"[SimpleManaOrb] {name} 动画播放完毕，停在最后一帧");

            // 如果需要循环播放
            if (loopAnimation && isEmpty)
            {
                yield return new WaitForSeconds(animationInterval);
                animationCoroutine = StartCoroutine(PlayAnimationCoroutine());
            }
        }

        /// <summary>
        /// 停止动画播放
        /// </summary>
        private void StopAnimation()
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }
            isAnimationPlaying = false;
        }

        /// <summary>
        /// 立即切空，不播放动画（直接显示最后一张图片）
        /// </summary>
        public void SetEmptyImmediate()
        {
            StopAnimation();

            isEmpty = true;
            hasPlayedAnimation = true;
            isAnimationPlaying = false;

            // 直接设置为最后一帧（空蓝）
            SetFrame(orbTextures.Count - 1);

            if (showDebugInfo)
                Debug.Log($"[SimpleManaOrb] {name} 立即切空");
        }

        /// <summary>
        /// 是否为空状态
        /// </summary>
        public bool IsEmpty() => isEmpty;

        /// <summary>
        /// 是否已播放过动画
        /// </summary>
        public bool HasPlayedAnimation() => hasPlayedAnimation;

        /// <summary>
        /// 动画是否正在播放
        /// </summary>
        public bool IsAnimationPlaying() => isAnimationPlaying;

        /// <summary>
        /// 获取动画进度 (0–1)
        /// </summary>
        public float GetAnimationProgress()
        {
            if (orbTextures.Count <= 1) return 0f;

            return (float)currentFrameIndex / (orbTextures.Count - 1);
        }

        /// <summary>
        /// 获取当前帧索引
        /// </summary>
        public int GetCurrentFrameIndex() => currentFrameIndex;

        /// <summary>
        /// 获取总帧数
        /// </summary>
        public int GetTotalFrames() => orbTextures.Count;

        /// <summary>
        /// 设置动画间隔时间
        /// </summary>
        public void SetAnimationInterval(float interval)
        {
            animationInterval = Mathf.Max(0.01f, interval);

            if (showDebugInfo)
                Debug.Log($"[SimpleManaOrb] {name} 动画间隔设置为 {animationInterval} 秒");
        }

        /// <summary>
        /// 设置是否循环播放
        /// </summary>
        public void SetLoopAnimation(bool loop)
        {
            loopAnimation = loop;

            if (showDebugInfo)
                Debug.Log($"[SimpleManaOrb] {name} 循环播放设置为 {loop}");
        }

        /// <summary>
        /// 添加图片到列表
        /// </summary>
        public void AddTexture(Texture2D texture)
        {
            if (texture != null)
            {
                orbTextures.Add(texture);

                if (showDebugInfo)
                    Debug.Log($"[SimpleManaOrb] {name} 添加图片 {texture.name}，当前共 {orbTextures.Count} 张");
            }
        }

        /// <summary>
        /// 清空图片列表
        /// </summary>
        public void ClearTextures()
        {
            orbTextures.Clear();

            if (showDebugInfo)
                Debug.Log($"[SimpleManaOrb] {name} 清空图片列表");
        }

        /// <summary>
        /// 设置图片列表
        /// </summary>
        public void SetTextures(List<Texture2D> textures)
        {
            orbTextures = new List<Texture2D>(textures);
            ValidateTextures();

            // 如果当前不为空状态，重置为满蓝
            if (!isEmpty)
            {
                ResetToFull();
            }

            if (showDebugInfo)
                Debug.Log($"[SimpleManaOrb] {name} 设置图片列表，共 {orbTextures.Count} 张");
        }

        /// <summary>
        /// 获取当前状态描述，用于 UIManager 调试或显示
        /// </summary>
        public string GetStatusDescription()
        {
            if (!isEmpty)
                return $"满蓝 (帧 {currentFrameIndex + 1}/{orbTextures.Count})";
            if (isAnimationPlaying)
                return $"变空中 (帧 {currentFrameIndex + 1}/{orbTextures.Count}, {GetAnimationProgress():P0})";
            return hasPlayedAnimation ? $"空蓝 (帧 {currentFrameIndex + 1}/{orbTextures.Count}, 已播放动画)" : $"空蓝 (帧 {currentFrameIndex + 1}/{orbTextures.Count}, 直接设置)";
        }

        /// <summary>
        /// 获取详细状态信息
        /// </summary>
        public string GetDetailedStatus()
        {
            return $"SimpleManaOrb [{name}]\n" +
                   $"  状态: {GetStatusDescription()}\n" +
                   $"  图片数量: {orbTextures.Count}\n" +
                   $"  当前帧: {currentFrameIndex + 1}/{orbTextures.Count}\n" +
                   $"  动画间隔: {animationInterval}秒\n" +
                   $"  循环播放: {loopAnimation}\n" +
                   $"  是否为空: {isEmpty}\n" +
                   $"  已播放动画: {hasPlayedAnimation}\n" +
                   $"  正在播放: {isAnimationPlaying}";
        }

        #region 调试功能

        private void OnValidate()
        {
            // 在编辑器中验证设置
            if (Application.isPlaying) return;

            ValidateTextures();
        }

        [ContextMenu("🔄 重置为满蓝")]
        private void TestResetToFull()
        {
            if (Application.isPlaying)
            {
                ResetToFull();
            }
        }

        [ContextMenu("✨ 播放扣蓝动画")]
        private void TestPlayEmptyAnimation()
        {
            if (Application.isPlaying)
            {
                PlayEmptyAnimation();
            }
        }

        [ContextMenu("⚡ 立即切空")]
        private void TestSetEmptyImmediate()
        {
            if (Application.isPlaying)
            {
                SetEmptyImmediate();
            }
        }

        [ContextMenu("🔍 显示详细状态")]
        private void ShowDetailedStatus()
        {
            Debug.Log(GetDetailedStatus());
        }

        [ContextMenu("📊 验证图片设置")]
        private void TestValidateTextures()
        {
            ValidateTextures();
        }

        #endregion

        private void OnDestroy()
        {
            // 清理协程
            StopAnimation();
        }
    }
}