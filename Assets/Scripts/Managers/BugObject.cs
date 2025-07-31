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
        private Coroutine flickerCoroutine;

        // ��Ч����
        private GameObject spawnedEffect;

        #region Unity��������

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

        #region ��ʼ��

        private void CacheComponents()
        {
            objectRenderer = GetComponent<Renderer>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            objectCollider2D = GetComponent<Collider2D>();
            objectCollider3D = GetComponent<Collider>();
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