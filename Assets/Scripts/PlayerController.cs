using UnityEngine;

namespace BugFixerGame
{
    public class PlayerController : MonoBehaviour
    {
        [Header("����������")]
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
                Debug.LogError("PlayerController�Ҳ��������");
                enabled = false;
                return;
            }

            cameraController = playerCamera.GetComponent<CameraController>();
        }

        private void Update()
        {
            DetectBugInCenter();

            // �ո���޸�
            if (Input.GetKeyDown(KeyCode.Space))
            {
                TryFixBug();
            }

            // ������Ҳ���޸�
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
                Debug.Log($"�������: {hitObj.name}");

                BugObject bug = hitObj.GetComponent<BugObject>();
                if (bug != null)
                {
                    Debug.Log($"���е��� BugObject: {bug.name}��IsBugActive: {bug.IsBugActive()}��");
                    currentDetectedBug = bug;
                }
                else
                {
                    Debug.Log($"���е�����û�й� BugObject �ű�");
                }
            }
            else
            {
                Debug.Log("��Ļ����û�������κ�����");
            }
        }

        private void TryFixBug()
        {
            if (currentDetectedBug != null && currentDetectedBug.IsBugActive())
            {
                Debug.Log($"�����޸� Bug: {currentDetectedBug.name}");
                ProcessBugClick(currentDetectedBug);
            }
            else
            {
                Debug.Log("û�м�⵽���޸��� BugObject");
            }
        }

        private void ProcessBugClick(BugObject bugObject)
        {
            if (bugObject == null || !bugObject.IsBugActive()) return;

            Debug.Log($"��ҵ����Bug����: {bugObject.name}");
            OnBugObjectClicked?.Invoke(bugObject);
            bugObject.OnClickedByPlayer();
        }

        #region �����ӿ�

        public Camera GetCamera() => playerCamera;
        public BugObject GetCurrentDetectedBug() => currentDetectedBug;

        public void SetClickableLayerMask(LayerMask layerMask)
        {
            clickableLayerMask = layerMask;
        }

        #endregion
    }
}
