// InfoSystemManager.cs
using UnityEngine;

namespace BugFixerGame
{
    public class InfoSystemManager : MonoBehaviour
    {
        [Header("Assign in Inspector")]
        [SerializeField] private Sprite bugSprite;
        [SerializeField] private Sprite messageSprite;
        [SerializeField] private Sprite alertSprite;

        [Header("Options")]
        [SerializeField] private bool initializeOnStart = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

        private void Start()
        {
            if (initializeOnStart)
                Initialize();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                InfoDisplayUI.Instance?.TogglePanel();
        }

        public void Initialize()
        {
            if (InfoDisplayUI.Instance != null)
            {
                InfoDisplayUI.Instance.SetBackgroundSprites(
                    bugSprite,
                    messageSprite,
                    alertSprite
                );
                InfoDisplayUI.Instance.RefreshBugInfo();
            }
        }
    }
}
