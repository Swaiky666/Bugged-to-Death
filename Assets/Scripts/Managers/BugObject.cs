using System;
using System.Collections;
using UnityEngine;

namespace BugFixerGame
{
    // Bug对象脚本 - 挂载到需要Bug效果的GameObject上
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

        [Header("点击检测配置")]
        [SerializeField] private bool enableClickToFix = true;      // 是否启用点击修复
        [SerializeField] private LayerMask clickLayerMask = -1;     // 点击检测层级

        [Header("闪烁Bug设置")]
        [SerializeField] private float flickerInterval = 0.5f;
        [SerializeField] private float flickerAlphaMin = 0.3f;
        [SerializeField] private float flickerAlphaMax = 1f;

        [Header("碰撞丢失Bug设置")]
        [SerializeField] private float collisionMissingAlpha = 0.8f;

        [Header("材质Bug设置")]
        [SerializeField] private Material buggyMaterial; // 紫色材质等

        [Header("移动Bug设置")]
        [SerializeField] private Vector3 bugOffset = Vector3.zero;

        // 组件缓存
        private Renderer objectRenderer;
        private SpriteRenderer spriteRenderer;
        private Collider2D objectCollider2D;
        private Collider objectCollider3D;

        // 原始状态保存
        private Vector3 originalPosition;
        private Color originalColor;
        private Material originalMaterial;
        private bool originalCollider2DEnabled;
        private bool originalCollider3DEnabled;

        // 运行时状态
        private bool isBugActive = false;
        private bool isBeingFixed = false;  // 是否正在修复中
        private Coroutine flickerCoroutine;
        private Coroutine popupCoroutine;

        // 特效对象
        private GameObject spawnedEffect;

        // 点击检测组件
        private Collider2D clickCollider2D;
        private Collider clickCollider3D;

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
            HandleClickDetection();
        }

        private void OnDestroy()
        {
            DeactivateBug();
        }

        #endregion

        #region Bug修复系统

        private void StartBugFix()
        {
            if (isBeingFixed) return;

            isBeingFixed = true;

            // 立即停用Bug效果
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

            // 销毁Bug物体（延迟销毁以确保事件处理完成）
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

        #region 点击检测系统

        // 由PlayerController调用的点击方法
        public void OnClickedByPlayer()
        {
            // 只有当Bug激活且没有正在修复时才处理点击
            if (!isBugActive || isBeingFixed) return;

            Debug.Log($"玩家点击了Bug物体: {gameObject.name}");

            // 触发点击事件
            OnBugClicked?.Invoke(this);

            // 开始修复Bug
            StartBugFix();
        }

        private void HandleClickDetection()
        {
            // 这个方法现在由PlayerController处理，保留作为备用
            // 只有当Bug激活且启用点击修复时才检测点击
            if (!isBugActive || !enableClickToFix || isBeingFixed) return;

            if (Input.GetMouseButtonDown(0)) // 左键点击
            {
                CheckMouseClick();
            }
        }

        private void CheckMouseClick()
        {
            Vector3 mousePosition = Input.mousePosition;
            Ray ray = Camera.main.ScreenPointToRay(mousePosition);

            // 2D点击检测
            if (clickCollider2D != null)
            {
                Vector2 worldPoint = Camera.main.ScreenToWorldPoint(mousePosition);
                if (clickCollider2D.OverlapPoint(worldPoint))
                {
                    OnClickDetected();
                    return;
                }
            }

            // 3D点击检测
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
            Debug.Log($"Bug物体被直接点击: {gameObject.name}");

            // 触发点击事件
            OnBugClicked?.Invoke(this);

            // 开始修复Bug
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

            // 点击检测组件（可能与碰撞检测组件相同）
            clickCollider2D = objectCollider2D;
            clickCollider3D = objectCollider3D;

            // 如果没有碰撞体，尝试添加一个用于点击检测
            if (clickCollider2D == null && clickCollider3D == null)
            {
                if (spriteRenderer != null)
                {
                    // 2D物体添加BoxCollider2D
                    clickCollider2D = gameObject.AddComponent<BoxCollider2D>();
                    clickCollider2D.isTrigger = true;
                }
                else if (objectRenderer != null)
                {
                    // 3D物体添加BoxCollider
                    clickCollider3D = gameObject.AddComponent<BoxCollider>();
                    clickCollider3D.isTrigger = true;
                }
            }
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
            // 保存原始位置
            originalPosition = transform.position;

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
                originalCollider2DEnabled = objectCollider2D.enabled;
            if (objectCollider3D != null)
                originalCollider3DEnabled = objectCollider3D.enabled;
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

            // 恢复原始透明度
            RestoreOriginalColor();
        }

        private void ApplyCollisionMissingEffect()
        {
            // 禁用碰撞体
            if (objectCollider2D != null)
                objectCollider2D.enabled = false;
            if (objectCollider3D != null)
                objectCollider3D.enabled = false;

            // 降低透明度作为视觉提示
            SetAlpha(collisionMissingAlpha);
        }

        private void RemoveCollisionMissingEffect()
        {
            // 恢复碰撞体
            if (objectCollider2D != null)
                objectCollider2D.enabled = originalCollider2DEnabled;
            if (objectCollider3D != null)
                objectCollider3D.enabled = originalCollider3DEnabled;

            // 恢复原始透明度
            RestoreOriginalColor();
        }

        private void ApplyClippingBugEffect()
        {
            // 让对象部分"陷入"地面或其他对象
            transform.position = originalPosition + new Vector3(0, -0.5f, 0);
        }

        private void RemoveClippingBugEffect()
        {
            transform.position = originalPosition;
        }

        #endregion

        #region 辅助方法

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

        // 运行时设置Bug参数
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

        #endregion
    }
}