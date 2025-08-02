// MessageTrigger.cs
using UnityEngine;

namespace BugFixerGame
{
    public enum TriggerMode { OnEnter, OnExit, OnEnterAndExit, OnStay }

    [RequireComponent(typeof(Collider))]
    public class MessageTrigger : MonoBehaviour
    {
        [Header("Trigger Settings")]
        [SerializeField] private TriggerMode triggerMode = TriggerMode.OnEnter;
        [SerializeField] private string targetTag = "Player";
        [SerializeField] private bool requirePlayerComponent = true;

        [Header("Room ID (optional)")]
        [SerializeField] private string roomId = "";

        [Header("Message Content")]
        [SerializeField] private string messageTitle = "Title";
        [SerializeField] private string messageDescription = "Description";
        [SerializeField] private float displayTime = 5f;

        public string MessageTitle => messageTitle;
        public string MessageDescription => messageDescription;
        public float DisplayTime => displayTime;
        public string RoomId => roomId;

        public static event System.Action<MessageTrigger, GameObject> OnTriggerActivated;

        private Collider col;
        private bool hasTriggered = false;

        private void Awake()
        {
            col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(targetTag)) return;
            if (requirePlayerComponent && other.GetComponent<Player>() == null) return;
            if (!hasTriggered || triggerMode == TriggerMode.OnEnterAndExit)
            {
                OnTriggerActivated?.Invoke(this, other.gameObject);
                hasTriggered = true;
            }
        }
    }
}
