// UIManager.cs - 修复重复方法定义问题
using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace BugFixerGame
{
    public class UIManager : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject gameOverPanel;     // Bad End面板
        [SerializeField] private GameObject happyEndPanel;     // Happy End面板
        [SerializeField] private GameObject badEndPanel;       // 额外的Bad End面板（如果需要）

        [Header("过场动画系统")]
        [SerializeField] private CutsceneManager cutsceneManager;                               // 过场动画管理器引用
        [SerializeField] private GameObject cutscenePanel;                                       // 过场动画面板（用于背景遮罩等）
        [SerializeField] private float animationCheckInterval = 0.1f;                          // 动画状态检查间隔
        [SerializeField] private bool enableSkipCutscene = true;                               // 是否允许跳过过场动画
        [SerializeField] private KeyCode skipKey = KeyCode.Space;                              // 跳过动画的按键
        [SerializeField] private Button skipButton;                                            // 跳过按钮（可选）
        [SerializeField] private bool debugCutscene = true;                                    // 调试过场动画
        [SerializeField] private bool autoFindCutsceneManager = true;                          // 自动查找CutsceneManager

        [Header("过场动画设置")]
        [SerializeField] private string animationTriggerName = "Play";                         // 动画触发器名称
        [SerializeField] private string animationStateName = "CutsceneAnimation";              // 动画状态名称（用于检测完成）
        [SerializeField] private float maxAnimationWaitTime = 30f;                            // 单个动画最大等待时间（防止卡死）
        [SerializeField] private float animationTransitionTime = 0.5f;                        // 动画间切换时间
        [SerializeField] private float minimumAnimationTime = 1f;                             // 每个动画最少播放时间
        [SerializeField] private float defaultAnimationTime = 5f;                             // 如果无法检测动画长度时的默认时间
        [SerializeField] private bool forceNonLooping = true;                                 // 强制确保动画不循环播放
        [SerializeField] private bool useSimpleTimeBasedDetection = false;                    // 使用简单的基于时间的检测（适用于复杂动画）

        [Header("检测UI控制")]
        [SerializeField] private GameObject crosshair;             // 十字准心对象
        [SerializeField] private GameObject magicCircle;           // 魔法圈对象
        [SerializeField] private bool enableDetectionUIControl = true;      // 是否启用检测UI控制
        [SerializeField] private bool debugDetectionUI = true;             // 调试检测UI（默认开启）

        [Header("魔法球UI设置")]
        [SerializeField] private Transform manaOrbsContainer;           // 魔法球容器
        [SerializeField] private GameObject manaOrbPrefab;              // 魔法球预制体

        [Header("魔法球布局设置")]
        [SerializeField] private float orbSpacing = 60f;               // 魔法球间距
        [SerializeField] private bool useHorizontalLayout = true;       // 是否使用水平布局
        [SerializeField] private Vector3 startPosition = Vector3.zero; // 起始位置偏移

        [Header("UI Elements")]
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button quitGameButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button returnToMenuButton;
        [SerializeField] private Button restartGameButton;

        [Header("主菜单音频控制")]
        [SerializeField] private Slider mainMenuMusicSlider;     // 主菜单音乐音量滑条
        [SerializeField] private Slider mainMenuSFXSlider;       // 主菜单音效音量滑条

        [Header("暂停菜单音频控制")]
        [SerializeField] private Slider pauseMusicSlider;       // 暂停菜单音乐音量滑条
        [SerializeField] private Slider pauseSFXSlider;         // 暂停菜单音效音量滑条

        [Header("Game End UI Elements")]
        [SerializeField] private Button badEndRestartButton;           // Bad End重新开始按钮
        [SerializeField] private Button badEndMenuButton;              // Bad End返回菜单按钮
        [SerializeField] private Button happyEndRestartButton;         // Happy End重新开始按钮
        [SerializeField] private Button happyEndMenuButton;            // Happy End返回菜单按钮

        [Header("Game End Settings")]
        [SerializeField] private float gameEndFadeTime = 1f;           // 游戏结束面板淡入时间
        [SerializeField] private bool unlockCursorOnGameEnd = true;     // 游戏结束时是否解锁鼠标

        // 魔法球管理
        private List<SimpleManaOrb> manaOrbs = new List<SimpleManaOrb>();
        private int currentMaxMana = 0;

        // 检测UI状态管理
        private bool originalCrosshairState = true;
        private bool originalMagicCircleState = false;
        private bool isDetectionUIActive = false;

        // 过场动画状态管理
        private bool isPlayingCutscene = false;
        private int currentCutsceneIndex = 0;
        private GameObject currentCutsceneInstance = null;
        private Animator currentCutsceneAnimator = null;
        private bool cutsceneSkipped = false;
        private bool isBadEndPlaying = false;
        private bool isHappyEndPlaying = false;

        public static UIManager Instance { get; private set; }

        // 过场动画事件
        public static event Action OnCutsceneStarted;           // 过场动画开始
        public static event Action OnCutsceneCompleted;         // 过场动画完成
        public static event Action<int> OnCutsceneChanged;      // 过场动画切换 (当前索引)
        public static event Action OnCutsceneSkipped;           // 过场动画被跳过

        #region Unity生命周期

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeUI();
                InitializeDetectionUI();
                InitializeCutsceneSystem();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            GameManager.OnManaChanged += UpdateMana;
            GameManager.OnPauseStateChanged += TogglePauseMenu;
            GameManager.OnGameOver += ShowBadEnd;           // 订阅Bad End事件
            GameManager.OnHappyEnd += ShowHappyEnd;         // 订阅Happy End事件
            GameManager.OnGameEnded += HandleGameEnded;     // 订阅通用游戏结束事件

            // 订阅Player的检测相关事件
            Player.OnObjectHoldProgress += HandleDetectionProgress;
            Player.OnHoldCancelled += HandleDetectionCancelled;
        }

        private void OnDisable()
        {
            GameManager.OnManaChanged -= UpdateMana;
            GameManager.OnPauseStateChanged -= TogglePauseMenu;
            GameManager.OnGameOver -= ShowBadEnd;
            GameManager.OnHappyEnd -= ShowHappyEnd;
            GameManager.OnGameEnded -= HandleGameEnded;

            // 取消订阅Player事件
            Player.OnObjectHoldProgress -= HandleDetectionProgress;
            Player.OnHoldCancelled -= HandleDetectionCancelled;

            // 恢复检测UI状态
            RestoreDetectionUIState();

            // 清理过场动画
            CleanupCurrentCutscene();
        }

        private void Update()
        {
            // 只有在过场面板打开时，才允许空格跳过
            if (cutscenePanel != null && cutscenePanel.activeSelf
                && Input.GetKeyDown(skipKey))
            {
                SkipCutscene();
            }
        }

        #endregion

        #region 过场动画系统

        /// <summary>
        /// 初始化过场动画系统
        /// </summary>
        private void InitializeCutsceneSystem()
        {
            // 自动查找CutsceneManager
            if (autoFindCutsceneManager && cutsceneManager == null)
            {
                cutsceneManager = FindObjectOfType<CutsceneManager>();
                if (cutsceneManager != null)
                {
                    Debug.Log($"🎬 UIManager: 自动找到CutsceneManager: {cutsceneManager.name}");
                }
            }

            // 检查CutsceneManager
            if (cutsceneManager == null)
            {
                Debug.LogWarning("⚠️ UIManager: 未找到CutsceneManager，过场动画功能将被禁用");
                return;
            }

            // 设置跳过按钮
            if (skipButton != null)
            {
                skipButton.onClick.AddListener(SkipCutscene);
            }

            // 初始状态：隐藏过场动画面板
            SetPanel(cutscenePanel, false);

            if (debugCutscene)
            {
                Debug.Log($"🎬 UIManager: 过场动画系统初始化完成，CutsceneManager: {cutsceneManager.name}");
                Debug.Log($"🎬 UIManager: 共{GetCutsceneCount()}个动画prefab");

                // 显示动画时长信息
                ShowAnimationDurationInfo();
            }
        }

        /// <summary>
        /// 播放所有 Intro 分类的过场（按列表顺序）
        /// </summary>
        public IEnumerator PlayIntroSequence()
        {
            var indices = cutsceneManager.GetIndicesByCategory(CutsceneCategory.Intro);
            foreach (int idx in indices)
                yield return StartCoroutine(PlaySingleCutscene(idx));
        }

        /// <summary>
        /// 播放所有 HappyEnd 分类的过场
        /// </summary>
        public IEnumerator PlayHappyEndSequence()
        {
            var indices = cutsceneManager.GetIndicesByCategory(CutsceneCategory.HappyEnd);
            foreach (int idx in indices)
                yield return StartCoroutine(PlaySingleCutscene(idx));
        }

        /// <summary>
        /// 播放所有 BadEnd 分类的过场
        /// </summary>
        public IEnumerator PlayBadEndSequence()
        {
            var indices = cutsceneManager.GetIndicesByCategory(CutsceneCategory.BadEnd);
            foreach (int idx in indices)
                yield return StartCoroutine(PlaySingleCutscene(idx));
        }


        /// <summary>
        /// 显示所有动画的时长信息
        /// </summary>
        private void ShowAnimationDurationInfo()
        {
            if (cutsceneManager == null) return;

            Debug.Log("=== 过场动画时长信息 ===");
            Debug.Log($"总动画数量: {cutsceneManager.GetCutsceneCount()}");
            Debug.Log($"总播放时长: {cutsceneManager.GetTotalDuration():F1}秒");

            var allInfos = cutsceneManager.GetAllCutsceneInfos();
            for (int i = 0; i < allInfos.Count; i++)
            {
                var info = allInfos[i];
                if (info?.prefab != null)
                {
                    float duration = cutsceneManager.GetAnimationDuration(i);
                    string statusText = info.GetStatusSummary();
                    Debug.Log($"  {i + 1}. {info.animationName}: {duration:F1}秒 ({statusText})");
                }
            }

            if (cutsceneManager.HasLoopingAnimations())
            {
                Debug.LogWarning("⚠️ 检测到循环动画，可能需要特殊处理");
                var loopingAnims = cutsceneManager.GetLoopingAnimations();
                foreach (var anim in loopingAnims)
                {
                    Debug.LogWarning($"  循环动画: {anim.animationName} - {anim.duration:F1}秒");
                }
            }
        }

        /// <summary>
        /// 开始播放过场动画序列
        /// </summary>
        public void StartCutsceneSequence()
        {
            // 检查CutsceneManager
            if (cutsceneManager == null)
            {
                Debug.LogWarning("⚠️ UIManager: CutsceneManager未设置，直接开始游戏");
                StartGameDirectly();
                return;
            }

            // 检查是否有有效的过场动画
            if (!cutsceneManager.HasValidCutscenes())
            {
                Debug.LogWarning("⚠️ UIManager: 没有有效的过场动画或动画被禁用，直接开始游戏");
                StartGameDirectly();
                return;
            }

            if (isPlayingCutscene)
            {
                Debug.LogWarning("⚠️ UIManager: 过场动画已经在播放中");
                return;
            }

            if (debugCutscene)
            {
                Debug.Log("🎬 UIManager: 开始播放过场动画序列");
                // 确保动画已分析并显示时长信息
                cutsceneManager.AnalyzeAllAnimations();
                ShowAnimationDurationInfo();
            }

            // 显示CutsceneManager（如果它被隐藏了）
            cutsceneManager.Show();

            // 初始化状态
            isPlayingCutscene = true;
            currentCutsceneIndex = 0;
            cutsceneSkipped = false;

            // 显示过场动画面板
            SetPanel(cutscenePanel, true);

            // 隐藏主菜单面板
            SetPanel(mainMenuPanel, false);

            // 触发开始事件
            OnCutsceneStarted?.Invoke();

            // 开始播放第一个动画
            StartCoroutine(PlayCutsceneSequence());
        }

        /// <summary>
        /// 播放过场动画序列的协程
        /// </summary>
        private IEnumerator PlayCutsceneSequence()
        {
            int totalCutscenes = GetCutsceneCount();
            Debug.Log($"🎬 UIManager: 开始播放过场动画序列，总共 {totalCutscenes} 个动画");
            Debug.Log($"🎬 UIManager: 预计总播放时长: {cutsceneManager.GetTotalDuration():F1}秒");

            for (currentCutsceneIndex = 0; currentCutsceneIndex < totalCutscenes; currentCutsceneIndex++)
            {
                float expectedDuration = cutsceneManager.GetAnimationDuration(currentCutsceneIndex);
                Debug.Log($"🎬 UIManager: 准备播放动画 {currentCutsceneIndex + 1}/{totalCutscenes}，预期时长: {expectedDuration:F1}秒");

                // 检查是否被跳过
                if (cutsceneSkipped)
                {
                    Debug.Log("🎬 UIManager: 过场动画序列被跳过，退出循环");
                    break;
                }

                // 播放当前动画
                float startTime = Time.time;
                yield return StartCoroutine(PlaySingleCutscene(currentCutsceneIndex));
                float actualDuration = Time.time - startTime;

                Debug.Log($"🎬 UIManager: 动画 {currentCutsceneIndex + 1} 播放完成");
                Debug.Log($"🎬 UIManager: 实际播放时间: {actualDuration:F1}秒，预期: {expectedDuration:F1}秒");

                // 动画间的过渡时间
                if (currentCutsceneIndex < totalCutscenes - 1 && !cutsceneSkipped)
                {
                    Debug.Log($"🎬 UIManager: 等待过渡时间 {animationTransitionTime} 秒");
                    yield return new WaitForSeconds(animationTransitionTime);
                }
            }

            Debug.Log("🎬 UIManager: 所有动画播放完成，调用 CompleteCutsceneSequence()");

            // 完成所有动画
            CompleteCutsceneSequence();
        }

        /// <summary>
        /// 播放单个过场动画
        /// </summary>
        private IEnumerator PlaySingleCutscene(int index)
        {
            // 检查CutsceneManager
            if (cutsceneManager == null)
            {
                Debug.LogError("❌ UIManager: CutsceneManager为空，无法播放动画");
                yield break;
            }

            // 从CutsceneManager获取prefab
            GameObject prefab = cutsceneManager.GetCutscenePrefab(index);
            if (prefab == null)
            {
                Debug.LogError($"❌ UIManager: 无法获取过场动画prefab {index}");
                yield break;
            }

            // 获取容器
            Transform container = cutsceneManager.GetCutsceneContainer();
            if (container == null)
            {
                Debug.LogError($"❌ UIManager: CutsceneManager容器为空");
                yield break;
            }

            // 获取动画信息
            var animInfo = cutsceneManager.GetAnimationInfo(index);
            float expectedDuration = cutsceneManager.GetAnimationDuration(index);

            string timingSource = animInfo?.useManualDuration == true ? "手动设置" : "自动检测";
            Debug.Log($"🎬 UIManager: 开始播放过场动画 {index + 1}/{GetCutsceneCount()}: {prefab.name}");
            Debug.Log($"🎬 UIManager: 动画信息 - 时长: {expectedDuration:F1}秒 ({timingSource}), 循环: {animInfo?.isLooping}, 状态: {animInfo?.GetStatusSummary()}");

            // 清理之前的动画实例
            CleanupCurrentCutscene();

            // 实例化新的动画prefab
            try
            {
                currentCutsceneInstance = Instantiate(prefab, container);
                Debug.Log($"🎬 UIManager: 成功实例化动画prefab: {currentCutsceneInstance.name}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ UIManager: 实例化动画prefab时发生错误: {e.Message}");
                yield break;
            }

            currentCutsceneAnimator = currentCutsceneInstance.GetComponent<Animator>();

            if (currentCutsceneAnimator == null)
            {
                Debug.LogError($"❌ UIManager: 过场动画prefab {prefab.name} 没有Animator组件，使用默认播放时间");
                yield return new WaitForSeconds(defaultAnimationTime);
                yield break;
            }

            Debug.Log($"🎬 UIManager: 找到Animator组件: {currentCutsceneAnimator.name}");

            // 验证动画设置
            if (debugCutscene)
            {
                ValidateAnimationSettings(currentCutsceneAnimator);
            }

            // 特殊处理循环动画
            if (animInfo != null && animInfo.isLooping && forceNonLooping)
            {
                Debug.LogWarning($"⚠️ UIManager: 动画 {animInfo.animationName} 是循环播放，将使用固定时长: {expectedDuration:F1}秒");
            }

            // 触发动画切换事件
            try
            {
                OnCutsceneChanged?.Invoke(index);
                Debug.Log($"🎬 UIManager: 触发动画切换事件: {index}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ UIManager: 触发动画切换事件时发生错误: {e.Message}");
            }

            // 播放动画
            if (!string.IsNullOrEmpty(animationTriggerName))
            {
                try
                {
                    currentCutsceneAnimator.SetTrigger(animationTriggerName);
                    Debug.Log($"🎬 UIManager: 设置动画触发器: {animationTriggerName}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"❌ UIManager: 设置动画触发器时发生错误: {e.Message}");
                    Debug.LogError($"💡 UIManager: 提示 - 请确保Animator Controller中有名为 '{animationTriggerName}' 的触发器参数");
                }
            }
            else
            {
                Debug.Log("🎬 UIManager: 动画触发器名称为空，尝试直接播放默认动画");
            }

            // 等待动画播放完成（使用CutsceneManager提供的精确时长）
            Debug.Log($"🎬 UIManager: 开始等待动画播放完成，使用时长: {expectedDuration:F1}秒");
            yield return StartCoroutine(WaitForAnimationComplete(expectedDuration, animInfo));

            Debug.Log($"🎬 UIManager: 过场动画 {index + 1} 播放完成");
        }

        /// <summary>
        /// 等待动画播放完成（改进版本，支持手动时间设置）
        /// </summary>
        private IEnumerator WaitForAnimationComplete(float expectedDuration = -1f, CutsceneInfo animInfo = null)
        {
            if (currentCutsceneAnimator == null)
            {
                Debug.LogWarning("⚠️ UIManager: 当前动画器为空，使用默认播放时间");
                yield return new WaitForSeconds(defaultAnimationTime);
                yield break;
            }

            float startTime = Time.time;
            float animationDuration = expectedDuration > 0 ? expectedDuration : defaultAnimationTime;
            bool isLooping = animInfo?.isLooping ?? false;
            bool isManualTiming = animInfo?.useManualDuration ?? false;

            string timingMode = isManualTiming ? "手动设置" : "自动检测";
            Debug.Log($"🎬 UIManager: 开始等待动画播放完成，时长: {animationDuration:F2}秒，模式: {timingMode}，循环: {isLooping}");

            // 等待1帧让动画系统初始化
            yield return null;

            // 如果使用手动时间设置，或者启用简单时间检测，或者是循环动画，直接使用固定时长
            if (isManualTiming || useSimpleTimeBasedDetection || (isLooping && forceNonLooping))
            {
                Debug.Log($"🎬 UIManager: 使用固定时长模式，播放时间: {animationDuration}秒");
                yield return new WaitForSeconds(animationDuration);
                Debug.Log("🎬 UIManager: 固定时长播放完成");

                // 主动停止动画器，防止重复播放
                if (currentCutsceneAnimator != null && currentCutsceneAnimator.gameObject != null)
                {
                    currentCutsceneAnimator.enabled = false;
                    Debug.Log("🎬 UIManager: 主动停止动画器");
                }
                yield break;
            }

            // 自动检测模式：等待最小播放时间（缩短以避免重复播放）
            float minWaitTime = Mathf.Max(minimumAnimationTime, animationDuration * 0.05f);
            while (Time.time - startTime < minWaitTime && !cutsceneSkipped)
            {
                yield return new WaitForSeconds(0.05f);
            }

            if (cutsceneSkipped)
            {
                Debug.Log("🎬 UIManager: 动画在最小时间等待中被跳过");
                yield break;
            }

            // 主要等待逻辑 - 使用智能检测（仅用于自动检测模式）
            bool animationCompleted = false;
            float targetPlayTime = animationDuration * 0.98f; // 稍微提前结束，避免重复播放
            float lastNormalizedTime = -1f;
            int sameTimeCount = 0;
            float lastLogTime = 0f;
            bool hasReachedNearEnd = false; // 标记是否接近结束

            Debug.Log($"🎬 UIManager: 开始智能检测等待 - 目标时间: {targetPlayTime:F2}秒 (98%时长)");

            while (!animationCompleted && !cutsceneSkipped)
            {
                float currentTime = Time.time - startTime;

                // 超时检查
                if (currentTime > maxAnimationWaitTime)
                {
                    Debug.LogWarning($"⚠️ UIManager: 动画播放超时 ({maxAnimationWaitTime}s)，强制结束");
                    break;
                }

                // 获取当前动画状态
                AnimatorStateInfo currentState = currentCutsceneAnimator.GetCurrentAnimatorStateInfo(0);
                float normalizedTime = currentState.normalizedTime;

                // 定期输出调试信息
                if (debugCutscene && currentTime - lastLogTime >= 1f)
                {
                    float progress = currentTime / animationDuration;
                    Debug.Log($"🎬 UIManager: 播放进度 - 时间: {currentTime:F1}s/{animationDuration:F1}s ({progress:P0}), normalizedTime: {normalizedTime:F3}");
                    lastLogTime = currentTime;
                }

                // 检测方法1: 提前检测接近结束（主要方法 - 在95%时开始准备）
                if (currentTime >= animationDuration * 0.95f)
                {
                    hasReachedNearEnd = true;
                }

                // 检测方法2: 精确时长检测（98%时长）
                if (currentTime >= targetPlayTime)
                {
                    animationCompleted = true;
                    Debug.Log($"🎬 UIManager: 动画完成 - 基于精确时长98% ({currentTime:F2}s >= {targetPlayTime:F2}s)");
                    break;
                }

                // 检测方法3: normalizedTime检测（在接近结束时更积极）
                if (hasReachedNearEnd && !isLooping && normalizedTime >= 0.95f)
                {
                    animationCompleted = true;
                    Debug.Log($"🎬 UIManager: 动画完成 - normalizedTime接近完成 ({normalizedTime:F3}) 且已达95%时长");
                    break;
                }

                // 检测方法4: 防止normalizedTime超过1.0后重新开始
                if (!isLooping && normalizedTime >= 1.0f && currentTime >= animationDuration * 0.8f)
                {
                    animationCompleted = true;
                    Debug.Log($"🎬 UIManager: 动画完成 - normalizedTime >= 1.0 ({normalizedTime:F3})，防止重复播放");
                    break;
                }

                // 检测方法5: normalizedTime停止变化检测（最后的保险）
                if (hasReachedNearEnd && Mathf.Abs(normalizedTime - lastNormalizedTime) < 0.001f && normalizedTime > 0.9f)
                {
                    sameTimeCount++;
                    if (sameTimeCount >= 3) // 进一步减少等待次数
                    {
                        animationCompleted = true;
                        Debug.Log($"🎬 UIManager: 动画完成 - normalizedTime停止变化 (在 {normalizedTime:F3})");
                        break;
                    }
                }
                else
                {
                    sameTimeCount = 0;
                }

                lastNormalizedTime = normalizedTime;
                yield return new WaitForSeconds(0.05f); // 更频繁的检测
            }

            if (cutsceneSkipped)
            {
                Debug.Log("🎬 UIManager: 动画在智能等待循环中被跳过");
            }
            else if (animationCompleted)
            {
                float totalTime = Time.time - startTime;
                float accuracy = Mathf.Abs(totalTime - animationDuration) / animationDuration * 100f;
                Debug.Log($"🎬 UIManager: 动画播放完成 - 实际耗时: {totalTime:F2}秒, 预期: {animationDuration:F2}秒, 误差: {accuracy:F1}%");

                // 主动停止动画器，防止重复播放
                if (currentCutsceneAnimator != null && currentCutsceneAnimator.gameObject != null)
                {
                    currentCutsceneAnimator.enabled = false;
                    Debug.Log("🎬 UIManager: 主动停止动画器，防止重复播放");
                }
            }
        }

        /// <summary>
        /// 跳过当前正在播放的过场动画
        /// </summary>
        public void SkipCutscene()
        {
            // 只有在正在播放且允许跳过时生效
            if (!isPlayingCutscene || !enableSkipCutscene)
                return;

            if (debugCutscene)
                Debug.Log("🎬 UIManager: 用户跳过过场动画");

            // 标记已跳过
            cutsceneSkipped = true;
            OnCutsceneSkipped?.Invoke();

            // 停掉所有协程
            StopAllCoroutines();

            // 如果正在播放 Bad End，就直接跳到 Bad End 面板
            if (isBadEndPlaying)
            {
                // 隐藏过场面板
                SetPanel(cutscenePanel, false);
                // 显示 Bad End 面板（淡入）
                StartCoroutine(ShowGameEndPanelWithFade(badEndPanel ?? gameOverPanel));
                UnlockCursorForUI();

                isBadEndPlaying = false;
                isPlayingCutscene = false;
                return;
            }

            // 如果正在播放 Happy End，就直接跳到 Happy End 面板
            if (isHappyEndPlaying)
            {
                SetPanel(cutscenePanel, false);
                StartCoroutine(ShowGameEndPanelWithFade(happyEndPanel));
                UnlockCursorForUI();

                isHappyEndPlaying = false;
                isPlayingCutscene = false;
                return;
            }

            // 否则按 Intro 的正常跳过：结束开场并进入游戏
            GameManager.Instance.StartGame();           // 启动游戏逻辑
            SetPanel(mainMenuPanel, false);
            SetPanel(hudPanel, true);
            SetPanel(cutscenePanel, false);             // 关闭过场面板
            isPlayingCutscene = false;
        }



        /// <summary>
        /// 完成过场动画序列
        /// </summary>
        private void CompleteCutsceneSequence()
        {
            Debug.Log($"🎬 UIManager: 过场动画序列完成开始处理 (跳过: {cutsceneSkipped})");

            // 清理当前动画实例
            CleanupCurrentCutscene();

            // 隐藏过场动画面板
            SetPanel(cutscenePanel, false);
            Debug.Log("🎬 UIManager: 隐藏过场动画面板");

            // 隐藏CutsceneManager（不再需要时）
            if (cutsceneManager != null)
            {
                cutsceneManager.Hide();
                Debug.Log("🎬 UIManager: 隐藏CutsceneManager");
            }

            // 重置状态
            isPlayingCutscene = false;
            currentCutsceneIndex = 0;
            cutsceneSkipped = false;

            Debug.Log("🎬 UIManager: 重置过场动画状态");

            // 触发完成事件
            try
            {
                OnCutsceneCompleted?.Invoke();
                Debug.Log("🎬 UIManager: 触发过场动画完成事件");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ UIManager: 触发过场动画完成事件时发生错误: {e.Message}");
            }

            // 开始游戏
            Debug.Log("🎬 UIManager: 准备调用 StartGameDirectly()");
            StartGameDirectly();
        }

        /// <summary>
        /// 清理当前过场动画实例
        /// </summary>
        private void CleanupCurrentCutscene()
        {
            if (currentCutsceneInstance != null)
            {
                if (debugCutscene)
                    Debug.Log($"🧹 UIManager: 清理过场动画实例: {currentCutsceneInstance.name}");

                Destroy(currentCutsceneInstance);
                currentCutsceneInstance = null;
            }

            currentCutsceneAnimator = null;
        }

        /// <summary>
        /// 直接开始游戏（跳过过场动画）
        /// </summary>
        private void StartGameDirectly()
        {
            Debug.Log("🎮 UIManager: 准备开始游戏...");

            // 检查GameManager实例
            if (GameManager.Instance == null)
            {
                Debug.LogError("❌ UIManager: GameManager.Instance 为空！无法开始游戏");
                return;
            }

            Debug.Log("🎮 UIManager: GameManager实例正常，开始游戏");

            // 播放按钮音效
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayButtonClickSound();
                Debug.Log("🔊 UIManager: 播放按钮音效");
            }
            else
            {
                Debug.LogWarning("⚠️ UIManager: AudioManager.Instance 为空，跳过音效");
            }

            // 调用GameManager开始游戏
            try
            {
                GameManager.Instance.StartGame();
                Debug.Log("✅ UIManager: 成功调用 GameManager.StartGame()");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ UIManager: 调用 GameManager.StartGame() 时发生错误: {e.Message}");
                Debug.LogError($"❌ UIManager: 堆栈跟踪: {e.StackTrace}");
            }
        }

        /// <summary>
        /// 验证动画设置是否正确
        /// </summary>
        private void ValidateAnimationSettings(Animator animator)
        {
            if (animator == null) return;

            Debug.Log("🔍 UIManager: 开始验证动画设置");

            // 检查Animator Controller
            if (animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning("⚠️ UIManager: Animator没有设置AnimatorController");
                return;
            }

            // 检查触发器参数
            if (!string.IsNullOrEmpty(animationTriggerName))
            {
                bool hasTrigger = false;
                foreach (var param in animator.parameters)
                {
                    if (param.name == animationTriggerName && param.type == AnimatorControllerParameterType.Trigger)
                    {
                        hasTrigger = true;
                        break;
                    }
                }

                if (!hasTrigger)
                {
                    Debug.LogWarning($"⚠️ UIManager: Animator Controller中未找到触发器参数 '{animationTriggerName}'");
                }
                else
                {
                    Debug.Log($"✅ UIManager: 找到触发器参数 '{animationTriggerName}'");
                }
            }

            // 检查动画clip
            AnimatorClipInfo[] clipInfos = animator.GetCurrentAnimatorClipInfo(0);
            if (clipInfos.Length > 0)
            {
                foreach (var clipInfo in clipInfos)
                {
                    AnimationClip clip = clipInfo.clip;
                    if (clip != null)
                    {
                        Debug.Log($"🎬 UIManager: 动画Clip - 名称: {clip.name}, 长度: {clip.length:F2}s, 循环: {clip.isLooping}");

                        if (clip.isLooping && forceNonLooping)
                        {
                            Debug.LogWarning($"⚠️ UIManager: 动画 '{clip.name}' 设置为循环播放，建议改为非循环");
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning("⚠️ UIManager: 未找到当前播放的动画Clip");
            }

            Debug.Log("🔍 UIManager: 动画设置验证完成");
        }

        /// <summary>
        /// 检查是否正在播放过场动画
        /// </summary>
        public bool IsPlayingCutscene()
        {
            return isPlayingCutscene;
        }

        /// <summary>
        /// 获取当前过场动画索引
        /// </summary>
        public int GetCurrentCutsceneIndex()
        {
            return currentCutsceneIndex;
        }

        /// <summary>
        /// 获取过场动画总数
        /// </summary>
        public int GetCutsceneCount()
        {
            return cutsceneManager != null ? cutsceneManager.GetCutsceneCount() : 0;
        }

        /// <summary>
        /// 检查CutsceneManager是否可用
        /// </summary>
        public bool IsCutsceneManagerAvailable()
        {
            return cutsceneManager != null && cutsceneManager.HasValidCutscenes();
        }

        /// <summary>
        /// 获取当前播放的动画信息
        /// </summary>
        public CutsceneInfo GetCurrentAnimationInfo()
        {
            if (cutsceneManager != null && currentCutsceneIndex >= 0 && currentCutsceneIndex < cutsceneManager.GetCutsceneCount())
            {
                return cutsceneManager.GetAnimationInfo(currentCutsceneIndex);
            }
            return null;
        }

        /// <summary>
        /// 获取剩余播放时间
        /// </summary>
        public float GetRemainingCutsceneTime()
        {
            if (!isPlayingCutscene || cutsceneManager == null)
                return 0f;

            float totalRemaining = 0f;
            int totalCutscenes = cutsceneManager.GetCutsceneCount();

            // 计算剩余动画的总时长
            for (int i = currentCutsceneIndex + 1; i < totalCutscenes; i++)
            {
                totalRemaining += cutsceneManager.GetAnimationDuration(i);
                if (i < totalCutscenes - 1)
                {
                    totalRemaining += animationTransitionTime; // 过渡时间
                }
            }

            return totalRemaining;
        }

        #endregion

        #region 检测UI控制

        /// <summary>
        /// 初始化检测UI控制
        /// </summary>
        private void InitializeDetectionUI()
        {
            if (!enableDetectionUIControl)
            {
                Debug.Log("🎮 UIManager: 检测UI控制未启用，跳过初始化");
                return;
            }

            Debug.Log("🎮 UIManager: 开始初始化检测UI控制");

            // 记录原始状态 - 确保crosshair默认是显示的
            if (crosshair != null)
            {
                // 先确保crosshair是显示状态，再记录原始状态
                crosshair.SetActive(true);
                originalCrosshairState = true; // 强制设为true，因为crosshair应该默认显示
                Debug.Log($"🎯 UIManager: Crosshair设置为显示状态并记录原始状态 - {originalCrosshairState} (对象: {crosshair.name})");
            }
            else
            {
                Debug.LogWarning("⚠️ UIManager: Crosshair对象未设置！请在Inspector中拖入Crosshair对象。");
                originalCrosshairState = true; // 默认值
            }

            if (magicCircle != null)
            {
                // magic circle 默认应该是隐藏的
                magicCircle.SetActive(false);
                originalMagicCircleState = false; // 强制设为false，因为magic circle应该默认隐藏
                Debug.Log($"🔮 UIManager: Magic Circle设置为隐藏状态并记录原始状态 - {originalMagicCircleState} (对象: {magicCircle.name})");
            }
            else
            {
                Debug.LogWarning("⚠️ UIManager: Magic Circle对象未设置！请在Inspector中拖入Magic Circle对象。");
                originalMagicCircleState = false; // 默认值
            }

            isDetectionUIActive = false;
            Debug.Log("✅ UIManager: 检测UI控制初始化完成");
        }

        /// <summary>
        /// 处理检测进度事件
        /// </summary>
        private void HandleDetectionProgress(GameObject detectedObject, float progress)
        {
            if (!enableDetectionUIControl) return;

            // 如果是第一次接收到进度事件（progress > 0且UI未激活），则开始检测
            if (progress > 0f && !isDetectionUIActive)
            {
                StartDetectionUI();
            }
        }

        /// <summary>
        /// 处理检测取消事件
        /// </summary>
        private void HandleDetectionCancelled()
        {
            if (!enableDetectionUIControl) return;

            Debug.Log("🎮 UIManager: 收到检测取消事件");

            // 立即恢复
            EndDetectionUI();

            // 延迟恢复，确保状态正确（防止其他代码干扰）
            StartCoroutine(DelayedRestoreDetectionUI());
        }

        /// <summary>
        /// 延迟恢复检测UI状态（确保状态正确）
        /// </summary>
        private System.Collections.IEnumerator DelayedRestoreDetectionUI()
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame(); // 等待两帧确保所有更新完成

            if (!isDetectionUIActive) // 只有在不是检测状态时才恢复
            {
                Debug.Log("🔄 UIManager: 延迟恢复检测UI状态");
                ForceRestoreDetectionUIState();
            }
        }

        /// <summary>
        /// 开始检测UI状态
        /// </summary>
        private void StartDetectionUI()
        {
            if (isDetectionUIActive)
            {
                if (debugDetectionUI)
                    Debug.Log("🎮 UIManager: 检测UI已经激活，跳过");
                return;
            }

            isDetectionUIActive = true;

            Debug.Log("🎮 UIManager: 开始检测UI状态");

            // 隐藏crosshair
            if (crosshair != null)
            {
                crosshair.SetActive(false);
                Debug.Log("🎯 UIManager: Crosshair 已隐藏");
            }

            // 显示magic circle
            if (magicCircle != null)
            {
                magicCircle.SetActive(true);
                Debug.Log("🔮 UIManager: Magic Circle 已显示");
            }
        }

        /// <summary>
        /// 结束检测UI状态
        /// </summary>
        private void EndDetectionUI()
        {
            if (!isDetectionUIActive)
            {
                ForceRestoreDetectionUIState();
                return;
            }

            isDetectionUIActive = false;
            RestoreDetectionUIState();
        }

        /// <summary>
        /// 恢复检测UI到原始状态
        /// </summary>
        private void RestoreDetectionUIState()
        {
            if (!enableDetectionUIControl) return;

            Debug.Log("🔄 UIManager: 恢复检测UI到原始状态");

            // 恢复crosshair
            if (crosshair != null)
            {
                crosshair.SetActive(originalCrosshairState);
            }

            // 恢复magic circle
            if (magicCircle != null)
            {
                magicCircle.SetActive(originalMagicCircleState);
            }

            isDetectionUIActive = false;
        }

        /// <summary>
        /// 强制恢复检测UI状态（用于确保状态正确）
        /// </summary>
        private void ForceRestoreDetectionUIState()
        {
            if (!enableDetectionUIControl) return;

            // 强制恢复crosshair
            if (crosshair != null)
            {
                crosshair.SetActive(originalCrosshairState);
            }

            // 强制恢复magic circle
            if (magicCircle != null)
            {
                magicCircle.SetActive(originalMagicCircleState);
            }

            isDetectionUIActive = false;
        }

        #endregion

        #region UI初始化

        private void InitializeUI()
        {
            // 初始化显示面板
            SetPanel(mainMenuPanel, true);
            SetPanel(hudPanel, false);
            SetPanel(pausePanel, false);
            SetPanel(gameOverPanel, false);
            SetPanel(happyEndPanel, false);
            SetPanel(badEndPanel, false);

            // 按钮绑定
            SetupButtons();

            // 初始化音频slider
            InitializeAudioSliders();

            Debug.Log("UIManager 初始化完成");
        }

        private void SetupButtons()
        {
            // 主菜单按钮 - 修改为播放过场动画
            if (startGameButton)
            {
                startGameButton.onClick.AddListener(() => {
                    AudioManager.Instance?.PlayButtonClickSound();
                                // 不直接播放整个序列／不直接显示 HUD
                                // 而是先播放“Intro”分类的开头过场
                    StartCoroutine(ShowIntroSequence());
                            });
            }

            if (quitGameButton)
            {
                quitGameButton.onClick.AddListener(() => {
                    AudioManager.Instance?.PlayButtonClickSound();
                    Application.Quit();
                });
            }

            // 暂停菜单按钮
            if (resumeButton)
            {
                resumeButton.onClick.RemoveAllListeners();
                resumeButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlayButtonClickSound();
                    GameManager.Instance.ResumeGame();
                    // 重新锁定鼠标
                    var camCtrl = Camera.main?.GetComponent<CameraController>();
                    if (camCtrl != null)
                        camCtrl.SetCursorLocked(true);
                    else
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }
                });
            }

            if (returnToMenuButton)
                returnToMenuButton.onClick.AddListener(() => {
                    AudioManager.Instance?.PlayButtonClickSound();
                    GameManager.Instance.ReturnToMainMenu();
                });

            // 游戏结束按钮
            if (restartGameButton)
                restartGameButton.onClick.AddListener(() => {
                    HideAllGameEndPanels();
                    RestartGame();
                });

            if (badEndRestartButton)
                badEndRestartButton.onClick.AddListener(() => {
                    HideAllGameEndPanels();
                    RestartGame();
                });

            if (badEndMenuButton)
                badEndMenuButton.onClick.AddListener(() => {
                    HideAllGameEndPanels();
                    GameManager.Instance.ReturnToMainMenu();
                });

            if (happyEndRestartButton)
                happyEndRestartButton.onClick.AddListener(() => {
                    HideAllGameEndPanels();
                    RestartGame();
                });

            if (happyEndMenuButton)
                happyEndMenuButton.onClick.AddListener(() => {
                    HideAllGameEndPanels();
                    GameManager.Instance.ReturnToMainMenu();
                });
        }

        #endregion

        #region 音频Slider管理

        /// <summary>
        /// 初始化音频Slider
        /// </summary>
        private void InitializeAudioSliders()
        {
            // 从PlayerPrefs加载保存的音量设置
            float savedMusicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
            float savedSFXVolume = PlayerPrefs.GetFloat("SFXVolume", 0.8f);

            // 设置AudioManager的音量
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetMusicVolume(savedMusicVolume);
                AudioManager.Instance.SetSFXVolume(savedSFXVolume);
            }

            // 设置所有slider的属性和初始值
            SetupSlider(mainMenuMusicSlider, savedMusicVolume, OnMusicVolumeChanged);
            SetupSlider(mainMenuSFXSlider, savedSFXVolume, OnSFXVolumeChanged);
            SetupSlider(pauseMusicSlider, savedMusicVolume, OnMusicVolumeChanged);
            SetupSlider(pauseSFXSlider, savedSFXVolume, OnSFXVolumeChanged);

            Debug.Log($"🔊 音频Slider初始化完成 - 音乐: {savedMusicVolume:F2}, 音效: {savedSFXVolume:F2}");
        }

        /// <summary>
        /// 设置单个Slider的属性
        /// </summary>
        private void SetupSlider(Slider slider, float initialValue, UnityEngine.Events.UnityAction<float> callback)
        {
            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.value = initialValue;
                slider.onValueChanged.AddListener(callback);
            }
        }

        /// <summary>
        /// 音乐音量改变回调
        /// </summary>
        private void OnMusicVolumeChanged(float value)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetMusicVolume(value);
            }

            // 同步更新所有音乐slider的值
            UpdateSliderValue(mainMenuMusicSlider, value);
            UpdateSliderValue(pauseMusicSlider, value);

            // 保存设置
            PlayerPrefs.SetFloat("MusicVolume", value);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 音效音量改变回调
        /// </summary>
        private void OnSFXVolumeChanged(float value)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetSFXVolume(value);
                // 播放测试音效
                AudioManager.Instance.PlayButtonClickSound();
            }

            // 同步更新所有音效slider的值
            UpdateSliderValue(mainMenuSFXSlider, value);
            UpdateSliderValue(pauseSFXSlider, value);

            // 保存设置
            PlayerPrefs.SetFloat("SFXVolume", value);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 更新slider的值（不触发回调）
        /// </summary>
        private void UpdateSliderValue(Slider slider, float value)
        {
            if (slider != null && !Mathf.Approximately(slider.value, value))
            {
                // 临时移除监听器，更新值，然后重新添加
                slider.onValueChanged.RemoveAllListeners();
                slider.value = value;

                // 重新添加对应的监听器
                if (slider == mainMenuMusicSlider || slider == pauseMusicSlider)
                {
                    slider.onValueChanged.AddListener(OnMusicVolumeChanged);
                }
                else if (slider == mainMenuSFXSlider || slider == pauseSFXSlider)
                {
                    slider.onValueChanged.AddListener(OnSFXVolumeChanged);
                }
            }
        }

        #endregion

        #region 魔法球管理

        /// <summary>
        /// 创建魔法球
        /// </summary>
        private void CreateManaOrbs(int maxMana)
        {
            // 清除现有魔法球
            ClearManaOrbs();

            if (manaOrbsContainer == null || manaOrbPrefab == null)
            {
                Debug.LogError("❌ 魔法球容器或预制体未设置！");
                return;
            }

            currentMaxMana = maxMana;

            for (int i = 0; i < maxMana; i++)
            {
                // 实例化魔法球
                GameObject orbGO = Instantiate(manaOrbPrefab, manaOrbsContainer);

                // 设置位置
                Vector3 position = CalculateOrbPosition(i);
                orbGO.transform.localPosition = position;

                // 获取SimpleManaOrb组件
                SimpleManaOrb orbScript = orbGO.GetComponent<SimpleManaOrb>();
                if (orbScript == null)
                {
                    orbScript = orbGO.AddComponent<SimpleManaOrb>();
                }

                // 确保魔法球处于满状态
                orbScript.ResetToFull();

                // 添加到列表
                manaOrbs.Add(orbScript);

                // 设置名称便于调试
                orbGO.name = $"ManaOrb_{i + 1}";
            }
        }

        /// <summary>
        /// 计算魔法球位置
        /// </summary>
        private Vector3 CalculateOrbPosition(int index)
        {
            Vector3 position = startPosition;

            if (useHorizontalLayout)
            {
                // 水平排列
                position.x += index * orbSpacing;
            }
            else
            {
                // 垂直排列
                position.y -= index * orbSpacing;
            }

            return position;
        }

        /// <summary>
        /// 清除所有魔法球
        /// </summary>
        private void ClearManaOrbs()
        {
            foreach (var orb in manaOrbs)
            {
                if (orb != null && orb.gameObject != null)
                {
                    DestroyImmediate(orb.gameObject);
                }
            }
            manaOrbs.Clear();

            // 同时清理容器中可能残留的子对象
            if (manaOrbsContainer != null)
            {
                for (int i = manaOrbsContainer.childCount - 1; i >= 0; i--)
                {
                    Transform child = manaOrbsContainer.GetChild(i);
                    if (child != null)
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }

            currentMaxMana = 0;
        }

        /// <summary>
        /// 更新魔法值显示
        /// </summary>
        private void UpdateMana(int currentMana, int maxMana)
        {
            // 如果魔法球数量不匹配或为空，重新创建
            if (manaOrbs.Count != maxMana || currentMaxMana != maxMana || manaOrbs.Count == 0)
            {
                CreateManaOrbs(maxMana);
            }

            // 更新魔法球状态
            UpdateManaOrbsDisplay(currentMana, maxMana);
        }

        /// <summary>
        /// 更新魔法球显示状态
        /// </summary>
        private void UpdateManaOrbsDisplay(int currentMana, int maxMana)
        {
            for (int i = 0; i < manaOrbs.Count; i++)
            {
                if (manaOrbs[i] == null) continue;

                if (i < currentMana)
                {
                    // 这个魔法球应该是满的
                    if (manaOrbs[i].IsEmpty())
                    {
                        // 如果当前是空的，重置为满
                        manaOrbs[i].ResetToFull();
                    }
                }
                else
                {
                    // 这个魔法球应该是空的
                    if (!manaOrbs[i].IsEmpty())
                    {
                        // 如果当前是满的，播放变空动画
                        manaOrbs[i].PlayEmptyAnimation();
                    }
                }
            }
        }

        #endregion

        #region 游戏结束界面管理

        /// <summary>
        /// 显示Bad End界面
        /// </summary>
        private void ShowBadEnd()
        {
            StartCoroutine(ShowBadEndSequence());
        }

        /// <summary>
        /// 显示Happy End界面
        /// </summary>
        private void ShowHappyEnd()
        {
            StartCoroutine(ShowHappyEndSequence());
        }

        /// <summary>
        /// 处理通用游戏结束事件
        /// </summary>
        private void HandleGameEnded(string endType)
        {
            // 游戏结束时恢复检测UI状态
            RestoreDetectionUIState();
        }

        /// <summary>
        /// Intro 播放完毕或跳过后的收尾流程
        /// </summary>
        private IEnumerator CompleteIntroFlow()
        {
            // 隐藏过场面板
            SetPanel(cutscenePanel, false);

            // 显示 HUD 面板
            SetPanel(hudPanel, true);

            // （可选）在这里实例化或激活开场后需要的物件
            // Instantiate(introEndPrefab);

            isPlayingCutscene = false;
            yield break;
        }

        /// <summary>
        /// Bad End 播放完毕或跳过后的收尾流程
        /// </summary>
        private IEnumerator CompleteBadEndFlow()
        {
            // 隐藏过场面板
            SetPanel(cutscenePanel, false);

            // 显示 Bad End 面板
            if (badEndPanel != null)
                yield return StartCoroutine(ShowGameEndPanelWithFade(badEndPanel));
            else if (gameOverPanel != null)
                yield return StartCoroutine(ShowGameEndPanelWithFade(gameOverPanel));

            // （可选）实例化 BadEnd 相关物件
            // Instantiate(badEndEffectPrefab);

            UnlockCursorForUI();

            isBadEndPlaying = false;
            isPlayingCutscene = false;
        }

        /// <summary>
        /// Happy End 播放完毕或跳过后的收尾流程
        /// </summary>
        private IEnumerator CompleteHappyEndFlow()
        {
            // 隐藏过场面板
            SetPanel(cutscenePanel, false);

            // 显示 Happy End 面板
            if (happyEndPanel != null)
                yield return StartCoroutine(ShowGameEndPanelWithFade(happyEndPanel));

            // （可选）实例化 HappyEnd 相关物件
            // Instantiate(happyEndEffectPrefab);

            UnlockCursorForUI();

            isHappyEndPlaying = false;
            isPlayingCutscene = false;
        }

        /// <summary>
        /// Bad End 流程：调出过场面板 → 播放 BadEnd 分类动画 → 关闭过场面板 → 显示 Bad End 面板
        /// </summary>
        private IEnumerator ShowBadEndSequence()
        {
            isPlayingCutscene = true;
            isBadEndPlaying = true;
            cutsceneSkipped = false;

            SetPanel(cutscenePanel, true);
            SetPanel(mainMenuPanel, false);

            yield return StartCoroutine(PlayBadEndSequence());
            yield return StartCoroutine(CompleteBadEndFlow());
        }

        /// <summary>
        /// Happy End 流程：调出过场面板 → 播放 HappyEnd 分类动画 → 关闭过场面板 → 显示 Happy End 面板
        /// </summary>
        private IEnumerator ShowHappyEndSequence()
        {
            isPlayingCutscene = true;
            isHappyEndPlaying = true;
            cutsceneSkipped = false;

            SetPanel(cutscenePanel, true);
            SetPanel(mainMenuPanel, false);

            yield return StartCoroutine(PlayHappyEndSequence());
            yield return StartCoroutine(CompleteHappyEndFlow());
        }

        /// <summary>
        /// Intro 流程：调出过场面板 → 播放 Intro 分类动画 → 启动游戏 → 关闭过场面板 → 显示 HUD
        /// </summary>
        private IEnumerator ShowIntroSequence()
        {
            // 1. 标记开始播放
            isPlayingCutscene = true;
            cutsceneSkipped = false;

            // 2. 调出过场面板，隐藏 HUD
            SetPanel(cutscenePanel, true);
            SetPanel(hudPanel, false);

            // 3. 播放 Intro 分类的所有过场动画
            yield return StartCoroutine(PlayIntroSequence());

            // —— 播放完毕后，先启动游戏 —— 
            GameManager.Instance.StartGame();

            SetPanel(mainMenuPanel, false);

            // 4. 显示 HUD 面板
            SetPanel(hudPanel, true);

            

            // 5. 再关闭过场面板
            SetPanel(cutscenePanel, false);


            // 6. 重置播放标记
            isPlayingCutscene = false;
        }




        /// <summary>
        /// 带淡入效果显示游戏结束面板
        /// </summary>
        private System.Collections.IEnumerator ShowGameEndPanelWithFade(GameObject panel)
        {
            if (panel == null) yield break;

            // 立即显示面板
            panel.SetActive(true);

            // 如果有CanvasGroup组件，播放淡入动画
            CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                float elapsed = 0f;

                while (elapsed < gameEndFadeTime)
                {
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / gameEndFadeTime);
                    yield return null;
                }

                canvasGroup.alpha = 1f;
            }
        }

        /// <summary>
        /// 隐藏所有游戏结束面板
        /// </summary>
        private void HideAllGameEndPanels()
        {
            SetPanel(gameOverPanel, false);
            SetPanel(happyEndPanel, false);
            SetPanel(badEndPanel, false);
        }

        /// <summary>
        /// 为UI交互解锁鼠标
        /// </summary>
        private void UnlockCursorForUI()
        {
            if (!unlockCursorOnGameEnd) return;

            var camCtrl = Camera.main?.GetComponent<CameraController>();
            if (camCtrl != null)
                camCtrl.SetCursorLocked(false);
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        #endregion

        #region UI面板管理

        private void TogglePauseMenu(bool isPaused)
        {
            SetPanel(pausePanel, isPaused);
        }

        private void SetPanel(GameObject panel, bool state)
        {
            if (panel)
                panel.SetActive(state);
        }

        public void ShowHUD()
        {
            SetPanel(hudPanel, true);

            // 强制重新生成魔法球UI
            if (GameManager.Instance != null)
            {
                int currentMana = GameManager.Instance.GetCurrentMana();
                int maxMana = GameManager.Instance.GetMaxMana();

                // 清除现有魔法球
                ClearManaOrbs();

                // 重新创建魔法球
                CreateManaOrbs(maxMana);

                // 更新魔法球状态
                UpdateManaOrbsDisplay(currentMana, maxMana);
            }

            // 重置检测UI到默认状态
            if (crosshair != null) crosshair.SetActive(true);
            if (magicCircle != null) magicCircle.SetActive(false);
            isDetectionUIActive = false;

            //切换到游戏音乐
            AudioManager.Instance?.OnShowGameHUD();
        }

        /// <summary>
        /// 显示主菜单，并隐藏所有游戏内面板
        /// </summary>
        public void ShowMainMenu()
        {
            SetPanel(mainMenuPanel, true);
            SetPanel(hudPanel, false);
            SetPanel(pausePanel, false);
            SetPanel(cutscenePanel, false); // 确保隐藏过场动画面板
            HideAllGameEndPanels();

            // 清理魔法球UI
            ClearManaOrbs();

            // 清理过场动画相关
            CleanupCurrentCutscene();
            isPlayingCutscene = false;

            // 隐藏CutsceneManager
            if (cutsceneManager != null)
            {
                cutsceneManager.Hide();
                if (debugCutscene)
                    Debug.Log("🎬 UIManager: 返回主菜单时隐藏CutsceneManager");
            }

            // 重置检测UI到默认状态
            if (crosshair != null) crosshair.SetActive(true);
            if (magicCircle != null) magicCircle.SetActive(false);
            isDetectionUIActive = false;

            // 切换到主菜单音乐
            AudioManager.Instance?.OnShowMainMenu();
        }

        #endregion

        #region 过场动画时长管理 - 转发给 CutsceneManager




        /// <summary>
        /// 设置指定动画的手动播放时间
        /// </summary>
        public void SetManualDuration(int index, float duration, bool enable = true)
        {
            cutsceneManager.SetManualDuration(index, duration, enable);
        }

        /// <summary>
        /// 切换指定动画到自动检测模式
        /// </summary>
        public void UseAutoDetection(int index)
        {
            cutsceneManager.UseAutoDetection(index);
        }

        /// <summary>
        /// 获取有多少动画使用了手动时间设置
        /// </summary>
        public int GetManualTimingCount()
        {
            return cutsceneManager.GetManualTimingCount();
        }

        /// <summary>
        /// 检查指定动画是否使用手动时间
        /// </summary>
        public bool IsUsingManualTiming(int index)
        {
            return cutsceneManager.IsUsingManualTiming(index);
        }

        /// <summary>
        /// 批量设置所有动画使用自动检测
        /// </summary>
        public void ResetAllCutscenesToAuto()
        {
            cutsceneManager.SetAllToAutoDetection();
        }

        #endregion


        #region 公共接口

        /// <summary>
        /// 设置魔法球间距
        /// </summary>
        public void SetOrbSpacing(float spacing)
        {
            orbSpacing = spacing;
            // 重新计算所有魔法球位置
            for (int i = 0; i < manaOrbs.Count; i++)
            {
                if (manaOrbs[i] != null)
                {
                    Vector3 newPosition = CalculateOrbPosition(i);
                    manaOrbs[i].transform.localPosition = newPosition;
                }
            }
        }

        /// <summary>
        /// 设置布局方向
        /// </summary>
        public void SetHorizontalLayout(bool horizontal)
        {
            useHorizontalLayout = horizontal;
            // 重新计算所有魔法球位置
            for (int i = 0; i < manaOrbs.Count; i++)
            {
                if (manaOrbs[i] != null)
                {
                    Vector3 newPosition = CalculateOrbPosition(i);
                    manaOrbs[i].transform.localPosition = newPosition;
                }
            }
        }

        #endregion

        #region 重启游戏

        /// <summary>
        /// 重新开始游戏：先清理当前游戏状态，再播放开头过场动画或直接开始游戏
        /// </summary>
        private void RestartGame()
        {
            // 清理当前游戏对象、UI、事件等
            GameManager.Instance.CleanupCurrentGame();

            //直接启动游戏
            GameManager.Instance.StartGame();
        }


        #endregion

        #region 调试功能

        [Header("调试功能")]
        [SerializeField] private bool showDebugGUI = false;

        private void OnGUI()
        {
            if (!showDebugGUI || cutsceneManager == null)
                return;

            GUILayout.BeginArea(new Rect(10, 10, 400, 700));
            GUILayout.Label("=== UIManager 过场动画调试 ===");

            // 基本信息
            GUILayout.Label($"CutsceneManager 名称: {cutsceneManager.name}");
            GUILayout.Label($"过场动画总数: {cutsceneManager.GetCutsceneCount()}");
            GUILayout.Label($"手动设置数量: {cutsceneManager.GetManualTimingCount()}");

            // 每个动画的状态与时长
            var infos = cutsceneManager.GetAllCutsceneInfos();
            for (int i = 0; i < infos.Count; i++)
            {
                var info = infos[i];
                if (info.prefab == null) continue;

                string status = info.GetStatusSummary();
                float duration = cutsceneManager.GetAnimationDuration(i);
                GUILayout.Label($"[{i}] {info.animationName} — {duration:F1}s  ({status})");
            }

            // 一键全部切回自动检测
            if (GUILayout.Button("全部自动检测"))
            {
                cutsceneManager.SetAllToAutoDetection();
            }

            GUILayout.EndArea();
        }

        #endregion


        /// <summary>
        /// 查找CutsceneManager
        /// </summary>
        [ContextMenu("查找CutsceneManager")]
        public void FindCutsceneManager()
        {
            cutsceneManager = FindObjectOfType<CutsceneManager>();
            if (cutsceneManager != null)
            {
                Debug.Log($"✅ UIManager: 找到CutsceneManager: {cutsceneManager.name}");
                // 自动分析动画信息
                cutsceneManager.AnalyzeAllAnimations();
                ShowAnimationDurationInfo();
            }
            else
            {
                Debug.LogWarning("⚠️ UIManager: 未找到CutsceneManager");
            }
        }

        /// <summary>
        /// 测试第一个过场动画
        /// </summary>
        [ContextMenu("测试第一个过场动画")]
        public void TestFirstCutscene()
        {
            if (IsCutsceneManagerAvailable())
            {
                StartCoroutine(PlaySingleCutscene(0));
            }
            else
            {
                Debug.LogWarning("⚠️ UIManager: 没有可测试的过场动画prefab");
            }
        }

        /// <summary>
        /// 启用简单时间检测模式
        /// </summary>
        [ContextMenu("启用简单时间检测模式")]
        public void EnableSimpleTimeDetection()
        {
            useSimpleTimeBasedDetection = true;
            Debug.Log("🎬 UIManager: 已启用简单时间检测模式 - 适用于复杂动画或循环动画");
        }

        /// <summary>
        /// 禁用简单时间检测模式
        /// </summary>
        [ContextMenu("禁用简单时间检测模式")]
        public void DisableSimpleTimeDetection()
        {
            useSimpleTimeBasedDetection = false;
            Debug.Log("🎬 UIManager: 已禁用简单时间检测模式 - 使用智能动画完成检测");
        }

        /// <summary>
        /// 验证当前动画设置
        /// </summary>
        [ContextMenu("验证动画设置")]
        public void ValidateCurrentAnimationSettings()
        {
            if (currentCutsceneAnimator != null)
            {
                ValidateAnimationSettings(currentCutsceneAnimator);
            }
            else
            {
                Debug.LogWarning("⚠️ UIManager: 当前没有活动的动画实例");

                if (cutsceneManager != null && cutsceneManager.GetCutsceneCount() > 0)
                {
                    Debug.Log("💡 UIManager: 尝试验证第一个动画prefab的设置");
                    GameObject firstPrefab = cutsceneManager.GetCutscenePrefab(0);
                    if (firstPrefab != null)
                    {
                        Animator prefabAnimator = firstPrefab.GetComponent<Animator>();
                        if (prefabAnimator != null)
                        {
                            ValidateAnimationSettings(prefabAnimator);
                        }
                        else
                        {
                            Debug.LogWarning($"⚠️ UIManager: 第一个动画prefab {firstPrefab.name} 没有Animator组件");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 显示详细的播放设置信息
        /// </summary>
        [ContextMenu("显示播放设置详情")]
        public void ShowPlaybackSettings()
        {
            Debug.Log("=== UIManager 过场动画播放设置 ===");
            Debug.Log($"动画触发器名称: {animationTriggerName}");
            Debug.Log($"动画状态名称: {animationStateName}");
            Debug.Log($"最大等待时间: {maxAnimationWaitTime}s");
            Debug.Log($"动画间过渡时间: {animationTransitionTime}s");
            Debug.Log($"最小播放时间: {minimumAnimationTime}s");
            Debug.Log($"默认播放时间: {defaultAnimationTime}s");
            Debug.Log($"强制非循环播放: {forceNonLooping}");
            Debug.Log($"使用简单时间检测: {useSimpleTimeBasedDetection}");
            Debug.Log($"允许跳过动画: {enableSkipCutscene}");
            Debug.Log($"跳过按键: {skipKey}");
            Debug.Log($"调试模式: {debugCutscene}");
            Debug.Log($"自动查找CutsceneManager: {autoFindCutsceneManager}");

            if (cutsceneManager != null)
            {
                Debug.Log($"CutsceneManager: {cutsceneManager.name} (动画数量: {cutsceneManager.GetCutsceneCount()})");
                Debug.Log($"总播放时长: {cutsceneManager.GetTotalDuration():F1}秒");
            }
            else
            {
                Debug.Log("CutsceneManager: 未设置");
            }
        }

        /// <summary>
        /// 重置为推荐设置
        /// </summary>
        [ContextMenu("重置为推荐设置")]
        public void ResetToRecommendedSettings()
        {
            animationTriggerName = "Play";
            animationStateName = "CutsceneAnimation";
            maxAnimationWaitTime = 30f;
            animationTransitionTime = 0.5f;
            minimumAnimationTime = 1f;
            defaultAnimationTime = 5f;
            forceNonLooping = true;
            useSimpleTimeBasedDetection = false;
            enableSkipCutscene = true;
            skipKey = KeyCode.Space;
            debugCutscene = true;
            autoFindCutsceneManager = true;

            Debug.Log("🔄 UIManager: 已重置为推荐设置");
            ShowPlaybackSettings();
        }


    }
}