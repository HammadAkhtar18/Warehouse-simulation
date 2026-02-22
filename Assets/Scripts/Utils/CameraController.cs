// CameraController.cs - Isometric/top-down camera with pan, zoom, and rotate controls
// WASD: Pan, Scroll: Zoom, Right-drag: Rotate

using UnityEngine;

namespace WarehouseSimulation.Utils
{
    /// <summary>
    /// Camera controller for the warehouse simulation.
    /// Provides an isometric/top-down view with intuitive navigation controls.
    /// 
    /// Controls:
    /// - WASD / Arrow Keys: Pan the camera
    /// - Mouse Scroll: Zoom in/out
    /// - Right Mouse Button + Drag: Rotate view
    /// - Middle Mouse Button + Drag: Pan (alternative)
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Pan Settings")]
        [SerializeField] private float panSpeed = 20f;
        [SerializeField] private float panBorderThickness = 10f;  // Edge scrolling border (pixels)

        [Header("Zoom Settings")]
        [SerializeField] private float zoomSpeed = 10f;
        [SerializeField] private float minZoom = 10f;
        [SerializeField] private float maxZoom = 60f;

        [Header("Rotation Settings")]
        [SerializeField] private float rotationSpeed = 100f;

        [Header("Bounds")]
        [SerializeField] private float minX = -5f;
        [SerializeField] private float maxX = 50f;
        [SerializeField] private float minZ = -5f;
        [SerializeField] private float maxZ = 40f;

        private float currentZoom;
        private float currentRotationY;
        private Vector3 lastMousePos;

        private void Start()
        {
            // Set initial camera position and angle for isometric view
            if (Camera.main != null)
            {
                transform.position = new Vector3(20f, 30f, 0f);
                transform.rotation = Quaternion.Euler(60f, 0f, 0f);
                currentZoom = 30f;
                currentRotationY = 0f;
            }
        }

        private void LateUpdate()
        {
            HandlePan();
            HandleZoom();
            HandleRotation();
            ClampPosition();
        }

        /// <summary>
        /// WASD/Arrow key panning. Moves the camera along the XZ plane
        /// relative to its current facing direction.
        /// </summary>
        private void HandlePan()
        {
            Vector3 direction = Vector3.zero;

            // Keyboard input
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                direction += transform.forward;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                direction -= transform.forward;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                direction -= transform.right;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                direction += transform.right;

            // Middle mouse button drag panning
            if (Input.GetMouseButton(2))
            {
                Vector3 delta = Input.mousePosition - lastMousePos;
                direction -= transform.right * delta.x * 0.02f;
                direction -= transform.forward * delta.y * 0.02f;
            }

            // Project direction onto XZ plane
            direction.y = 0;
            direction.Normalize();

            // Apply movement
            float adjustedSpeed = panSpeed * (currentZoom / 30f); // Scale speed with zoom
            transform.position += direction * adjustedSpeed * Time.unscaledDeltaTime;

            lastMousePos = Input.mousePosition;
        }

        /// <summary>
        /// Mouse scroll wheel zoom. Adjusts the camera's Y position
        /// and forward offset to zoom in/out while maintaining angle.
        /// </summary>
        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                currentZoom -= scroll * zoomSpeed * 3f;
                currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);

                // Adjust camera height based on zoom
                Vector3 pos = transform.position;
                pos.y = currentZoom;
                transform.position = pos;
            }
        }

        /// <summary>
        /// Right-click drag rotation. Rotates the camera around the Y axis
        /// to orbit the view around the warehouse.
        /// </summary>
        private void HandleRotation()
        {
            if (Input.GetMouseButton(1)) // Right mouse button
            {
                float mouseX = Input.GetAxis("Mouse X");
                currentRotationY += mouseX * rotationSpeed * Time.unscaledDeltaTime;
                
                // Apply rotation while maintaining the downward angle
                transform.rotation = Quaternion.Euler(60f, currentRotationY, 0f);
            }
        }

        /// <summary>
        /// Prevents the camera from moving outside the warehouse bounds.
        /// </summary>
        private void ClampPosition()
        {
            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.z = Mathf.Clamp(pos.z, minZ, maxZ);
            pos.y = Mathf.Clamp(pos.y, minZoom, maxZoom);
            transform.position = pos;
        }
    }
}
