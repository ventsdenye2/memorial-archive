using UnityEngine;

namespace MemorialArchive.Gameplay.Camera.View
{
    /// <summary>
    /// Smoothly follows the player while keeping the orthographic viewport inside
    /// the current scene's camera bounds.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class CameraFollowView : MonoBehaviour
    {
        private const string CameraBoundsObjectName = "CameraConfiner";

        [Header("Follow")]
        [SerializeField, Min(0.01f)] private float followSmoothTime = 0.35f;

        [Header("Scene Bounds Fallback")]
        [SerializeField, Min(0.01f)] private float segmentWidth = 38.4f;
        [SerializeField, Min(1)] private int defaultSegmentCount = 4;
        [SerializeField, Min(1)] private int floor4SegmentCount = 2;
        [SerializeField, Min(0.01f)] private float sceneHeight = 10.8f;
        [SerializeField, Min(0.01f)] private float referenceOrthographicSize = 5.4f;

        [Tooltip("Optional explicit scene bounds. When empty, all active CameraConfiner bounds in this scene are combined.")]
        [SerializeField] private Collider2D cameraBounds;

        private UnityEngine.Camera controlledCamera;
        private Bounds worldBounds;
        private Vector3 followVelocity;
        private float cachedAspect = -1f;
        private bool hasWorldBounds;
        private bool hasSnappedToTarget;

        public Transform Target { get; private set; }

        private void Awake()
        {
            controlledCamera = GetComponent<UnityEngine.Camera>();

            // This component is the single camera authority. Leaving a Brain active
            // would make Cinemachine overwrite the bounded position in LateUpdate.
            var brain = GetComponent<Cinemachine.CinemachineBrain>();
            if (brain != null)
            {
                brain.enabled = false;
            }

            ResolveWorldBounds();
            RefreshLensForAspect();
        }

        private void Start()
        {
            if (Target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                SetTarget(player != null ? player.transform : null);
            }
        }

        private void LateUpdate()
        {
            if (Target == null || controlledCamera == null)
            {
                return;
            }

            RefreshLensForAspect();

            var desiredPosition = GetClampedPosition(Target.position);
            if (!hasSnappedToTarget)
            {
                transform.position = desiredPosition;
                followVelocity = Vector3.zero;
                hasSnappedToTarget = true;
                return;
            }

            var nextPosition = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref followVelocity,
                followSmoothTime,
                Mathf.Infinity,
                Time.deltaTime);

            // Clamp after smoothing as well, so neither accumulated velocity nor a
            // runtime aspect-ratio change can reveal content outside the scene.
            transform.position = ClampCameraPosition(nextPosition);
        }

        public void SetTarget(Transform value)
        {
            if (Target == value)
            {
                return;
            }

            Target = value;
            followVelocity = Vector3.zero;
            hasSnappedToTarget = false;
        }

/// <summary>
/// Keeps an actor inside the same scene bounds used by the camera.
/// CameraFollowView remains the single authority for scene extents, while
/// callers provide their own collider so the actor's full shape stays in
/// the playable area (not only its transform pivot).
/// </summary>
public Vector2 ClampActorPosition(Vector2 position, Collider2D actor)
{
    if (!hasWorldBounds)
    {
        return position;
    }

    var extents = actor != null ? actor.bounds.extents : Vector3.zero;
    position.x = ClampAxis(
        position.x,
        worldBounds.min.x + extents.x,
        worldBounds.max.x - extents.x,
        worldBounds.center.x);
    position.y = ClampAxis(
        position.y,
        worldBounds.min.y + extents.y,
        worldBounds.max.y - extents.y,
        worldBounds.center.y);
    return position;
}


        private void ResolveWorldBounds()
        {
            if (cameraBounds == null)
            {
                var candidates = FindObjectsOfType<Collider2D>(true);
                Bounds combinedBounds = default;
                var foundBounds = false;
                foreach (var candidate in candidates)
                {
                    if (candidate == null ||
                        !candidate.isActiveAndEnabled ||
                        candidate.gameObject.scene != gameObject.scene ||
                        candidate.name != CameraBoundsObjectName)
                    {
                        continue;
                    }

                    var candidateBounds = candidate.bounds;
                    if (candidateBounds.size.sqrMagnitude <= 0f)
                    {
                        continue;
                    }

                    if (!foundBounds)
                    {
                        combinedBounds = candidateBounds;
                        foundBounds = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(candidateBounds);
                    }
                }

                if (foundBounds)
                {
                    worldBounds = combinedBounds;
                    hasWorldBounds = true;
                    return;
                }
            }

            if (cameraBounds != null && cameraBounds.bounds.size.sqrMagnitude > 0f)
            {
                worldBounds = cameraBounds.bounds;
                hasWorldBounds = true;
                return;
            }

            var segmentCount = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Floor_4F"
                ? floor4SegmentCount
                : defaultSegmentCount;
            var mapWidth = segmentWidth * segmentCount;
            worldBounds = new Bounds(
                new Vector3((mapWidth - segmentWidth) * 0.5f, 0f, 0f),
                new Vector3(mapWidth, sceneHeight, 0f));
            hasWorldBounds = true;
        }

        private void RefreshLensForAspect()
        {
            if (!hasWorldBounds || controlledCamera == null || controlledCamera.aspect <= 0f)
            {
                return;
            }

            if (Mathf.Approximately(cachedAspect, controlledCamera.aspect))
            {
                return;
            }

            cachedAspect = controlledCamera.aspect;
            var maxVerticalHalfSize = worldBounds.extents.y;
            var maxHorizontalHalfSize = worldBounds.extents.x / controlledCamera.aspect;
            controlledCamera.orthographicSize = Mathf.Min(
                referenceOrthographicSize,
                maxVerticalHalfSize,
                maxHorizontalHalfSize);

            // Re-clamp immediately after a window/resolution change.
            if (hasSnappedToTarget)
            {
                transform.position = ClampCameraPosition(transform.position);
            }
        }

        private Vector3 GetClampedPosition(Vector3 targetPosition)
        {
            targetPosition.z = transform.position.z;
            return ClampCameraPosition(targetPosition);
        }

        private Vector3 ClampCameraPosition(Vector3 position)
        {
            if (!hasWorldBounds || controlledCamera == null)
            {
                return position;
            }

            var verticalExtent = controlledCamera.orthographicSize;
            var horizontalExtent = verticalExtent * controlledCamera.aspect;
            position.x = ClampAxis(
                position.x,
                worldBounds.min.x + horizontalExtent,
                worldBounds.max.x - horizontalExtent,
                worldBounds.center.x);
            position.y = ClampAxis(
                position.y,
                worldBounds.min.y + verticalExtent,
                worldBounds.max.y - verticalExtent,
                worldBounds.center.y);
            position.z = transform.position.z;
            return position;
        }

        private static float ClampAxis(float value, float minimum, float maximum, float fallback)
        {
            return minimum <= maximum ? Mathf.Clamp(value, minimum, maximum) : fallback;
        }
    }
}
