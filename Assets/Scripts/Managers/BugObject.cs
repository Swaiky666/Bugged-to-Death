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
        private Coroutine flickerCoroutine;

        // 特效对象
        private GameObject spawnedEffect;

        #region Unity生命周期

        private void Awake()
        {
            CacheComponents();
            SaveOriginalState();

            if (startWithBugActive)
            {
                ActivateBug();
            }
        }

        private void OnDestroy()
        {
            DeactivateBug();
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