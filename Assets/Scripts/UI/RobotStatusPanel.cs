// RobotStatusPanel.cs - Displays status table for all robots in the simulation
// Shows each robot's current state, tasks completed, and current assignment.

using UnityEngine;
using WarehouseSimulation.Core;
using WarehouseSimulation.Agents;
using WarehouseSimulation.Managers;

namespace WarehouseSimulation.UI
{
    /// <summary>
    /// Draws a robot status table in the bottom-left corner.
    /// Each row shows a robot's index, current state, tasks completed, and assignment.
    /// </summary>
    public class RobotStatusPanel : MonoBehaviour
    {
        [Header("Display Settings")]
        [SerializeField] private bool showPanel = true;
        [SerializeField] private int fontSize = 12;

        private RobotCoordinator coordinator;
        private GUIStyle headerStyle;
        private GUIStyle cellStyle;
        private GUIStyle boxStyle;
        private bool stylesInitialized;

        public void SetCoordinator(RobotCoordinator rc)
        {
            coordinator = rc;
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.95f, 1f) },
                alignment = TextAnchor.MiddleCenter
            };

            cellStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize - 1,
                normal = { textColor = new Color(0.8f, 0.85f, 0.9f) },
                alignment = TextAnchor.MiddleCenter
            };

            boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(2, 2, new Color(0.1f, 0.12f, 0.18f, 0.85f)) }
            };

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (!showPanel || coordinator == null || coordinator.RobotCount == 0) return;

            InitStyles();

            float width = 420f;
            float rowHeight = 22f;
            float headerHeight = 30f;
            float tableHeight = headerHeight + (coordinator.RobotCount * rowHeight) + 40f;
            float margin = 10f;

            Rect panelRect = new Rect(margin, Screen.height - tableHeight - margin, width, tableHeight);
            GUI.Box(panelRect, "", boxStyle);

            float x = panelRect.x + 10;
            float y = panelRect.y + 10;

            GUI.Label(new Rect(x, y, width - 20, 20), "🤖 ROBOT STATUS", headerStyle);
            y += 25f;

            // Table header
            float[] colWidths = { 50f, 80f, 80f, 190f };
            string[] headers = { "ID", "State", "Tasks", "Assignment" };

            for (int c = 0; c < headers.Length; c++)
            {
                float colX = x;
                for (int j = 0; j < c; j++) colX += colWidths[j];
                GUI.Label(new Rect(colX, y, colWidths[c], rowHeight), headers[c], headerStyle);
            }
            y += rowHeight;

            // Robot rows
            foreach (var robot in coordinator.Robots)
            {
                if (robot == null) continue;

                string stateStr = robot.CurrentState.ToString();
                string taskStr = robot.TasksCompleted.ToString();
                string assignStr = robot.CurrentTask != null
                    ? $"{robot.CurrentTask.Type} #{robot.CurrentTask.TaskId}"
                    : "—";

                // Color the state text
                Color stateColor = GetStateColor(robot.CurrentState);
                GUIStyle coloredCell = new GUIStyle(cellStyle);
                coloredCell.normal.textColor = stateColor;

                float colX = x;
                GUI.Label(new Rect(colX, y, colWidths[0], rowHeight),
                    $"R{robot.RobotIndex}", cellStyle);
                colX += colWidths[0];

                GUI.Label(new Rect(colX, y, colWidths[1], rowHeight),
                    stateStr, coloredCell);
                colX += colWidths[1];

                GUI.Label(new Rect(colX, y, colWidths[2], rowHeight),
                    taskStr, cellStyle);
                colX += colWidths[2];

                GUI.Label(new Rect(colX, y, colWidths[3], rowHeight),
                    assignStr, cellStyle);

                y += rowHeight;
            }
        }

        private Color GetStateColor(RobotState state)
        {
            switch (state)
            {
                case RobotState.Idle: return Color.green;
                case RobotState.Moving: return new Color(0.4f, 0.7f, 1f);
                case RobotState.Picking: return Color.yellow;
                case RobotState.Delivering: return new Color(1f, 0.6f, 0.2f);
                case RobotState.Restocking: return new Color(1f, 0.6f, 0.2f);
                case RobotState.Error: return Color.red;
                default: return Color.white;
            }
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
    }
}
