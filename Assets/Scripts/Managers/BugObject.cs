using System;
using System.Collections;
using UnityEngine;

namespace BugFixerGame
{
    // Bug������ - ���������ҪBugЧ����GameObject��
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

        [Header("��˸Bug����")]
        [SerializeField] private float flickerInterval = 0.5f;
        [SerializeField] private float flickerAlphaMin = 0.3f;
        [SerializeField] private float flickerAlphaMax = 1f;

        [Header("��ײ��ʧBug����")]
        [SerializeField] private float collisionMissingAlpha = 0.8f;

        [Header("����Bug����")]
        [SerializeField] private Material buggyMaterial; // ��ɫ���ʵ�

        [Header("��˸Bug���Ժ��޸�")]
        [SerializeField] private bool debugFlickering = true;
        [SerializeField] private bool autoFixMaterialTransparency = true; // �Զ��޸�����͸��������

        // �������
        private Renderer objectRenderer;
        private SpriteRenderer spriteRenderer;
        private Collider2D objectCollider2D;
        private Collider objectCollider3D;

        // ԭʼ״̬����
        private Color originalColor;
        private Material originalMaterial;
        private bool originalCollider2DEnabled;
        private bool originalCollider3DEnabled;
        private bool originalCollider2DIsTrigger;
        private bool originalCollider3DIsTrigger;

        // ��ǰ״̬
        private bool isBugActive = false;
        private bool isBeingFixed = false;  // �Ƿ������޸���
        private Coroutine flickerCoroutine;
        private Coroutine popupCoroutine;

