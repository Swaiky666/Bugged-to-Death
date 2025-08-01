using System;
using System.Collections;
using UnityEngine;

namespace BugFixerGame
{
    // Bug对象类 - 负责管理需要Bug效果的GameObject上
    public class BugObject : MonoBehaviour
    {
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

        [Header("碰撞丢失Bug设置")]
        [SerializeField] private float collisionMissingAlpha = 0.8f;

        [Header("材质Bug设置")]
        [SerializeField] private Material buggyMaterial; // 纯色材质等

        [Header("URP闪烁修复")]
        [SerializeField] private bool useURPCompatibility = true; // 启用URP兼容模式
        [SerializeField] private Material transparentMaterialPrefab; // 预制的透明材质（在Inspector中分配）

        [Header("闪烁Bug调试和修复")]
        [SerializeField] private bool debugFlickering = true;
        [SerializeField] private bool autoFixMaterialTransparency = true; // 自动修复材质透明度设置

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

        // 当前状态
        private bool isBugActive = false;
        private bool isBeingFixed = false;  // 是否正在修复中
        private Coroutine flickerCoroutine;
        private Coroutine popupCoroutine;

        // 特效对象
        private GameObject spawnedEffect;

        // URP材质相关常量
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int SurfaceTypeProperty = Shader.PropertyToID("_Surface");
        private static readonly int BlendModeProperty = Shader.PropertyToID("_Blend");
        private static readonly int AlphaClipProperty = Shader.PropertyToID("_AlphaClip");
        private static readonly int SrcBlendProperty = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendProperty = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteProperty = Shader.PropertyToID("_ZWrite");

        // 事件
        public static event Action<BugObject> OnBugClicked;         // Bug被点击
        public static event Action<BugObject> OnBugFixed;           // Bug被修复完成

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
            // 已经由Player系统管理点击检测，现在主要由Player处理
            // HandleClickDetection(); // 注释掉这行
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

            // 并即停用Bug效果
            DeactivateBug();

            // 显示正确物体并播放动画
            if (correctObject != null)
            {
                ShowCorrectObject();
            }
            else
            {
                // 如果没有正确物体，直接完成修复
                CompleteBugFix();
            }
        }

        private void ShowCorrectObject()
        {
            correctObject.SetActive(true);

            // 播放弹出动画
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

            // 动画完成后，等待一段时间再完成修复
            yield return new WaitForSeconds(0.5f);

            CompleteBugFix();
        }

        private void CompleteBugFix()
        {
            Debug.Log($"Bug修复完成: {gameObject.name}");

            // 触发修复完成事件
            OnBugFixed?.Invoke(this);

            // 销毁Bug物体（或者销毁它和相关事件处理完成）
            StartCoroutine(DestroyBugObjectDelayed());
        }

        private IEnumerator DestroyBugObjectDelayed()
        {
            yield return new WaitForEndOfFrame();

            // 销毁这个Bug物体
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        #endregion

        #region 点击检测相关

        // 由Player调用的点击方法（长按2秒后才会调用）
        public void OnClickedByPlayer()
        {
            // 只有当Bug激活且没有正在修复时才处理点击
            if (!isBugActive || isBeingFixed) return;

            Debug.Log($"玩家长按2秒完成，开始修复Bug物体: {gameObject.name}");

            // 触发点击事件
            OnBugClicked?.Invoke(this);

            // 开始修复Bug
            StartBugFix();
        }

        // 已经由Player系统管理的点击检测代码，现在主要由Player处理交互

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
            // 确保正确物体在开始时是隐藏的
            if (correctObject != null)
            {
                correctObject.SetActive(false);
            }
        }

        private void SaveOriginalState()
        {
            // 保存原始颜色和材质
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

                // 已经由ObjectMoved和ClippingBug，需要用户直接在Scene中摆放

                default:
                    Debug.LogWarning($"未实现的Bug类型: {bugType}");
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

            // 清理生成的特效
            if (spawnedEffect != null)
            {
                DestroyImmediate(spawnedEffect);
                spawnedEffect = null;
            }
        }

