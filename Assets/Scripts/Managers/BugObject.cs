using System;
using System.Collections;
using UnityEngine;

namespace BugFixerGame
{
    // Bug����ű� - ���ص���ҪBugЧ����GameObject��
    public class BugObject : MonoBehaviour
    {
        [Header("Bug����")]
        [SerializeField] private BugType bugType = BugType.None;
        [SerializeField] private bool startWithBugActive = false;

        [Header("��ȷ��������")]
        [SerializeField] private GameObject correctObject;          // ��ȷ������
        [Tooltip("�޸�Bug����ʾ����ȷ����")]
        public GameObject CorrectObject
        {
            get { return correctObject; }
            set { correctObject = value; InitializeCorrectObject(); }
        }
        [SerializeField] private float popupAnimationTime = 0.5f;  // ��������ʱ��
        [SerializeField]
        private AnimationCurve popupCurve = new AnimationCurve(
            new Keyframe(0, 0, 0, 0),
            new Keyframe(0.6f, 1.1f, 2, 2),
            new Keyframe(1, 1, 0, 0)
        ); // ������������

        [Header("����������")]
        [SerializeField] private bool enableClickToFix = true;      // �Ƿ����õ���޸�
        [SerializeField] private LayerMask clickLayerMask = -1;     // ������㼶

        [Header("��˸Bug����")]
        [SerializeField] private float flickerInterval = 0.5f;
        [SerializeField] private float flickerAlphaMin = 0.3f;
        [SerializeField] private float flickerAlphaMax = 1f;

        [Header("��ײ��ʧBug����")]
        [SerializeField] private float collisionMissingAlpha = 0.8f;

        [Header("����Bug����")]
        [SerializeField] private Material buggyMaterial; // ��ɫ���ʵ�

        [Header("�ƶ�Bug����")]
        [SerializeField] private Vector3 bugOffset = Vector3.zero;

        // �������
        private Renderer objectRenderer;
        private SpriteRenderer spriteRenderer;
        private Collider2D objectCollider2D;
        private Collider objectCollider3D;

        // ԭʼ״̬����
        private Vector3 originalPosition;
        private Color originalColor;
        private Material originalMaterial;
        private bool originalCollider2DEnabled;
        private bool originalCollider3DEnabled;

        // ����ʱ״̬
        private bool isBugActive = false;
        private bool isBeingFixed = false;  // �Ƿ������޸���
        private Coroutine flickerCoroutine;
        private Coroutine popupCoroutine;

        // ��Ч����
        private GameObject spawnedEffect;

        // ���������
        private Collider2D clickCollider2D;
        private Collider clickCollider3D;

        // �¼�
        public static event Action<BugObject> OnBugClicked;         // Bug�����
        public static event Action<BugObject> OnBugFixed;           // Bug���޸����

        #region Unity��������

        private void Awake()
        {
            CacheComponents();
            SaveOriginalState();
            InitializeCorrectObject();

            if (startWithBugActive)
            {
                ActivateBug();
            }
        }

        private void Update()
        {
            HandleClickDetection();
        }

        private void OnDestroy()
        {
            DeactivateBug();
        }

        #endregion

        #region Bug�޸�ϵͳ

        private void StartBugFix()
        {
            if (isBeingFixed) return;

            isBeingFixed = true;

            // ����ͣ��BugЧ��
            DeactivateBug();

            // ��ʾ��ȷ���岢���Ŷ���
            if (correctObject != null)
            {
                ShowCorrectObject();
            }
            else
            {
                // ���û����ȷ���壬ֱ������޸�
                CompleteBugFix();
            }
        }

        private void ShowCorrectObject()
        {
            correctObject.SetActive(true);

            // ���ŵ�������
            if (popupCoroutine != null)
                StopCoroutine(popupCoroutine);

            popupCoroutine = StartCoroutine(PopupAnimation());
        }