        // ��Ч����
        private GameObject spawnedEffect;

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
            // �Ѿ���Playerϵͳ��������⣬������Ҫ��Player����
            // HandleClickDetection(); // ע�͵�����
        }

        private void OnDestroy()
        {
            DeactivateBug();
        }

        #endregion

        #region Bug�޸����

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

            // ����Bug���壨����������������¼�������ɣ�
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

        #region ���������

        // ��Player���õĵ������������2���Ż���ã�
        public void OnClickedByPlayer()
        {
            // ֻ�е�Bug������û�������޸�ʱ�Ŵ�����
            if (!isBugActive || isBeingFixed) return;

            Debug.Log($"��ҳ���2����ɣ���ʼ�޸�Bug����: {gameObject.name}");

            // ��������¼�
            OnBugClicked?.Invoke(this);

            // ��ʼ�޸�Bug
            StartBugFix();
        }

        // �Ѿ���Playerϵͳ����ĵ�������룬������Ҫ��Player������

        #endregion

        #region ��ʼ��

        private void CacheComponents()
        {
            objectRenderer = GetComponent<Renderer>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            objectCollider2D = GetComponent<Collider2D>();
            objectCollider3D = GetComponent<Collider>();
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
            {
                originalCollider2DEnabled = objectCollider2D.enabled;
                originalCollider2DIsTrigger = objectCollider2D.isTrigger;
            }
            if (objectCollider3D != null)
            {
                originalCollider3DEnabled = objectCollider3D.enabled;
                originalCollider3DIsTrigger = objectCollider3D.isTrigger;
            }
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

                case BugType.MaterialMissing:
                    ApplyMaterialMissingEffect();
                    break;

                case BugType.ObjectFlickering:
                    ApplyFlickeringEffect();
                    break;

                case BugType.CollisionMissing:
                    ApplyCollisionMissingEffect();
                    break;

                // �Ѿ���ObjectMoved��ClippingBug����Ҫ�û�ֱ����Scene�аڷ�

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

                case BugType.MaterialMissing:
                    RemoveMaterialMissingEffect();
                    break;

                case BugType.ObjectFlickering:
                    RemoveFlickeringEffect();
                    break;

                case BugType.CollisionMissing:
                    RemoveCollisionMissingEffect();
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
            // ������ȫ͸����������GameObject����״̬
            SetAlpha(0f);

            // ����ײ����ΪTrigger���������Ա�������߼�⵽�����������������ײ
            if (objectCollider2D != null)
            {
                objectCollider2D.isTrigger = true;
            }
            if (objectCollider3D != null)
            {
                objectCollider3D.isTrigger = true;
            }

            Debug.Log($"ObjectMissingЧ����{gameObject.name} ��Ϊ͸��������ײ�����ɵ��");
        }

        private void RemoveObjectMissingEffect()
        {
            // �ָ�ԭʼ͸����
            RestoreOriginalColor();

            // �ָ���ײ��ԭʼ״̬
            if (objectCollider2D != null)
            {
                objectCollider2D.isTrigger = originalCollider2DIsTrigger;
            }
            if (objectCollider3D != null)
            {
                objectCollider3D.isTrigger = originalCollider3DIsTrigger;
            }
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
            if (debugFlickering)
                Debug.Log($"[��˸����] ��ʼӦ����˸Ч���� {gameObject.name}");

            // ��鲢���ò���͸����֧��
            if (autoFixMaterialTransparency)
            {
                EnsureMaterialSupportsTransparency();
            }

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

                if (debugFlickering)
                    Debug.Log($"[��˸����] ֹͣ��˸Ч��");
            }

            // �ָ�ԭʼ͸����
            RestoreOriginalColor();
        }

        private void ApplyCollisionMissingEffect()
        {
            // ����ײ����ΪTrigger���������Ա������û��������ײ
            if (objectCollider2D != null)
            {
                objectCollider2D.isTrigger = true;
            }
            if (objectCollider3D != null)
            {
                objectCollider3D.isTrigger = true;
            }

            // ����͸������Ϊ�Ӿ���ʾ
            SetAlpha(collisionMissingAlpha);

            Debug.Log($"CollisionMissingЧ����{gameObject.name} ��ײ��ΪTrigger���ɵ������������ײ");
        }

        private void RemoveCollisionMissingEffect()
        {
            // �ָ���ײ��ԭʼ״̬
            if (objectCollider2D != null)
            {
                objectCollider2D.isTrigger = originalCollider2DIsTrigger;
            }
            if (objectCollider3D != null)
            {
                objectCollider3D.isTrigger = originalCollider3DIsTrigger;
            }

            // �ָ�ԭʼ͸����
            RestoreOriginalColor();
        }

        #endregion

        #region ��ǿ����˸����

        private void EnsureMaterialSupportsTransparency()
        {
            if (objectRenderer != null)
            {
                Material mat = objectRenderer.material;

                if (debugFlickering)
                    Debug.Log($"[���ʼ��] ������: {mat.name}, Shader: {mat.shader.name}");

                // ����Ƿ���Standard shader
                if (mat.shader.name.Contains("Standard"))
                {
                    // ��ȡ��ǰ��Ⱦģʽ
                    float currentMode = mat.HasProperty("_Mode") ? mat.GetFloat("_Mode") : 0;

                    if (debugFlickering)
                        Debug.Log($"[���ʼ��] ��ǰ��Ⱦģʽ: {currentMode} (0=Opaque, 1=Cutout, 2=Fade, 3=Transparent)");

                    // �������͸��ģʽ������Ϊ͸��
                    if (currentMode < 2) // 0=Opaque, 1=Cutout����֧��͸����
                    {
                        SetMaterialToTransparent(mat);

                        if (debugFlickering)
                            Debug.Log($"[�����޸�] �Զ����ò��� {mat.name} Ϊ͸��ģʽ");
                    }
                }
                else if (mat.shader.name.Contains("Sprites"))
                {
                    // Sprite����ͨ����Ȼ֧��͸����
                    if (debugFlickering)
                        Debug.Log($"[���ʼ��] Sprite������֧��͸����");
                }
                else
                {
                    if (debugFlickering)
                        Debug.LogWarning($"[���ʼ��] δ֪Shader����: {mat.shader.name}��������Ҫ�ֶ�����͸����֧��");
                }
            }
            else if (spriteRenderer != null)
            {
                if (debugFlickering)
                    Debug.Log($"[���ʼ��] ʹ��SpriteRenderer����Ȼ֧��͸����");
            }
            else
            {
                if (debugFlickering)
                    Debug.LogError($"[���ʼ��] δ�ҵ�Renderer��SpriteRenderer�����");
            }
        }

        private void SetMaterialToTransparent(Material mat)
        {
            // ����Ϊ͸��ģʽ
            mat.SetFloat("_Mode", 3); // 3 = Transparent mode
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }

        private IEnumerator FlickerCoroutine()
        {
            bool isVisible = true;
            float flickerCount = 0;
            float startTime = Time.time;

            if (debugFlickering)
            {
                Debug.Log($"[��˸����] ��˸Э�̿�ʼ");
                Debug.Log($"- ���: {flickerInterval}��");
                Debug.Log($"- ͸���ȷ�Χ: {flickerAlphaMin} - {flickerAlphaMax}");
                Debug.Log($"- ��ʼ͸����: {GetCurrentAlpha()}");
            }

            while (isBugActive)
            {
                isVisible = !isVisible;
                float targetAlpha = isVisible ? flickerAlphaMax : flickerAlphaMin;

                // ����͸����
                bool success = SetAlpha(targetAlpha);

                flickerCount++;

                // ֻ��ǰ�������������Ϣ������ˢ��
                if (debugFlickering && flickerCount <= 5)
                {
                    Debug.Log($"[��˸����] ��{flickerCount}����˸ - Ŀ��:{targetAlpha}, ���óɹ�:{success}, ʵ��:{GetCurrentAlpha()}");
                }

                // ÿ10�����һ��״̬��Ϣ
                if (debugFlickering && (Time.time - startTime) > 10f && flickerCount % 20 == 0)
                {
                    Debug.Log($"[��˸״̬] ����{Time.time - startTime:F1}��, ��˸{flickerCount}��, ��ǰ͸����:{GetCurrentAlpha()}");
                }

                yield return new WaitForSeconds(flickerInterval);
            }

            if (debugFlickering)
                Debug.Log($"[��˸����] ��˸Э�̽��� - �ܹ���˸{flickerCount}�Σ�����{Time.time - startTime:F1}��");
        }

        #endregion

        #region ��������

        private bool SetAlpha(float alpha)
        {
            bool success = false;

            if (spriteRenderer != null)
            {
                Color color = spriteRenderer.color;
                color.a = alpha;
                spriteRenderer.color = color;
                success = true;

                if (debugFlickering && Time.frameCount % 120 == 0) // ÿ2�����һ�Σ�����60FPS��
                    Debug.Log($"[͸��������] SpriteRenderer����Ϊ {alpha}");
            }
            else if (objectRenderer != null)
            {
                // ע�⣺�޸�material.color�ᴴ������ʵ��
                Color color = objectRenderer.material.color;
                color.a = alpha;
                objectRenderer.material.color = color;
                success = true;

                if (debugFlickering && Time.frameCount % 120 == 0) // ÿ2�����һ��
                    Debug.Log($"[͸��������] Material����Ϊ {alpha}");
            }
            else
            {
                if (debugFlickering)
                    Debug.LogError($"[͸��������] δ�ҵ�������͸���ȵ�Renderer�����");
            }

            return success;
        }

        private float GetCurrentAlpha()
        {
            if (spriteRenderer != null)
                return spriteRenderer.color.a;
            else if (objectRenderer != null)
                return objectRenderer.material.color.a;

            return -1f; // ��ʾ�޷���ȡ
        }

        private void RestoreOriginalColor()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;

                if (debugFlickering)
                    Debug.Log($"[͸���Ȼָ�] SpriteRenderer�ָ��� {originalColor.a}");
            }
            else if (objectRenderer != null)
            {
                objectRenderer.material.color = originalColor;

                if (debugFlickering)
                    Debug.Log($"[͸���Ȼָ�] Material�ָ��� {originalColor.a}");
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

        public void SetFlickerInterval(float interval)
        {
            flickerInterval = interval;
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

        [ContextMenu("?? ������˸Ч��")]
        private void DebugTestFlickering()
        {
            if (Application.isPlaying)
            {
                debugFlickering = true;
                Debug.Log("=== ��ʼ��˸Ч������ ===");

                // ���浱ǰ����
                BugType originalBugType = bugType;
                bool originalBugActive = isBugActive;

                // ����Ϊ��˸���Ͳ�����
                SetBugType(BugType.ObjectFlickering);
                ActivateBug();

                // 5���ֹͣ����
                StartCoroutine(StopTestAfterDelay(5f, originalBugType, originalBugActive));
            }
            else
            {
                Debug.Log("��������ʱ������˸Ч��");
            }
        }

        private IEnumerator StopTestAfterDelay(float delay, BugType originalType, bool wasActive)
        {
            yield return new WaitForSeconds(delay);

            Debug.Log("=== ��˸Ч�����Խ��� ===");

            // �ָ�ԭʼ����
            DeactivateBug();
            SetBugType(originalType);

            if (wasActive)
                ActivateBug();
        }

        [ContextMenu("?? ����������")]
        private void DebugCheckMaterial()
        {
            Debug.Log("=== �������ü�� ===");

            if (objectRenderer != null)
            {
                Material mat = objectRenderer.sharedMaterial;
                Debug.Log($"��������: {mat.name}");
                Debug.Log($"Shader: {mat.shader.name}");

                if (mat.HasProperty("_Mode"))
                {
                    float mode = mat.GetFloat("_Mode");
                    string modeText = mode == 0 ? "Opaque(��͸��)" :
                                     mode == 1 ? "Cutout(�ü�)" :
                                     mode == 2 ? "Fade(����)" :
                                     mode == 3 ? "Transparent(͸��)" : "δ֪";
                    Debug.Log($"��Ⱦģʽ: {mode} ({modeText})");
                }

                Debug.Log($"��ǰ������ɫ: {mat.color}");
                Debug.Log($"��Ⱦ����: {mat.renderQueue}");

                // ����Ƿ�֧��͸����
                bool supportsTransparency = mat.renderQueue >= 3000 || mat.shader.name.Contains("Transparent") || mat.shader.name.Contains("Fade");
                Debug.Log($"�Ƿ�֧��͸����: {supportsTransparency}");

                if (!supportsTransparency)
                {
                    Debug.LogWarning("?? ���ʿ��ܲ�֧��͸���ȣ����齫Rendering Mode����ΪTransparent");
                }
            }
            else if (spriteRenderer != null)
            {
                Debug.Log($"ʹ��SpriteRenderer: {spriteRenderer.sprite?.name}");
                Debug.Log($"��ǰ��ɫ: {spriteRenderer.color}");
                Debug.Log("SpriteRenderer��Ȼ֧��͸���� ?");
            }
            else
            {
                Debug.LogError("? δ�ҵ�Renderer��SpriteRenderer�����");
                Debug.LogError("��ȷ��GameObject��MeshRenderer��SpriteRenderer������Renderer���");
            }

            Debug.Log("=== ������ ===");
        }

        [ContextMenu("?? �ֶ�����͸����")]
        private void DebugTestTransparency()
        {
            if (Application.isPlaying)
            {
                StartCoroutine(TestTransparencyAnimation());
            }
            else
            {
                Debug.Log("��������ʱ����͸����");
            }
        }

        private IEnumerator TestTransparencyAnimation()
        {
            Debug.Log("��ʼ͸���Ȳ��Զ���...");

            float duration = 3f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.PingPong(elapsed, 1f); // 0��1֮������

                SetAlpha(alpha);
                Debug.Log($"����͸����: {alpha:F2}");

                yield return new WaitForSeconds(0.1f);
            }

            // �ָ�ԭʼ͸����
            RestoreOriginalColor();
            Debug.Log("͸���Ȳ������");
        }

        #endregion
    }
}