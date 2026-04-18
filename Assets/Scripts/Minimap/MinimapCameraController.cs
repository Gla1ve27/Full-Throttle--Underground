using UnityEngine;

namespace FullThrottle.Minimap
{
    /// <summary>
    /// Follows a target from above using an orthographic camera.
    /// Put this on the minimap camera.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class MinimapCameraController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Follow")]
        [SerializeField] private float height = 80f;
        [SerializeField] private Vector3 worldOffset = Vector3.zero;
        [SerializeField] private bool rotateWithTarget = true;
        [SerializeField] private bool smoothFollow = true;
        [SerializeField] private float followLerpSpeed = 12f;

        [Header("View")]
        [SerializeField] private float orthographicSize = 50f;

        private Camera cam;

        public Transform Target => target;
        public bool RotateWithTarget => rotateWithTarget;
        public float Height => height;
        public float OrthoSize => orthographicSize;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthographicSize;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void SetRotateWithTarget(bool value)
        {
            rotateWithTarget = value;
        }

        public void SetOrthographicSize(float value)
        {
            orthographicSize = Mathf.Max(1f, value);
            if (cam == null)
            {
                cam = GetComponent<Camera>();
            }

            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = orthographicSize;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desiredPosition = target.position + worldOffset + Vector3.up * height;

            if (smoothFollow)
            {
                transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * followLerpSpeed);
            }
            else
            {
                transform.position = desiredPosition;
            }

            if (rotateWithTarget)
            {
                transform.rotation = Quaternion.Euler(90f, target.eulerAngles.y, 0f);
            }
            else
            {
                transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = orthographicSize;
            }
        }
#endif
    }
}
