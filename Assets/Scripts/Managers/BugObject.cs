using System;
using System.Collections;
using UnityEngine;

namespace BugFixerGame
{
    // Bug对象类 - 负责管理需要Bug效果的GameObject上
    public class BugObject : MonoBehaviour
    {
        // 用于判断当前对象是否为Bug
public bool IsBug => bugType != BugType.None;


        [Header("Bug配置")]
        [SerializeField] private BugType bugType = BugType.None;
        [SerializeField] private bool startWithBugActive = false;

        [Header("正确物体配置")]
        [SerializeField] private GameObject correctObject;          // 正确的物体
        [Tooltip("修复Bug后显示的正确物体")]
        public GameObject CorrectObject
        {
            get { return correctObject; }
            set { correctObject = value; InitializeCorrectObject(); }
        }
        [SerializeField] private float popupAnimationTime = 0.5f;  // 弹出动画时间
        [SerializeField]
        private AnimationCurve popupCurve = new AnimationCurve(
            new Keyframe(0, 0, 0, 0),
            new Keyframe(0.6f, 1.1f, 2, 2),
            new Keyframe(1, 1, 0, 0)
        ); // 弹出动画曲线

        [Header("闪烁Bug设置")]
        [SerializeField] private float flickerInterval = 0.5f;
        [SerializeField] private float flickerAlphaMin = 0.3f;
        [SerializeField] private float flickerAlphaMax = 1f;

        [Header("碰撞缺失Bug设置")]
        [SerializeField] private float collisionMissingAlpha = 0.8f;

        [Header("材质Bug设置")]
        [SerializeField] private Material buggyMaterial; // 错误材质（如紫色材质）
        [SerializeField] private bool hideMaterialCompletely = false; // 是否完全隐藏材质

        [Header("错误物体Bug设置")]
        [SerializeField] private GameObject wrongObject; // 错误的替代物体

        [Header("缺失物体Bug设置")]
        [SerializeField] private bool hideObjectCompletely = false; // 是否完全隐藏物体
        [SerializeField] private float missingObjectAlpha = 0f; // 缺失物体的透明度

        [Header("震动Bug设置")]
        [SerializeField] private float shakeIntensity = 0.1f; // 震动强度
        [SerializeField] private float shakeSpeed = 10f; // 震动频率
        [SerializeField] private Vector3 shakeDirection = Vector3.one; // 震动方向

        [Header("位移/穿模Bug设置")]
        [SerializeField] private Vector3 wrongPosition = Vector3.zero; // 错误位置偏移
        [SerializeField] private Vector3 wrongRotation = Vector3.zero; // 错误旋转
        [SerializeField] private Vector3 wrongScale = Vector3.one; // 错误缩放
        [SerializeField] private bool enableClippingMode = false; // 启用穿模模式
        [SerializeField] private float clippingOffset = -0.5f; // 穿模偏移量

        [Header("URP闪烁修复")]
        [SerializeField] private bool useURPCompatibility = true; // 启用URP兼容模式
        [SerializeField] private Material transparentMaterialPrefab; // 预制的透明材质

        [Header("调试设置")]
        [SerializeField] private bool debugFlickering = true;
        [SerializeField] private bool autoFixMaterialTransparency = true;
        [SerializeField] private bool showDebugInfo = false;

        // 组件缓存
        private Renderer objectRenderer;
        private SpriteRenderer spriteRenderer;
        private Collider2D objectCollider2D;
        private Collider objectCollider3D;

        // 原始状态保存
        private Color originalColor;
        private Material originalMaterial;
        private bool originalCollider2DEnabled;
        private bool originalCollider3DEnabled;
        private bool originalCollider2DIsTrigger;
        private bool originalCollider3DIsTrigger;
        private Vector3 originalPosition;
        private Vector3 originalRotation;
        private Vector3 originalScale;

