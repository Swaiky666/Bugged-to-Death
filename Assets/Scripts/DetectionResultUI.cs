// DetectionResultUI.cs - 检测结果UI显示系统
using UnityEngine;
using System.Collections;

namespace BugFixerGame
{
    public class DetectionResultUI : MonoBehaviour
    {
        [Header("UI组件")]
        [SerializeField] private GameObject successUI;         // 检测成功UI
        [SerializeField] private GameObject failureUI;         // 检测失败UI

        [Header("显示设置")]
        [SerializeField] private float displayDuration = 0.75f;     // 显示持续时间
        [SerializeField] private float fadeInTime = 0.2f;           // 淡入时间
        [SerializeField] private float fadeOutTime = 0.2f;          // 淡出时间
        [SerializeField] private bool useScaleAnimation = true;      // 是否使用缩放动画
        [SerializeField] private Vector3 startScale = new Vector3(0.5f, 0.5f, 1f);
        [SerializeField] private Vector3 endScale = Vector3.one;

        [Header("位置设置")]
        [SerializeField] private bool useRandomPosition = false;     // 是否使用随机位置
        [SerializeField] private Vector2 positionRange = new Vector2(100f, 50f); // 随机位置范围

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private bool testOnStart = false;             // 启动时测试

        private CanvasGroup successCanvasGroup;
        private CanvasGroup failureCanvasGroup;
        private Vector3 successOriginalPosition;
        private Vector3 failureOriginalPosition;
        private Coroutine displayCoroutine;
        private bool isDisplaying = false;

        // 单例
        public static DetectionResultUI Instance { get; private set; }

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
            if (testOnStart)
            {
                StartCoroutine(TestDisplaySequence());
            }
        }

        private void OnEnable()
        {
            // 订阅Player的检测结果事件
            Player.OnDetectionResult += ShowDetectionResult;
        }

        private void OnDisable()
        {
            // 取消订阅
            Player.OnDetectionResult -= ShowDetectionResult;
        }

        #endregion

        #region 初始化

        private void InitializeUI()
        {
            // 初始化成功UI
            if (successUI != null)
            {
                successCanvasGroup = successUI.GetComponent<CanvasGroup>();
                if (successCanvasGroup == null)
                {
                    successCanvasGroup = successUI.AddComponent<CanvasGroup>();
                }
                successOriginalPosition = successUI.transform.localPosition;
                successUI.SetActive(false);
            }
            else
            {
                Debug.LogError("❌ DetectionResultUI: successUI未设置！请在Inspector中配置成功UI引用。");
            }

            // 初始化失败UI
            if (failureUI != null)
            {
                failureCanvasGroup = failureUI.GetComponent<CanvasGroup>();
                if (failureCanvasGroup == null)
                {
                    failureCanvasGroup = failureUI.AddComponent<CanvasGroup>();
                }
                failureOriginalPosition = failureUI.transform.localPosition;
                failureUI.SetActive(false);
            }
            else
            {
                Debug.LogError("❌ DetectionResultUI: failureUI未设置！请在Inspector中配置失败UI引用。");
            }

            Debug.Log("✅ DetectionResultUI 初始化完成");
        }

        #endregion

        #region 结果显示

        /// <summary>
        /// 显示检测结果
        /// </summary>
        /// <param name="isSuccess">是否检测成功</param>
        private void ShowDetectionResult(bool isSuccess)
        {
            if (isDisplaying)
            {
                Debug.Log($"⚠️ DetectionResultUI: 正在显示其他结果，跳过新的显示请求");
                return;
            }

            Debug.Log($"🎯 DetectionResultUI: 显示检测结果 - {(isSuccess ? "成功" : "失败")}");

            if (displayCoroutine != null)
            {
                StopCoroutine(displayCoroutine);
            }

            displayCoroutine = StartCoroutine(DisplayResultCoroutine(isSuccess));
        }

