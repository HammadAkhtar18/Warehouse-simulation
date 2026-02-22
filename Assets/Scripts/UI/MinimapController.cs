// MinimapController.cs - Overhead minimap display showing robot positions
// Uses a secondary camera to render a top-down view into a corner overlay.

using UnityEngine;
using WarehouseSimulation.Core;

namespace WarehouseSimulation.UI
{
    /// <summary>
    /// Creates and manages a minimap in the bottom-right corner.
    /// Uses a separate orthographic camera looking straight down at the warehouse.
    /// Robot positions appear as colored dots based on their current state.
    /// </summary>
    public class MinimapController : MonoBehaviour
    {
        [Header("Minimap Settings")]
        [SerializeField] private float minimapSize = 200f;  // Pixels
        [SerializeField] private float cameraHeight = 50f;
        [SerializeField] private float orthoSize = 25f;

        private Camera minimapCamera;
        private RenderTexture minimapTexture;

        private void Start()
        {
            CreateMinimapCamera();
        }

        /// <summary>
        /// Creates a dedicated overhead camera for the minimap.
        /// The camera renders to a RenderTexture which is displayed as a GUI overlay.
        /// </summary>
        private void CreateMinimapCamera()
        {
            // Create minimap camera
            GameObject camObj = new GameObject("MinimapCamera");
            camObj.transform.SetParent(transform);
            camObj.transform.position = new Vector3(
                WarehouseConstants.WarehouseWidth / 2f,
                cameraHeight,
                WarehouseConstants.WarehouseLength / 2f
            );
            camObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            minimapCamera = camObj.AddComponent<Camera>();
            minimapCamera.orthographic = true;
            minimapCamera.orthographicSize = orthoSize;
            minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            minimapCamera.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
            minimapCamera.depth = 10; // Render on top
            minimapCamera.cullingMask = ~0; // Render everything

            // Render to texture instead of screen
            minimapTexture = new RenderTexture(256, 256, 16);
            minimapCamera.targetTexture = minimapTexture;
        }

        private void OnGUI()
        {
            if (minimapTexture == null) return;

            float margin = 10f;
            Rect minimapRect = new Rect(
                Screen.width - minimapSize - margin,
                Screen.height - minimapSize - margin,
                minimapSize,
                minimapSize
            );

            // Draw border
            Rect borderRect = new Rect(
                minimapRect.x - 2, minimapRect.y - 2,
                minimapRect.width + 4, minimapRect.height + 4
            );
            GUI.DrawTexture(borderRect, Texture2D.whiteTexture);

            // Draw minimap texture
            GUI.DrawTexture(minimapRect, minimapTexture);
        }

        private void OnDestroy()
        {
            if (minimapTexture != null)
            {
                minimapTexture.Release();
            }
        }
    }
}