        // 当前状态
        private bool isBugActive = false;
        private bool isBeingFixed = false;
        private Coroutine flickerCoroutine;
        private Coroutine shakeCoroutine;
        private Coroutine popupCoroutine;

        // 显示相关
        private GameObject spawnedWrongObject;
        private GameObject spawnedEffect;

        // URP材质相关常量
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int SurfaceTypeProperty = Shader.PropertyToID("_Surface");
        private static readonly int BlendModeProperty = Shader.PropertyToID("_Blend");
        private static readonly int SrcBlendProperty = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendProperty = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteProperty = Shader.PropertyToID("_ZWrite");

        // 事件
        public static event Action<BugObject> OnBugClicked;
        public static event Action<BugObject> OnBugFixed;

        #region Unity生命周期

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
            // 已经由Player系统管理点击检测
        }

        private void OnDestroy()
        {
            DeactivateBug();
        }

        #endregion

        #region Bug修复相关

        private void StartBugFix()
        {
            if (isBeingFixed) return;

            isBeingFixed = true;
            DeactivateBug();

            if (correctObject != null)
            {
                ShowCorrectObject();
            }
            else
            {
                CompleteBugFix();
            }
        }

        private void ShowCorrectObject()
        {
            correctObject.SetActive(true);

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
            yield return new WaitForSeconds(0.5f);
            CompleteBugFix();
        }

        private void CompleteBugFix()
        {
            Debug.Log($"Bug修复完成: {gameObject.name}");
            OnBugFixed?.Invoke(this);
            StartCoroutine(DestroyBugObjectDelayed());
        }

        private IEnumerator DestroyBugObjectDelayed()
        {
            yield return new WaitForEndOfFrame();
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        #endregion

        #region 点击检测相关

        public void OnClickedByPlayer()
        {
            if (!isBugActive || isBeingFixed) return;

            Debug.Log($"玩家长按完成，开始修复Bug: {gameObject.name} ({bugType})");
            OnBugClicked?.Invoke(this);
            StartBugFix();
        }

        #endregion

        #region 初始化

        private void CacheComponents()
        {
            objectRenderer = GetComponent<Renderer>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            objectCollider2D = GetComponent<Collider2D>();
            objectCollider3D = GetComponent<Collider>();
        }

        private void InitializeCorrectObject()
        {
            if (correctObject != null)
            {
                correctObject.SetActive(false);
            }
        }

        private void SaveOriginalState()
        {
            // 保存颜色和材质
            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
            }
            else if (objectRenderer != null)
            {
                originalColor = objectRenderer.material.color;
                originalMaterial = objectRenderer.material;
            }

            // 保存碰撞体状态
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

            // 保存变换状态
            originalPosition = transform.localPosition;
            originalRotation = transform.localEulerAngles;
            originalScale = transform.localScale;
        }

        #endregion

        #region Bug控制

        public void ActivateBug()
        {
            if (isBugActive) return;

            isBugActive = true;
            ApplyBugEffect();
            Debug.Log($"激活Bug效果: {bugType} on {gameObject.name}");
        }

        public void DeactivateBug()
        {
            if (!isBugActive) return;

            isBugActive = false;
            RemoveBugEffect();
            Debug.Log($"停用Bug效果: {bugType} on {gameObject.name}");
        }

        public void ToggleBug()
        {
            if (isBugActive)
                DeactivateBug();
            else
                ActivateBug();
        }

        #endregion

        #region Bug效果实现

        private void ApplyBugEffect()
        {
            switch (bugType)
            {
                case BugType.ObjectFlickering:
                    ApplyFlickeringEffect();
                    break;

                case BugType.CollisionMissing:
                    ApplyCollisionMissingEffect();
                    break;

                case BugType.WrongOrMissingMaterial:
                    ApplyWrongOrMissingMaterialEffect();
                    break;

                case BugType.WrongObject:
                    ApplyWrongObjectEffect();
                    break;

                case BugType.MissingObject:
                    ApplyMissingObjectEffect();
                    break;

                case BugType.ObjectShaking:
                    ApplyObjectShakingEffect();
                    break;

                case BugType.ObjectMovedOrClipping:
                    ApplyObjectMovedOrClippingEffect();
                    break;

                default:
                    Debug.LogWarning($"未实现的Bug类型: {bugType}");
                    break;
            }
        }