        /// <summary>
        /// 显示结果协程
        /// </summary>
        private IEnumerator DisplayResultCoroutine(bool isSuccess)
        {
            isDisplaying = true;

            GameObject targetUI = isSuccess ? successUI : failureUI;
            CanvasGroup targetCanvasGroup = isSuccess ? successCanvasGroup : failureCanvasGroup;
            Vector3 originalPosition = isSuccess ? successOriginalPosition : failureOriginalPosition;

            if (targetUI == null || targetCanvasGroup == null)
            {
                Debug.LogError($"❌ DetectionResultUI: {(isSuccess ? "成功" : "失败")}UI组件缺失！");
                isDisplaying = false;
                yield break;
            }

            // 设置随机位置（如果启用）
            if (useRandomPosition)
            {
                Vector3 randomOffset = new Vector3(
                    Random.Range(-positionRange.x, positionRange.x),
                    Random.Range(-positionRange.y, positionRange.y),
                    0f
                );
                targetUI.transform.localPosition = originalPosition + randomOffset;
            }

            // 显示UI并开始淡入动画
            targetUI.SetActive(true);
            yield return StartCoroutine(FadeInAnimation(targetUI, targetCanvasGroup));

            // 等待显示时间
            yield return new WaitForSecondsRealtime(displayDuration);

            // 淡出动画
            yield return StartCoroutine(FadeOutAnimation(targetUI, targetCanvasGroup));

            // 隐藏UI并恢复原始位置
            targetUI.SetActive(false);
            targetUI.transform.localPosition = originalPosition;

            isDisplaying = false;
            Debug.Log($"✅ DetectionResultUI: {(isSuccess ? "成功" : "失败")}结果显示完成");
        }

        #endregion

        #region 动画

        /// <summary>
        /// 淡入动画
        /// </summary>
        private IEnumerator FadeInAnimation(GameObject targetUI, CanvasGroup canvasGroup)
        {
            // 设置初始状态
            canvasGroup.alpha = 0f;
            if (useScaleAnimation)
            {
                targetUI.transform.localScale = startScale;
            }

            float elapsedTime = 0f;

            while (elapsedTime < fadeInTime)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float t = elapsedTime / fadeInTime;

                // 淡入
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);

                // 缩放动画
                if (useScaleAnimation)
                {
                    targetUI.transform.localScale = Vector3.Lerp(startScale, endScale, t);
                }

