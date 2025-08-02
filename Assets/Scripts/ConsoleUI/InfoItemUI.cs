// InfoItemUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BugFixerGame
{
    public class InfoItemUI : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text descriptionText;

        public void SetupInfo(InfoData info, Sprite sprite)
        {
            if (backgroundImage != null)
                backgroundImage.sprite = sprite;

            if (descriptionText != null)
                descriptionText.text = info.Description;
        }
    }
}