        private void RemoveBugEffect()
        {
            switch (bugType)
            {
                case BugType.ObjectFlickering:
                    RemoveFlickeringEffect();
                    break;

                case BugType.CollisionMissing:
                    RemoveCollisionMissingEffect();
                    break;

                case BugType.WrongOrMissingMaterial:
                    RemoveWrongOrMissingMaterialEffect();
                    break;

                case BugType.WrongObject:
                    RemoveWrongObjectEffect();
                    break;

                case BugType.MissingObject:
                    RemoveMissingObjectEffect();
                    break;

                case BugType.ObjectShaking:
                    RemoveObjectShakingEffect();
                    break;

                case BugType.ObjectMovedOrClipping:
                    RemoveObjectMovedOrClippingEffect();
                    break;
            }

            // 清理生成的对象和特效
            if (spawnedWrongObject != null)
            {
                DestroyImmediate(spawnedWrongObject);
                spawnedWrongObject = null;
            }
            if (spawnedEffect != null)
            {
                DestroyImmediate(spawnedEffect);
                spawnedEffect = null;
            }
        }

        #endregion

        #region 具体Bug效果实现

        // 1. 物体闪烁
        private void ApplyFlickeringEffect()
        {
            if (debugFlickering)
                Debug.Log($"[闪烁] 开始应用闪烁效果到 {gameObject.name}");

            if (useURPCompatibility)
            {
                SetupURPTransparency();
            }
            else
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
            }
            RestoreOriginalColor();
        }

        // 2. 碰撞缺失
        private void ApplyCollisionMissingEffect()
        {
            if (objectCollider2D != null)
            {
                objectCollider2D.isTrigger = true;
            }
            if (objectCollider3D != null)
            {
                objectCollider3D.isTrigger = true;
            }

            SetAlpha(collisionMissingAlpha);
            Debug.Log($"CollisionMissing效果：{gameObject.name} 碰撞设为Trigger");
        }

        private void RemoveCollisionMissingEffect()
        {
            if (objectCollider2D != null)
            {
                objectCollider2D.isTrigger = originalCollider2DIsTrigger;
            }
            if (objectCollider3D != null)
            {
                objectCollider3D.isTrigger = originalCollider3DIsTrigger;
            }
            RestoreOriginalColor();
        }

        // 3. 错误或缺失材质
        private void ApplyWrongOrMissingMaterialEffect()
        {
            if (hideMaterialCompletely)
            {
                // 完全隐藏材质（设为透明）
                SetAlpha(0f);
                Debug.Log($"MaterialMissing效果：{gameObject.name} 材质完全隐藏");
            }
            else if (buggyMaterial != null)
            {
                // 应用错误材质
                if (objectRenderer != null)
                {
                    objectRenderer.material = buggyMaterial;
                }
                Debug.Log($"WrongMaterial效果：{gameObject.name} 应用错误材质");
            }
            else
            {
                // 默认：设置为半透明提示缺失
                SetAlpha(0.3f);
                Debug.Log($"MaterialMissing效果：{gameObject.name} 材质半透明");
            }
        }

        private void RemoveWrongOrMissingMaterialEffect()
        {
            if (originalMaterial != null && objectRenderer != null)
            {
                objectRenderer.material = originalMaterial;
            }
            RestoreOriginalColor();
        }

        // 4. 错误物体
        private void ApplyWrongObjectEffect()
        {
            if (wrongObject != null)
            {
                // 显示错误物体
                spawnedWrongObject = Instantiate(wrongObject, transform.position, transform.rotation, transform.parent);
                // 隐藏原物体
                SetAlpha(0f);
                SetCollidersAsTrigger(true);
                Debug.Log($"WrongObject效果：{gameObject.name} 显示错误物体 {wrongObject.name}");
            }
            else
            {
                Debug.LogWarning($"WrongObject效果：{gameObject.name} 没有设置错误物体！");
                // 如果没有设置错误物体，默认隐藏原物体
                SetAlpha(0.1f);
                SetCollidersAsTrigger(true);
            }
        }

        private void RemoveWrongObjectEffect()
        {
            if (spawnedWrongObject != null)
            {
                DestroyImmediate(spawnedWrongObject);
                spawnedWrongObject = null;
            }

            RestoreOriginalColor();
            SetCollidersAsTrigger(false);
        }

        // 5. 缺失物体
        private void ApplyMissingObjectEffect()
        {
            if (hideObjectCompletely)
            {
                // 完全隐藏物体
                SetAlpha(0f);
                SetCollidersAsTrigger(true);
                Debug.Log($"MissingObject效果：{gameObject.name} 物体完全隐藏");
            }
            else
            {
                // 使用自定义透明度
                SetAlpha(missingObjectAlpha);
                SetCollidersAsTrigger(true);
                Debug.Log($"MissingObject效果：{gameObject.name} 物体透明度设为 {missingObjectAlpha}");
            }
        }

        private void RemoveMissingObjectEffect()
        {
            RestoreOriginalColor();
            SetCollidersAsTrigger(false);
        }

        // 6. 物体震动
        private void ApplyObjectShakingEffect()
        {
            if (shakeCoroutine != null)
                StopCoroutine(shakeCoroutine);

            shakeCoroutine = StartCoroutine(ShakeCoroutine());
            Debug.Log($"ObjectShaking效果：{gameObject.name} 开始震动");
        }

        private void RemoveObjectShakingEffect()
        {
            if (shakeCoroutine != null)
            {
                StopCoroutine(shakeCoroutine);
                shakeCoroutine = null;
            }

            // 恢复原始位置
            transform.localPosition = originalPosition;
        }

        // 7. 物体位移或穿模
        private void ApplyObjectMovedOrClippingEffect()
        {
            if (enableClippingMode)
            {
                // 穿模效果：向下移动一定距离
                Vector3 clippingPosition = originalPosition + Vector3.down * clippingOffset;
                transform.localPosition = clippingPosition;
                Debug.Log($"Clipping效果：{gameObject.name} 穿模到 {clippingPosition}");
            }
            else
            {
                // 位移效果：应用错误的变换
                if (wrongPosition != Vector3.zero)
                    transform.localPosition = originalPosition + wrongPosition;

                if (wrongRotation != Vector3.zero)
                    transform.localEulerAngles = originalRotation + wrongRotation;

                if (wrongScale != Vector3.one)
                    transform.localScale = Vector3.Scale(originalScale, wrongScale);

                Debug.Log($"ObjectMoved效果：{gameObject.name} 位置/旋转/缩放改变");
            }
        }

        private void RemoveObjectMovedOrClippingEffect()
        {
            // 恢复原始变换
            transform.localPosition = originalPosition;
            transform.localEulerAngles = originalRotation;
            transform.localScale = originalScale;
        }

        #endregion

        #region 震动协程

        private IEnumerator ShakeCoroutine()
        {
            while (isBugActive)
            {
                Vector3 randomOffset = new Vector3(
                    UnityEngine.Random.Range(-shakeIntensity, shakeIntensity) * shakeDirection.x,
                    UnityEngine.Random.Range(-shakeIntensity, shakeIntensity) * shakeDirection.y,
                    UnityEngine.Random.Range(-shakeIntensity, shakeIntensity) * shakeDirection.z
                );

                transform.localPosition = originalPosition + randomOffset;
                yield return new WaitForSeconds(1f / shakeSpeed);
            }
        }

        #endregion

        #region 闪烁协程

        private IEnumerator FlickerCoroutine()
        {
            bool isVisible = true;
            float flickerCount = 0;

            if (debugFlickering)
                Debug.Log($"[闪烁] 协程开始 - 间隔:{flickerInterval}s");

            while (isBugActive)
            {
                isVisible = !isVisible;
                float targetAlpha = isVisible ? flickerAlphaMax : flickerAlphaMin;
                SetAlpha(targetAlpha);

                flickerCount++;
                if (debugFlickering && flickerCount <= 3)
                    Debug.Log($"[闪烁] 第{flickerCount}次 - 透明度:{targetAlpha}");

                yield return new WaitForSeconds(flickerInterval);
            }
        }

        #endregion

        #region URP兼容功能

        private void SetupURPTransparency()
        {
            if (objectRenderer == null && spriteRenderer == null)
            {
                Debug.LogError("[URP] 未找到Renderer组件！");
                return;
            }

            if (spriteRenderer != null)
            {
                if (debugFlickering)
                    Debug.Log("[URP] SpriteRenderer支持透明度");
                return;
            }

            if (objectRenderer != null)
            {
                Material currentMat = objectRenderer.material;

                if (IsURPShader(currentMat.shader))
                {
                    SetupURPMaterialTransparency(currentMat);
                }
                else if (transparentMaterialPrefab != null)
                {
                    if (originalMaterial == null)
                        originalMaterial = currentMat;
                    objectRenderer.material = transparentMaterialPrefab;
                    if (debugFlickering)
                        Debug.Log("[URP] 使用预制透明材质");
                }
                else
                {
                    EnsureMaterialSupportsTransparency();
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
            try
            {
                if (mat.HasProperty(SurfaceTypeProperty))
                    mat.SetFloat(SurfaceTypeProperty, 1.0f); // Transparent

                if (mat.HasProperty(BlendModeProperty))
                    mat.SetFloat(BlendModeProperty, 0.0f); // Alpha

                if (mat.HasProperty(SrcBlendProperty))
                    mat.SetFloat(SrcBlendProperty, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);

                if (mat.HasProperty(DstBlendProperty))
                    mat.SetFloat(DstBlendProperty, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

                if (mat.HasProperty(ZWriteProperty))
                    mat.SetFloat(ZWriteProperty, 0.0f);

                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                if (debugFlickering)
                    Debug.Log("[URP] 材质透明度设置完成");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[URP] 设置透明度出错: {e.Message}");
            }
        }

        private void EnsureMaterialSupportsTransparency()
        {
            if (objectRenderer != null)
            {
                Material mat = objectRenderer.material;

                if (mat.shader.name.Contains("Standard"))
                {
                    float currentMode = mat.HasProperty("_Mode") ? mat.GetFloat("_Mode") : 0;
                    if (currentMode < 2)
                    {
                        SetMaterialToTransparent(mat);
                    }
                }
            }
        }

        private void SetMaterialToTransparent(Material mat)
        {
            mat.SetFloat("_Mode", 3); // Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
        }

        #endregion

        #region 辅助方法

        private bool SetAlpha(float alpha)
        {
            bool success = false;

            if (spriteRenderer != null)
            {
                Color color = spriteRenderer.color;
                color.a = alpha;
                spriteRenderer.color = color;
                success = true;
            }
            else if (objectRenderer != null)
            {
                Material mat = objectRenderer.material;

                if (mat.HasProperty(BaseColorProperty))
                {
                    Color color = mat.GetColor(BaseColorProperty);
                    color.a = alpha;
                    mat.SetColor(BaseColorProperty, color);
                    success = true;
                }
                else if (mat.HasProperty("_Color"))
                {
                    Color color = mat.color;
                    color.a = alpha;
                    mat.color = color;
                    success = true;
                }
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
            return -1f;
        }

        private void RestoreOriginalColor()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
            else if (objectRenderer != null)
            {
                if (transparentMaterialPrefab != null && originalMaterial != null &&
                    objectRenderer.material == transparentMaterialPrefab)
                {
                    objectRenderer.material = originalMaterial;
                }
                else
                {
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
                }
            }
        }

        private void SetCollidersAsTrigger(bool asTrigger)
        {
            if (objectCollider2D != null)
            {
                objectCollider2D.isTrigger = asTrigger ? true : originalCollider2DIsTrigger;
            }
            if (objectCollider3D != null)
            {
                objectCollider3D.isTrigger = asTrigger ? true : originalCollider3DIsTrigger;
            }
        }

        #endregion

        #region 公共接口

        public BugType GetBugType() => bugType;
        public bool IsBugActive() => isBugActive;
        public bool IsBeingFixed() => isBeingFixed;
        public GameObject GetCorrectObject() => correctObject;

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

        public void SetBuggyMaterial(Material material) => buggyMaterial = material;
        public void SetWrongObject(GameObject obj) => wrongObject = obj;

        #endregion

        #region 调试功能

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
            if (screenPos.z > 0 && screenPos.x > 0 && screenPos.x < Screen.width && screenPos.y > 0 && screenPos.y < Screen.height)
            {
                Vector2 guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y);
                GUI.Box(new Rect(guiPos.x - 60, guiPos.y - 40, 120, 80),
                    $"{gameObject.name}\n{bugType}\n{(isBugActive ? "ON" : "OFF")}\nAlpha: {GetCurrentAlpha():F2}");
            }
        }

        [ContextMenu("✅ 激活Bug")]
        private void DebugActivateBug() => ActivateBug();

        [ContextMenu("❌ 停用Bug")]
        private void DebugDeactivateBug() => DeactivateBug();

        [ContextMenu("🔄 切换Bug")]
        private void DebugToggleBug() => ToggleBug();

        [ContextMenu("🔧 测试当前Bug效果")]
        private void DebugTestCurrentBug()
        {
            if (Application.isPlaying)
            {
                Debug.Log($"=== 测试Bug效果: {bugType} ===");
                BugType original = bugType;
                bool wasActive = isBugActive;

                ActivateBug();
                StartCoroutine(StopTestAfterDelay(5f, original, wasActive));
            }
            else
            {
                Debug.Log("请在运行时测试Bug效果");
            }
        }

        private IEnumerator StopTestAfterDelay(float delay, BugType originalType, bool wasActive)
        {
            yield return new WaitForSeconds(delay);
            Debug.Log($"=== Bug效果测试结束: {bugType} ===");

            DeactivateBug();
            bugType = originalType;
            if (wasActive) ActivateBug();
        }

        [ContextMenu("🔍 检查组件和设置")]
        private void DebugCheckComponents()
        {
            Debug.Log("=== BugObject组件检查 ===");
            Debug.Log($"Bug类型: {bugType}");
            Debug.Log($"激活状态: {isBugActive}");
            Debug.Log($"Renderer: {(objectRenderer ? objectRenderer.name : "无")}");
            Debug.Log($"SpriteRenderer: {(spriteRenderer ? spriteRenderer.name : "无")}");
            Debug.Log($"Collider2D: {(objectCollider2D ? objectCollider2D.name : "无")}");
            Debug.Log($"Collider3D: {(objectCollider3D ? objectCollider3D.name : "无")}");
            Debug.Log($"当前透明度: {GetCurrentAlpha()}");

            if (objectRenderer != null)
            {
                Debug.Log($"材质: {objectRenderer.material.name}");
                Debug.Log($"Shader: {objectRenderer.material.shader.name}");
                Debug.Log($"是否URP: {IsURPShader(objectRenderer.material.shader)}");
            }
        }

        #endregion
    }
}