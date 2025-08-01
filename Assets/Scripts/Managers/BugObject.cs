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

        [Header("URP��˸�޸�")]
        [SerializeField] private bool useURPCompatibility = true; // ����URP����ģʽ
        [SerializeField] private Material transparentMaterialPrefab; // Ԥ�Ƶ�͸�����ʣ���Inspector�з��䣩

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

        // URP������س���
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int SurfaceTypeProperty = Shader.PropertyToID("_Surface");
        private static readonly int BlendModeProperty = Shader.PropertyToID("_Blend");
        private static readonly int AlphaClipProperty = Shader.PropertyToID("_AlphaClip");
        private static readonly int SrcBlendProperty = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendProperty = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteProperty = Shader.PropertyToID("_ZWrite");

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

        #region URP���ݵ���˸����

        private void ApplyFlickeringEffect()
        {
            if (debugFlickering)
                Debug.Log($"[URP��˸] ��ʼӦ����˸Ч���� {gameObject.name}");

            // URP�����Դ���
            if (useURPCompatibility)
            {
                SetupURPTransparency();
            }
            else
            {
                // ��ͳ��ʽ��������URP�в�������
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
                    Debug.Log($"[URP��˸] ֹͣ��˸Ч��");
            }

            // �ָ�ԭʼ͸����
            RestoreOriginalColor();
        }

        private void SetupURPTransparency()
        {
            if (objectRenderer == null && spriteRenderer == null)
            {
                Debug.LogError("[URP��˸] δ�ҵ�Renderer�����");
                return;
            }

            // ����SpriteRenderer��ͨ������Ҫ���⴦��
            if (spriteRenderer != null)
            {
                if (debugFlickering)
                    Debug.Log("[URP��˸] ʹ��SpriteRenderer����֧��͸����");
                return;
            }

            // ����MeshRenderer��URP����
            if (objectRenderer != null)
            {
                Material currentMat = objectRenderer.material;

                if (debugFlickering)
                {
                    Debug.Log($"[URP��˸] ��ǰ����: {currentMat.name}");
                    Debug.Log($"[URP��˸] ��ǰShader: {currentMat.shader.name}");
                }

                // ����Ƿ���URP Shader
                if (IsURPShader(currentMat.shader))
                {
                    SetupURPMaterialTransparency(currentMat);
                }
                else
                {
                    // �������URP shader������ʹ��Ԥ��͸������
                    if (transparentMaterialPrefab != null)
                    {
                        if (debugFlickering)
                            Debug.Log("[URP��˸] ʹ��Ԥ��͸�������滻");

                        // ����ԭʼ����
                        if (originalMaterial == null)
                            originalMaterial = currentMat;

                        // Ӧ��͸������
                        objectRenderer.material = transparentMaterialPrefab;
                    }
                    else
                    {
                        Debug.LogWarning($"[URP��˸] ���� {currentMat.name} ����URP shader��û��Ԥ��͸�����ʣ�");
                        Debug.LogWarning("[URP��˸] ����Inspector�з���Transparent Material Prefab");

                        // ���Դ�ͳ��ʽ��Ϊ����
                        EnsureMaterialSupportsTransparency();
                    }
                }
            }
        }

        private bool IsURPShader(Shader shader)
        {
            string shaderName = shader.name.ToLower();
            return shaderName.Contains("universal render pipeline") ||
                   shaderName.Contains("urp") ||
                   shaderName.Contains("universal/lit") ||
                   shaderName.Contains("universal/simple lit") ||
                   shaderName.Contains("universal/unlit");
        }

        private void SetupURPMaterialTransparency(Material mat)
        {
            if (debugFlickering)
                Debug.Log("[URP��˸] ����URP����͸����֧��");

            try
            {
                // ����Surface TypeΪTransparent (1.0)
                if (mat.HasProperty(SurfaceTypeProperty))
                {
                    mat.SetFloat(SurfaceTypeProperty, 1.0f); // 1 = Transparent
                    if (debugFlickering)
                        Debug.Log("[URP��˸] Surface Type����ΪTransparent");
                }

                // ����Blend ModeΪAlpha (0)
                if (mat.HasProperty(BlendModeProperty))
                {
                    mat.SetFloat(BlendModeProperty, 0.0f); // 0 = Alpha
                }

                // ���û�ϲ���
                if (mat.HasProperty(SrcBlendProperty))
                    mat.SetFloat(SrcBlendProperty, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);

                if (mat.HasProperty(DstBlendProperty))
                    mat.SetFloat(DstBlendProperty, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

                if (mat.HasProperty(ZWriteProperty))
                    mat.SetFloat(ZWriteProperty, 0.0f); // �ر����д��

                // ����͸���ؼ���
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");

                // ������Ⱦ����
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                if (debugFlickering)
                    Debug.Log("[URP��˸] URP͸�����������");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[URP��˸] ����URP͸����ʱ����: {e.Message}");
            }
        }

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
            // ����Ϊ͸��ģʽ��Built-in RP Standard shader��
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
                Material mat = objectRenderer.material;

                // URPʹ��_BaseColor����
                if (mat.HasProperty(BaseColorProperty))
                {
                    Color color = mat.GetColor(BaseColorProperty);
                    color.a = alpha;
                    mat.SetColor(BaseColorProperty, color);
                    success = true;

                    if (debugFlickering && Time.frameCount % 120 == 0)
                        Debug.Log($"[͸��������] URP _BaseColor͸����: {alpha}");
                }
                // ���ã����Դ�ͳ��color����
                else if (mat.HasProperty("_Color"))
                {
                    Color color = mat.color;
                    color.a = alpha;
                    mat.color = color;
                    success = true;

                    if (debugFlickering && Time.frameCount % 120 == 0)
                        Debug.Log($"[͸��������] ��ͳColor͸����: {alpha}");
                }
                else
                {
                    if (debugFlickering)
                        Debug.LogWarning("[͸��������] ����û��_BaseColor��_Color���ԣ�");
                }
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
            {
                Material mat = objectRenderer.material;
                if (mat.HasProperty(BaseColorProperty))
                    return mat.GetColor(BaseColorProperty).a;
                else if (mat.HasProperty("_Color"))
                    return mat.color.a;
            }

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
                // ���ʹ����Ԥ��͸�����ʣ��ָ�ԭʼ����
                if (transparentMaterialPrefab != null && originalMaterial != null &&
                    objectRenderer.material == transparentMaterialPrefab)
                {
                    objectRenderer.material = originalMaterial;
                    if (debugFlickering)
                        Debug.Log($"[͸���Ȼָ�] �ѻָ�ԭʼ����: {originalMaterial.name}");
                }
                else
                {
                    // �ָ���ɫ
                    Material mat = objectRenderer.material;
                    if (mat.HasProperty(BaseColorProperty))
                    {
                        Color color = mat.GetColor(BaseColorProperty);
                        color.a = originalColor.a;
                        mat.SetColor(BaseColorProperty, color);
                    }
                    else if (mat.HasProperty("_Color"))
                    {
                        Color color = mat.color;
                        color.a = originalColor.a;
                        mat.color = color;
                    }

                    if (debugFlickering)
                        Debug.Log($"[͸���Ȼָ�] ������ɫ�ѻָ�");
                }
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

        [ContextMenu("?? ���URP��������")]
        private void DebugCheckURPMaterial()
        {
            Debug.Log("=== URP�������ü�� ===");

            if (objectRenderer != null)
            {
                Material mat = objectRenderer.sharedMaterial;
                Debug.Log($"��������: {mat.name}");
                Debug.Log($"Shader: {mat.shader.name}");
                Debug.Log($"�Ƿ�URP Shader: {IsURPShader(mat.shader)}");

                // ���URP��������
                if (mat.HasProperty(SurfaceTypeProperty))
                {
                    float surfaceType = mat.GetFloat(SurfaceTypeProperty);
                    string surfaceTypeText = surfaceType == 0 ? "Opaque(��͸��)" : "Transparent(͸��)";
                    Debug.Log($"Surface Type: {surfaceType} ({surfaceTypeText})");
                }
                else
                {
                    Debug.LogWarning("����û��_Surface���ԣ����ܲ���URP���ʣ�");
                }

                if (mat.HasProperty(BaseColorProperty))
                {
                    Color baseColor = mat.GetColor(BaseColorProperty);
                    Debug.Log($"Base Color: {baseColor}");
                }
                else if (mat.HasProperty("_Color"))
                {
                    Debug.Log($"Color: {mat.color}");
                }
                else
                {
                    Debug.LogWarning("����û����ɫ���ԣ�");
                }

                Debug.Log($"��Ⱦ����: {mat.renderQueue}");

                // ���ؼ���
                if (mat.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"))
                    Debug.Log("? _SURFACE_TYPE_TRANSPARENT �ؼ���������");
                else
                    Debug.Log("? _SURFACE_TYPE_TRANSPARENT �ؼ���δ����");

            }
            else if (spriteRenderer != null)
            {
                Debug.Log($"ʹ��SpriteRenderer: {spriteRenderer.sprite?.name}");
                Debug.Log($"��ǰ��ɫ: {spriteRenderer.color}");
                Debug.Log("? SpriteRenderer��Ȼ֧��͸����");
            }
            else
            {
                Debug.LogError("? δ�ҵ�Renderer�����");
            }

            // ���Ԥ��͸������
            if (transparentMaterialPrefab != null)
            {
                Debug.Log($"? Ԥ��͸������: {transparentMaterialPrefab.name}");
                Debug.Log($"Ԥ�Ʋ���Shader: {transparentMaterialPrefab.shader.name}");
            }
            else
            {
                Debug.LogWarning("?? û������Ԥ��͸������");
            }

            Debug.Log("=== ������ ===");
        }

        [ContextMenu("?? �Զ��޸�URP͸����")]
        private void DebugFixURPTransparency()
        {
            if (Application.isPlaying)
            {
                Debug.Log("��ʼURP͸�����Զ��޸�...");
                useURPCompatibility = true;
                SetupURPTransparency();
                Debug.Log("URP͸�����޸����");
            }
            else
            {
                Debug.Log("��������ʱִ��URP͸�����޸�");
            }
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