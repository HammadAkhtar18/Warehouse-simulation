// UIManager.cs - Central UI controller for controls panel and dashboard coordination
// Manages simulation controls (Start/Pause/Reset, time scale, robot count) via IMGUI.

using UnityEngine;
using WarehouseSimulation.Core;
using WarehouseSimulation.Managers;

namespace WarehouseSimulation.UI
{
    /// <summary>
    /// Central UI manager that coordinates all dashboard elements and simulation controls.
    /// 
    /// Controls panel (top-left):
    /// - Start/Pause/Reset buttons
    /// - Time scale slider (0.5x - 4x)
    /// - Order rate slider
    /// - Robot count slider
    /// 
    /// Also initializes and connects:
    /// - MetricsDisplay (top-right)
    /// - RobotStatusPanel (bottom-left)
    /// - MinimapController (bottom-right)
    /// - PerformanceTracker (data source)
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        private GameManager gameManager;
        private MetricsDisplay metricsDisplay;
        private RobotStatusPanel robotStatusPanel;
        private MinimapController minimapController;

        // Control panel state
        private float timeScaleSlider = 1f;
        private float orderRateSlider = WarehouseConstants.DefaultOrderInterval;
        private float robotCountSlider = WarehouseConstants.DefaultRobotCount;
        private bool showLearningGraphs = false;

        // GUI styles
        private GUIStyle buttonStyle;
        private GUIStyle labelStyle;
        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private GUIStyle graphBoxStyle;
        private bool stylesInitialized;

        // ──────────────────────────────────────────────
        // INITIALIZATION
        // ──────────────────────────────────────────────

        /// <summary>
        /// Initialize with GameManager reference and set up UI components.
        /// </summary>
        public void Initialize(GameManager gm)
        {
            gameManager = gm;

            // Create or find UI sub-components
            metricsDisplay = gameObject.GetComponent<MetricsDisplay>() ?? gameObject.AddComponent<MetricsDisplay>();
            robotStatusPanel = gameObject.GetComponent<RobotStatusPanel>() ?? gameObject.AddComponent<RobotStatusPanel>();
            minimapController = gameObject.GetComponent<MinimapController>() ?? gameObject.AddComponent<MinimapController>();

            // Connect data sources
            if (gm.PerformanceTracker != null)
                metricsDisplay.SetTracker(gm.PerformanceTracker);
            
            if (gm.RobotCoordinator != null)
                robotStatusPanel.SetCoordinator(gm.RobotCoordinator);

            timeScaleSlider = gm.TimeScale;
            robotCountSlider = gm.RobotCount;

            Debug.Log("[UIManager] Initialized");
        }

        // ──────────────────────────────────────────────
        // GUI RENDERING
        // ──────────────────────────────────────────────

