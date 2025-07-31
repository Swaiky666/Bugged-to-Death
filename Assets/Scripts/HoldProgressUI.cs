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
        [SerializeField] private Image progressFillImage;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI objectNameText;

        [Header("设置")]
        [SerializeField] private string progressTextFormat = "检测中... {0:P0}";
        [SerializeField] private Color progressColor = Color.green;
        [SerializeField] private Color bugProgressColor = Color.red;           // bug物体检测时的颜色
        [SerializeField] private Color normalProgressColor = Color.blue;       // 普通物体检测时的颜色
        [SerializeField] private AnimationCurve progressCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("动画")]
        [SerializeField] private float fadeTime = 0.2f;
        [SerializeField] private Vector3 scaleFrom = new Vector3(0.8f, 0.8f, 1f);
        [SerializeField] private Vector3 scaleTo = Vector3.one;

        private CanvasGroup canvasGroup;
        private GameObject currentObject;    // 改为检测任何GameObject
        private BugObject currentBugObject;  // 如果是BugObject，保存引用
        private Coroutine fadeCoroutine;

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
            // 订阅Player事件 - 改为新的事件
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

            // 初始化进度条颜色
            if (progressFillImage != null)
            {
                progressFillImage.color = progressColor;
            }

            // 初始状态隐藏
            HideProgressImmediate();
        }

        #endregion

        #region 进度显示

        private void ShowProgress(GameObject detectedObject, float progress)
        {
            // 如果是新的对象，重新显示UI
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

            // 根据物体类型设置进度条颜色
            UpdateProgressColor();

            // 淡入动画
            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeIn());
        }

        private void UpdateProgress(float progress)
        {
            // 应用动画曲线
            float curvedProgress = progressCurve.Evaluate(progress);

            // 更新进度条
            if (progressFillImage != null)
            {
                progressFillImage.fillAmount = curvedProgress;
            }

            // 更新文本
            if (progressText != null)
            {
                progressText.text = string.Format(progressTextFormat, progress);
            }
        }

        private void UpdateProgressColor()
        {
            if (progressFillImage == null) return;

            // 根据物体类型设置不同颜色
            if (currentBugObject != null && currentBugObject.IsBugActive())
            {
                // 如果是激活的bug物体，使用bug颜色
                progressFillImage.color = bugProgressColor;
            }
            else if (currentBugObject != null)
            {
                // 如果是未激活的bug物体，使用普通颜色
                progressFillImage.color = normalProgressColor;
            }
            else
            {
                // 如果是普通物体，使用普通颜色
                progressFillImage.color = normalProgressColor;
            }
        }

        private void HideProgress()
        {
            currentObject = null;
            currentBugObject = null;

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

            currentObject = null;
            currentBugObject = null;
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
                case BugType.ObjectMissing: return "物件缺失";
                case BugType.ObjectMoved: return "位置错误";
                case BugType.MaterialMissing: return "材质丢失";
                case BugType.ObjectFlickering: return "物件闪烁";
                case BugType.CollisionMissing: return "碰撞丢失";
                case BugType.ClippingBug: return "穿模Bug";
                case BugType.ObjectAdded: return "多余物件";
                case BugType.CodeEffect: return "代码异常";
                case BugType.ExtraEyes: return "异常监视";
                default: return bugType.ToString();
            }
        }

        #endregion

        #region 公共接口

        public void SetProgressColor(Color color)
        {
            progressColor = color;
            if (progressFillImage != null && currentBugObject == null)
            {
                progressFillImage.color = color;
            }
        }

        public void SetBugProgressColor(Color color)
        {
            bugProgressColor = color;
            UpdateProgressColor();
        }

        public void SetNormalProgressColor(Color color)
        {
            normalProgressColor = color;
            UpdateProgressColor();
        }

        public void SetProgressTextFormat(string format)
        {
            progressTextFormat = format;
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

        #endregion

        #region 调试功能

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(Screen.width - 350, Screen.height - 200, 330, 190));
            GUILayout.Label("=== Hold Progress UI Debug ===");
            GUILayout.Label($"显示状态: {IsShowing()}");
            GUILayout.Label($"当前物体: {(currentObject ? currentObject.name : "无")}");
            GUILayout.Label($"当前Bug物体: {(currentBugObject ? currentBugObject.name : "无")}");
            GUILayout.Label($"检测到激活Bug: {IsDetectingBug()}");
            GUILayout.Label($"透明度: {(canvasGroup ? canvasGroup.alpha.ToString("F2") : "无")}");

            if (GUILayout.Button("测试显示进度"))
            {
                if (currentObject == null)
                {
                    // 尝试创建测试对象进行测试
                    GameObject testObj = new GameObject("TestObject");
                    StartCoroutine(TestProgressAnimation(testObj));
                }
            }

            if (GUILayout.Button("测试Bug物体进度"))
            {
                // 尝试找场景中的Bug对象进行测试
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

        #endregion
    }
}