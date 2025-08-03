using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        [Header("多Renderer支持 - 增强版")]
        [SerializeField] private bool includeChildRenderers = true;  // 是否包含子物体的Renderer
        [SerializeField] private bool includeInactiveChildren = false; // 是否包含未激活的子物体
        [SerializeField] private int maxSearchDepth = -1; // 最大搜索深度，-1表示无限制
        [SerializeField] private bool debugRendererSearch = true;    // 调试Renderer搜索过程
        [SerializeField] private List<string> excludeByName = new List<string>(); // 按名称排除的物体
        [SerializeField] private List<string> excludeByTag = new List<string>(); // 按标签排除的物体

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
        [SerializeField] private float flickerAlphaMin = 0.1f;
        [SerializeField] private float flickerAlphaMax = 1f;
        [SerializeField] private bool flickerRandomInterval = false; // 随机闪烁间隔
        [SerializeField] private Vector2 flickerIntervalRange = new Vector2(0.2f, 0.8f);

        [Header("碰撞缺失Bug设置")]
        [SerializeField] private float collisionMissingAlpha = 0.8f;
        [SerializeField] private bool disableCollidersCompletely = false; // 完全禁用碰撞体

        [Header("材质Bug设置")]
        [SerializeField] private Material buggyMaterial; // 错误材质（如紫色材质）
        [SerializeField] private bool hideMaterialCompletely = false; // 是否完全隐藏材质
        [SerializeField] private List<Material> buggyMaterials = new List<Material>(); // 多个错误材质

        [Header("错误物体Bug设置")]
        [SerializeField] private GameObject wrongObject; // 错误的替代物体
        [SerializeField] private List<GameObject> wrongObjects = new List<GameObject>(); // 多个错误物体
        [SerializeField] private bool hideOriginalWhenShowingWrong = true; // 显示错误物体时隐藏原物体

        [Header("缺失物体Bug设置")]
        [SerializeField] private bool hideObjectCompletely = false; // 是否完全隐藏物体
        [SerializeField] private float missingObjectAlpha = 0f; // 缺失物体的透明度
        [SerializeField] private bool disableCollidersWhenMissing = true; // 缺失时禁用碰撞体

        [Header("震动Bug设置")]
        [SerializeField] private float shakeIntensity = 0.1f; // 震动强度
        [SerializeField] private float shakeSpeed = 10f; // 震动频率
        [SerializeField] private Vector3 shakeDirection = Vector3.one; // 震动方向
        [SerializeField] private bool randomShakeDirection = false; // 随机震动方向
        [SerializeField] private AnimationCurve shakeIntensityCurve = AnimationCurve.Constant(0, 1, 1); // 震动强度曲线

        [Header("位移/穿模Bug设置")]
        [SerializeField] private Vector3 wrongPosition = Vector3.zero; // 错误位置偏移
        [SerializeField] private Vector3 wrongRotation = Vector3.zero; // 错误旋转
        [SerializeField] private Vector3 wrongScale = Vector3.one; // 错误缩放
        [SerializeField] private bool enableClippingMode = false; // 启用穿模模式
        [SerializeField] private float clippingOffset = -0.5f; // 穿模偏移量
        [SerializeField] private bool useWorldSpace = false; // 使用世界空间坐标

        [Header("URP闪烁修复")]
        [SerializeField] private bool useURPCompatibility = true; // 启用URP兼容模式
        [SerializeField] private Material transparentMaterialPrefab; // 预制的透明材质

        [Header("高级设置")]
        [SerializeField] private bool preserveChildrenOrder = true; // 保持子物体顺序
        [SerializeField] private bool cacheComponentsOnStart = true; // 开始时缓存组件
        [SerializeField] private bool autoDetectShaderType = true; // 自动检测着色器类型

        [Header("调试设置")]
        [SerializeField] private bool debugFlickering = true;
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private bool logComponentDetails = false; // 记录组件详情

        // 渲染器组件存储 - 增强版
        [System.Serializable]
        public class RendererData
        {
            public Renderer renderer;
            public SpriteRenderer spriteRenderer;
            public string objectPath; // 物体路径
            public int depth; // 深度层级
            public Material originalMaterial;
            public Color originalColor;
            public bool wasActive; // 原始激活状态
        }

        [System.Serializable]
        public class ColliderData
        {
            public Collider collider3D;
            public Collider2D collider2D;
            public string objectPath;
            public bool originalEnabled;
            public bool originalIsTrigger;
        }

        // 组件数据存储
        private List<RendererData> allRendererData = new List<RendererData>();
        private List<ColliderData> allColliderData = new List<ColliderData>();

        // 原始状态保存
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
        private List<GameObject> spawnedWrongObjects = new List<GameObject>();
        private GameObject spawnedEffect;

        // 震动相关
        private float shakeStartTime;
        private Vector3 currentShakeOffset;

        // URP材质相关常量
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int SurfaceTypeProperty = Shader.PropertyToID("_Surface");
        private static readonly int BlendModeProperty = Shader.PropertyToID("_Blend");
        private static readonly int SrcBlendProperty = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendProperty = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteProperty = Shader.PropertyToID("_ZWrite");
        private static readonly int MainTexProperty = Shader.PropertyToID("_MainTex");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        // 事件
        public static event Action<BugObject> OnBugClicked;
        public static event Action<BugObject> OnBugFixed;

        #region Unity生命周期

        private void Awake()
        {
            if (cacheComponentsOnStart)
            {
                CacheAllComponents();
                SaveOriginalState();
            }

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

        private void Start()
        {
            // 确保在Start中也进行一次组件缓存，以防某些组件在Awake时还未初始化
            if (!cacheComponentsOnStart)
            {
                CacheAllComponents();
                SaveOriginalState();
            }
        }

        private void Update()
        {
            // 已经由Player系统管理点击检测
        }

        private void OnDestroy()
        {
            DeactivateBug();
            CleanupSpawnedObjects();
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

        #region 增强的组件缓存系统

        private void CacheAllComponents()
        {
            if (debugRendererSearch)
                Debug.Log($"🔍 开始深度搜索 {gameObject.name} 的所有组件...");

            // 清空现有数据
            allRendererData.Clear();
            allColliderData.Clear();

            // 搜索所有渲染器组件
            SearchRenderersRecursive(transform, "", 0);

            // 搜索所有碰撞体组件
            SearchCollidersRecursive(transform, "", 0);

            if (debugRendererSearch)
            {
                Debug.Log($"🔍 搜索完成：找到 {allRendererData.Count} 个渲染器，{allColliderData.Count} 个碰撞体");

                if (logComponentDetails)
                {
                    LogComponentDetails();
                }
            }
        }

        private void SearchRenderersRecursive(Transform current, string path, int depth)
        {
            // 检查深度限制
            if (maxSearchDepth >= 0 && depth > maxSearchDepth)
                return;

            // 检查是否应该包含未激活的物体
            if (!includeInactiveChildren && !current.gameObject.activeInHierarchy)
                return;

            // 检查排除列表
            if (ShouldExcludeObject(current.gameObject))
                return;

            string currentPath = string.IsNullOrEmpty(path) ? current.name : $"{path}/{current.name}";

            // 获取当前物体的Renderer组件
            Renderer renderer = current.GetComponent<Renderer>();
            SpriteRenderer spriteRenderer = current.GetComponent<SpriteRenderer>();

            if (renderer != null)
            {
                RendererData data = new RendererData
                {
                    renderer = renderer,
                    objectPath = currentPath,
                    depth = depth,
                    wasActive = current.gameObject.activeInHierarchy
                };

                // 保存原始材质和颜色
                SaveRendererOriginalState(data);
                allRendererData.Add(data);

                if (debugRendererSearch)
                    Debug.Log($"  ✅ 找到Renderer: {currentPath} ({renderer.GetType().Name}) - 深度:{depth}");
            }

            if (spriteRenderer != null)
            {
                RendererData data = new RendererData
                {
                    spriteRenderer = spriteRenderer,
                    objectPath = currentPath,
                    depth = depth,
                    wasActive = current.gameObject.activeInHierarchy
                };

                data.originalColor = spriteRenderer.color;
                allRendererData.Add(data);

                if (debugRendererSearch)
                    Debug.Log($"  ✅ 找到SpriteRenderer: {currentPath} - 深度:{depth}");
            }

            // 递归搜索子物体
            if (includeChildRenderers)
            {
                for (int i = 0; i < current.childCount; i++)
                {
                    SearchRenderersRecursive(current.GetChild(i), currentPath, depth + 1);
                }
            }
        }

        private void SearchCollidersRecursive(Transform current, string path, int depth)
        {
            // 检查深度限制
            if (maxSearchDepth >= 0 && depth > maxSearchDepth)
                return;

            // 检查是否应该包含未激活的物体
            if (!includeInactiveChildren && !current.gameObject.activeInHierarchy)
                return;

            // 检查排除列表
            if (ShouldExcludeObject(current.gameObject))
                return;

            string currentPath = string.IsNullOrEmpty(path) ? current.name : $"{path}/{current.name}";

            // 获取碰撞体组件
            Collider[] colliders3D = current.GetComponents<Collider>();
            Collider2D[] colliders2D = current.GetComponents<Collider2D>();

            foreach (var collider in colliders3D)
            {
                ColliderData data = new ColliderData
                {
                    collider3D = collider,
                    objectPath = currentPath,
                    originalEnabled = collider.enabled,
                    originalIsTrigger = collider.isTrigger
                };
                allColliderData.Add(data);
            }

            foreach (var collider in colliders2D)
            {
                ColliderData data = new ColliderData
                {
                    collider2D = collider,
                    objectPath = currentPath,
                    originalEnabled = collider.enabled,
                    originalIsTrigger = collider.isTrigger
                };
                allColliderData.Add(data);
            }

            // 递归搜索子物体
            if (includeChildRenderers)
            {
                for (int i = 0; i < current.childCount; i++)
                {
                    SearchCollidersRecursive(current.GetChild(i), currentPath, depth + 1);
                }
            }
        }

        private bool ShouldExcludeObject(GameObject obj)
        {
            // 按名称排除
            foreach (string name in excludeByName)
            {
                if (!string.IsNullOrEmpty(name) && obj.name.Contains(name))
                    return true;
            }

            // 按标签排除
            foreach (string tag in excludeByTag)
            {
                if (!string.IsNullOrEmpty(tag) && obj.CompareTag(tag))
                    return true;
            }

            return false;
        }

        private void SaveRendererOriginalState(RendererData data)
        {
            if (data.renderer != null)
            {
                data.originalMaterial = data.renderer.material;

                // 尝试获取材质颜色
                Material mat = data.renderer.material;
                if (mat.HasProperty(BaseColorProperty))
                {
                    data.originalColor = mat.GetColor(BaseColorProperty);
                }
                else if (mat.HasProperty(ColorProperty))
                {
                    data.originalColor = mat.GetColor(ColorProperty);
                }
                else
                {
                    data.originalColor = Color.white;
                }
            }
        }

        private void LogComponentDetails()
        {
            Debug.Log("=== 渲染器详情 ===");
            foreach (var data in allRendererData)
            {
                string type = data.renderer != null ? data.renderer.GetType().Name : "SpriteRenderer";
                string material = data.renderer != null ? data.renderer.material.name : "N/A";
                Debug.Log($"  {data.objectPath} - {type} - 材质:{material} - 深度:{data.depth}");
            }

            Debug.Log("=== 碰撞体详情 ===");
            foreach (var data in allColliderData)
            {
                string type = data.collider3D != null ? data.collider3D.GetType().Name : data.collider2D.GetType().Name;
                Debug.Log($"  {data.objectPath} - {type} - 启用:{data.originalEnabled} - Trigger:{data.originalIsTrigger}");
            }
        }

        #endregion

        #region 初始化

        private void InitializeCorrectObject()
        {
            if (correctObject != null)
            {
                correctObject.SetActive(false);
            }
        }

        private void SaveOriginalState()
        {
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

            CleanupSpawnedObjects();
        }

        private void CleanupSpawnedObjects()
        {
            // 清理生成的错误物体
            foreach (var obj in spawnedWrongObjects)
            {
                if (obj != null)
                    DestroyImmediate(obj);
            }
            spawnedWrongObjects.Clear();

            // 清理特效
            if (spawnedEffect != null)
            {
                DestroyImmediate(spawnedEffect);
                spawnedEffect = null;
            }
        }

        #endregion

        #region 具体Bug效果实现 - 增强版

        // 1. 物体闪烁 - 增强版
        private void ApplyFlickeringEffect()
        {
            if (debugFlickering)
                Debug.Log($"[闪烁] 开始应用闪烁效果到 {gameObject.name}");

            // 检查是否有任何渲染器组件
            if (allRendererData.Count == 0)
            {
                if (debugFlickering)
                    Debug.LogWarning($"[闪烁] {gameObject.name} 及其子物体都没有渲染器组件，跳过闪烁效果");
                return;
            }

            if (useURPCompatibility)
            {
                SetupURPTransparencyForAll();
            }
            else
            {
                EnsureAllMaterialsSupportTransparency();
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
            RestoreAllOriginalColors();
        }

        // 2. 碰撞缺失 - 增强版
        private void ApplyCollisionMissingEffect()
        {
            if (disableCollidersCompletely)
            {
                // 完全禁用所有碰撞体
                SetAllCollidersEnabled(false);
                Debug.Log($"CollisionMissing效果：{gameObject.name} 及子物体所有碰撞体已禁用");
            }
            else
            {
                // 设置所有碰撞体为Trigger
                SetAllCollidersAsTrigger(true);
                Debug.Log($"CollisionMissing效果：{gameObject.name} 及子物体碰撞设为Trigger");
            }

            SetAlphaForAllRenderers(collisionMissingAlpha);
        }

        private void RemoveCollisionMissingEffect()
        {
            // 恢复所有碰撞体状态
            RestoreAllCollidersState();
            RestoreAllOriginalColors();
        }

        // 3. 错误或缺失材质 - 增强版
        private void ApplyWrongOrMissingMaterialEffect()
        {
            if (hideMaterialCompletely)
            {
                // 完全隐藏材质（设为透明）
                SetAlphaForAllRenderers(0f);
                Debug.Log($"MaterialMissing效果：{gameObject.name} 及子物体材质完全隐藏");
            }
            else if (buggyMaterials.Count > 0 || buggyMaterial != null)
            {
                // 应用错误材质到所有Renderer
                ApplyBuggyMaterialsToAll();
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
            RestoreAllOriginalMaterials();
            RestoreAllOriginalColors();
        }

        // 4. 错误物体 - 增强版
        private void ApplyWrongObjectEffect()
        {
            List<GameObject> objectsToSpawn = GetWrongObjectsToSpawn();

            if (objectsToSpawn.Count > 0)
            {
                // 生成错误物体
                foreach (var wrongObj in objectsToSpawn)
                {
                    if (wrongObj != null)
                    {
                        Vector3 spawnPos = useWorldSpace ? transform.position : transform.localPosition;
                        Quaternion spawnRot = useWorldSpace ? transform.rotation : transform.localRotation;
                        Transform parent = useWorldSpace ? null : transform.parent;

                        GameObject spawned = Instantiate(wrongObj, spawnPos, spawnRot, parent);
                        spawnedWrongObjects.Add(spawned);
                    }
                }

                if (hideOriginalWhenShowingWrong)
                {
                    // 隐藏原物体
                    SetAlphaForAllRenderers(0f);
                    SetAllCollidersAsTrigger(true);
                }

                Debug.Log($"WrongObject效果：{gameObject.name} 显示 {spawnedWrongObjects.Count} 个错误物体");
            }
            else
            {
                Debug.LogWarning($"WrongObject效果：{gameObject.name} 没有设置错误物体！");
                // 如果没有设置错误物体，默认隐藏原物体
                SetAlphaForAllRenderers(0.1f);
                SetAllCollidersAsTrigger(true);
            }
        }

        private void RemoveWrongObjectEffect()
        {
            // 清理生成的错误物体
            CleanupSpawnedObjects();

            // 恢复原物体显示
            RestoreAllOriginalColors();
            RestoreAllCollidersState();
        }

        // 5. 缺失物体 - 增强版
        private void ApplyMissingObjectEffect()
        {
            if (hideObjectCompletely)
            {
                // 完全隐藏物体
                SetAlphaForAllRenderers(0f);
                if (disableCollidersWhenMissing)
                {
                    SetAllCollidersEnabled(false);
                }
                else
                {
                    SetAllCollidersAsTrigger(true);
                }
                Debug.Log($"MissingObject效果：{gameObject.name} 及子物体完全隐藏");
            }
            else
            {
                // 使用自定义透明度
                SetAlphaForAllRenderers(missingObjectAlpha);
                if (disableCollidersWhenMissing)
                {
                    SetAllCollidersEnabled(false);
                }
                else
                {
                    SetAllCollidersAsTrigger(true);
                }
                Debug.Log($"MissingObject效果：{gameObject.name} 及子物体透明度设为 {missingObjectAlpha}");
            }
        }

        private void RemoveMissingObjectEffect()
        {
            RestoreAllOriginalColors();
            RestoreAllCollidersState();
        }

        // 6. 物体震动 - 增强版
        private void ApplyObjectShakingEffect()
        {
            if (shakeCoroutine != null)
                StopCoroutine(shakeCoroutine);

            shakeStartTime = Time.time;
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
            if (useWorldSpace)
                transform.position = transform.parent != null ?
                    transform.parent.TransformPoint(originalPosition) : originalPosition;
            else
                transform.localPosition = originalPosition;

            currentShakeOffset = Vector3.zero;
        }

        // 7. 物体位移或穿模 - 增强版
        private void ApplyObjectMovedOrClippingEffect()
        {
            if (enableClippingMode)
            {
                // 穿模效果：向下移动一定距离
                Vector3 clippingPosition = originalPosition + Vector3.down * clippingOffset;

                if (useWorldSpace)
                {
                    transform.position = transform.parent != null ?
                        transform.parent.TransformPoint(clippingPosition) : clippingPosition;
                }
                else
                {
                    transform.localPosition = clippingPosition;
                }

                Debug.Log($"Clipping效果：{gameObject.name} 穿模到 {clippingPosition}");
            }
            else
            {
                // 位移效果：应用错误的变换
                if (wrongPosition != Vector3.zero)
                {
                    Vector3 newPos = originalPosition + wrongPosition;
                    if (useWorldSpace)
                    {
                        transform.position = transform.parent != null ?
                            transform.parent.TransformPoint(newPos) : newPos;
                    }
                    else
                    {
                        transform.localPosition = newPos;
                    }
                }

                if (wrongRotation != Vector3.zero)
                {
                    if (useWorldSpace)
                        transform.eulerAngles = originalRotation + wrongRotation;
                    else
                        transform.localEulerAngles = originalRotation + wrongRotation;
                }

                if (wrongScale != Vector3.one)
                    transform.localScale = Vector3.Scale(originalScale, wrongScale);

                Debug.Log($"ObjectMoved效果：{gameObject.name} 位置/旋转/缩放改变");
            }
        }

        private void RemoveObjectMovedOrClippingEffect()
        {
            // 恢复原始变换
            if (useWorldSpace)
            {
                if (transform.parent != null)
                {
                    transform.position = transform.parent.TransformPoint(originalPosition);
                    transform.eulerAngles = originalRotation;
                }
                else
                {
                    transform.position = originalPosition;
                    transform.eulerAngles = originalRotation;
                }
            }
            else
            {
                transform.localPosition = originalPosition;
                transform.localEulerAngles = originalRotation;
            }

            transform.localScale = originalScale;
        }

        #endregion

        #region 震动协程 - 增强版

        private IEnumerator ShakeCoroutine()
        {
            while (isBugActive)
            {
                float elapsedTime = Time.time - shakeStartTime;
                float intensityMultiplier = shakeIntensityCurve.Evaluate(elapsedTime);

                Vector3 shakeDir = randomShakeDirection ?
                    new Vector3(
                        UnityEngine.Random.Range(-1f, 1f),
                        UnityEngine.Random.Range(-1f, 1f),
                        UnityEngine.Random.Range(-1f, 1f)
                    ).normalized : shakeDirection.normalized;

                Vector3 randomOffset = new Vector3(
                    UnityEngine.Random.Range(-shakeIntensity, shakeIntensity) * shakeDir.x * intensityMultiplier,
                    UnityEngine.Random.Range(-shakeIntensity, shakeIntensity) * shakeDir.y * intensityMultiplier,
                    UnityEngine.Random.Range(-shakeIntensity, shakeIntensity) * shakeDir.z * intensityMultiplier
                );

                currentShakeOffset = randomOffset;

                if (useWorldSpace)
                {
                    Vector3 worldPos = transform.parent != null ?
                        transform.parent.TransformPoint(originalPosition) : originalPosition;
                    transform.position = worldPos + randomOffset;
                }
                else
                {
                    transform.localPosition = originalPosition + randomOffset;
                }

                yield return new WaitForSeconds(1f / shakeSpeed);
            }
        }

        #endregion

        #region 闪烁协程 - 增强版

        private IEnumerator FlickerCoroutine()
        {
            bool isVisible = true;
            float flickerCount = 0;

            if (debugFlickering)
                Debug.Log($"[闪烁] 协程开始 - 基础间隔:{flickerInterval}s");

            while (isBugActive)
            {
                isVisible = !isVisible;
                float targetAlpha = isVisible ? flickerAlphaMax : flickerAlphaMin;
                SetAlphaForAllRenderers(targetAlpha);

                flickerCount++;
                if (debugFlickering && flickerCount <= 3)
                    Debug.Log($"[闪烁] 第{flickerCount}次 - 透明度:{targetAlpha}");

                // 使用随机间隔或固定间隔
                float waitTime = flickerRandomInterval ?
                    UnityEngine.Random.Range(flickerIntervalRange.x, flickerIntervalRange.y) :
                    flickerInterval;

                yield return new WaitForSeconds(waitTime);
            }
        }

        #endregion

        #region URP兼容功能 - 增强版

        private void SetupURPTransparencyForAll()
        {
            if (allRendererData.Count == 0)
            {
                if (debugFlickering)
                    Debug.LogWarning($"[URP] {gameObject.name} 没有找到任何渲染器组件，跳过URP透明度设置");
                return;
            }

            if (debugFlickering)
                Debug.Log($"[URP] 为 {allRendererData.Count} 个渲染器设置透明度支持");

            foreach (var data in allRendererData)
            {
                if (data.spriteRenderer != null)
                {
                    // SpriteRenderer本身就支持透明度
                    continue;
                }

                if (data.renderer != null)
                {
                    Material currentMat = data.renderer.material;

                    if (autoDetectShaderType && IsURPShader(currentMat.shader))
                    {
                        SetupURPMaterialTransparency(currentMat);
                    }
                    else if (transparentMaterialPrefab != null)
                    {
                        data.renderer.material = transparentMaterialPrefab;
                        if (debugFlickering)
                            Debug.Log($"[URP] {data.objectPath} 使用预制透明材质");
                    }
                    else
                    {
                        EnsureMaterialSupportsTransparency(data.renderer);
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
                   shaderName.Contains("universal/unlit") ||
                   shaderName.Contains("universal/2d");
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

        private void EnsureAllMaterialsSupportTransparency()
        {
            foreach (var data in allRendererData)
            {
                if (data.renderer != null)
                {
                    EnsureMaterialSupportsTransparency(data.renderer);
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

        #region 辅助方法 - 增强版

        /// <summary>
        /// 为所有渲染器设置透明度
        /// </summary>
        private bool SetAlphaForAllRenderers(float alpha)
        {
            bool success = false;

            foreach (var data in allRendererData)
            {
                // 跳过未激活的物体（除非设置包含未激活物体）
                if (!includeInactiveChildren && !data.wasActive)
                    continue;

                if (data.spriteRenderer != null)
                {
                    Color color = data.spriteRenderer.color;
                    color.a = alpha;
                    data.spriteRenderer.color = color;
                    success = true;
                }
                else if (data.renderer != null)
                {
                    Material mat = data.renderer.material;

                    if (mat.HasProperty(BaseColorProperty))
                    {
                        Color color = mat.GetColor(BaseColorProperty);
                        color.a = alpha;
                        mat.SetColor(BaseColorProperty, color);
                        success = true;
                    }
                    else if (mat.HasProperty(ColorProperty))
                    {
                        Color color = mat.GetColor(ColorProperty);
                        color.a = alpha;
                        mat.SetColor(ColorProperty, color);
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

            foreach (var data in allRendererData)
            {
                if (data.spriteRenderer != null)
                {
                    totalAlpha += data.spriteRenderer.color.a;
                    count++;
                }
                else if (data.renderer != null)
                {
                    Material mat = data.renderer.material;
                    if (mat.HasProperty(BaseColorProperty))
                    {
                        totalAlpha += mat.GetColor(BaseColorProperty).a;
                        count++;
                    }
                    else if (mat.HasProperty(ColorProperty))
                    {
                        totalAlpha += mat.GetColor(ColorProperty).a;
                        count++;
                    }
                }
            }

            return count > 0 ? totalAlpha / count : -1f;
        }

        /// <summary>
        /// 恢复所有原始颜色
        /// </summary>
        private void RestoreAllOriginalColors()
        {
            foreach (var data in allRendererData)
            {
                if (data.spriteRenderer != null)
                {
                    data.spriteRenderer.color = data.originalColor;
                }
                else if (data.renderer != null)
                {
                    // 如果使用了预制的透明材质，先恢复原始材质
                    if (transparentMaterialPrefab != null &&
                        data.renderer.material == transparentMaterialPrefab &&
                        data.originalMaterial != null)
                    {
                        data.renderer.material = data.originalMaterial;
                    }

                    Material mat = data.renderer.material;
                    Color originalColor = data.originalColor;

                    if (mat.HasProperty(BaseColorProperty))
                    {
                        mat.SetColor(BaseColorProperty, originalColor);
                    }
                    else if (mat.HasProperty(ColorProperty))
                    {
                        mat.SetColor(ColorProperty, originalColor);
                    }
                }
            }
        }

        /// <summary>
        /// 恢复所有原始材质
        /// </summary>
        private void RestoreAllOriginalMaterials()
        {
            foreach (var data in allRendererData)
            {
                if (data.renderer != null && data.originalMaterial != null)
                {
                    data.renderer.material = data.originalMaterial;
                }
            }
        }

        /// <summary>
        /// 应用错误材质到所有渲染器
        /// </summary>
        private void ApplyBuggyMaterialsToAll()
        {
            List<Material> materialsToUse = new List<Material>();

            if (buggyMaterials.Count > 0)
            {
                materialsToUse.AddRange(buggyMaterials);
            }
            else if (buggyMaterial != null)
            {
                materialsToUse.Add(buggyMaterial);
            }

            if (materialsToUse.Count == 0)
                return;

            int materialIndex = 0;
            foreach (var data in allRendererData)
            {
                if (data.renderer != null)
                {
                    // 循环使用错误材质
                    Material matToApply = materialsToUse[materialIndex % materialsToUse.Count];
                    data.renderer.material = matToApply;
                    materialIndex++;
                }
            }
        }

        /// <summary>
        /// 获取要生成的错误物体列表
        /// </summary>
        private List<GameObject> GetWrongObjectsToSpawn()
        {
            List<GameObject> result = new List<GameObject>();

            if (wrongObjects.Count > 0)
            {
                result.AddRange(wrongObjects);
            }
            else if (wrongObject != null)
            {
                result.Add(wrongObject);
            }

            // 移除空引用
            result.RemoveAll(obj => obj == null);
            return result;
        }

        /// <summary>
        /// 设置所有碰撞体为Trigger状态
        /// </summary>
        private void SetAllCollidersAsTrigger(bool asTrigger)
        {
            foreach (var data in allColliderData)
            {
                if (data.collider2D != null)
                {
                    data.collider2D.isTrigger = asTrigger ? true : data.originalIsTrigger;
                }

                if (data.collider3D != null)
                {
                    data.collider3D.isTrigger = asTrigger ? true : data.originalIsTrigger;
                }
            }
        }

        /// <summary>
        /// 设置所有碰撞体启用状态
        /// </summary>
        private void SetAllCollidersEnabled(bool enabled)
        {
            foreach (var data in allColliderData)
            {
                if (data.collider2D != null)
                {
                    data.collider2D.enabled = enabled;
                }

                if (data.collider3D != null)
                {
                    data.collider3D.enabled = enabled;
                }
            }
        }

        /// <summary>
        /// 恢复所有碰撞体状态
        /// </summary>
        private void RestoreAllCollidersState()
        {
            foreach (var data in allColliderData)
            {
                if (data.collider2D != null)
                {
                    data.collider2D.enabled = data.originalEnabled;
                    data.collider2D.isTrigger = data.originalIsTrigger;
                }

                if (data.collider3D != null)
                {
                    data.collider3D.enabled = data.originalEnabled;
                    data.collider3D.isTrigger = data.originalIsTrigger;
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
        /// 获取渲染器组件统计信息
        /// </summary>
        public string GetRendererInfo()
        {
            int rendererCount = 0;
            int spriteRendererCount = 0;

            foreach (var data in allRendererData)
            {
                if (data.renderer != null) rendererCount++;
                if (data.spriteRenderer != null) spriteRendererCount++;
            }

            return $"Renderer: {rendererCount}, SpriteRenderer: {spriteRendererCount}, " +
                   $"Collider2D: {allColliderData.Count(d => d.collider2D != null)}, " +
                   $"Collider3D: {allColliderData.Count(d => d.collider3D != null)}";
        }

        /// <summary>
        /// 设置是否包含子物体渲染器
        /// </summary>
        public void SetIncludeChildRenderers(bool include)
        {
            includeChildRenderers = include;
            // 重新缓存组件
            CacheAllComponents();
            SaveOriginalState();
        }

        /// <summary>
        /// 添加排除的物体名称
        /// </summary>
        public void AddExcludeByName(string name)
        {
            if (!excludeByName.Contains(name))
            {
                excludeByName.Add(name);
                CacheAllComponents();
                SaveOriginalState();
            }
        }

        /// <summary>
        /// 添加排除的物体标签
        /// </summary>
        public void AddExcludeByTag(string tag)
        {
            if (!excludeByTag.Contains(tag))
            {
                excludeByTag.Add(tag);
                CacheAllComponents();
                SaveOriginalState();
            }
        }

        /// <summary>
        /// 设置最大搜索深度
        /// </summary>
        public void SetMaxSearchDepth(int depth)
        {
            maxSearchDepth = depth;
            CacheAllComponents();
            SaveOriginalState();
        }

        /// <summary>
        /// 强制重新缓存所有组件
        /// </summary>
        public void RefreshComponents()
        {
            CacheAllComponents();
            SaveOriginalState();
        }

        /// <summary>
        /// 获取所有渲染器路径信息
        /// </summary>
        public List<string> GetAllRendererPaths()
        {
            List<string> paths = new List<string>();
            foreach (var data in allRendererData)
            {
                string type = data.renderer != null ? "Renderer" : "SpriteRenderer";
                paths.Add($"{data.objectPath} ({type}) - 深度:{data.depth}");
            }
            return paths;
        }

        /// <summary>
        /// 获取所有碰撞体路径信息
        /// </summary>
        public List<string> GetAllColliderPaths()
        {
            List<string> paths = new List<string>();
            foreach (var data in allColliderData)
            {
                string type = data.collider3D != null ? data.collider3D.GetType().Name : data.collider2D.GetType().Name;
                paths.Add($"{data.objectPath} ({type})");
            }
            return paths;
        }

        #endregion

        #region 调试功能 - 增强版

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
            if (screenPos.z > 0 && screenPos.x > 0 && screenPos.x < Screen.width && screenPos.y > 0 && screenPos.y < Screen.height)
            {
                Vector2 guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y);
                GUI.Box(new Rect(guiPos.x - 100, guiPos.y - 80, 200, 160),
                    $"{gameObject.name}\n{bugType}\n{(isBugActive ? "ON" : "OFF")}\n" +
                    $"渲染器: {allRendererData.Count}\n" +
                    $"碰撞体: {allColliderData.Count}\n" +
                    $"平均透明度: {GetAverageAlpha():F2}\n" +
                    $"深度范围: {GetDepthRange()}");
            }
        }

        private string GetDepthRange()
        {
            if (allRendererData.Count == 0) return "0";

            int minDepth = allRendererData.Min(d => d.depth);
            int maxDepth = allRendererData.Max(d => d.depth);

            return minDepth == maxDepth ? minDepth.ToString() : $"{minDepth}-{maxDepth}";
        }

        [ContextMenu("✅ 激活Bug")]
        private void DebugActivateBug() => ActivateBug();

        [ContextMenu("❌ 停用Bug")]
        private void DebugDeActivateBug() => DeactivateBug();

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
            Debug.Log($"包含子物体渲染器: {includeChildRenderers}");
            Debug.Log($"最大搜索深度: {(maxSearchDepth < 0 ? "无限制" : maxSearchDepth.ToString())}");
            Debug.Log($"组件统计: {GetRendererInfo()}");
            Debug.Log($"平均透明度: {GetAverageAlpha()}");

            Debug.Log("=== 渲染器详细列表 ===");
            foreach (var path in GetAllRendererPaths())
            {
                Debug.Log($"  {path}");
            }

            Debug.Log("=== 碰撞体详细列表 ===");
            foreach (var path in GetAllColliderPaths())
            {
                Debug.Log($"  {path}");
            }

            if (excludeByName.Count > 0)
            {
                Debug.Log($"=== 按名称排除: {string.Join(", ", excludeByName)} ===");
            }

            if (excludeByTag.Count > 0)
            {
                Debug.Log($"=== 按标签排除: {string.Join(", ", excludeByTag)} ===");
            }
        }

        [ContextMenu("🔄 重新扫描组件")]
        private void DebugRescanComponents()
        {
            if (Application.isPlaying)
            {
                Debug.Log("🔄 重新扫描组件...");
                CacheAllComponents();
                SaveOriginalState();
                Debug.Log($"✅ 扫描完成: {GetRendererInfo()}");
                Debug.Log($"✅ 深度范围: {GetDepthRange()}");
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

        [ContextMenu("🔍 显示层级结构")]
        private void DebugShowHierarchy()
        {
            Debug.Log("=== 物体层级结构 ===");
            ShowHierarchyRecursive(transform, "", 0);
        }

        private void ShowHierarchyRecursive(Transform current, string prefix, int depth)
        {
            string indent = new string(' ', depth * 2);
            Renderer renderer = current.GetComponent<Renderer>();
            SpriteRenderer spriteRenderer = current.GetComponent<SpriteRenderer>();
            Collider[] colliders3D = current.GetComponents<Collider>();
            Collider2D[] colliders2D = current.GetComponents<Collider2D>();

            string components = "";
            if (renderer != null) components += "[R]";
            if (spriteRenderer != null) components += "[SR]";
            if (colliders3D.Length > 0) components += $"[C3D:{colliders3D.Length}]";
            if (colliders2D.Length > 0) components += $"[C2D:{colliders2D.Length}]";

            bool isExcluded = ShouldExcludeObject(current.gameObject);
            string status = isExcluded ? "[排除]" : "";
            if (!current.gameObject.activeInHierarchy) status += "[未激活]";

            Debug.Log($"{indent}{current.name} {components} {status}");

            for (int i = 0; i < current.childCount; i++)
            {
                ShowHierarchyRecursive(current.GetChild(i), prefix, depth + 1);
            }
        }

        [ContextMenu("⚡ 测试所有Bug类型")]
        private void DebugTestAllBugTypes()
        {
            if (Application.isPlaying)
            {
                StartCoroutine(TestAllBugTypesCoroutine());
            }
        }

        private IEnumerator TestAllBugTypesCoroutine()
        {
            BugType originalType = bugType;
            bool wasActive = isBugActive;

            BugType[] bugTypes = {
                BugType.ObjectFlickering,
                BugType.CollisionMissing,
                BugType.WrongOrMissingMaterial,
                BugType.WrongObject,
                BugType.MissingObject,
                BugType.ObjectShaking,
                BugType.ObjectMovedOrClipping
            };

            foreach (var type in bugTypes)
            {
                Debug.Log($"=== 测试 {type} ===");
                DeactivateBug();
                bugType = type;
                ActivateBug();
                yield return new WaitForSeconds(3f);
            }

            DeactivateBug();
            bugType = originalType;
            if (wasActive) ActivateBug();

            Debug.Log("=== 所有Bug类型测试完成 ===");
        }

        [ContextMenu("📊 性能统计")]
        private void DebugPerformanceStats()
        {
            Debug.Log("=== 性能统计 ===");
            Debug.Log($"渲染器数据条目: {allRendererData.Count}");
            Debug.Log($"碰撞体数据条目: {allColliderData.Count}");

            int activeRenderers = allRendererData.Count(d =>
                (d.renderer != null && d.renderer.gameObject.activeInHierarchy) ||
                (d.spriteRenderer != null && d.spriteRenderer.gameObject.activeInHierarchy));

            int activeColliders = allColliderData.Count(d =>
                (d.collider3D != null && d.collider3D.gameObject.activeInHierarchy) ||
                (d.collider2D != null && d.collider2D.gameObject.activeInHierarchy));

            Debug.Log($"活跃渲染器: {activeRenderers}");
            Debug.Log($"活跃碰撞体: {activeColliders}");

            var depthGroups = allRendererData.GroupBy(d => d.depth).OrderBy(g => g.Key);
            Debug.Log("深度分布:");
            foreach (var group in depthGroups)
            {
                Debug.Log($"  深度 {group.Key}: {group.Count()} 个组件");
            }
        }

        [ContextMenu("🧹 清理无效引用")]
        private void DebugCleanupInvalidReferences()
        {
            int removedRenderers = allRendererData.RemoveAll(d =>
                d.renderer == null && d.spriteRenderer == null);

            int removedColliders = allColliderData.RemoveAll(d =>
                d.collider3D == null && d.collider2D == null);

            Debug.Log($"清理完成: 移除 {removedRenderers} 个无效渲染器引用, {removedColliders} 个无效碰撞体引用");

            if (removedRenderers > 0 || removedColliders > 0)
            {
                Debug.Log("建议重新扫描组件以确保数据完整性");
            }
        }

        [ContextMenu("🎯 单独测试闪烁")]
        private void DebugTestFlickering()
        {
            if (Application.isPlaying)
            {
                StartCoroutine(TestSingleBugType(BugType.ObjectFlickering, 10f));
            }
        }

        [ContextMenu("🔥 单独测试震动")]
        private void DebugTestShaking()
        {
            if (Application.isPlaying)
            {
                StartCoroutine(TestSingleBugType(BugType.ObjectShaking, 8f));
            }
        }

        [ContextMenu("👻 单独测试缺失")]
        private void DebugTestMissing()
        {
            if (Application.isPlaying)
            {
                StartCoroutine(TestSingleBugType(BugType.MissingObject, 5f));
            }
        }

        private IEnumerator TestSingleBugType(BugType testType, float duration)
        {
            BugType originalType = bugType;
            bool wasActive = isBugActive;

            Debug.Log($"=== 开始测试 {testType} - 持续 {duration}秒 ===");

            DeactivateBug();
            bugType = testType;
            ActivateBug();

            yield return new WaitForSeconds(duration);

            DeactivateBug();
            bugType = originalType;
            if (wasActive) ActivateBug();

            Debug.Log($"=== {testType} 测试完成 ===");
        }

        [ContextMenu("🔧 重置到默认设置")]
        private void DebugResetToDefaults()
        {
            DeactivateBug();

            // 重置基本设置
            bugType = BugType.None;
            startWithBugActive = false;

            // 重置渲染器搜索设置
            includeChildRenderers = true;
            includeInactiveChildren = false;
            maxSearchDepth = -1;
            debugRendererSearch = true;
            excludeByName.Clear();
            excludeByTag.Clear();

            // 重置Bug效果设置
            flickerInterval = 0.5f;
            flickerAlphaMin = 0.1f;
            flickerAlphaMax = 1f;
            flickerRandomInterval = false;

            collisionMissingAlpha = 0.8f;
            disableCollidersCompletely = false;

            hideMaterialCompletely = false;
            hideOriginalWhenShowingWrong = true;

            hideObjectCompletely = false;
            missingObjectAlpha = 0f;
            disableCollidersWhenMissing = true;

            shakeIntensity = 0.1f;
            shakeSpeed = 10f;
            shakeDirection = Vector3.one;
            randomShakeDirection = false;

            wrongPosition = Vector3.zero;
            wrongRotation = Vector3.zero;
            wrongScale = Vector3.one;
            enableClippingMode = false;
            clippingOffset = -0.5f;
            useWorldSpace = false;

            // 重置高级设置
            useURPCompatibility = true;
            preserveChildrenOrder = true;
            cacheComponentsOnStart = true;
            autoDetectShaderType = true;

            // 重置调试设置
            debugFlickering = true;
            showDebugInfo = false;
            logComponentDetails = false;

            Debug.Log("已重置所有设置到默认值");

            // 重新缓存组件
            CacheAllComponents();
            SaveOriginalState();
        }

        #endregion

        #region 运行时配置接口

        /// <summary>
        /// 运行时配置闪烁参数
        /// </summary>
        public void ConfigureFlickering(float interval, float minAlpha, float maxAlpha, bool randomInterval = false)
        {
            flickerInterval = interval;
            flickerAlphaMin = minAlpha;
            flickerAlphaMax = maxAlpha;
            flickerRandomInterval = randomInterval;

            // 如果当前正在闪烁，重新启动协程以应用新设置
            if (isBugActive && bugType == BugType.ObjectFlickering)
            {
                if (flickerCoroutine != null)
                    StopCoroutine(flickerCoroutine);
                flickerCoroutine = StartCoroutine(FlickerCoroutine());
            }
        }

        /// <summary>
        /// 运行时配置震动参数
        /// </summary>
        public void ConfigureShaking(float intensity, float speed, Vector3 direction, bool randomDirection = false)
        {
            shakeIntensity = intensity;
            shakeSpeed = speed;
            shakeDirection = direction;
            randomShakeDirection = randomDirection;

            // 如果当前正在震动，重新启动协程以应用新设置
            if (isBugActive && bugType == BugType.ObjectShaking)
            {
                if (shakeCoroutine != null)
                    StopCoroutine(shakeCoroutine);
                shakeStartTime = Time.time;
                shakeCoroutine = StartCoroutine(ShakeCoroutine());
            }
        }

        /// <summary>
        /// 运行时配置位移参数
        /// </summary>
        public void ConfigureMovement(Vector3 position, Vector3 rotation, Vector3 scale, bool worldSpace = false)
        {
            wrongPosition = position;
            wrongRotation = rotation;
            wrongScale = scale;
            useWorldSpace = worldSpace;

            // 如果当前正在位移，重新应用效果
            if (isBugActive && bugType == BugType.ObjectMovedOrClipping)
            {
                RemoveObjectMovedOrClippingEffect();
                ApplyObjectMovedOrClippingEffect();
            }
        }

        /// <summary>
        /// 运行时配置穿模参数
        /// </summary>
        public void ConfigureClipping(bool enableClipping, float offset)
        {
            enableClippingMode = enableClipping;
            clippingOffset = offset;

            // 如果当前正在穿模，重新应用效果
            if (isBugActive && bugType == BugType.ObjectMovedOrClipping)
            {
                RemoveObjectMovedOrClippingEffect();
                ApplyObjectMovedOrClippingEffect();
            }
        }

        /// <summary>
        /// 添加错误材质
        /// </summary>
        public void AddBuggyMaterial(Material material)
        {
            if (material != null && !buggyMaterials.Contains(material))
            {
                buggyMaterials.Add(material);

                // 如果当前正在使用错误材质效果，重新应用
                if (isBugActive && bugType == BugType.WrongOrMissingMaterial)
                {
                    ApplyBuggyMaterialsToAll();
                }
            }
        }

        /// <summary>
        /// 添加错误物体
        /// </summary>
        public void AddWrongObject(GameObject obj)
        {
            if (obj != null && !wrongObjects.Contains(obj))
            {
                wrongObjects.Add(obj);
            }
        }

        /// <summary>
        /// 清除所有错误材质
        /// </summary>
        public void ClearBuggyMaterials()
        {
            buggyMaterials.Clear();
        }

        /// <summary>
        /// 清除所有错误物体
        /// </summary>
        public void ClearWrongObjects()
        {
            wrongObjects.Clear();
        }

        /// <summary>
        /// 获取当前Bug效果的详细状态
        /// </summary>
        public string GetBugEffectStatus()
        {
            if (!isBugActive)
                return "Bug效果未激活";

            switch (bugType)
            {
                case BugType.ObjectFlickering:
                    return $"闪烁效果 - 间隔:{flickerInterval}s, 透明度:{flickerAlphaMin}-{flickerAlphaMax}";

                case BugType.CollisionMissing:
                    return $"碰撞缺失 - 透明度:{collisionMissingAlpha}, 完全禁用:{disableCollidersCompletely}";

                case BugType.WrongOrMissingMaterial:
                    return $"材质错误 - 错误材质数量:{buggyMaterials.Count}, 完全隐藏:{hideMaterialCompletely}";

                case BugType.WrongObject:
                    return $"错误物体 - 错误物体数量:{wrongObjects.Count}, 已生成:{spawnedWrongObjects.Count}";

                case BugType.MissingObject:
                    return $"缺失物体 - 透明度:{missingObjectAlpha}, 完全隐藏:{hideObjectCompletely}";

                case BugType.ObjectShaking:
                    return $"物体震动 - 强度:{shakeIntensity}, 频率:{shakeSpeed}Hz, 随机方向:{randomShakeDirection}";

                case BugType.ObjectMovedOrClipping:
                    return $"位移/穿模 - 穿模模式:{enableClippingMode}, 位置偏移:{wrongPosition}";

                default:
                    return $"未知Bug类型: {bugType}";
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