        private void InitStyles()
        {
            if (stylesInitialized) return;

            // Glassmorphism-style panel background
            boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeGradientTexture(2, 64,
                    new Color(0.08f, 0.10f, 0.16f, 0.92f),
                    new Color(0.06f, 0.08f, 0.14f, 0.88f)) }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 32,
                normal = {
                    textColor = Color.white,
                    background = MakeTexture(2, 2, new Color(0.15f, 0.35f, 0.65f, 0.9f))
                },
                hover = {
                    textColor = new Color(0.7f, 0.95f, 1f),
                    background = MakeTexture(2, 2, new Color(0.2f, 0.45f, 0.8f, 0.95f))
                },
                active = {
                    textColor = Color.white,
                    background = MakeTexture(2, 2, new Color(0.1f, 0.25f, 0.5f, 1f))
                }
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.75f, 0.82f, 0.95f) }
            };

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.4f, 0.85f, 1f) }
            };

            graphBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(2, 2, new Color(0.05f, 0.08f, 0.12f, 0.9f)) }
            };

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (gameManager == null) return;

            InitStyles();

            DrawControlPanel();

            if (showLearningGraphs)
            {
                DrawLearningGraphs();
            }
        }

        /// <summary>
        /// Draws the control panel on the top-left of the screen.
        /// Contains simulation controls and parameter sliders.
        /// </summary>
        private void DrawControlPanel()
        {
            float width = 260f;
            float height = 340f;
            float margin = 10f;

            Rect panelRect = new Rect(margin, margin, width, height);
            GUI.Box(panelRect, "", boxStyle);

            GUILayout.BeginArea(new Rect(panelRect.x + 15, panelRect.y + 10,
                                         panelRect.width - 30, panelRect.height - 20));

            GUILayout.Label("⚙️ SIMULATION CONTROLS", headerStyle);
            GUILayout.Space(10);

            // ── Start/Pause/Reset Buttons ──
            GUILayout.BeginHorizontal();

            string pauseText = gameManager.IsRunning ? "⏸ Pause" : "▶️ Resume";
            if (GUILayout.Button(pauseText, buttonStyle))
            {
                gameManager.TogglePause();
            }

            if (GUILayout.Button("🔄 Reset", buttonStyle))
            {
                gameManager.ResetSimulation();
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            // ── Time Scale Slider ──
            GUILayout.Label($"Time Scale: {timeScaleSlider:F1}x", labelStyle);
            float newTimeScale = GUILayout.HorizontalSlider(timeScaleSlider, 0.5f, 4f);
            if (Mathf.Abs(newTimeScale - timeScaleSlider) > 0.05f)
            {
                timeScaleSlider = newTimeScale;
                gameManager.SetTimeScale(timeScaleSlider);
            }
            GUILayout.Space(8);

            // ── Order Rate Slider ──
            GUILayout.Label($"Order Interval: {orderRateSlider:F1}s", labelStyle);
            float newRate = GUILayout.HorizontalSlider(orderRateSlider,
                WarehouseConstants.MinOrderInterval, WarehouseConstants.MaxOrderInterval);
            if (Mathf.Abs(newRate - orderRateSlider) > 0.1f)
            {
                orderRateSlider = newRate;
                if (gameManager.TaskManager != null)
                    gameManager.TaskManager.OrderInterval = orderRateSlider;
            }
            GUILayout.Space(8);

            // ── Robot Count Slider ──
            GUILayout.Label($"Robots: {(int)robotCountSlider}", labelStyle);
            float newCount = GUILayout.HorizontalSlider(robotCountSlider,
                WarehouseConstants.MinRobots, WarehouseConstants.MaxRobots);
            int newCountInt = Mathf.RoundToInt(newCount);
            if (newCountInt != (int)robotCountSlider)
            {
                robotCountSlider = newCountInt;
                gameManager.SetRobotCount(newCountInt);
            }
            GUILayout.Space(10);

            // ── Learning Graphs Toggle ──
            if (GUILayout.Button(showLearningGraphs ? "📈 Hide Graphs" : "📈 Show Graphs", buttonStyle))
            {
                showLearningGraphs = !showLearningGraphs;
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// Draws simple learning progress graphs using IMGUI line drawing.
        /// Shows: Cumulative Reward, Completion Time, Collision Rate, Throughput.
        /// </summary>
        private void DrawLearningGraphs()
        {
            if (gameManager.PerformanceTracker == null) return;

            float graphWidth = 300f;
            float graphHeight = 120f;
            float margin = 10f;
            float startX = 280f;
            float startY = margin;

            var tracker = gameManager.PerformanceTracker;

            // Draw 4 graphs vertically
            DrawGraph("Cumulative Reward", tracker.RewardHistory,
                new Rect(startX, startY, graphWidth, graphHeight), Color.green);
            startY += graphHeight + margin;

            DrawGraph("Avg Completion Time (s)", tracker.CompletionTimeHistory,
                new Rect(startX, startY, graphWidth, graphHeight), Color.yellow);
            startY += graphHeight + margin;

            DrawGraph("Collision Rate (/min)", tracker.CollisionRateHistory,
                new Rect(startX, startY, graphWidth, graphHeight), Color.red);
            startY += graphHeight + margin;

            DrawGraph("Throughput (tasks/min)", tracker.ThroughputHistory,
                new Rect(startX, startY, graphWidth, graphHeight), Color.cyan);
        }

        /// <summary>
        /// Draws a simple line graph within the given rect.
        /// Uses GL.Lines for direct rendering of data points.
        /// </summary>
        private void DrawGraph(string title, System.Collections.Generic.List<Vector2> data, Rect rect, Color lineColor)
        {
            GUI.Box(rect, "", graphBoxStyle);

            // Title
            GUI.Label(new Rect(rect.x + 5, rect.y + 2, rect.width, 20f),
                title, new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = lineColor }
                });

            if (data == null || data.Count < 2) return;

            // Calculate value range for normalization
            float minVal = float.MaxValue;
            float maxVal = float.MinValue;
            foreach (var point in data)
            {
                if (point.y < minVal) minVal = point.y;
                if (point.y > maxVal) maxVal = point.y;
            }

            float range = maxVal - minVal;
            if (range < 0.01f) range = 1f;

            // Draw line graph
            float graphLeft = rect.x + 5f;
            float graphRight = rect.x + rect.width - 5f;
            float graphTop = rect.y + 20f;
            float graphBottom = rect.y + rect.height - 5f;
            float graphW = graphRight - graphLeft;
            float graphH = graphBottom - graphTop;

            // Draw current value text
            if (data.Count > 0)
            {
                string valueStr = data[data.Count - 1].y.ToString("F1");
                GUI.Label(new Rect(rect.x + rect.width - 60, rect.y + 2, 55, 20),
                    valueStr, new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 11,
                        alignment = TextAnchor.MiddleRight,
                        normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
                    });
            }

            // Draw data points as connected dots using GUI
            for (int i = 1; i < data.Count; i++)
            {
                float x1 = graphLeft + ((float)(i - 1) / (data.Count - 1)) * graphW;
                float y1 = graphBottom - ((data[i - 1].y - minVal) / range) * graphH;
                float x2 = graphLeft + ((float)i / (data.Count - 1)) * graphW;
                float y2 = graphBottom - ((data[i].y - minVal) / range) * graphH;

                // Draw a line segment using a thin rect
                DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), lineColor);
            }
        }

        /// <summary>
        /// Draws a line between two GUI points using a rotated texture.
        /// </summary>
        private void DrawLine(Vector2 a, Vector2 b, Color color)
        {
            float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            float length = Vector2.Distance(a, b);

            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - 1, length, 2), MakeColorTexture(color));
            GUI.matrix = Matrix4x4.identity; // Reset rotation
        }

        // ──────────────────────────────────────────────
        // UTILITY
        // ──────────────────────────────────────────────

        private Texture2D cachedColorTexture;
        private Color cachedColor;

        private Texture2D MakeColorTexture(Color color)
        {
            if (cachedColorTexture != null && cachedColor == color)
                return cachedColorTexture;

            cachedColorTexture = MakeTexture(1, 1, color);
            cachedColor = color;
            return cachedColorTexture;
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
