// InfoItemUI.cs - 单个信息项的UI组件
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BugFixerGame
{
    /// <summary>
    /// 信息项UI组件 - 负责显示单个信息项
    /// </summary>
    public class InfoItemUI : MonoBehaviour
    {
        [Header("UI组件")]
        [SerializeField] private Image backgroundImage;         // 背景图片
        [SerializeField] private TextMeshProUGUI descriptionText; // 描述文本

        [Header("文本样式")]
        [SerializeField] private Color bugTextColor = Color.red;
        [SerializeField] private Color messageTextColor = Color.blue;
        [SerializeField] private Color alertTextColor = new Color(1f, 0.5f, 0f, 1f); // 橙色

        [Header("布局设置")]
        [SerializeField] private float minHeight = 80f;        // 最小高度
        [SerializeField] private float maxHeight = 200f;       // 最大高度
        [SerializeField] private bool autoSizeHeight = true;   // 自动调整高度

        // 当前信息数据
        private InfoData currentInfo;

        #region Unity生命周期

        private void Awake()
        {
            // 自动查找组件（如果没有在Inspector中设置）
            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();

            if (descriptionText == null)
                descriptionText = GetComponentInChildren<TextMeshProUGUI>();

            // 验证必要组件
            ValidateComponents();
        }

        #endregion

        #region 设置信息

        /// <summary>
        /// 设置信息内容
        /// </summary>
        public void SetupInfo(InfoData info, Sprite backgroundSprite = null)
        {
            if (info == null)
            {
                Debug.LogError("❌ InfoItemUI: 尝试设置空的信息数据");
                return;
            }

            currentInfo = info;

            // 设置背景图片
            if (backgroundImage != null)
            {
                if (backgroundSprite != null)
                {
                    backgroundImage.sprite = backgroundSprite;
                    backgroundImage.color = Color.white;
                }
                else
                {
                    // 如果没有背景图片，使用纯色背景
                    backgroundImage.sprite = null;
                    backgroundImage.color = GetDefaultBackgroundColor(info.type);
                }
            }

            // 设置描述文本和颜色
            if (descriptionText != null)
            {
                descriptionText.text = info.description;
                descriptionText.color = GetTextColor(info.type);
            }

            // 自动调整高度
            if (autoSizeHeight)
            {
                StartCoroutine(AdjustHeightNextFrame());
            }

            Debug.Log($"✅ InfoItemUI: 设置{info.type}信息 - {info.description}");
        }

        #endregion

        #region 样式设置

        /// <summary>
        /// 获取文本颜色
        /// </summary>
        private Color GetTextColor(InfoType type)
        {
            switch (type)
            {
                case InfoType.Bug:
                    return bugTextColor;
                case InfoType.Message:
                    return messageTextColor;
                case InfoType.Alert:
                    return alertTextColor;
                default:
                    return Color.white;
            }
        }

        /// <summary>
        /// 获取默认背景颜色（当没有背景图片时使用）
        /// </summary>
        private Color GetDefaultBackgroundColor(InfoType type)
        {
            switch (type)
            {
                case InfoType.Bug:
                    return new Color(0.8f, 0.2f, 0.2f, 0.8f); // 红色半透明
                case InfoType.Message:
                    return new Color(0.2f, 0.2f, 0.8f, 0.8f); // 蓝色半透明
                case InfoType.Alert:
                    return new Color(0.8f, 0.6f, 0.2f, 0.8f); // 橙色半透明
                default:
                    return new Color(0.5f, 0.5f, 0.5f, 0.8f); // 灰色半透明
            }
        }

        #endregion

        #region 高度调整

        /// <summary>
        /// 在下一帧调整高度（确保文本布局完成）
        /// </summary>
        private System.Collections.IEnumerator AdjustHeightNextFrame()
        {
            yield return null; // 等待一帧
            AdjustHeight();
        }

        /// <summary>
        /// 根据内容调整高度
        /// </summary>
        private void AdjustHeight()
        {
            if (!autoSizeHeight) return;

            RectTransform rectTransform = GetComponent<RectTransform>();
            if (rectTransform == null) return;

            float requiredHeight = CalculateRequiredHeight();
            float finalHeight = Mathf.Clamp(requiredHeight, minHeight, maxHeight);

            Vector2 sizeDelta = rectTransform.sizeDelta;
            sizeDelta.y = finalHeight;
            rectTransform.sizeDelta = sizeDelta;

            Debug.Log($"📏 InfoItemUI: 调整高度 {requiredHeight:F1} -> {finalHeight:F1}");
        }

        /// <summary>
        /// 计算所需高度
        /// </summary>
        private float CalculateRequiredHeight()
        {
            float totalHeight = 0f;
            float padding = 20f; // 基础内边距

            // 描述高度
            if (descriptionText != null)
            {
                totalHeight += descriptionText.preferredHeight;
            }

            totalHeight += padding;
            return totalHeight;
        }

        #endregion

        #region 组件验证

        /// <summary>
        /// 验证必要组件
        /// </summary>
        private void ValidateComponents()
        {
            bool hasErrors = false;

            if (backgroundImage == null)
            {
                Debug.LogWarning("⚠️ InfoItemUI: backgroundImage未设置，将尝试自动获取");
                backgroundImage = GetComponent<Image>();
                if (backgroundImage == null)
                {
                    Debug.LogError("❌ InfoItemUI: 无法找到背景Image组件");
                    hasErrors = true;
                }
            }

            if (descriptionText == null)
            {
                Debug.LogWarning("⚠️ InfoItemUI: descriptionText未设置");
                hasErrors = true;
            }

            if (hasErrors)
            {
                Debug.LogError("❌ InfoItemUI: 组件设置不完整，请在Inspector中配置必要的UI组件引用");
            }
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 获取当前信息数据
        /// </summary>
        public InfoData GetCurrentInfo()
        {
            return currentInfo;
        }

        /// <summary>
        /// 设置文本颜色
        /// </summary>
        public void SetTextColors(Color bugText, Color messageText, Color alertText)
        {
            bugTextColor = bugText;
            messageTextColor = messageText;
            alertTextColor = alertText;

            // 如果当前有信息，重新设置颜色
            if (currentInfo != null && descriptionText != null)
            {
                descriptionText.color = GetTextColor(currentInfo.type);
            }
        }

        /// <summary>
        /// 设置高度范围
        /// </summary>
        public void SetHeightRange(float min, float max, bool autoSize = true)
        {
            minHeight = min;
            maxHeight = max;
            autoSizeHeight = autoSize;

            if (autoSize)
            {
                AdjustHeight();
            }
        }

        /// <summary>
        /// 强制刷新显示
        /// </summary>
        public void RefreshDisplay()
        {
            if (currentInfo != null)
            {
                SetupInfo(currentInfo);
            }
        }

        #endregion

        #region 调试功能

        [ContextMenu("🔍 检查组件")]
        private void CheckComponents()
        {
            Debug.Log("=== InfoItemUI 组件检查 ===");
            Debug.Log($"背景图片: {(backgroundImage != null ? backgroundImage.name : "未设置")}");
            Debug.Log($"描述文本: {(descriptionText != null ? descriptionText.name : "未设置")}");
            Debug.Log($"当前信息: {(currentInfo != null ? $"{currentInfo.type} - {currentInfo.description}" : "无")}");
            Debug.Log($"自动调整高度: {autoSizeHeight}");
            Debug.Log($"高度范围: {minHeight} - {maxHeight}");
        }

        [ContextMenu("📏 测试高度调整")]
        private void TestAdjustHeight()
        {
            if (Application.isPlaying)
            {
                AdjustHeight();
                Debug.Log("高度调整测试完成");
            }
        }

        #endregion
    }
}