        private IEnumerator PopupAnimation()
        {
            Vector3 originalScale = correctObject.transform.localScale;
            correctObject.transform.localScale = Vector3.zero;

            float elapsedTime = 0f;

            while (elapsedTime < popupAnimationTime)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / popupAnimationTime;
                float curveValue = popupCurve.Evaluate(progress);

                correctObject.transform.localScale = originalScale * curveValue;

                yield return null;
            }

            correctObject.transform.localScale = originalScale;

            // ������ɺ󣬵ȴ�һ��ʱ��������޸�
            yield return new WaitForSeconds(0.5f);

            CompleteBugFix();
        }

        private void CompleteBugFix()
        {
            Debug.Log($"Bug�޸����: {gameObject.name}");

            // �����޸�����¼�
            OnBugFixed?.Invoke(this);

            // ����Bug���壨�ӳ�������ȷ���¼�������ɣ�
            StartCoroutine(DestroyBugObjectDelayed());
        }

        private IEnumerator DestroyBugObjectDelayed()
        {
            yield return new WaitForEndOfFrame();

            // �������Bug����
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        #endregion

        #region ������ϵͳ

        // ��PlayerController���õĵ������
        public void OnClickedByPlayer()
        {
            // ֻ�е�Bug������û�������޸�ʱ�Ŵ�����
            if (!isBugActive || isBeingFixed) return;

            Debug.Log($"��ҵ����Bug����: {gameObject.name}");

            // ��������¼�
            OnBugClicked?.Invoke(this);

            // ��ʼ�޸�Bug
            StartBugFix();
        }

        private void HandleClickDetection()
        {
            // �������������PlayerController����������Ϊ����
            // ֻ�е�Bug���������õ���޸�ʱ�ż����
            if (!isBugActive || !enableClickToFix || isBeingFixed) return;

            if (Input.GetMouseButtonDown(0)) // ������
            {
                CheckMouseClick();
            }
        }

        private void CheckMouseClick()
        {
            Vector3 mousePosition = Input.mousePosition;
            Ray ray = Camera.main.ScreenPointToRay(mousePosition);

            // 2D������
            if (clickCollider2D != null)
            {
                Vector2 worldPoint = Camera.main.ScreenToWorldPoint(mousePosition);
                if (clickCollider2D.OverlapPoint(worldPoint))
                {
                    OnClickDetected();
                    return;
                }
            }

            // 3D������
            if (clickCollider3D != null)
            {
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, Mathf.Infinity, clickLayerMask))
                {
                    if (hit.collider == clickCollider3D)
                    {
                        OnClickDetected();
                        return;
                    }
                }
            }
        }

        private void OnClickDetected()
        {
            Debug.Log($"Bug���屻ֱ�ӵ��: {gameObject.name}");

            // ��������¼�
            OnBugClicked?.Invoke(this);

            // ��ʼ�޸�Bug
            StartBugFix();
        }

        #endregion

        #region ��ʼ��

        private void CacheComponents()
        {
            objectRenderer = GetComponent<Renderer>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            objectCollider2D = GetComponent<Collider2D>();
            objectCollider3D = GetComponent<Collider>();

            // �������������������ײ��������ͬ��
            clickCollider2D = objectCollider2D;
            clickCollider3D = objectCollider3D;

            // ���û����ײ�壬�������һ�����ڵ�����
            if (clickCollider2D == null && clickCollider3D == null)
            {
                if (spriteRenderer != null)
                {
                    // 2D�������BoxCollider2D
                    clickCollider2D = gameObject.AddComponent<BoxCollider2D>();
                    clickCollider2D.isTrigger = true;
                }
                else if (objectRenderer != null)
                {
                    // 3D�������BoxCollider
                    clickCollider3D = gameObject.AddComponent<BoxCollider>();
                    clickCollider3D.isTrigger = true;
                }
            }
        }

        private void InitializeCorrectObject()
        {
            // ȷ����ȷ�����ڿ�ʼʱ�����ص�
            if (correctObject != null)
            {
                correctObject.SetActive(false);
            }
        }

        private void SaveOriginalState()
        {
            // ����ԭʼλ��
            originalPosition = transform.position;

            // ����ԭʼ��ɫ�Ͳ���
            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
            }
            else if (objectRenderer != null)
            {
                originalColor = objectRenderer.material.color;
                originalMaterial = objectRenderer.material;
            }

            // ������ײ��״̬
            if (objectCollider2D != null)
                originalCollider2DEnabled = objectCollider2D.enabled;
            if (objectCollider3D != null)
                originalCollider3DEnabled = objectCollider3D.enabled;
        }

        #endregion

        #region Bug����

        public void ActivateBug()
        {
            if (isBugActive) return;

            isBugActive = true;
            ApplyBugEffect();

            Debug.Log($"����BugЧ��: {bugType} on {gameObject.name}");
        }

        public void DeactivateBug()
        {
            if (!isBugActive) return;

            isBugActive = false;
            RemoveBugEffect();

            Debug.Log($"ͣ��BugЧ��: {bugType} on {gameObject.name}");
        }

        public void ToggleBug()
        {
            if (isBugActive)
                DeactivateBug();
            else
                ActivateBug();
        }

        #endregion

        #region BugЧ��ʵ��

        private void ApplyBugEffect()
        {
            switch (bugType)
            {
                case BugType.ObjectMissing:
                    ApplyObjectMissingEffect();
                    break;

                case BugType.ObjectMoved:
                    ApplyObjectMovedEffect();
                    break;

                case BugType.MaterialMissing:
                    ApplyMaterialMissingEffect();
                    break;

                case BugType.ObjectFlickering:
                    ApplyFlickeringEffect();
                    break;

                case BugType.CollisionMissing:
                    ApplyCollisionMissingEffect();
                    break;

                case BugType.ClippingBug:
                    ApplyClippingBugEffect();
                    break;

                default:
                    Debug.LogWarning($"δʵ�ֵ�Bug����: {bugType}");
                    break;
            }
        }

        private void RemoveBugEffect()
        {
            switch (bugType)
            {
                case BugType.ObjectMissing:
                    RemoveObjectMissingEffect();
                    break;

                case BugType.ObjectMoved:
                    RemoveObjectMovedEffect();
                    break;

                case BugType.MaterialMissing:
                    RemoveMaterialMissingEffect();
                    break;

                case BugType.ObjectFlickering:
                    RemoveFlickeringEffect();
                    break;

                case BugType.CollisionMissing:
                    RemoveCollisionMissingEffect();
                    break;

                case BugType.ClippingBug:
                    RemoveClippingBugEffect();
                    break;
            }

            // �������ɵ���Ч
            if (spawnedEffect != null)
            {
                DestroyImmediate(spawnedEffect);
                spawnedEffect = null;
            }
        }

        #endregion

        #region ����BugЧ��ʵ��

        private void ApplyObjectMissingEffect()
        {
            gameObject.SetActive(false);
        }

        private void RemoveObjectMissingEffect()
        {
            gameObject.SetActive(true);
        }

        private void ApplyObjectMovedEffect()
        {
            transform.position = originalPosition + bugOffset;
        }

        private void RemoveObjectMovedEffect()
        {
            transform.position = originalPosition;
        }

        private void ApplyMaterialMissingEffect()
        {
            if (buggyMaterial != null)
            {
                if (objectRenderer != null)
                {
                    objectRenderer.material = buggyMaterial;
                }
            }
        }

        private void RemoveMaterialMissingEffect()
        {
            if (originalMaterial != null && objectRenderer != null)
            {
                objectRenderer.material = originalMaterial;
            }
        }

        private void ApplyFlickeringEffect()
        {
            if (flickerCoroutine != null)
                StopCoroutine(flickerCoroutine);

            flickerCoroutine = StartCoroutine(FlickerCoroutine());
        }

        private void RemoveFlickeringEffect()
        {
            if (flickerCoroutine != null)
            {
                StopCoroutine(flickerCoroutine);
                flickerCoroutine = null;
            }

            // �ָ�ԭʼ͸����
            RestoreOriginalColor();
        }

        private void ApplyCollisionMissingEffect()
        {
            // ������ײ��
            if (objectCollider2D != null)
                objectCollider2D.enabled = false;
            if (objectCollider3D != null)
                objectCollider3D.enabled = false;

            // ����͸������Ϊ�Ӿ���ʾ
            SetAlpha(collisionMissingAlpha);
        }

        private void RemoveCollisionMissingEffect()
        {
            // �ָ���ײ��
            if (objectCollider2D != null)
                objectCollider2D.enabled = originalCollider2DEnabled;
            if (objectCollider3D != null)
                objectCollider3D.enabled = originalCollider3DEnabled;

            // �ָ�ԭʼ͸����
            RestoreOriginalColor();
        }

        private void ApplyClippingBugEffect()
        {
            // �ö��󲿷�"����"�������������
            transform.position = originalPosition + new Vector3(0, -0.5f, 0);
        }

        private void RemoveClippingBugEffect()
        {
            transform.position = originalPosition;
        }

        #endregion

        #region ��������

        private IEnumerator FlickerCoroutine()
        {
            bool isVisible = true;

            while (isBugActive)
            {
                isVisible = !isVisible;
                float targetAlpha = isVisible ? flickerAlphaMax : flickerAlphaMin;
                SetAlpha(targetAlpha);

                yield return new WaitForSeconds(flickerInterval);
            }
        }

        private void SetAlpha(float alpha)
        {
            if (spriteRenderer != null)
            {
                Color color = spriteRenderer.color;
                color.a = alpha;
                spriteRenderer.color = color;
            }
            else if (objectRenderer != null)
            {
                Color color = objectRenderer.material.color;
                color.a = alpha;
                objectRenderer.material.color = color;
            }
        }

        private void RestoreOriginalColor()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
            else if (objectRenderer != null)
            {
                objectRenderer.material.color = originalColor;
            }
        }

        #endregion

        #region �����ӿ�

        public BugType GetBugType()
        {
            return bugType;
        }

        public bool IsBugActive()
        {
            return isBugActive;
        }

        public bool IsBeingFixed()
        {
            return isBeingFixed;
        }

        public GameObject GetCorrectObject()
        {
            return correctObject;
        }

        public void SetCorrectObject(GameObject obj)
        {
            correctObject = obj;
            InitializeCorrectObject();
        }

        public void SetBugType(BugType newBugType)
        {
            if (isBugActive)
            {
                DeactivateBug();
                bugType = newBugType;
                ActivateBug();
            }
            else
            {
                bugType = newBugType;
            }
        }

        // ����ʱ����Bug����
        public void SetFlickerInterval(float interval)
        {
            flickerInterval = interval;
        }

        public void SetBugOffset(Vector3 offset)
        {
            bugOffset = offset;
        }

        public void SetBuggyMaterial(Material material)
        {
            buggyMaterial = material;
        }

        #endregion

        #region ���Թ���

        [Header("����")]
        [SerializeField] private bool showDebugInfo = false;

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
            if (screenPos.z > 0 && screenPos.x > 0 && screenPos.x < Screen.width && screenPos.y > 0 && screenPos.y < Screen.height)
            {
                Vector2 guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y);

                GUI.Box(new Rect(guiPos.x - 50, guiPos.y - 30, 100, 60),
                    $"{gameObject.name}\n{bugType}\n{(isBugActive ? "ON" : "OFF")}");
            }
        }

        // Inspector��ť
        [ContextMenu("����Bug")]
        private void DebugActivateBug()
        {
            ActivateBug();
        }

        [ContextMenu("ͣ��Bug")]
        private void DebugDeactivateBug()
        {
            DeactivateBug();
        }

        [ContextMenu("�л�Bug")]
        private void DebugToggleBug()
        {
            ToggleBug();
        }

        #endregion
    }
}