        #endregion

        #region 具体Bug效果实现

        private void ApplyObjectMissingEffect()
        {
            // 设置完全透明，但保持GameObject激活状态
            SetAlpha(0f);

            // 将碰撞体设为Trigger，这样可以被鼠标射线检测到，但不会产生物理碰撞
            if (objectCollider2D != null)
            {
                objectCollider2D.isTrigger = true;
            }
            if (objectCollider3D != null)
            {
                objectCollider3D.isTrigger = true;
            }

            Debug.Log($"ObjectMissing效果：{gameObject.name} 设为透明且无碰撞，但可点击");
        }

        private void RemoveObjectMissingEffect()
        {
            // 恢复原始透明度
            RestoreOriginalColor();

            // 恢复碰撞体原始状态
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
            // 将碰撞体设为Trigger，这样可以被点击但没有物理碰撞
            if (objectCollider2D != null)
            {
                objectCollider2D.isTrigger = true;
            }
            if (objectCollider3D != null)
            {
                objectCollider3D.isTrigger = true;
            }

            // 降低透明度作为视觉提示
            SetAlpha(collisionMissingAlpha);

            Debug.Log($"CollisionMissing效果：{gameObject.name} 碰撞设为Trigger，可点击但无物理碰撞");
        }

        private void RemoveCollisionMissingEffect()
        {
            // 恢复碰撞体原始状态
            if (objectCollider2D != null)
            {
                objectCollider2D.isTrigger = originalCollider2DIsTrigger;
            }
            if (objectCollider3D != null)
            {
                objectCollider3D.isTrigger = originalCollider3DIsTrigger;
            }

            // 恢复原始透明度
            RestoreOriginalColor();
        }

        #endregion

        #region URP兼容的闪烁功能

        private void ApplyFlickeringEffect()
        {
            if (debugFlickering)
                Debug.Log($"[URP闪烁] 开始应用闪烁效果到 {gameObject.name}");

            // URP兼容性处理
            if (useURPCompatibility)
            {
                SetupURPTransparency();
            }
            else
            {
                // 传统方式（可能在URP中不工作）
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
                    Debug.Log($"[URP闪烁] 停止闪烁效果");
            }

            // 恢复原始透明度
            RestoreOriginalColor();
        }

