// MetricsDisplay.cs - Formats and renders performance metrics as on-screen text
// Lightweight HUD-style display using Unity's legacy GUI for immediate results.

using UnityEngine;
using WarehouseSimulation.Core;
using WarehouseSimulation.Managers;

namespace WarehouseSimulation.UI
{
    /// <summary>
    /// Draws a real-time metrics HUD in the top-right corner of the screen.
    /// Uses Unity's IMGUI system for simplicity and reliability.
    /// 
    /// Displayed metrics:
    /// - Orders completed (total + per hour estimate)
    /// - Average delivery time
    /// - Robot utilization %
    /// - Collision count
    /// - Throughput (tasks/min)
    /// - Queue size
    /// - Simulation uptime
    /// </summary>
    public class MetricsDisplay : MonoBehaviour
    {
        [Header("Display Settings")]
        [SerializeField] private bool showMetrics = true;
        [SerializeField] private int fontSize = 14;

        private PerformanceTracker tracker;
        private GUIStyle headerStyle;
        private GUIStyle valueStyle;
        private GUIStyle boxStyle;
        private bool stylesInitialized;

        public void SetTracker(PerformanceTracker pt)
        {
            tracker = pt;
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize + 2,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.95f, 1f) }
            };

            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                normal = { textColor = new Color(0.8f, 0.9f, 1f) }
            };

            boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(2, 2, new Color(0.1f, 0.12f, 0.18f, 0.85f)) }
            };

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (!showMetrics || tracker == null) return;

            InitStyles();

            float width = 280f;
            float height = 300f;
            float margin = 10f;

            Rect panelRect = new Rect(Screen.width - width - margin, margin, width, height);

            GUI.Box(panelRect, "", boxStyle);

            GUILayout.BeginArea(new Rect(panelRect.x + 10, panelRect.y + 10,
                                         panelRect.width - 20, panelRect.height - 20));

            GUILayout.Label("📊 PERFORMANCE DASHBOARD", headerStyle);
            GUILayout.Space(5);

            DrawMetric("Orders Completed", tracker.OrdersCompleted.ToString());
            DrawMetric("Restocks Completed", tracker.RestocksCompleted.ToString());
            DrawMetric("Avg Delivery Time", $"{tracker.AverageDeliveryTime:F1}s");
            DrawMetric("Robot Utilization", $"{tracker.RobotUtilization:F0}%");
            DrawMetric("Collisions", tracker.TotalCollisions.ToString());
            DrawMetric("Throughput", $"{tracker.Throughput:F1} tasks/min");
            DrawMetric("Queue Size", tracker.TasksInQueue.ToString());

            GUILayout.Space(5);

            // Uptime formatting
            float uptime = tracker.SimulationUptime;
            int minutes = (int)(uptime / 60f);
            int seconds = (int)(uptime % 60f);
            DrawMetric("Uptime", $"{minutes:D2}:{seconds:D2}");

            GUILayout.EndArea();
        }

        private void DrawMetric(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ":", valueStyle, GUILayout.Width(160));
            GUILayout.Label(value, valueStyle);
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Creates a solid-color texture for GUI backgrounds.
        /// </summary>
        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;

            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
