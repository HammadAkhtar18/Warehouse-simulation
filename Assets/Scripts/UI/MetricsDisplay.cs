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
        [SerializeField] private int fontSize = 13;

        private PerformanceTracker tracker;
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle labelStyle;
        private GUIStyle valueStyle;
        private GUIStyle goodValueStyle;
        private GUIStyle warnValueStyle;
        private GUIStyle badValueStyle;
        private GUIStyle boxStyle;
        private GUIStyle separatorStyle;
        private bool stylesInitialized;

        public void SetTracker(PerformanceTracker pt)
        {
            tracker = pt;
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

            // Glassmorphism-style background
            boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeGradientTexture(2, 64,
                    new Color(0.08f, 0.10f, 0.16f, 0.92f),
                    new Color(0.06f, 0.08f, 0.14f, 0.88f)) }
            };

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize + 4,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.4f, 0.85f, 1f) }
            };

            subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize + 1,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.7f, 0.8f, 0.95f) }
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                normal = { textColor = new Color(0.65f, 0.72f, 0.82f) }
            };

            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.9f, 0.95f, 1f) }
            };

            goodValueStyle = new GUIStyle(valueStyle)
            {
                normal = { textColor = new Color(0.3f, 0.9f, 0.4f) }
            };

            warnValueStyle = new GUIStyle(valueStyle)
            {
                normal = { textColor = new Color(1f, 0.8f, 0.2f) }
            };

            badValueStyle = new GUIStyle(valueStyle)
            {
                normal = { textColor = new Color(1f, 0.35f, 0.3f) }
            };

            separatorStyle = new GUIStyle()
            {
                normal = { background = MakeTexture(2, 2, new Color(0.3f, 0.5f, 0.8f, 0.3f)) },
                fixedHeight = 1,
                margin = new RectOffset(0, 0, 4, 4)
            };

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (!showMetrics || tracker == null) return;

            InitStyles();

            float width = 290f;
            float height = 380f;
            float margin = 10f;

            Rect panelRect = new Rect(Screen.width - width - margin, margin, width, height);

            GUI.Box(panelRect, "", boxStyle);

            GUILayout.BeginArea(new Rect(panelRect.x + 12, panelRect.y + 8,
                                         panelRect.width - 24, panelRect.height - 16));

            GUILayout.Label("PERFORMANCE DASHBOARD", headerStyle);
            GUILayout.Box("", separatorStyle, GUILayout.ExpandWidth(true));
            GUILayout.Space(2);

            // Operations section
            GUILayout.Label("Operations", subHeaderStyle);
            DrawMetric("Orders Completed", tracker.OrdersCompleted.ToString(),
                tracker.OrdersCompleted > 0 ? goodValueStyle : valueStyle);
            DrawMetric("Restocks Completed", tracker.RestocksCompleted.ToString(),
                tracker.RestocksCompleted > 0 ? goodValueStyle : valueStyle);
            DrawMetric("Avg Delivery Time", $"{tracker.AverageDeliveryTime:F1}s",
                tracker.AverageDeliveryTime < 15f ? goodValueStyle :
                tracker.AverageDeliveryTime < 30f ? warnValueStyle : badValueStyle);
            DrawMetric("Throughput", $"{tracker.Throughput:F1} tasks/min",
                tracker.Throughput > 1f ? goodValueStyle : valueStyle);

            GUILayout.Box("", separatorStyle, GUILayout.ExpandWidth(true));

            // Robot section
            GUILayout.Label("Robots", subHeaderStyle);
            DrawMetric("Utilization", $"{tracker.RobotUtilization:F0}%",
                tracker.RobotUtilization > 70f ? goodValueStyle :
                tracker.RobotUtilization > 40f ? warnValueStyle : badValueStyle);
            DrawMetric("Collisions", tracker.TotalCollisions.ToString(),
                tracker.TotalCollisions == 0 ? goodValueStyle :
                tracker.TotalCollisions < 10 ? warnValueStyle : badValueStyle);
            DrawMetric("Queue Size", tracker.TasksInQueue.ToString(), valueStyle);

            GUILayout.Box("", separatorStyle, GUILayout.ExpandWidth(true));

            // ML Training section
            GUILayout.Label("ML Training", subHeaderStyle);
            float uptime = tracker.SimulationUptime;
            int minutes = (int)(uptime / 60f);
            int seconds = (int)(uptime % 60f);
            DrawMetric("Episode Time", $"{minutes:D2}:{seconds:D2}", valueStyle);
            DrawMetric("Total Tasks Done",
                (tracker.OrdersCompleted + tracker.RestocksCompleted).ToString(),
                (tracker.OrdersCompleted + tracker.RestocksCompleted) > 0 ? goodValueStyle : valueStyle);

            GUILayout.EndArea();
        }

        private void DrawMetric(string label, string value, GUIStyle style)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ":", labelStyle, GUILayout.Width(155));
            GUILayout.Label(value, style);
            GUILayout.EndHorizontal();
        }

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

        private Texture2D MakeGradientTexture(int width, int height, Color topColor, Color bottomColor)
        {
            Texture2D texture = new Texture2D(width, height);
            for (int y = 0; y < height; y++)
            {
                Color c = Color.Lerp(bottomColor, topColor, (float)y / height);
                for (int x = 0; x < width; x++)
                    texture.SetPixel(x, y, c);
            }
            texture.Apply();
            return texture;
        }
    }
}