        private void SetupURPTransparency()
        {
            if (objectRenderer == null && spriteRenderer == null)
            {
                Debug.LogError("[URP闪烁] 未找到Renderer组件！");
                return;
            }

            // 处理SpriteRenderer（通常不需要特殊处理）
            if (spriteRenderer != null)
            {
                if (debugFlickering)
                    Debug.Log("[URP闪烁] 使用SpriteRenderer，已支持透明度");
                return;
            }

            // 处理MeshRenderer的URP材质
            if (objectRenderer != null)
            {
                Material currentMat = objectRenderer.material;

                if (debugFlickering)
                {
                    Debug.Log($"[URP闪烁] 当前材质: {currentMat.name}");
                    Debug.Log($"[URP闪烁] 当前Shader: {currentMat.shader.name}");
                }

                // 检查是否是URP Shader
                if (IsURPShader(currentMat.shader))
                {
                    SetupURPMaterialTransparency(currentMat);
                }
                else
                {
                    // 如果不是URP shader，尝试使用预制透明材质
                    if (transparentMaterialPrefab != null)
                    {
                        if (debugFlickering)
                            Debug.Log("[URP闪烁] 使用预制透明材质替换");

                        // 保存原始材质
                        if (originalMaterial == null)
                            originalMaterial = currentMat;

                        // 应用透明材质
                        objectRenderer.material = transparentMaterialPrefab;
                    }
                    else
                    {
                        Debug.LogWarning($"[URP闪烁] 材质 {currentMat.name} 不是URP shader且没有预制透明材质！");
                        Debug.LogWarning("[URP闪烁] 请在Inspector中分配Transparent Material Prefab");

                        // 尝试传统方式作为备用
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
                Debug.Log("[URP闪烁] 设置URP材质透明度支持");

            try
            {
                // 设置Surface Type为Transparent (1.0)
                if (mat.HasProperty(SurfaceTypeProperty))
                {
                    mat.SetFloat(SurfaceTypeProperty, 1.0f); // 1 = Transparent
                    if (debugFlickering)
                        Debug.Log("[URP闪烁] Surface Type设置为Transparent");
                }

                // 设置Blend Mode为Alpha (0)
                if (mat.HasProperty(BlendModeProperty))
                {
                    mat.SetFloat(BlendModeProperty, 0.0f); // 0 = Alpha
                }

                // 设置混合参数
                if (mat.HasProperty(SrcBlendProperty))
                    mat.SetFloat(SrcBlendProperty, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);

                if (mat.HasProperty(DstBlendProperty))
                    mat.SetFloat(DstBlendProperty, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

                if (mat.HasProperty(ZWriteProperty))
                    mat.SetFloat(ZWriteProperty, 0.0f); // 关闭深度写入

                // 启用透明关键字
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");

                // 设置渲染队列
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                if (debugFlickering)
                    Debug.Log("[URP闪烁] URP透明度设置完成");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[URP闪烁] 设置URP透明度时出错: {e.Message}");
            }
        }

        private void EnsureMaterialSupportsTransparency()
        {
            if (objectRenderer != null)
            {
                Material mat = objectRenderer.material;

                if (debugFlickering)
                    Debug.Log($"[材质检查] 检查材质: {mat.name}, Shader: {mat.shader.name}");

                // 检查是否是Standard shader
                if (mat.shader.name.Contains("Standard"))
                {
                    // 获取当前渲染模式
                    float currentMode = mat.HasProperty("_Mode") ? mat.GetFloat("_Mode") : 0;

                    if (debugFlickering)
                        Debug.Log($"[材质检查] 当前渲染模式: {currentMode} (0=Opaque, 1=Cutout, 2=Fade, 3=Transparent)");

                    // 如果不是透明模式，设置为透明
                    if (currentMode < 2) // 0=Opaque, 1=Cutout都不支持透明度
                    {
                        SetMaterialToTransparent(mat);

                        if (debugFlickering)
                            Debug.Log($"[材质修复] 自动设置材质 {mat.name} 为透明模式");
                    }
                }
                else if (mat.shader.name.Contains("Sprites"))
                {
                    // Sprite材质通常天然支持透明度
                    if (debugFlickering)
                        Debug.Log($"[材质检查] Sprite材质已支持透明度");
                }
                else
                {
                    if (debugFlickering)
                        Debug.LogWarning($"[材质检查] 未知Shader类型: {mat.shader.name}，可能需要手动设置透明度支持");
                }
            }
            else if (spriteRenderer != null)
            {
                if (debugFlickering)
                    Debug.Log($"[材质检查] 使用SpriteRenderer，天然支持透明度");
            }
            else
            {
                if (debugFlickering)
                    Debug.LogError($"[材质检查] 未找到Renderer或SpriteRenderer组件！");
            }
        }

        private void SetMaterialToTransparent(Material mat)
        {
            // 设置为透明模式（Built-in RP Standard shader）
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
                Debug.Log($"[闪烁调试] 闪烁协程开始");
                Debug.Log($"- 间隔: {flickerInterval}秒");
                Debug.Log($"- 透明度范围: {flickerAlphaMin} - {flickerAlphaMax}");
                Debug.Log($"- 初始透明度: {GetCurrentAlpha()}");
            }

            while (isBugActive)
            {
                isVisible = !isVisible;
                float targetAlpha = isVisible ? flickerAlphaMax : flickerAlphaMin;

                // 设置透明度
                bool success = SetAlpha(targetAlpha);

                flickerCount++;

                // 只在前几次输出调试信息，避免刷屏
                if (debugFlickering && flickerCount <= 5)
                {
                    Debug.Log($"[闪烁调试] 第{flickerCount}次闪烁 - 目标:{targetAlpha}, 设置成功:{success}, 实际:{GetCurrentAlpha()}");
                }

                // 每10秒输出一次状态信息
                if (debugFlickering && (Time.time - startTime) > 10f && flickerCount % 20 == 0)
                {
                    Debug.Log($"[闪烁状态] 运行{Time.time - startTime:F1}秒, 闪烁{flickerCount}次, 当前透明度:{GetCurrentAlpha()}");
                }

                yield return new WaitForSeconds(flickerInterval);
            }

            if (debugFlickering)
                Debug.Log($"[闪烁调试] 闪烁协程结束 - 总共闪烁{flickerCount}次，运行{Time.time - startTime:F1}秒");
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

                if (debugFlickering && Time.frameCount % 120 == 0) // 每2秒输出一次（假设60FPS）
                    Debug.Log($"[透明度设置] SpriteRenderer设置为 {alpha}");
            }
            else if (objectRenderer != null)
            {
                Material mat = objectRenderer.material;

                // URP使用_BaseColor属性
                if (mat.HasProperty(BaseColorProperty))
                {
                    Color color = mat.GetColor(BaseColorProperty);
                    color.a = alpha;
                    mat.SetColor(BaseColorProperty, color);
                    success = true;

                    if (debugFlickering && Time.frameCount % 120 == 0)
                        Debug.Log($"[透明度设置] URP _BaseColor透明度: {alpha}");
                }
                // 备用：尝试传统的color属性
                else if (mat.HasProperty("_Color"))
                {
                    Color color = mat.color;
                    color.a = alpha;
                    mat.color = color;
                    success = true;

                    if (debugFlickering && Time.frameCount % 120 == 0)
                        Debug.Log($"[透明度设置] 传统Color透明度: {alpha}");
                }
                else
                {
                    if (debugFlickering)
                        Debug.LogWarning("[透明度设置] 材质没有_BaseColor或_Color属性！");
                }
            }
            else
            {
                if (debugFlickering)
                    Debug.LogError($"[透明度设置] 未找到可设置透明度的Renderer组件！");
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

            return -1f; // 表示无法获取
        }

        private void RestoreOriginalColor()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;

                if (debugFlickering)
                    Debug.Log($"[透明度恢复] SpriteRenderer恢复到 {originalColor.a}");
            }
            else if (objectRenderer != null)
            {
                // 如果使用了预制透明材质，恢复原始材质
                if (transparentMaterialPrefab != null && originalMaterial != null &&
                    objectRenderer.material == transparentMaterialPrefab)
                {
                    objectRenderer.material = originalMaterial;
                    if (debugFlickering)
                        Debug.Log($"[透明度恢复] 已恢复原始材质: {originalMaterial.name}");
                }
                else
                {
                    // 恢复颜色
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
                        Debug.Log($"[透明度恢复] 材质颜色已恢复");
                }
            }
        }

        #endregion

        #region 公共接口

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

        #region 调试功能

        [Header("调试")]
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

        // Inspector按钮
        [ContextMenu("激活Bug")]
        private void DebugActivateBug()
        {
            ActivateBug();
        }

        [ContextMenu("停用Bug")]
        private void DebugDeactivateBug()
        {
            DeactivateBug();
        }

        [ContextMenu("切换Bug")]
        private void DebugToggleBug()
        {
            ToggleBug();
        }

        [ContextMenu("?? 测试闪烁效果")]
        private void DebugTestFlickering()
        {
            if (Application.isPlaying)
            {
                debugFlickering = true;
                Debug.Log("=== 开始闪烁效果测试 ===");

                // 保存当前设置
                BugType originalBugType = bugType;
                bool originalBugActive = isBugActive;

                // 设置为闪烁类型并激活
                SetBugType(BugType.ObjectFlickering);
                ActivateBug();

                // 5秒后停止测试
                StartCoroutine(StopTestAfterDelay(5f, originalBugType, originalBugActive));
            }
            else
            {
                Debug.Log("请在运行时测试闪烁效果");
            }
        }

        private IEnumerator StopTestAfterDelay(float delay, BugType originalType, bool wasActive)
        {
            yield return new WaitForSeconds(delay);

            Debug.Log("=== 闪烁效果测试结束 ===");

            // 恢复原始设置
            DeactivateBug();
            SetBugType(originalType);

            if (wasActive)
                ActivateBug();
        }

        [ContextMenu("?? 检查URP材质设置")]
        private void DebugCheckURPMaterial()
        {
            Debug.Log("=== URP材质设置检查 ===");

            if (objectRenderer != null)
            {
                Material mat = objectRenderer.sharedMaterial;
                Debug.Log($"材质名称: {mat.name}");
                Debug.Log($"Shader: {mat.shader.name}");
                Debug.Log($"是否URP Shader: {IsURPShader(mat.shader)}");

                // 检查URP特有属性
                if (mat.HasProperty(SurfaceTypeProperty))
                {
                    float surfaceType = mat.GetFloat(SurfaceTypeProperty);
                    string surfaceTypeText = surfaceType == 0 ? "Opaque(不透明)" : "Transparent(透明)";
                    Debug.Log($"Surface Type: {surfaceType} ({surfaceTypeText})");
                }
                else
                {
                    Debug.LogWarning("材质没有_Surface属性（可能不是URP材质）");
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
                    Debug.LogWarning("材质没有颜色属性！");
                }

                Debug.Log($"渲染队列: {mat.renderQueue}");

                // 检查关键字
                if (mat.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"))
                    Debug.Log("? _SURFACE_TYPE_TRANSPARENT 关键字已启用");
                else
                    Debug.Log("? _SURFACE_TYPE_TRANSPARENT 关键字未启用");

            }
            else if (spriteRenderer != null)
            {
                Debug.Log($"使用SpriteRenderer: {spriteRenderer.sprite?.name}");
                Debug.Log($"当前颜色: {spriteRenderer.color}");
                Debug.Log("? SpriteRenderer天然支持透明度");
            }
            else
            {
                Debug.LogError("? 未找到Renderer组件！");
            }

            // 检查预制透明材质
            if (transparentMaterialPrefab != null)
            {
                Debug.Log($"? 预制透明材质: {transparentMaterialPrefab.name}");
                Debug.Log($"预制材质Shader: {transparentMaterialPrefab.shader.name}");
            }
            else
            {
                Debug.LogWarning("?? 没有设置预制透明材质");
            }

            Debug.Log("=== 检查完成 ===");
        }

        [ContextMenu("?? 自动修复URP透明度")]
        private void DebugFixURPTransparency()
        {
            if (Application.isPlaying)
            {
                Debug.Log("开始URP透明度自动修复...");
                useURPCompatibility = true;
                SetupURPTransparency();
                Debug.Log("URP透明度修复完成");
            }
            else
            {
                Debug.Log("请在运行时执行URP透明度修复");
            }
        }

        [ContextMenu("?? 手动测试透明度")]
        private void DebugTestTransparency()
        {
            if (Application.isPlaying)
            {
                StartCoroutine(TestTransparencyAnimation());
            }
            else
            {
                Debug.Log("请在运行时测试透明度");
            }
        }

        private IEnumerator TestTransparencyAnimation()
        {
            Debug.Log("开始透明度测试动画...");

            float duration = 3f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.PingPong(elapsed, 1f); // 0到1之间往返

                SetAlpha(alpha);
                Debug.Log($"测试透明度: {alpha:F2}");

                yield return new WaitForSeconds(0.1f);
            }

            // 恢复原始透明度
            RestoreOriginalColor();
            Debug.Log("透明度测试完成");
        }

        #endregion
    }
}