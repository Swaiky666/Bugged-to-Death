using System;
using System.Collections;
using System.Collections.Generic;
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

        [Header("Bug信息显示")]
        [SerializeField, TextArea(3, 6)]
        private string bugDescription = "";
        [SerializeField]
        private string bugTitle = "";
        [SerializeField]
        private bool showInInfoPanel = true; // 是否在信息面板中显示

        [Header("多Renderer支持")]
        [SerializeField] private bool includeChildRenderers = true;  // 是否包含子物体的Renderer
        [SerializeField] private bool debugRendererSearch = true;    // 调试Renderer搜索过程

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
            new Keyframe(1, 1, 1, 0)
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
        [SerializeField] private bool showDebugInfo = false;

        // 修改：支持多个Renderer组件
        private List<Renderer> objectRenderers = new List<Renderer>();
        private List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();
        private List<Collider2D> objectColliders2D = new List<Collider2D>();
        private List<Collider> objectColliders3D = new List<Collider>();

        // 原始状态保存 - 修改为列表
        private List<Color> originalColors = new List<Color>();
        private List<Material> originalMaterials = new List<Material>();
        private List<bool> originalCollider2DEnabled = new List<bool>();
        private List<bool> originalCollider3DEnabled = new List<bool>();
        private List<bool> originalCollider2DIsTrigger = new List<bool>();
        private List<bool> originalCollider3DIsTrigger = new List<bool>();
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

            // 如果bug描述为空，使用bug类型作为默认描述
            if (string.IsNullOrEmpty(bugDescription) && bugType != BugType.None)
            {
                bugDescription = GameStatsHelper.GetBugTypeDisplayName(bugType);
            }

            // 如果bug标题为空，使用物体名称作为默认标题
            if (string.IsNullOrEmpty(bugTitle))
            {
                bugTitle = gameObject.name;
            }

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

        #region Bug信息接口

        /// <summary>
        /// 获取Bug描述信息
        /// </summary>
        public string GetBugDescription() => bugDescription;

        /// <summary>
        /// 获取Bug标题
        /// </summary>
        public string GetBugTitle() => bugTitle;

        /// <summary>
        /// 是否应该在信息面板中显示
        /// </summary>
        public bool ShouldShowInInfoPanel() => showInInfoPanel && IsBug;

        /// <summary>
        /// 设置Bug描述信息
        /// </summary>
        public void SetBugDescription(string description)
        {
            bugDescription = description;
        }

        /// <summary>
        /// 设置Bug标题
        /// </summary>
        public void SetBugTitle(string title)
        {
            bugTitle = title;
        }

        /// <summary>
        /// 设置是否在信息面板显示
        /// </summary>
        public void SetShowInInfoPanel(bool show)
        {
            showInInfoPanel = show;
        }

        /// <summary>
        /// 获取Bug的完整信息
        /// </summary>
        public BugInfo GetBugInfo()
        {
            return new BugInfo
            {
                bugObject = this,
                title = bugTitle,
                description = bugDescription,
                bugType = bugType,
                isActive = isBugActive,
                position = transform.position,
                objectName = gameObject.name
            };
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

        #region 初始化 - 修改为支持多Renderer

        private void CacheComponents()
        {
            if (debugRendererSearch)
                Debug.Log($"🔍 开始搜索 {gameObject.name} 的Renderer组件...");

            // 清空所有列表
            objectRenderers.Clear();
            spriteRenderers.Clear();
            objectColliders2D.Clear();
            objectColliders3D.Clear();

            // 获取当前物体的组件
            Renderer selfRenderer = GetComponent<Renderer>();
            SpriteRenderer selfSpriteRenderer = GetComponent<SpriteRenderer>();

            if (selfRenderer != null)
            {
                objectRenderers.Add(selfRenderer);
                if (debugRendererSearch)
                    Debug.Log($"  ✅ 找到自身Renderer: {selfRenderer.GetType().Name}");
            }

            if (selfSpriteRenderer != null)
            {
                spriteRenderers.Add(selfSpriteRenderer);
                if (debugRendererSearch)
                    Debug.Log($"  ✅ 找到自身SpriteRenderer: {selfSpriteRenderer.name}");
            }

            // 如果启用了子物体搜索，获取所有子物体的Renderer组件
            if (includeChildRenderers)
            {
                Renderer[] childRenderers = GetComponentsInChildren<Renderer>();
                SpriteRenderer[] childSpriteRenderers = GetComponentsInChildren<SpriteRenderer>();

                foreach (var renderer in childRenderers)
                {
                    if (renderer != selfRenderer && !objectRenderers.Contains(renderer))
                    {
                        objectRenderers.Add(renderer);
                        if (debugRendererSearch)
                            Debug.Log($"  ✅ 找到子物体Renderer: {renderer.name} ({renderer.GetType().Name})");
                    }
                }

                foreach (var spriteRenderer in childSpriteRenderers)
                {
                    if (spriteRenderer != selfSpriteRenderer && !spriteRenderers.Contains(spriteRenderer))
                    {
                        spriteRenderers.Add(spriteRenderer);
                        if (debugRendererSearch)
                            Debug.Log($"  ✅ 找到子物体SpriteRenderer: {spriteRenderer.name}");
                    }
                }
            }

            // 获取碰撞体组件（包括子物体）
            Collider2D[] colliders2D = includeChildRenderers ? GetComponentsInChildren<Collider2D>() : GetComponents<Collider2D>();
            Collider[] colliders3D = includeChildRenderers ? GetComponentsInChildren<Collider>() : GetComponents<Collider>();

            objectColliders2D.AddRange(colliders2D);
            objectColliders3D.AddRange(colliders3D);

            if (debugRendererSearch)
            {
                Debug.Log($"🔍 搜索完成：找到 {objectRenderers.Count} 个Renderer，{spriteRenderers.Count} 个SpriteRenderer，{objectColliders2D.Count} 个Collider2D，{objectColliders3D.Count} 个Collider3D");
            }
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
            // 清空原始状态列表
            originalColors.Clear();
            originalMaterials.Clear();
            originalCollider2DEnabled.Clear();
            originalCollider3DEnabled.Clear();
            originalCollider2DIsTrigger.Clear();
            originalCollider3DIsTrigger.Clear();

            // 保存所有SpriteRenderer的颜色
            foreach (var spriteRenderer in spriteRenderers)
            {
                if (spriteRenderer != null)
                {
                    originalColors.Add(spriteRenderer.color);
                }
            }

            // 保存所有Renderer的材质和颜色
            foreach (var renderer in objectRenderers)
            {
                if (renderer != null)
                {
                    originalMaterials.Add(renderer.material);

                    // 尝试获取材质颜色
                    Color materialColor = Color.white;
                    if (renderer.material.HasProperty(BaseColorProperty))
                    {
                        materialColor = renderer.material.GetColor(BaseColorProperty);
                    }
                    else if (renderer.material.HasProperty("_Color"))
                    {
                        materialColor = renderer.material.color;
                    }
                    originalColors.Add(materialColor);
                }
            }

            // 保存所有碰撞体状态
            foreach (var collider2D in objectColliders2D)
            {
                if (collider2D != null)
                {
                    originalCollider2DEnabled.Add(collider2D.enabled);
                    originalCollider2DIsTrigger.Add(collider2D.isTrigger);
                }
            }

            foreach (var collider3D in objectColliders3D)
            {
                if (collider3D != null)
                {
                    originalCollider3DEnabled.Add(collider3D.enabled);
                    originalCollider3DIsTrigger.Add(collider3D.isTrigger);
                }
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

        #region 具体Bug效果实现 - 修改为支持多Renderer

        // 1. 物体闪烁
        private void ApplyFlickeringEffect()
        {
            if (debugFlickering)
                Debug.Log($"[闪烁] 开始应用闪烁效果到 {gameObject.name}");

            // 检查是否有任何Renderer组件
            if (objectRenderers.Count == 0 && spriteRenderers.Count == 0)
            {
                if (debugFlickering)
                    Debug.LogWarning($"[闪烁] {gameObject.name} 及其子物体都没有Renderer组件，跳过闪烁效果");
                return;
            }

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
            RestoreOriginalColors();
        }

        // 2. 碰撞缺失
        private void ApplyCollisionMissingEffect()
        {
            // 设置所有碰撞体为Trigger
            foreach (var collider2D in objectColliders2D)
            {
                if (collider2D != null)
                {
                    collider2D.isTrigger = true;
                }
            }
            foreach (var collider3D in objectColliders3D)
            {
                if (collider3D != null)
                {
                    collider3D.isTrigger = true;
                }
            }

            SetAlphaForAllRenderers(collisionMissingAlpha);
            Debug.Log($"CollisionMissing效果：{gameObject.name} 及子物体碰撞设为Trigger");
        }

        private void RemoveCollisionMissingEffect()
        {
            // 恢复所有碰撞体状态
            for (int i = 0; i < objectColliders2D.Count && i < originalCollider2DIsTrigger.Count; i++)
            {
                if (objectColliders2D[i] != null)
                {
                    objectColliders2D[i].isTrigger = originalCollider2DIsTrigger[i];
                }
            }
            for (int i = 0; i < objectColliders3D.Count && i < originalCollider3DIsTrigger.Count; i++)
            {
                if (objectColliders3D[i] != null)
                {
                    objectColliders3D[i].isTrigger = originalCollider3DIsTrigger[i];
                }
            }
            RestoreOriginalColors();
        }

        // 3. 错误或缺失材质
        private void ApplyWrongOrMissingMaterialEffect()
        {
            if (hideMaterialCompletely)
            {
                // 完全隐藏材质（设为透明）
                SetAlphaForAllRenderers(0f);
                Debug.Log($"MaterialMissing效果：{gameObject.name} 及子物体材质完全隐藏");
            }
            else if (buggyMaterial != null)
            {
                // 应用错误材质到所有Renderer
                foreach (var renderer in objectRenderers)
                {
                    if (renderer != null)
                    {
                        renderer.material = buggyMaterial;
                    }
                }
                Debug.Log($"WrongMaterial效果：{gameObject.name} 及子物体应用错误材质");
            }
            else
            {
                // 默认：设置为半透明提示缺失
                SetAlphaForAllRenderers(0.3f);
                Debug.Log($"MaterialMissing效果：{gameObject.name} 及子物体材质半透明");
            }
        }

        private void RemoveWrongOrMissingMaterialEffect()
        {
            // 恢复所有原始材质
            for (int i = 0; i < objectRenderers.Count && i < originalMaterials.Count; i++)
            {
                if (objectRenderers[i] != null && originalMaterials[i] != null)
                {
                    objectRenderers[i].material = originalMaterials[i];
                }
            }
            RestoreOriginalColors();
        }

        // 4. 错误物体
        private void ApplyWrongObjectEffect()
        {
            if (wrongObject != null)
            {
                // 显示错误物体
                spawnedWrongObject = Instantiate(wrongObject, transform.position, transform.rotation, transform.parent);
                // 隐藏原物体
                SetAlphaForAllRenderers(0f);
                SetCollidersAsTrigger(true);
                Debug.Log($"WrongObject效果：{gameObject.name} 显示错误物体 {wrongObject.name}");
            }
            else
            {
                Debug.LogWarning($"WrongObject效果：{gameObject.name} 没有设置错误物体！");
                // 如果没有设置错误物体，默认隐藏原物体
                SetAlphaForAllRenderers(0.1f);
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

            RestoreOriginalColors();
            SetCollidersAsTrigger(false);
        }

        // 5. 缺失物体
        private void ApplyMissingObjectEffect()
        {
            if (hideObjectCompletely)
            {
                // 完全隐藏物体
                SetAlphaForAllRenderers(0f);
                SetCollidersAsTrigger(true);
                Debug.Log($"MissingObject效果：{gameObject.name} 及子物体完全隐藏");
            }
            else
            {
                // 使用自定义透明度
                SetAlphaForAllRenderers(missingObjectAlpha);
                SetCollidersAsTrigger(true);
                Debug.Log($"MissingObject效果：{gameObject.name} 及子物体透明度设为 {missingObjectAlpha}");
            }
        }

        private void RemoveMissingObjectEffect()
        {
            RestoreOriginalColors();
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
                SetAlphaForAllRenderers(targetAlpha);

                flickerCount++;
                if (debugFlickering && flickerCount <= 3)
                    Debug.Log($"[闪烁] 第{flickerCount}次 - 透明度:{targetAlpha}");

                yield return new WaitForSeconds(flickerInterval);
            }
        }

        #endregion

        #region URP兼容功能 - 修改为支持多Renderer

        private void SetupURPTransparency()
        {
            if (objectRenderers.Count == 0 && spriteRenderers.Count == 0)
            {
                if (debugFlickering)
                    Debug.LogWarning($"[URP] {gameObject.name} 没有找到任何Renderer组件，跳过URP透明度设置");
                return;
            }

            if (debugFlickering)
                Debug.Log($"[URP] 为 {objectRenderers.Count} 个Renderer和 {spriteRenderers.Count} 个SpriteRenderer设置透明度支持");

            // SpriteRenderer本身就支持透明度
            if (spriteRenderers.Count > 0 && debugFlickering)
            {
                Debug.Log("[URP] SpriteRenderer支持透明度");
            }

            // 处理所有Renderer组件
            foreach (var renderer in objectRenderers)
            {
                if (renderer != null)
                {
                    Material currentMat = renderer.material;

                    if (IsURPShader(currentMat.shader))
                    {
                        SetupURPMaterialTransparency(currentMat);
                    }
                    else if (transparentMaterialPrefab != null)
                    {
                        renderer.material = transparentMaterialPrefab;
                        if (debugFlickering)
                            Debug.Log($"[URP] {renderer.name} 使用预制透明材质");
                    }
                    else
                    {
                        EnsureMaterialSupportsTransparency(renderer);
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
                    Debug.Log($"[URP] 材质 {mat.name} 透明度设置完成");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[URP] 设置材质 {mat.name} 透明度出错: {e.Message}");
            }
        }

        private void EnsureMaterialSupportsTransparency()
        {
            foreach (var renderer in objectRenderers)
            {
                if (renderer != null)
                {
                    EnsureMaterialSupportsTransparency(renderer);
                }
            }
        }

        private void EnsureMaterialSupportsTransparency(Renderer renderer)
        {
            Material mat = renderer.material;

            if (mat.shader.name.Contains("Standard"))
            {
                float currentMode = mat.HasProperty("_Mode") ? mat.GetFloat("_Mode") : 0;
                if (currentMode < 2)
                {
                    SetMaterialToTransparent(mat);
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

        #region 辅助方法 - 修改为支持多Renderer

        /// <summary>
        /// 为所有Renderer设置透明度
        /// </summary>
        private bool SetAlphaForAllRenderers(float alpha)
        {
            bool success = false;

            // 设置所有SpriteRenderer的透明度
            foreach (var spriteRenderer in spriteRenderers)
            {
                if (spriteRenderer != null)
                {
                    Color color = spriteRenderer.color;
                    color.a = alpha;
                    spriteRenderer.color = color;
                    success = true;
                }
            }

            // 设置所有Renderer的透明度
            foreach (var renderer in objectRenderers)
            {
                if (renderer != null)
                {
                    Material mat = renderer.material;

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
            }

            return success;
        }

        /// <summary>
        /// 获取平均透明度（用于调试）
        /// </summary>
        private float GetAverageAlpha()
        {
            float totalAlpha = 0f;
            int count = 0;

            foreach (var spriteRenderer in spriteRenderers)
            {
                if (spriteRenderer != null)
                {
                    totalAlpha += spriteRenderer.color.a;
                    count++;
                }
            }

            foreach (var renderer in objectRenderers)
            {
                if (renderer != null)
                {
                    Material mat = renderer.material;
                    if (mat.HasProperty(BaseColorProperty))
                    {
                        totalAlpha += mat.GetColor(BaseColorProperty).a;
                        count++;
                    }
                    else if (mat.HasProperty("_Color"))
                    {
                        totalAlpha += mat.color.a;
                        count++;
                    }
                }
            }

            return count > 0 ? totalAlpha / count : -1f;
        }

        /// <summary>
        /// 恢复所有原始颜色
        /// </summary>
        private void RestoreOriginalColors()
        {
            // 恢复SpriteRenderer颜色
            for (int i = 0; i < spriteRenderers.Count && i < originalColors.Count; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    spriteRenderers[i].color = originalColors[i];
                }
            }

            // 恢复Renderer材质颜色
            int colorIndex = spriteRenderers.Count; // 从SpriteRenderer数量开始的索引
            for (int i = 0; i < objectRenderers.Count && colorIndex < originalColors.Count; i++, colorIndex++)
            {
                if (objectRenderers[i] != null)
                {
                    // 如果使用了预制的透明材质，先恢复原始材质
                    if (transparentMaterialPrefab != null && i < originalMaterials.Count &&
                        objectRenderers[i].material == transparentMaterialPrefab && originalMaterials[i] != null)
                    {
                        objectRenderers[i].material = originalMaterials[i];
                    }

                    Material mat = objectRenderers[i].material;
                    Color originalColor = originalColors[colorIndex];

                    if (mat.HasProperty(BaseColorProperty))
                    {
                        mat.SetColor(BaseColorProperty, originalColor);
                    }
                    else if (mat.HasProperty("_Color"))
                    {
                        mat.color = originalColor;
                    }
                }
            }
        }

        /// <summary>
        /// 设置碰撞体为Trigger状态
        /// </summary>
        private void SetCollidersAsTrigger(bool asTrigger)
        {
            for (int i = 0; i < objectColliders2D.Count && i < originalCollider2DIsTrigger.Count; i++)
            {
                if (objectColliders2D[i] != null)
                {
                    objectColliders2D[i].isTrigger = asTrigger ? true : originalCollider2DIsTrigger[i];
                }
            }
            for (int i = 0; i < objectColliders3D.Count && i < originalCollider3DIsTrigger.Count; i++)
            {
                if (objectColliders3D[i] != null)
                {
                    objectColliders3D[i].isTrigger = asTrigger ? true : originalCollider3DIsTrigger[i];
                }
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

        /// <summary>
        /// 获取Renderer组件统计信息
        /// </summary>
        public string GetRendererInfo()
        {
            return $"Renderer: {objectRenderers.Count}, SpriteRenderer: {spriteRenderers.Count}, " +
                   $"Collider2D: {objectColliders2D.Count}, Collider3D: {objectColliders3D.Count}";
        }

        /// <summary>
        /// 设置是否包含子物体Renderer
        /// </summary>
        public void SetIncludeChildRenderers(bool include)
        {
            includeChildRenderers = include;
            // 重新缓存组件
            CacheComponents();
            SaveOriginalState();
        }

        #endregion

        #region 调试功能

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
            if (screenPos.z > 0 && screenPos.x > 0 && screenPos.x < Screen.width && screenPos.y > 0 && screenPos.y < Screen.height)
            {
                Vector2 guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y);
                GUI.Box(new Rect(guiPos.x - 80, guiPos.y - 60, 160, 120),
                    $"{gameObject.name}\n{bugType}\n{(isBugActive ? "ON" : "OFF")}\n" +
                    $"R:{objectRenderers.Count} SR:{spriteRenderers.Count}\n" +
                    $"Avg Alpha: {GetAverageAlpha():F2}");
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
            Debug.Log($"Bug标题: {bugTitle}");
            Debug.Log($"Bug描述: {bugDescription}");
            Debug.Log($"显示在信息面板: {showInInfoPanel}");
            Debug.Log($"包含子物体Renderer: {includeChildRenderers}");
            Debug.Log($"组件统计: {GetRendererInfo()}");
            Debug.Log($"平均透明度: {GetAverageAlpha()}");

            Debug.Log("=== Renderer列表 ===");
            for (int i = 0; i < objectRenderers.Count; i++)
            {
                var renderer = objectRenderers[i];
                Debug.Log($"  {i + 1}. {renderer.name} ({renderer.GetType().Name}) - 材质: {renderer.material.name}");
            }

            Debug.Log("=== SpriteRenderer列表 ===");
            for (int i = 0; i < spriteRenderers.Count; i++)
            {
                var spriteRenderer = spriteRenderers[i];
                Debug.Log($"  {i + 1}. {spriteRenderer.name} - 颜色: {spriteRenderer.color}");
            }
        }

        [ContextMenu("🔄 重新扫描组件")]
        private void DebugRescanComponents()
        {
            if (Application.isPlaying)
            {
                Debug.Log("🔄 重新扫描组件...");
                CacheComponents();
                SaveOriginalState();
                Debug.Log($"✅ 扫描完成: {GetRendererInfo()}");
            }
        }

        [ContextMenu("🎨 测试透明度设置")]
        private void DebugTestAlpha()
        {
            if (Application.isPlaying)
            {
                Debug.Log("🎨 测试透明度设置...");
                float testAlpha = 0.5f;
                bool success = SetAlphaForAllRenderers(testAlpha);
                Debug.Log($"透明度设置结果: {(success ? "成功" : "失败")}, 平均透明度: {GetAverageAlpha():F2}");
            }
        }

        #endregion
    }

    /// <summary>
    /// Bug信息数据类
    /// </summary>
    [System.Serializable]
    public class BugInfo
    {
        public BugObject bugObject;
        public string title;
        public string description;
        public BugType bugType;
        public bool isActive;
        public Vector3 position;
        public string objectName;

        public string GetBugTypeDisplayName()
        {
            return GameStatsHelper.GetBugTypeDisplayName(bugType);
        }
    }
}