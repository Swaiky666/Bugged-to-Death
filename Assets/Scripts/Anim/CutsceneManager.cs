// CutsceneManager.cs - 专门管理过场动画存储的脚本（支持手动时间设置）
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BugFixerGame
{
    /// <summary>
    /// 过场动画的分类
    /// </summary>
    public enum CutsceneCategory
    {
        Intro,
        HappyEnd,
        BadEnd
    }
    /// <summary>
    /// 过场动画信息类
    /// </summary>
    [System.Serializable]
    public class CutsceneInfo
    {
        

        [Header("—— 分类 ——")]
        public CutsceneCategory category = CutsceneCategory.Intro;

        [Header("基本信息")]
        public GameObject prefab;
        public string animationName = "";

        [Header("手动时间设置")]
        [Tooltip("是否使用手动设置的播放时间")]
        public bool useManualDuration = false;              // 是否使用手动时间
        [Tooltip("手动设置的播放时间（秒）")]
        public float manualDuration = 5f;                   // 手动设置的时间

        [Header("自动检测的动画详情")]
        [ReadOnly] public float duration = 0f;              // 自动检测的动画持续时间（秒）
        [ReadOnly] public bool isLooping = false;           // 是否循环
        [ReadOnly] public bool hasAnimator = false;         // 是否有Animator
        [ReadOnly] public bool hasValidClip = false;        // 是否有有效的动画Clip
        [ReadOnly] public int clipCount = 0;                // 动画Clip数量
        [ReadOnly] public string clipNames = "";            // 动画Clip名称列表

        [Header("状态")]
        [ReadOnly] public bool isAnalyzed = false;          // 是否已分析
        [ReadOnly] public string lastError = "";            // 最后的错误信息

        public CutsceneInfo(GameObject prefab)
        {
            this.prefab = prefab;
            this.animationName = prefab != null ? prefab.name : "未知";
        }

        /// <summary>
        /// 重置分析数据
        /// </summary>
        public void ResetAnalysis()
        {
            duration = 0f;
            isLooping = false;
            hasAnimator = false;
            hasValidClip = false;
            clipCount = 0;
            clipNames = "";
            isAnalyzed = false;
            lastError = "";
        }

        /// <summary>
        /// 获取状态摘要
        /// </summary>
        public string GetStatusSummary()
        {
            if (useManualDuration)
            {
                return $"手动设置: {manualDuration:F1}秒";
            }

            if (!isAnalyzed) return "未分析";
            if (!string.IsNullOrEmpty(lastError)) return $"错误: {lastError}";
            if (!hasAnimator) return "无Animator";
            if (!hasValidClip) return "无动画Clip";

            string loopStatus = isLooping ? " (循环)" : "";
            return $"自动检测: {duration:F1}秒{loopStatus}";
        }

        /// <summary>
        /// 获取实际使用的播放时长
        /// </summary>
        public float GetEffectiveDuration()
        {
            if (useManualDuration)
            {
                return manualDuration;
            }

            if (isAnalyzed && hasValidClip)
            {
                return duration;
            }

            return 0f;
        }

        /// <summary>
        /// 设置手动时间
        /// </summary>
        public void SetManualDuration(float time, bool enable = true)
        {
            manualDuration = time;
            useManualDuration = enable;
        }

        /// <summary>
        /// 切换到自动检测模式
        /// </summary>
        public void UseAutoDetection()
        {
            useManualDuration = false;
        }
    }

    /// <summary>
    /// 过场动画管理器 - 专门负责存储和管理过场动画prefab
    /// </summary>
    public class CutsceneManager : MonoBehaviour
    {
        [Header("过场动画存储")]
        [SerializeField] private List<CutsceneInfo> cutsceneInfos = new List<CutsceneInfo>();   // 过场动画信息列表
        [SerializeField] private bool autoAnalyzeOnAdd = true;                                   // 添加时自动分析
        [SerializeField] private bool showDetailedInfo = true;                                   // 显示详细信息

        [Header("动画容器设置")]
        [SerializeField] private bool autoCreateContainer = true;                               // 自动创建动画容器
        [SerializeField] private string containerName = "CutsceneContainer";                   // 容器名称
        [SerializeField] private Transform cutsceneContainer;                                    // 过场动画实例化容器

        [Header("管理器设置")]
        [SerializeField] private bool enableCutscenes = true;                                   // 是否启用过场动画
        [SerializeField] private bool debugMode = true;                                         // 调试模式

        [Header("运行时信息")]
        [SerializeField, ReadOnly] private bool isInitialized = false;                         // 是否已初始化
        [SerializeField, ReadOnly] private int totalCutscenes = 0;                            // 总动画数量
        [SerializeField, ReadOnly] private bool isHidden = false;                             // 是否已隐藏
        [SerializeField, ReadOnly] private float totalDuration = 0f;                          // 总动画时长
        [SerializeField, ReadOnly] private int analyzedCount = 0;                             // 已分析数量
        [SerializeField, ReadOnly] private int validAnimationsCount = 0;                      // 有效动画数量

        public static CutsceneManager Instance { get; private set; }

        // 事件
        public static event System.Action<CutsceneManager> OnCutsceneManagerInitialized;
        public static event System.Action<bool> OnVisibilityChanged; // 显隐状态改变

        #region Unity生命周期

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // 不使用DontDestroyOnLoad，让它跟随场景
                InitializeCutsceneManager();
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning("⚠️ CutsceneManager: 发现重复实例，销毁当前对象");
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // 在Start中再次确保初始化完成
            if (!isInitialized)
            {
                InitializeCutsceneManager();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化过场动画管理器
        /// </summary>
        private void InitializeCutsceneManager()
        {
            if (isInitialized)
            {
                if (debugMode)
                    Debug.Log("🎬 CutsceneManager: 已经初始化过，跳过");
                return;
            }

            if (debugMode)
                Debug.Log("🎬 CutsceneManager: 开始初始化");

            // 自动创建容器
            if (autoCreateContainer && cutsceneContainer == null)
            {
                CreateCutsceneContainer();
            }

            // 验证容器
            if (cutsceneContainer == null)
            {
                Debug.LogWarning("⚠️ CutsceneManager: 过场动画容器未设置，使用当前Transform作为容器");
                cutsceneContainer = transform;
            }

            // 更新统计信息
            UpdateCutsceneStats();

            // 自动分析动画信息
            if (autoAnalyzeOnAdd && cutsceneInfos.Count > 0)
            {
                AnalyzeAllAnimations();
            }

            // 初始状态设置
            SetVisibility(enableCutscenes);

            isInitialized = true;

            if (debugMode)
            {
                Debug.Log($"🎬 CutsceneManager: 初始化完成 - 共{totalCutscenes}个动画，已分析{analyzedCount}个，有效{validAnimationsCount}个，总时长{totalDuration:F1}秒，启用状态: {enableCutscenes}");
            }

            // 触发初始化事件
            OnCutsceneManagerInitialized?.Invoke(this);
        }

        /// <summary>
        /// 创建过场动画容器
        /// </summary>
        private void CreateCutsceneContainer()
        {
            GameObject containerObj = new GameObject(containerName);
            containerObj.transform.SetParent(transform);
            cutsceneContainer = containerObj.transform;

            if (debugMode)
                Debug.Log($"🎬 CutsceneManager: 自动创建动画容器: {containerName}");
        }

        /// <summary>
        /// 更新过场动画统计信息
        /// </summary>
        private void UpdateCutsceneStats()
        {
            // 清理null引用和无效项目
            for (int i = cutsceneInfos.Count - 1; i >= 0; i--)
            {
                if (cutsceneInfos[i] == null || cutsceneInfos[i].prefab == null)
                {
                    cutsceneInfos.RemoveAt(i);
                }
            }

            totalCutscenes = cutsceneInfos.Count;
            analyzedCount = 0;
            validAnimationsCount = 0;
            totalDuration = 0f;

            // 统计分析信息
            foreach (var info in cutsceneInfos)
            {
                if (info.useManualDuration)
                {
                    // 手动设置的动画算作有效
                    validAnimationsCount++;
                    totalDuration += info.manualDuration;
                }
                else if (info.isAnalyzed)
                {
                    analyzedCount++;
                    if (info.hasValidClip)
                    {
                        validAnimationsCount++;
                        totalDuration += info.duration;
                    }
                }
            }

            if (debugMode && totalCutscenes > 0)
            {
                Debug.Log($"🎬 CutsceneManager: 统计信息更新");
                Debug.Log($"  总动画数量: {totalCutscenes}");
                Debug.Log($"  已分析: {analyzedCount}/{totalCutscenes}");
                Debug.Log($"  有效动画: {validAnimationsCount} (包括手动设置)");
                Debug.Log($"  总时长: {totalDuration:F1}秒");

                for (int i = 0; i < cutsceneInfos.Count; i++)
                {
                    var info = cutsceneInfos[i];
                    Debug.Log($"  {i + 1}. {info.animationName} - {info.GetStatusSummary()}");
                }
            }
        }

        #endregion

        #region 动画分析系统

        /// <summary>
        /// 分析所有动画信息
        /// </summary>
        public void AnalyzeAllAnimations()
        {
            if (debugMode)
                Debug.Log("🔍 CutsceneManager: 开始分析所有动画信息");

            foreach (var info in cutsceneInfos)
            {
                if (!info.useManualDuration) // 只分析非手动设置的动画
                {
                    AnalyzeSingleAnimation(info);
                }
            }

            UpdateCutsceneStats();

            if (debugMode)
                Debug.Log($"🔍 CutsceneManager: 动画分析完成 - 总计 {analyzedCount}/{totalCutscenes} 个已分析");
        }

        /// <summary>
        /// 分析单个动画信息
        /// </summary>
        private void AnalyzeSingleAnimation(CutsceneInfo info)
        {
            if (info == null || info.prefab == null)
            {
                if (info != null)
                {
                    info.lastError = "Prefab为空";
                    info.isAnalyzed = true;
                }
                return;
            }

            // 如果使用手动时间，跳过分析
            if (info.useManualDuration)
            {
                if (debugMode)
                    Debug.Log($"🎬 CutsceneManager: 跳过分析 {info.animationName} (使用手动时间设置)");
                return;
            }

            // 重置分析数据
            info.ResetAnalysis();

            try
            {
                // 获取Animator组件
                Animator animator = info.prefab.GetComponent<Animator>();
                if (animator == null)
                {
                    info.lastError = "没有Animator组件";
                    info.isAnalyzed = true;
                    return;
                }

                info.hasAnimator = true;

                // 检查AnimatorController
                if (animator.runtimeAnimatorController == null)
                {
                    info.lastError = "没有AnimatorController";
                    info.isAnalyzed = true;
                    return;
                }

                // 分析动画Clips
                var clips = GetAnimationClips(animator);
                info.clipCount = clips.Count;

                if (clips.Count == 0)
                {
                    info.lastError = "没有找到动画Clips";
                    info.isAnalyzed = true;
                    return;
                }

                // 分析第一个（主要）动画clip
                var primaryClip = clips[0];
                info.duration = primaryClip.length;
                info.isLooping = primaryClip.isLooping;
                info.hasValidClip = true;

                // 生成clip名称列表
                info.clipNames = string.Join(", ", clips.Select(c => c.name));

                // 如果有多个clips，使用最长的作为duration
                if (clips.Count > 1)
                {
                    info.duration = clips.Max(c => c.length);

                    if (debugMode)
                        Debug.Log($"🎬 CutsceneManager: {info.animationName} 有 {clips.Count} 个clips，使用最长时间: {info.duration:F2}秒");
                }

                info.isAnalyzed = true;

                if (debugMode)
                {
                    Debug.Log($"🎬 CutsceneManager: 分析完成 - {info.animationName}");
                    Debug.Log($"  时长: {info.duration:F2}秒, 循环: {info.isLooping}, Clips: {info.clipCount}");
                }
            }
            catch (System.Exception e)
            {
                info.lastError = $"分析异常: {e.Message}";
                info.isAnalyzed = true;

                if (debugMode)
                    Debug.LogError($"❌ CutsceneManager: 分析 {info.animationName} 时发生错误: {e.Message}");
            }
        }

        /// <summary>
        /// 获取Animator中的所有AnimationClip
        /// </summary>
        private List<AnimationClip> GetAnimationClips(Animator animator)
        {
            var clips = new List<AnimationClip>();

            if (animator.runtimeAnimatorController == null)
                return clips;

#if UNITY_EDITOR
            // 在编辑器中，可以直接获取AnimationClips
            var controller = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            if (controller != null)
            {
                foreach (var layer in controller.layers)
                {
                    foreach (var state in layer.stateMachine.states)
                    {
                        if (state.state.motion is AnimationClip clip)
                        {
                            clips.Add(clip);
                        }
                    }
                }
            }
#else
            // 运行时，尝试通过其他方式获取
            var clipInfos = animator.GetCurrentAnimatorClipInfo(0);
            foreach (var clipInfo in clipInfos)
            {
                if (clipInfo.clip != null)
                {
                    clips.Add(clipInfo.clip);
                }
            }
#endif

            return clips;
        }

        /// <summary>
        /// 分析特定索引的动画
        /// </summary>
        public void AnalyzeAnimation(int index)
        {
            if (index >= 0 && index < cutsceneInfos.Count)
            {
                AnalyzeSingleAnimation(cutsceneInfos[index]);
                UpdateCutsceneStats();
            }
        }

        /// <summary>
        /// 获取动画时长（优先使用手动设置）
        /// </summary>
        public float GetAnimationDuration(int index)
        {
            if (index >= 0 && index < cutsceneInfos.Count)
            {
                var info = cutsceneInfos[index];
                
                // 优先使用手动设置的时间
                if (info.useManualDuration)
                {
                    if (debugMode)
                        Debug.Log($"🎬 CutsceneManager: 使用手动设置时间 - {info.animationName}: {info.manualDuration:F1}秒");
                    return info.manualDuration;
                }
                
                // 使用自动检测的时间
                if (info.isAnalyzed && info.hasValidClip)
                {
                    if (debugMode)
                        Debug.Log($"🎬 CutsceneManager: 使用自动检测时间 - {info.animationName}: {info.duration:F1}秒");
                    return info.duration;
                }
                
                if (debugMode)
                    Debug.LogWarning($"⚠️ CutsceneManager: 动画 {info.animationName} 未分析或无有效Clip，返回0");
            }
            return 0f;
        }

        /// <summary>
        /// 获取所有动画的总时长（包括手动设置的）
        /// </summary>
        public float GetTotalDuration()
        {
            float total = 0f;
            foreach (var info in cutsceneInfos)
            {
                if (info?.prefab != null)
                {
                    total += info.GetEffectiveDuration();
                }
            }
            return total;
        }

        /// <summary>
        /// 获取动画详细信息
        /// </summary>
        public CutsceneInfo GetAnimationInfo(int index)
        {
            if (index >= 0 && index < cutsceneInfos.Count)
            {
                return cutsceneInfos[index];
            }
            return null;
        }

        /// <summary>
        /// 检查是否有循环动画
        /// </summary>
        public bool HasLoopingAnimations()
        {
            return cutsceneInfos.Any(info => !info.useManualDuration && info.isAnalyzed && info.hasValidClip && info.isLooping);
        }

        /// <summary>
        /// 获取所有循环动画的列表
        /// </summary>
        public List<CutsceneInfo> GetLoopingAnimations()
        {
            return cutsceneInfos.Where(info => !info.useManualDuration && info.isAnalyzed && info.hasValidClip && info.isLooping).ToList();
        }

        #endregion

        #region 手动时间设置管理

        /// <summary>
        /// 设置指定动画的手动播放时间
        /// </summary>
        public void SetManualDuration(int index, float duration, bool enable = true)
        {
            if (index >= 0 && index < cutsceneInfos.Count)
            {
                var info = cutsceneInfos[index];
                info.SetManualDuration(duration, enable);
                UpdateCutsceneStats();

                if (debugMode)
                    Debug.Log($"🎬 CutsceneManager: 设置手动时间 - {info.animationName}: {duration:F1}秒 (启用: {enable})");
            }
        }

        /// <summary>
        /// 切换指定动画到自动检测模式
        /// </summary>
        public void UseAutoDetection(int index)
        {
            if (index >= 0 && index < cutsceneInfos.Count)
            {
                var info = cutsceneInfos[index];
                info.UseAutoDetection();
                UpdateCutsceneStats();

                if (debugMode)
                    Debug.Log($"🎬 CutsceneManager: 切换到自动检测 - {info.animationName}");
            }
        }

        /// <summary>
        /// 获取有多少动画使用了手动时间设置
        /// </summary>
        public int GetManualTimingCount()
        {
            return cutsceneInfos.Count(info => info.useManualDuration);
        }

        /// <summary>
        /// 检查指定动画是否使用手动时间
        /// </summary>
        public bool IsUsingManualTiming(int index)
        {
            if (index >= 0 && index < cutsceneInfos.Count)
            {
                return cutsceneInfos[index].useManualDuration;
            }
            return false;
        }

        /// <summary>
        /// 批量设置所有动画使用自动检测
        /// </summary>
        public void SetAllToAutoDetection()
        {
            foreach (var info in cutsceneInfos)
            {
                info.UseAutoDetection();
            }
            UpdateCutsceneStats();

            if (debugMode)
                Debug.Log("🎬 CutsceneManager: 所有动画已切换到自动检测模式");
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 获取过场动画数量
        /// </summary>
        public int GetCutsceneCount()
        {
            return cutsceneInfos.Count;
        }

        /// <summary>
        /// 获取指定索引的过场动画prefab
        /// </summary>
        public GameObject GetCutscenePrefab(int index)
        {
            if (index < 0 || index >= cutsceneInfos.Count)
            {
                Debug.LogError($"❌ CutsceneManager: 动画索引超出范围: {index} (总数: {cutsceneInfos.Count})");
                return null;
            }

            var info = cutsceneInfos[index];
            if (info == null || info.prefab == null)
            {
                Debug.LogError($"❌ CutsceneManager: 动画信息 {index} 为空");
                return null;
            }

            return info.prefab;
        }

        /// <summary>
        /// 获取所有过场动画prefab（只读）
        /// </summary>
        public IReadOnlyList<GameObject> GetAllCutscenePrefabs()
        {
            return cutsceneInfos.Where(info => info?.prefab != null).Select(info => info.prefab).ToList().AsReadOnly();
        }

        /// <summary>
        /// 获取所有动画信息（只读）
        /// </summary>
        public IReadOnlyList<CutsceneInfo> GetAllCutsceneInfos()
        {
            return cutsceneInfos.AsReadOnly();
        }

        /// <summary>
        /// 获取过场动画容器
        /// </summary>
        public Transform GetCutsceneContainer()
        {
            return cutsceneContainer;
        }

        /// <summary>
        /// 检查是否有有效的过场动画
        /// </summary>
        public bool HasValidCutscenes()
        {
            return enableCutscenes && cutsceneInfos.Count > 0 && cutsceneInfos.Any(info => info?.prefab != null);
        }

        /// <summary>
        /// 检查是否启用过场动画
        /// </summary>
        public bool IsCutsceneEnabled()
        {
            return enableCutscenes;
        }

        /// <summary>
        /// 设置是否启用过场动画
        /// </summary>
        public void SetCutsceneEnabled(bool enabled)
        {
            if (enableCutscenes != enabled)
            {
                enableCutscenes = enabled;
                SetVisibility(enabled);

                if (debugMode)
                    Debug.Log($"🎬 CutsceneManager: 过场动画启用状态改变: {enabled}");
            }
        }

        /// <summary>
        /// 设置管理器的可见性
        /// </summary>
        public void SetVisibility(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
                isHidden = !visible;

                if (debugMode)
                    Debug.Log($"🎬 CutsceneManager: 设置可见性: {visible}");

                OnVisibilityChanged?.Invoke(visible);
            }
        }

        /// <summary>
        /// 隐藏过场动画管理器
        /// </summary>
        public void Hide()
        {
            SetVisibility(false);
        }

        /// <summary>
        /// 显示过场动画管理器
        /// </summary>
        public void Show()
        {
            SetVisibility(true);
        }

        /// <summary>
        /// 检查是否已隐藏
        /// </summary>
        public bool IsHidden()
        {
            return isHidden;
        }

        #endregion

        #region 动画管理

        /// <summary>
        /// 添加过场动画prefab
        /// </summary>
        public void AddCutscenePrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogWarning("⚠️ CutsceneManager: 尝试添加空的动画prefab");
                return;
            }

            var info = new CutsceneInfo(prefab);
            cutsceneInfos.Add(info);

            // 自动分析（如果启用且不使用手动时间）
            if (autoAnalyzeOnAdd && !info.useManualDuration)
            {
                AnalyzeSingleAnimation(info);
            }

            UpdateCutsceneStats();

            if (debugMode)
                Debug.Log($"🎬 CutsceneManager: 添加动画prefab: {prefab.name}");
        }

        /// <summary>
        /// 移除指定索引的过场动画prefab
        /// </summary>
        public bool RemoveCutscenePrefab(int index)
        {
            if (index < 0 || index >= cutsceneInfos.Count)
            {
                Debug.LogWarning($"⚠️ CutsceneManager: 尝试移除无效索引的动画: {index}");
                return false;
            }

            var removedInfo = cutsceneInfos[index];
            cutsceneInfos.RemoveAt(index);
            UpdateCutsceneStats();

            if (debugMode)
                Debug.Log($"🎬 CutsceneManager: 移除动画prefab: {removedInfo?.animationName ?? "null"}");

            return true;
        }

        /// <summary>
        /// 清空所有过场动画prefab
        /// </summary>
        public void ClearAllCutscenes()
        {
            int count = cutsceneInfos.Count;
            cutsceneInfos.Clear();
            UpdateCutsceneStats();

            if (debugMode)
                Debug.Log($"🎬 CutsceneManager: 清空所有动画prefab ({count}个)");
        }

        /// <summary>
        /// 验证所有动画prefab
        /// </summary>
        public void ValidateAllPrefabs()
        {
            if (debugMode)
                Debug.Log("🎬 CutsceneManager: 开始验证所有动画prefab");

            foreach (var info in cutsceneInfos)
            {
                if (info.prefab == null)
                {
                    Debug.LogWarning($"⚠️ CutsceneManager: 动画信息中有空的prefab");
                    continue;
                }

                if (info.useManualDuration)
                {
                    Debug.Log($"✅ CutsceneManager: 动画prefab {info.prefab.name} 使用手动时间设置: {info.manualDuration:F1}秒");
                    continue;
                }

                Animator animator = info.prefab.GetComponent<Animator>();
                if (animator == null)
                {
                    Debug.LogWarning($"⚠️ CutsceneManager: 动画prefab {info.prefab.name} 缺少Animator组件");
                }
                else if (animator.runtimeAnimatorController == null)
                {
                    Debug.LogWarning($"⚠️ CutsceneManager: 动画prefab {info.prefab.name} 的Animator缺少AnimatorController");
                }
                else
                {
                    if (debugMode)
                        Debug.Log($"✅ CutsceneManager: 动画prefab {info.prefab.name} 验证通过");
                }
            }

            if (debugMode)
                Debug.Log("🎬 CutsceneManager: 动画prefab验证完成");
        }

        #endregion

        #region 调试功能

        [Header("调试功能")]
        [SerializeField] private bool showDebugGUI = false;

        private void OnGUI()
        {
            if (!showDebugGUI) return;

            GUILayout.BeginArea(new Rect(Screen.width - 450, 10, 430, 600));
            GUILayout.Label("=== CutsceneManager 调试 ===");

            GUILayout.Label($"初始化状态: {isInitialized}");
            GUILayout.Label($"启用状态: {enableCutscenes}");
            GUILayout.Label($"隐藏状态: {isHidden}");
            GUILayout.Label($"动画数量: {totalCutscenes}");
            GUILayout.Label($"已分析: {analyzedCount}/{totalCutscenes}");
            GUILayout.Label($"有效动画: {validAnimationsCount}");
            GUILayout.Label($"手动设置: {GetManualTimingCount()}/{totalCutscenes}");
            GUILayout.Label($"总时长: {totalDuration:F1}秒");
            GUILayout.Label($"容器: {(cutsceneContainer != null ? cutsceneContainer.name : "null")}");

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("显示/隐藏管理器"))
            {
                SetVisibility(!gameObject.activeSelf);
            }
            if (GUILayout.Button("启用/禁用动画"))
            {
                SetCutsceneEnabled(!enableCutscenes);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("分析所有动画"))
            {
                AnalyzeAllAnimations();
            }
            if (GUILayout.Button("全部自动检测"))
            {
                SetAllToAutoDetection();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("更新统计信息"))
            {
                UpdateCutsceneStats();
            }
            if (GUILayout.Button("清空所有动画"))
            {
                ClearAllCutscenes();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("动画详细信息:");

            if (cutsceneInfos.Count > 0)
            {
                for (int i = 0; i < cutsceneInfos.Count; i++)
                {
                    var info = cutsceneInfos[i];
                    if (info?.prefab != null)
                    {
                        string status = info.GetStatusSummary();
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"{i + 1}. {info.animationName}", GUILayout.Width(150));
                        GUILayout.Label(status, GUILayout.Width(150));
                        
                        if (info.useManualDuration)
                        {
                            if (GUILayout.Button("自动", GUILayout.Width(40)))
                            {
                                UseAutoDetection(i);
                            }
                        }
                        else
                        {
                            if (GUILayout.Button("手动", GUILayout.Width(40)))
                            {
                                SetManualDuration(i, 5f, true);
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                    else
                    {
                        GUILayout.Label($"{i + 1}. null");
                    }
                }
            }
            else
            {
                GUILayout.Label("  没有动画");
            }

            // 显示循环动画警告
            if (HasLoopingAnimations())
            {
                GUILayout.Space(10);
                GUI.color = Color.yellow;
                GUILayout.Label("⚠️ 检测到循环动画:");
                GUI.color = Color.white;

                var loopingAnims = GetLoopingAnimations();
                foreach (var loopAnim in loopingAnims)
                {
                    GUILayout.Label($"  • {loopAnim.animationName}");
                }
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// 添加测试动画
        /// </summary>
        [ContextMenu("添加测试动画")]
        public void TestAddEmptyAnimation()
        {
            Debug.Log("🧪 CutsceneManager: 请在Inspector中手动添加动画prefab到列表");
        }

        /// <summary>
        /// 分析所有动画
        /// </summary>
        [ContextMenu("分析所有动画信息")]
        public void MenuAnalyzeAllAnimations()
        {
            AnalyzeAllAnimations();
        }

        /// <summary>
        /// 显示动画时长摘要
        /// </summary>
        [ContextMenu("显示动画时长摘要")]
        public void ShowDurationSummary()
        {
            Debug.Log("=== 动画时长摘要 ===");
            Debug.Log($"总动画数量: {totalCutscenes}");
            Debug.Log($"已分析数量: {analyzedCount}");
            Debug.Log($"有效动画数量: {validAnimationsCount}");
            Debug.Log($"手动设置数量: {GetManualTimingCount()}");
            Debug.Log($"总播放时长: {totalDuration:F1}秒 ({totalDuration / 60:F1}分钟)");

            if (cutsceneInfos.Count > 0)
            {
                Debug.Log("\n各动画详细时长:");
                for (int i = 0; i < cutsceneInfos.Count; i++)
                {
                    var info = cutsceneInfos[i];
                    if (info?.prefab != null)
                    {
                        float effectiveDuration = info.GetEffectiveDuration();
                        string durationText = effectiveDuration > 0 ? $"{effectiveDuration:F1}秒" : "无效";
                        string modeText = info.useManualDuration ? " (手动)" : " (自动)";
                        string loopText = (!info.useManualDuration && info.isLooping) ? " (循环)" : "";
                        Debug.Log($"  {i + 1}. {info.animationName}: {durationText}{modeText}{loopText}");
                    }
                }
            }

            if (HasLoopingAnimations())
            {
                Debug.Log("\n⚠️ 检测到循环动画:");
                var loopingAnims = GetLoopingAnimations();
                foreach (var anim in loopingAnims)
                {
                    Debug.Log($"  • {anim.animationName} - {anim.duration:F1}秒 (循环)");
                }
            }
        }

        /// <summary>
        /// 检查动画完整性
        /// </summary>
        [ContextMenu("检查动画完整性")]
        public void CheckAnimationIntegrity()
        {
            Debug.Log("=== 动画完整性检查 ===");

            int validCount = 0;
            int errorCount = 0;
            int warningCount = 0;
            int manualCount = 0;

            for (int i = 0; i < cutsceneInfos.Count; i++)
            {
                var info = cutsceneInfos[i];

                if (info?.prefab == null)
                {
                    Debug.LogError($"❌ 索引 {i}: Prefab为空");
                    errorCount++;
                    continue;
                }

                if (info.useManualDuration)
                {
                    if (info.manualDuration > 0)
                    {
                        Debug.Log($"✅ {info.animationName}: 手动设置时间 {info.manualDuration:F1}秒");
                        validCount++;
                        manualCount++;
                    }
                    else
                    {
                        Debug.LogWarning($"⚠️ {info.animationName}: 手动时间设置为0或负数");
                        warningCount++;
                    }
                    continue;
                }

                if (!info.isAnalyzed)
                {
                    Debug.LogWarning($"⚠️ {info.animationName}: 未分析");
                    warningCount++;
                    continue;
                }

                if (!string.IsNullOrEmpty(info.lastError))
                {
                    Debug.LogError($"❌ {info.animationName}: {info.lastError}");
                    errorCount++;
                    continue;
                }

                if (!info.hasAnimator)
                {
                    Debug.LogError($"❌ {info.animationName}: 缺少Animator组件");
                    errorCount++;
                    continue;
                }

                if (!info.hasValidClip)
                {
                    Debug.LogError($"❌ {info.animationName}: 没有有效的动画Clip");
                    errorCount++;
                    continue;
                }

                if (info.isLooping)
                {
                    Debug.LogWarning($"⚠️ {info.animationName}: 动画设置为循环播放");
                    warningCount++;
                }

                if (info.duration <= 0)
                {
                    Debug.LogWarning($"⚠️ {info.animationName}: 动画时长为0或负数");
                    warningCount++;
                }

                validCount++;
                Debug.Log($"✅ {info.animationName}: 自动检测通过 ({info.duration:F1}秒)");
            }

            Debug.Log($"\n检查结果: {validCount}个有效 (其中{manualCount}个手动设置), {warningCount}个警告, {errorCount}个错误");

            if (errorCount == 0 && warningCount == 0)
            {
                Debug.Log("🎉 所有动画检查通过！");
            }
            else if (errorCount > 0)
            {
                Debug.LogWarning("🔧 建议修复错误后再使用过场动画系统");
            }
        }

        /// <summary>
        /// 重置所有分析数据
        /// </summary>
        [ContextMenu("重置分析数据")]
        public void ResetAllAnalysisData()
        {
            foreach (var info in cutsceneInfos)
            {
                if (!info.useManualDuration) // 只重置非手动设置的动画
                {
                    info.ResetAnalysis();
                }
            }
            UpdateCutsceneStats();
            Debug.Log("🔄 CutsceneManager: 所有自动检测的分析数据已重置");
        }

        /// <summary>
        /// 强制重新初始化
        /// </summary>
        [ContextMenu("强制重新初始化")]
        public void ForceReinitialize()
        {
            isInitialized = false;
            InitializeCutsceneManager();
        }

        /// <summary>
        /// 显示详细信息
        /// </summary>
        [ContextMenu("显示详细信息")]
        public void ShowDetailedInfo()
        {
            Debug.Log("=== CutsceneManager 详细信息 ===");
            Debug.Log($"实例: {(Instance != null ? "有效" : "无效")}");
            Debug.Log($"GameObject: {gameObject.name} (活跃: {gameObject.activeSelf})");
            Debug.Log($"初始化状态: {isInitialized}");
            Debug.Log($"启用状态: {enableCutscenes}");
            Debug.Log($"隐藏状态: {isHidden}");
            Debug.Log($"动画数量: {totalCutscenes}");
            Debug.Log($"已分析: {analyzedCount}/{totalCutscenes}");
            Debug.Log($"有效动画: {validAnimationsCount}");
            Debug.Log($"手动设置: {GetManualTimingCount()}/{totalCutscenes}");
            Debug.Log($"总时长: {totalDuration:F1}秒");
            Debug.Log($"容器: {(cutsceneContainer != null ? cutsceneContainer.name : "未设置")}");
            Debug.Log($"调试模式: {debugMode}");
            Debug.Log($"自动分析: {autoAnalyzeOnAdd}");
            Debug.Log($"显示详细信息: {showDetailedInfo}");

            if (cutsceneInfos.Count > 0)
            {
                Debug.Log("\n动画详细信息:");
                for (int i = 0; i < cutsceneInfos.Count; i++)
                {
                    var info = cutsceneInfos[i];
                    if (info?.prefab != null)
                    {
                        Debug.Log($"  {i + 1}. {info.animationName}");
                        Debug.Log($"      状态: {info.GetStatusSummary()}");
                        Debug.Log($"      时间模式: {(info.useManualDuration ? "手动设置" : "自动检测")}");
                        if (info.useManualDuration)
                        {
                            Debug.Log($"      手动时间: {info.manualDuration:F1}秒");
                        }
                        else
                        {
                            Debug.Log($"      有Animator: {info.hasAnimator}");
                            Debug.Log($"      有效Clip: {info.hasValidClip}");
                            Debug.Log($"      检测时长: {info.duration:F1}秒");
                        }
                        Debug.Log($"      Clip数量: {info.clipCount}");
                        if (!string.IsNullOrEmpty(info.clipNames))
                        {
                            Debug.Log($"      Clip名称: {info.clipNames}");
                        }
                        if (!string.IsNullOrEmpty(info.lastError))
                        {
                            Debug.Log($"      错误: {info.lastError}");
                        }
                    }
                    else
                    {
                        Debug.Log($"  {i + 1}. null或无效");
                    }
                }
            }
            else
            {
                Debug.Log("没有动画信息");
            }

            if (HasLoopingAnimations())
            {
                Debug.Log("\n⚠️ 循环动画:");
                var loopingAnims = GetLoopingAnimations();
                foreach (var anim in loopingAnims)
                {
                    Debug.Log($"  • {anim.animationName} - {anim.duration:F1}秒");
                }
            }
        }

        /// <summary>
        /// 设置所有动画为5秒手动时间（测试用）
        /// </summary>
        [ContextMenu("测试：设置所有为5秒手动时间")]
        public void SetAllTo5SecondsManual()
        {
            for (int i = 0; i < cutsceneInfos.Count; i++)
            {
                SetManualDuration(i, 5f, true);
            }
            Debug.Log("🎬 CutsceneManager: 所有动画已设置为5秒手动时间");
        }

        /// <summary>
        /// 设置所有动画为3秒手动时间（测试用）
        /// </summary>
        [ContextMenu("测试：设置所有为3秒手动时间")]
        public void SetAllTo3SecondsManual()
        {
            for (int i = 0; i < cutsceneInfos.Count; i++)
            {
                SetManualDuration(i, 3f, true);
            }
            Debug.Log("🎬 CutsceneManager: 所有动画已设置为3秒手动时间");
        }

        /// <summary>
        /// 切换所有动画到自动检测模式
        /// </summary>
        [ContextMenu("切换所有到自动检测")]
        public void MenuSetAllToAutoDetection()
        {
            SetAllToAutoDetection();
        }

        #endregion

        /// <summary>
        /// 返回所有属于指定分类的动画在 cutsceneInfos 中的索引列表
        /// </summary>
        public List<int> GetIndicesByCategory(CutsceneCategory cat)
        {
            var list = new List<int>();
            for (int i = 0; i < cutsceneInfos.Count; i++)
            {
                if (cutsceneInfos[i].category == cat)
                    list.Add(i);
            }
            return list;
        }

    }
}