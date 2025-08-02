// HoldProgressUI.cs - 使用Slider控制的长按进度UI系统
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace BugFixerGame
{
    public class HoldProgressUI : MonoBehaviour
    {
        [Header("UI组件")]
        [SerializeField] private GameObject progressPanel;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI objectNameText;

        [Header("设置")]
        [SerializeField] private string progressTextFormat = "检测中... {0:P0}";
        [SerializeField] private AnimationCurve progressCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("动画")]
        [SerializeField] private float fadeTime = 0.2f;
        [SerializeField] private Vector3 scaleFrom = new Vector3(0.8f, 0.8f, 1f);
        [SerializeField] private Vector3 scaleTo = Vector3.one;

        [Header("Slider设置")]
        [SerializeField] private bool animateSliderTransition = true;   // 是否启用滑块过渡动画
        [SerializeField] private float sliderTransitionSpeed = 5f;      // 滑块过渡速度

        private CanvasGroup canvasGroup;
        private GameObject currentObject;    // 当前检测的GameObject
        private BugObject currentBugObject;  // 如果是BugObject，保存引用
        private Coroutine fadeCoroutine;
        private float targetSliderValue = 0f;  // 目标滑块值
        private bool isUpdatingSlider = false; // 是否正在更新滑块

        // 单例
        public static HoldProgressUI Instance { get; private set; }

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

        private void OnEnable()
        {
            // 订阅Player事件 - 当前的事件
            Player.OnObjectHoldProgress += ShowProgress;
            Player.OnHoldCancelled += HideProgress;
        }

        private void OnDisable()
        {
            // 取消订阅
            Player.OnObjectHoldProgress -= ShowProgress;
            Player.OnHoldCancelled -= HideProgress;
        }

        #endregion

        #region 初始化

        private void InitializeUI()
        {
            // 获取CanvasGroup组件，如果没有则添加
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // 初始化进度滑块
            if (progressSlider != null)
            {
                progressSlider.minValue = 0f;
                progressSlider.maxValue = 1f;
                progressSlider.value = 0f;
                progressSlider.interactable = false; // 禁用用户交互
            }
            else
            {
                Debug.LogError("❌ HoldProgressUI: progressSlider未设置！请在Inspector中配置Slider引用。");
            }

            // 初始状态隐藏
            HideProgressImmediate();

            Debug.Log("✅ HoldProgressUI 初始化完成");
        }

        #endregion

        #region 进度显示

        private void ShowProgress(GameObject detectedObject, float progress)
        {
            // 如果是新的对象，重新显示进度面板
            if (currentObject != detectedObject)
            {
                currentObject = detectedObject;
                currentBugObject = detectedObject?.GetComponent<BugObject>();
                ShowProgressPanel();
            }

            // 更新进度
            UpdateProgress(progress);
        }

        private void ShowProgressPanel()
        {
            if (progressPanel != null)
                progressPanel.SetActive(true);

            // 设置物体名称和检测状态
            if (objectNameText != null && currentObject != null)
            {
                objectNameText.text = GetObjectDisplayName(currentObject);
            }

            // 淡入动画
            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeIn());
        }

        private void UpdateProgress(float progress)
        {
            // 应用动画曲线
            float curvedProgress = progressCurve.Evaluate(progress);

            // 更新目标滑块值
            targetSliderValue = curvedProgress;

            // 更新进度滑块
            if (progressSlider != null)
            {
                if (animateSliderTransition && !isUpdatingSlider)
                {
                    // 启动滑块过渡动画
                    StartCoroutine(AnimateSliderToTarget());
                }
                else
                {
                    // 直接设置值
                    progressSlider.value = curvedProgress;
                }
            }

            // 更新文本
            if (progressText != null)
            {
                progressText.text = string.Format(progressTextFormat, progress);
            }
        }

        /// <summary>
        /// 滑块过渡动画协程
        /// </summary>
        private IEnumerator AnimateSliderToTarget()
        {
            if (progressSlider == null) yield break;

            isUpdatingSlider = true;
            float startValue = progressSlider.value;
            float elapsedTime = 0f;
            float animationTime = Mathf.Abs(targetSliderValue - startValue) / sliderTransitionSpeed;

            while (elapsedTime < animationTime)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float t = elapsedTime / animationTime;
                progressSlider.value = Mathf.Lerp(startValue, targetSliderValue, t);
                yield return null;
            }

            progressSlider.value = targetSliderValue;
            isUpdatingSlider = false;
        }

        private void HideProgress()
        {
            currentObject = null;
            currentBugObject = null;

            // 重置滑块值
            if (progressSlider != null)
            {
                progressSlider.value = 0f;
            }
            targetSliderValue = 0f;
            isUpdatingSlider = false;

            // 淡出动画
            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeOut());
        }

        private void HideProgressImmediate()
        {
            if (progressPanel != null)
                progressPanel.SetActive(false);

            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            if (progressSlider != null)
                progressSlider.value = 0f;

            currentObject = null;
            currentBugObject = null;
            targetSliderValue = 0f;
            isUpdatingSlider = false;
        }

        #endregion

        #region 动画

        private IEnumerator FadeIn()
        {
            if (canvasGroup == null) yield break;

            // 设置初始状态
            canvasGroup.alpha = 0f;
            transform.localScale = scaleFrom;

            float elapsedTime = 0f;

            while (elapsedTime < fadeTime)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float t = elapsedTime / fadeTime;

                canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
                transform.localScale = Vector3.Lerp(scaleFrom, scaleTo, t);

                yield return null;
            }

            canvasGroup.alpha = 1f;
            transform.localScale = scaleTo;
        }

        private IEnumerator FadeOut()
        {
            if (canvasGroup == null) yield break;

            float startAlpha = canvasGroup.alpha;
            Vector3 startScale = transform.localScale;
            float elapsedTime = 0f;

            while (elapsedTime < fadeTime)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float t = elapsedTime / fadeTime;

                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                transform.localScale = Vector3.Lerp(startScale, scaleFrom, t);

                yield return null;
            }

            canvasGroup.alpha = 0f;
            transform.localScale = scaleFrom;

            if (progressPanel != null)
                progressPanel.SetActive(false);
        }

        #endregion

        #region 辅助方法

        private string GetObjectDisplayName(GameObject obj)
        {
            if (obj == null) return "未知物体";

            string baseName = obj.name;
            string statusText = "";

            // 检查是否是BugObject
            BugObject bugObj = obj.GetComponent<BugObject>();
            if (bugObj != null)
            {
                if (bugObj.IsBugActive())
                {
                    statusText = $" ({GetBugTypeDisplayName(bugObj.GetBugType())} - 激活)";
                }
                else
                {
                    statusText = $" ({GetBugTypeDisplayName(bugObj.GetBugType())} - 未激活)";
                }
            }
            else
            {
                statusText = " (普通物体)";
            }

            return $"检测: {baseName}{statusText}";
        }

        private string GetBugTypeDisplayName(BugType bugType)
        {
            switch (bugType)
            {
                case BugType.ObjectFlickering: return "物体闪烁";
                case BugType.CollisionMissing: return "碰撞缺失";
                case BugType.WrongOrMissingMaterial: return "错误或缺失材质";
                case BugType.WrongObject: return "错误物体";
                case BugType.MissingObject: return "缺失物体";
                case BugType.ObjectShaking: return "物体震动";
                case BugType.ObjectMovedOrClipping: return "物体位置或穿模";
                default: return bugType.ToString();
            }
        }

        #endregion

        #region 公共接口

        public void SetProgressTextFormat(string format)
        {
            progressTextFormat = format;
        }

        public void SetSliderTransitionSettings(bool animate, float speed)
        {
            animateSliderTransition = animate;
            sliderTransitionSpeed = speed;
        }

        public bool IsShowing()
        {
            return progressPanel != null && progressPanel.activeInHierarchy;
        }

        public GameObject GetCurrentObject()
        {
            return currentObject;
        }

        public BugObject GetCurrentBugObject()
        {
            return currentBugObject;
        }

        public bool IsDetectingBug()
        {
            return currentBugObject != null && currentBugObject.IsBugActive();
        }

        public float GetCurrentSliderValue()
        {
            return progressSlider != null ? progressSlider.value : 0f;
        }

        public float GetTargetSliderValue()
        {
            return targetSliderValue;
        }

        public bool IsUpdatingSlider()
        {
            return isUpdatingSlider;
        }

        #endregion

        #region 调试功能

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(Screen.width - 380, Screen.height - 220, 360, 210));
            GUILayout.Label("=== Hold Progress UI Debug ===");
            GUILayout.Label($"显示状态: {IsShowing()}");
            GUILayout.Label($"当前物体: {(currentObject ? currentObject.name : "无")}");
            GUILayout.Label($"当前BugObject: {(currentBugObject ? currentBugObject.name : "无")}");
            GUILayout.Label($"检测到激活Bug: {IsDetectingBug()}");
            GUILayout.Label($"透明度: {(canvasGroup ? canvasGroup.alpha.ToString("F2") : "无")}");
            GUILayout.Label($"滑块当前值: {GetCurrentSliderValue():F2}");
            GUILayout.Label($"滑块目标值: {GetTargetSliderValue():F2}");
            GUILayout.Label($"滑块动画中: {isUpdatingSlider}");

            if (GUILayout.Button("测试显示进度"))
            {
                if (currentObject == null)
                {
                    // 创建测试对象进行测试
                    GameObject testObj = new GameObject("TestObject");
                    StartCoroutine(TestProgressAnimation(testObj));
                }
            }

            if (GUILayout.Button("测试BugObject进度"))
            {
                // 尝试找到场景中的Bug对象进行测试
                BugObject testBug = FindObjectOfType<BugObject>();
                if (testBug != null)
                {
                    StartCoroutine(TestProgressAnimation(testBug.gameObject));
                }
            }

            if (GUILayout.Button("隐藏进度"))
            {
                HideProgress();
            }

            GUILayout.EndArea();
        }

        private IEnumerator TestProgressAnimation(GameObject testObj)
        {
            float duration = 2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = elapsed / duration;
                ShowProgress(testObj, progress);
                yield return null;
            }

            HideProgress();

            // 如果是测试创建的对象，销毁它
            if (testObj.name == "TestObject")
            {
                DestroyImmediate(testObj);
            }
        }

        [ContextMenu("🎮 测试滑块进度动画")]
        private void TestSliderAnimation()
        {
            if (Application.isPlaying)
            {
                StartCoroutine(TestSliderAnimationCoroutine());
            }
        }

        private IEnumerator TestSliderAnimationCoroutine()
        {
            GameObject testObj = new GameObject("SliderTestObject");

            // 显示进度面板
            ShowProgressPanel();
            currentObject = testObj;

            // 测试不同进度值
            float[] testValues = { 0f, 0.25f, 0.5f, 0.75f, 1f, 0f };

            foreach (float value in testValues)
            {
                Debug.Log($"🎮 测试滑块值: {value:P0}");
                UpdateProgress(value);
                yield return new WaitForSecondsRealtime(1f);
            }

            HideProgress();
            DestroyImmediate(testObj);
        }

        [ContextMenu("🔍 检查滑块组件")]
        private void CheckSliderComponents()
        {
            Debug.Log("=== 滑块组件检查 ===");
            Debug.Log($"Slider引用: {(progressSlider != null ? "已设置" : "未设置")}");

            if (progressSlider != null)
            {
                Debug.Log($"滑块值范围: {progressSlider.minValue} - {progressSlider.maxValue}");
                Debug.Log($"当前值: {progressSlider.value}");
                Debug.Log($"可交互: {progressSlider.interactable}");

                if (progressSlider.fillRect != null)
                {
                    var fillImage = progressSlider.fillRect.GetComponent<Image>();
                    Debug.Log($"填充区域: {(fillImage != null ? "有效" : "无效")}");
                }

                if (progressSlider.handleRect != null)
                {
                    var handleImage = progressSlider.handleRect.GetComponent<Image>();
                    Debug.Log($"把手区域: {(handleImage != null ? "有效" : "无效")}");
                }
            }

            Debug.Log($"动画过渡: {animateSliderTransition}");
            Debug.Log($"过渡速度: {sliderTransitionSpeed}");
            Debug.Log($"当前目标值: {targetSliderValue}");
            Debug.Log($"正在更新: {isUpdatingSlider}");
        }

        [ContextMenu("🔄 强制重置滑块")]
        private void ForceResetSlider()
        {
            if (Application.isPlaying)
            {
                Debug.Log("🔄 强制重置滑块状态");
                HideProgressImmediate();

                if (progressSlider != null)
                {
                    progressSlider.value = 0f;
                }

                targetSliderValue = 0f;
                isUpdatingSlider = false;

                Debug.Log("✅ 滑块重置完成");
            }
        }

        [ContextMenu("📊 显示详细状态")]
        private void ShowDetailedStatus()
        {
            Debug.Log("=== HoldProgressUI 详细状态 ===");
            Debug.Log($"实例状态: {(Instance == this ? "主实例" : "非主实例")}");
            Debug.Log($"面板激活: {IsShowing()}");
            Debug.Log($"当前检测物体: {(currentObject != null ? currentObject.name : "无")}");
            Debug.Log($"当前Bug物体: {(currentBugObject != null ? $"{currentBugObject.name} (激活: {currentBugObject.IsBugActive()})" : "无")}");
            Debug.Log($"CanvasGroup透明度: {(canvasGroup != null ? canvasGroup.alpha.ToString("F2") : "无")}");
            Debug.Log($"滑块组件: {(progressSlider != null ? "已设置" : "未设置")}");

            if (progressSlider != null)
            {
                Debug.Log($"滑块当前值: {progressSlider.value:F3}");
                Debug.Log($"滑块目标值: {targetSliderValue:F3}");
                Debug.Log($"滑块动画状态: {(isUpdatingSlider ? "更新中" : "空闲")}");
            }

            Debug.Log($"进度文本格式: {progressTextFormat}");
            Debug.Log($"动画设置 - 淡入时间: {fadeTime}s, 滑块动画: {animateSliderTransition}, 速度: {sliderTransitionSpeed}");
        }

        #endregion
    }
}