using UnityEngine;

namespace MemorialArchive.Gameplay.Camera.View
{
    public sealed class CameraFollowView : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);
        [SerializeField] private float followSpeed = 8f;

        public Transform Target => target;

        private void Start()
        {
            if (target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                target = player != null ? player.transform : null;
            }
        }

        public void SetTarget(Transform value) => target = value;

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            var desiredPosition = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * followSpeed);
        }
    }
}