                yield return null;
            }

            // 确保最终状态
            canvasGroup.alpha = 1f;
            if (useScaleAnimation)
            {
                targetUI.transform.localScale = endScale;
            }
        }

        /// <summary>
        /// 淡出动画
        /// </summary>
        private IEnumerator FadeOutAnimation(GameObject targetUI, CanvasGroup canvasGroup)
        {
            float startAlpha = canvasGroup.alpha;
            Vector3 currentScale = targetUI.transform.localScale;
            float elapsedTime = 0f;

            while (elapsedTime < fadeOutTime)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float t = elapsedTime / fadeOutTime;

                // 淡出
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);

                // 缩放动画
                if (useScaleAnimation)
                {
                    targetUI.transform.localScale = Vector3.Lerp(currentScale, startScale, t);
                }

                yield return null;
            }

            // 确保最终状态
            canvasGroup.alpha = 0f;
            if (useScaleAnimation)
            {
                targetUI.transform.localScale = startScale;
            }
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 设置显示时间
        /// </summary>
        public void SetDisplayDuration(float duration)
        {
            displayDuration = Mathf.Max(0.1f, duration);
        }

        /// <summary>
        /// 设置淡入淡出时间
        /// </summary>
        public void SetFadeTimes(float fadeIn, float fadeOut)
        {
            fadeInTime = Mathf.Max(0.1f, fadeIn);
            fadeOutTime = Mathf.Max(0.1f, fadeOut);
        }

        /// <summary>
        /// 设置是否使用缩放动画
        /// </summary>
        public void SetUseScaleAnimation(bool useScale)
        {
            useScaleAnimation = useScale;
        }

        /// <summary>
        /// 设置是否使用随机位置
        /// </summary>
        public void SetUseRandomPosition(bool useRandom)
        {
            useRandomPosition = useRandom;
        }

        /// <summary>
        /// 检查是否正在显示结果
        /// </summary>
        public bool IsDisplaying()
        {
            return isDisplaying;
        }

        /// <summary>
        /// 强制隐藏当前显示的UI
        /// </summary>
        public void ForceHide()
        {
            if (displayCoroutine != null)
            {
                StopCoroutine(displayCoroutine);
                displayCoroutine = null;
            }

            if (successUI != null) successUI.SetActive(false);
            if (failureUI != null) failureUI.SetActive(false);

            isDisplaying = false;

            Debug.Log("🛑 DetectionResultUI: 强制隐藏所有结果UI");
        }

        #endregion

        #region 调试功能

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(Screen.width - 300, 10, 280, 150));
            GUILayout.Label("=== Detection Result UI ===");
            GUILayout.Label($"正在显示: {isDisplaying}");
            GUILayout.Label($"显示时间: {displayDuration:F2}s");
            GUILayout.Label($"淡入时间: {fadeInTime:F2}s");
            GUILayout.Label($"淡出时间: {fadeOutTime:F2}s");
            GUILayout.Label($"缩放动画: {useScaleAnimation}");
            GUILayout.Label($"随机位置: {useRandomPosition}");

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("测试成功"))
            {
                ShowDetectionResult(true);
            }
            if (GUILayout.Button("测试失败"))
            {
                ShowDetectionResult(false);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("强制隐藏"))
            {
                ForceHide();
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// 测试显示序列
        /// </summary>
        private IEnumerator TestDisplaySequence()
        {
            yield return new WaitForSeconds(1f);

            Debug.Log("🧪 开始测试检测结果UI...");

            // 测试成功UI
            ShowDetectionResult(true);
            yield return new WaitForSeconds(displayDuration + fadeInTime + fadeOutTime + 0.5f);

            // 测试失败UI
            ShowDetectionResult(false);
            yield return new WaitForSeconds(displayDuration + fadeInTime + fadeOutTime + 0.5f);

            Debug.Log("✅ 检测结果UI测试完成");
        }

        [ContextMenu("🎯 测试成功结果")]
        private void TestSuccessResult()
        {
            if (Application.isPlaying)
            {
                ShowDetectionResult(true);
            }
        }

        [ContextMenu("❌ 测试失败结果")]
        private void TestFailureResult()
        {
            if (Application.isPlaying)
            {
                ShowDetectionResult(false);
            }
        }

        [ContextMenu("🔄 测试序列显示")]
        private void TestSequenceDisplay()
        {
            if (Application.isPlaying)
            {
                StartCoroutine(TestDisplaySequence());
            }
        }

        [ContextMenu("🛑 强制隐藏所有UI")]
        private void ContextForceHide()
        {
            if (Application.isPlaying)
            {
                ForceHide();
            }
        }

        [ContextMenu("🔍 检查组件设置")]
        private void CheckComponentSetup()
        {
            Debug.Log("=== DetectionResultUI 组件检查 ===");
            Debug.Log($"成功UI: {(successUI != null ? successUI.name : "未设置")}");
            Debug.Log($"失败UI: {(failureUI != null ? failureUI.name : "未设置")}");
            Debug.Log($"显示时间: {displayDuration}s");
            Debug.Log($"淡入时间: {fadeInTime}s");
            Debug.Log($"淡出时间: {fadeOutTime}s");
            Debug.Log($"缩放动画: {useScaleAnimation}");
            Debug.Log($"随机位置: {useRandomPosition} (范围: {positionRange})");

            if (successUI != null)
            {
                Debug.Log($"成功UI CanvasGroup: {(successCanvasGroup != null ? "已设置" : "自动添加")}");
            }

            if (failureUI != null)
            {
                Debug.Log($"失败UI CanvasGroup: {(failureCanvasGroup != null ? "已设置" : "自动添加")}");
            }
        }

        [ContextMenu("📊 显示详细状态")]
        private void ShowDetailedStatus()
        {
            Debug.Log("=== DetectionResultUI 详细状态 ===");
            Debug.Log($"实例状态: {(Instance == this ? "主实例" : "非主实例")}");
            Debug.Log($"正在显示: {isDisplaying}");
            Debug.Log($"显示协程: {(displayCoroutine != null ? "运行中" : "空闲")}");

            if (successUI != null)
            {
                Debug.Log($"成功UI激活: {successUI.activeInHierarchy}");
                Debug.Log($"成功UI透明度: {(successCanvasGroup != null ? successCanvasGroup.alpha.ToString("F2") : "无CanvasGroup")}");
            }

            if (failureUI != null)
            {
                Debug.Log($"失败UI激活: {failureUI.activeInHierarchy}");
                Debug.Log($"失败UI透明度: {(failureCanvasGroup != null ? failureCanvasGroup.alpha.ToString("F2") : "无CanvasGroup")}");
            }
        }

        #endregion
    }
}