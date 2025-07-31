using UnityEngine;

namespace BugFixerGame
{
    public class PlayerController : MonoBehaviour
    {
        [Header("点击检测设置")]
        [SerializeField] private LayerMask clickableLayerMask = -1;
        [SerializeField] private float centerRayDistance = 100f;
        [SerializeField] private bool showClickDebug = true;

        private Camera playerCamera;
        private CameraController cameraController;

        private BugObject currentDetectedBug;

        public static event System.Action<BugObject> OnBugObjectClicked;
        public static event System.Action<Vector3> OnEmptySpaceClicked;

        private void Awake()
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
            {
                playerCamera = FindObjectOfType<Camera>();
            }

            if (playerCamera == null)
            {
                Debug.LogError("PlayerController找不到相机！");
                enabled = false;
                return;
            }

            cameraController = playerCamera.GetComponent<CameraController>();
        }

        private void Update()
        {
            DetectBugInCenter();

            // 空格键修复
            if (Input.GetKeyDown(KeyCode.Space))
            {
                TryFixBug();
            }

            // 鼠标左键也能修复
            if (Input.GetMouseButtonDown(0))
            {
                TryFixBug();
            }
        }

        private void DetectBugInCenter()
        {
            Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
            Ray ray = playerCamera.ScreenPointToRay(screenCenter);

            if (showClickDebug)
            {
                Debug.DrawRay(ray.origin, ray.direction * centerRayDistance, Color.yellow);
            }

            RaycastHit hit;
            currentDetectedBug = null;

            if (Physics.Raycast(ray, out hit, centerRayDistance, clickableLayerMask))
            {
                GameObject hitObj = hit.collider.gameObject;
                Debug.Log($"检测命中: {hitObj.name}");

                BugObject bug = hitObj.GetComponent<BugObject>();
                if (bug != null)
                {
                    Debug.Log($"命中的是 BugObject: {bug.name}（IsBugActive: {bug.IsBugActive()}）");
                    currentDetectedBug = bug;
                }
                else
                {
                    Debug.Log($"命中的物体没有挂 BugObject 脚本");
                }
            }
            else
            {
                Debug.Log("屏幕中心没有命中任何物体");
            }
        }

        private void TryFixBug()
        {
            if (currentDetectedBug != null && currentDetectedBug.IsBugActive())
            {
                Debug.Log($"尝试修复 Bug: {currentDetectedBug.name}");
                ProcessBugClick(currentDetectedBug);
            }
            else
            {
                Debug.Log("没有检测到可修复的 BugObject");
            }
        }

        private void ProcessBugClick(BugObject bugObject)
        {
            if (bugObject == null || !bugObject.IsBugActive()) return;

            Debug.Log($"玩家点击了Bug物体: {bugObject.name}");
            OnBugObjectClicked?.Invoke(bugObject);
            bugObject.OnClickedByPlayer();
        }

        #region 公共接口

        public Camera GetCamera() => playerCamera;
        public BugObject GetCurrentDetectedBug() => currentDetectedBug;

        public void SetClickableLayerMask(LayerMask layerMask)
        {
            clickableLayerMask = layerMask;
        }

        #endregion
    }
}
