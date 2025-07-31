using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace BugFixerGame
{
    public class HoldProgressUI : MonoBehaviour
    {
        [Header("UI���")]
        [SerializeField] private GameObject progressPanel;
        [SerializeField] private Image progressFillImage;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI objectNameText;

        [Header("����")]
        [SerializeField] private string progressTextFormat = "�����... {0:P0}";
        [SerializeField] private Color progressColor = Color.green;
        [SerializeField] private Color bugProgressColor = Color.red;           // bug������ʱ����ɫ
        [SerializeField] private Color normalProgressColor = Color.blue;       // ��ͨ������ʱ����ɫ
        [SerializeField] private AnimationCurve progressCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("����")]
        [SerializeField] private float fadeTime = 0.2f;
        [SerializeField] private Vector3 scaleFrom = new Vector3(0.8f, 0.8f, 1f);
        [SerializeField] private Vector3 scaleTo = Vector3.one;

        private CanvasGroup canvasGroup;
        private GameObject currentObject;    // ��Ϊ����κ�GameObject
        private BugObject currentBugObject;  // �����BugObject����������
        private Coroutine fadeCoroutine;

        // ����
        public static HoldProgressUI Instance { get; private set; }

        #region Unity��������

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
            // ����Player�¼� - ��Ϊ�µ��¼�
            Player.OnObjectHoldProgress += ShowProgress;
            Player.OnHoldCancelled += HideProgress;
        }

        private void OnDisable()
        {
            // ȡ������
            Player.OnObjectHoldProgress -= ShowProgress;
            Player.OnHoldCancelled -= HideProgress;
        }

        #endregion

        #region ��ʼ��

        private void InitializeUI()
        {
            // ��ȡCanvasGroup��������û�������
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // ��ʼ����������ɫ
            if (progressFillImage != null)
            {
                progressFillImage.color = progressColor;
            }

            // ��ʼ״̬����
            HideProgressImmediate();
        }

        #endregion

        #region ������ʾ

        private void ShowProgress(GameObject detectedObject, float progress)
        {
            // ������µĶ���������ʾUI
            if (currentObject != detectedObject)
            {
                currentObject = detectedObject;
                currentBugObject = detectedObject?.GetComponent<BugObject>();
                ShowProgressPanel();
            }

            // ���½���
            UpdateProgress(progress);
        }

        private void ShowProgressPanel()
        {
            if (progressPanel != null)
                progressPanel.SetActive(true);

            // �����������ƺͼ��״̬
            if (objectNameText != null && currentObject != null)
            {
                objectNameText.text = GetObjectDisplayName(currentObject);
            }

            // ���������������ý�������ɫ
            UpdateProgressColor();

            // ���붯��
            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeIn());
        }

        private void UpdateProgress(float progress)
        {
            // Ӧ�ö�������
            float curvedProgress = progressCurve.Evaluate(progress);

            // ���½�����
            if (progressFillImage != null)
            {
                progressFillImage.fillAmount = curvedProgress;
            }

            // �����ı�
            if (progressText != null)
            {
                progressText.text = string.Format(progressTextFormat, progress);
            }
        }

        private void UpdateProgressColor()
        {
            if (progressFillImage == null) return;

            // ���������������ò�ͬ��ɫ
            if (currentBugObject != null && currentBugObject.IsBugActive())
            {
                // ����Ǽ����bug���壬ʹ��bug��ɫ
                progressFillImage.color = bugProgressColor;
            }
            else if (currentBugObject != null)
            {
                // �����δ�����bug���壬ʹ����ͨ��ɫ
                progressFillImage.color = normalProgressColor;
            }
            else
            {
                // �������ͨ���壬ʹ����ͨ��ɫ
                progressFillImage.color = normalProgressColor;
            }
        }

        private void HideProgress()
        {
            currentObject = null;
            currentBugObject = null;

            // ��������
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

        #region ����

        private IEnumerator FadeIn()
        {
            if (canvasGroup == null) yield break;

            // ���ó�ʼ״̬
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

        #region ��������

        private string GetObjectDisplayName(GameObject obj)
        {
            if (obj == null) return "δ֪����";

            string baseName = obj.name;
            string statusText = "";

            // ����Ƿ���BugObject
            BugObject bugObj = obj.GetComponent<BugObject>();
            if (bugObj != null)
            {
                if (bugObj.IsBugActive())
                {
                    statusText = $" ({GetBugTypeDisplayName(bugObj.GetBugType())} - ����)";
                }
                else
                {
                    statusText = $" ({GetBugTypeDisplayName(bugObj.GetBugType())} - δ����)";
                }
            }
            else
            {
                statusText = " (��ͨ����)";
            }

            return $"���: {baseName}{statusText}";
        }

        private string GetBugTypeDisplayName(BugType bugType)
        {
            switch (bugType)
            {
                case BugType.ObjectMissing: return "���ȱʧ";
                case BugType.ObjectMoved: return "λ�ô���";
                case BugType.MaterialMissing: return "���ʶ�ʧ";
                case BugType.ObjectFlickering: return "�����˸";
                case BugType.CollisionMissing: return "��ײ��ʧ";
                case BugType.ClippingBug: return "��ģBug";
                case BugType.ObjectAdded: return "�������";
                case BugType.CodeEffect: return "�����쳣";
                case BugType.ExtraEyes: return "�쳣����";
                default: return bugType.ToString();
            }
        }

        #endregion

        #region �����ӿ�

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

        #region ���Թ���

        [Header("����")]
        [SerializeField] private bool showDebugInfo = false;

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(Screen.width - 350, Screen.height - 200, 330, 190));
            GUILayout.Label("=== Hold Progress UI Debug ===");
            GUILayout.Label($"��ʾ״̬: {IsShowing()}");
            GUILayout.Label($"��ǰ����: {(currentObject ? currentObject.name : "��")}");
            GUILayout.Label($"��ǰBug����: {(currentBugObject ? currentBugObject.name : "��")}");
            GUILayout.Label($"��⵽����Bug: {IsDetectingBug()}");
            GUILayout.Label($"͸����: {(canvasGroup ? canvasGroup.alpha.ToString("F2") : "��")}");

            if (GUILayout.Button("������ʾ����"))
            {
                if (currentObject == null)
                {
                    // ���Դ������Զ�����в���
                    GameObject testObj = new GameObject("TestObject");
                    StartCoroutine(TestProgressAnimation(testObj));
                }
            }

            if (GUILayout.Button("����Bug�������"))
            {
                // �����ҳ����е�Bug������в���
                BugObject testBug = FindObjectOfType<BugObject>();
                if (testBug != null)
                {
                    StartCoroutine(TestProgressAnimation(testBug.gameObject));
                }
            }

            if (GUILayout.Button("���ؽ���"))
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

            // ����ǲ��Դ����Ķ���������
            if (testObj.name == "TestObject")
            {
                DestroyImmediate(testObj);
            }
        }

        #endregion
    }
}