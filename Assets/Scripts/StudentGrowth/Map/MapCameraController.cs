using UnityEngine;

namespace StudentGrowth
{
    [DisallowMultipleComponent]
    public sealed class MapCameraController : MonoBehaviour
    {
        public Camera targetCamera;
        public Transform followTarget;
        public Collider2D cameraBounds;
        public bool useExplicitBounds;
        public Vector2 minPosition = new Vector2(-3.2f, -1.8f);
        public Vector2 maxPosition = new Vector2(3.2f, 1.8f);
        public bool followEnabled = true;
        public float smoothTime = 0.08f;

        private Vector3 smoothVelocity;

        private void Awake()
        {
            EnsureCamera();
        }

        private void LateUpdate()
        {
            if (!followEnabled || followTarget == null)
            {
                return;
            }

            EnsureCamera();
            Vector3 desiredPosition = new Vector3(
                followTarget.position.x,
                followTarget.position.y,
                transform.position.z);
            Vector3 nextPosition = smoothTime <= 0f
                ? desiredPosition
                : Vector3.SmoothDamp(transform.position, desiredPosition, ref smoothVelocity, smoothTime);
            transform.position = ClampToBounds(nextPosition);
        }

        public void Configure(Transform target, Collider2D bounds)
        {
            followTarget = target;
            cameraBounds = bounds;
            SnapToTarget();
        }

        public void SetExplicitBounds(Vector2 min, Vector2 max)
        {
            useExplicitBounds = true;
            minPosition = min;
            maxPosition = max;
        }

        public void SnapToTarget()
        {
            if (followTarget == null)
            {
                return;
            }

            transform.position = ClampToBounds(new Vector3(
                followTarget.position.x,
                followTarget.position.y,
                transform.position.z));
            smoothVelocity = Vector3.zero;
        }

        public Vector3 ClampToBounds(Vector3 position)
        {
            EnsureCamera();
            if (targetCamera == null || !targetCamera.orthographic)
            {
                return position;
            }

            Bounds bounds;
            if (cameraBounds != null)
            {
                bounds = cameraBounds.bounds;
            }
            else if (useExplicitBounds)
            {
                bounds = new Bounds();
                bounds.SetMinMax(minPosition, maxPosition);
            }
            else
            {
                return position;
            }

            float halfHeight = targetCamera.orthographicSize;
            float halfWidth = halfHeight * targetCamera.aspect;
            float minX = bounds.min.x + halfWidth;
            float maxX = bounds.max.x - halfWidth;
            float minY = bounds.min.y + halfHeight;
            float maxY = bounds.max.y - halfHeight;

            if (minX > maxX)
            {
                position.x = bounds.center.x;
            }
            else
            {
                position.x = Mathf.Clamp(position.x, minX, maxX);
            }

            if (minY > maxY)
            {
                position.y = bounds.center.y;
            }
            else
            {
                position.y = Mathf.Clamp(position.y, minY, maxY);
            }

            return position;
        }

        private void EnsureCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            if (targetCamera != null)
            {
                targetCamera.orthographic = true;
            }
        }
    }
}
