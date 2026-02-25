// RobotVisuals.cs - Visual feedback system for robot state
// Manages color-coded status lights, carrying indicators, path lines, and completion particles.

using UnityEngine;
using WarehouseSimulation.Core;

namespace WarehouseSimulation.Agents
{
    /// <summary>
    /// Handles all visual feedback for a robot:
    /// - Status light color changes based on robot state
    /// - Carrying indicator visibility when transporting items
    /// - Path line renderer showing planned route
    /// - Task completion particle effects
    /// 
    /// Color scheme:
    /// ┌──────────────┬───────────────────┐
    /// │ State        │ Color             │
    /// ├──────────────┼───────────────────┤
    /// │ Idle         │ Green             │
    /// │ Moving       │ Blue              │
    /// │ Picking      │ Yellow            │
    /// │ Delivering   │ Orange            │
    /// │ Restocking   │ Orange            │
    /// │ Error        │ Red               │
    /// └──────────────┴───────────────────┘
    /// </summary>
    public class RobotVisuals : MonoBehaviour
    {
        [Header("Visual Components")]
        [SerializeField] private Renderer statusLightRenderer;
        [SerializeField] private GameObject carryIndicator;
        [SerializeField] private LineRenderer pathLine;

        private Material statusMaterial;
        private Material carryMaterial;
        private RobotState lastState;

        // ──────────────────────────────────────────────
        // INITIALIZATION
        // ──────────────────────────────────────────────

        private void Awake()
        {
            FindVisualComponents();
        }

        private void Start()
        {
            InitializeMaterials();
            UpdateState(RobotState.Idle, false);
        }

        /// <summary>
        /// Finds child visual components by name convention.
        /// </summary>
        private void FindVisualComponents()
        {
            // Find status light (child sphere)
            Transform statusLight = transform.Find("StatusLight");
            if (statusLight != null)
            {
                statusLightRenderer = statusLight.GetComponent<Renderer>();
            }

            // Find carry indicator
            Transform carryObj = transform.Find("CarryIndicator");
            if (carryObj != null)
            {
                carryIndicator = carryObj.gameObject;
            }

            // Setup path line renderer
            SetupPathLine();
        }

        private void InitializeMaterials()
        {
            if (statusLightRenderer != null)
            {
                // Clone the renderer's existing material (already has correct URP shader)
                statusMaterial = new Material(statusLightRenderer.sharedMaterial);
                statusMaterial.EnableKeyword("_EMISSION");
                if (statusMaterial.HasProperty("_Metallic")) statusMaterial.SetFloat("_Metallic", 0.3f);
                if (statusMaterial.HasProperty("_Smoothness")) statusMaterial.SetFloat("_Smoothness", 0.8f);
                statusLightRenderer.material = statusMaterial;
            }

            if (carryIndicator != null)
            {
                Renderer carryRend = carryIndicator.GetComponent<Renderer>();
                if (carryRend != null)
                {
                    carryMaterial = new Material(carryRend.sharedMaterial);
                    carryMaterial.color = new Color(0.7f, 0.45f, 0.15f); // Brown box
                    carryMaterial.SetColor("_BaseColor", carryMaterial.color);
                    if (carryMaterial.HasProperty("_Metallic")) carryMaterial.SetFloat("_Metallic", 0.1f);
                    if (carryMaterial.HasProperty("_Smoothness")) carryMaterial.SetFloat("_Smoothness", 0.3f);
                    carryRend.material = carryMaterial;
                }
            }
        }

        private void Update()
        {
            // Gentle pulsing glow on the status light
            if (statusMaterial != null)
            {
                float pulse = 1f + 0.3f * Mathf.Sin(Time.time * 3f + RobotIndex * 0.5f);
                Color baseColor = GetStateColor(lastState);
                statusMaterial.SetColor("_EmissionColor", baseColor * pulse);
            }
        }

        private int RobotIndex => GetComponent<RobotAgent>()?.RobotIndex ?? 0;

        private void SetupPathLine()
        {
            pathLine = gameObject.GetComponent<LineRenderer>();
            if (pathLine == null)
            {
                pathLine = gameObject.AddComponent<LineRenderer>();
            }

            pathLine.startWidth = 0.05f;
            pathLine.endWidth = 0.05f;
            pathLine.positionCount = 0;
            pathLine.material = new Material(Shader.Find("Sprites/Default"));
            pathLine.startColor = new Color(0.3f, 0.8f, 1f, 0.5f);
            pathLine.endColor = new Color(0.3f, 0.8f, 1f, 0.1f);
            pathLine.useWorldSpace = true;
        }

        // ──────────────────────────────────────────────
        // STATE UPDATES
        // ──────────────────────────────────────────────

        /// <summary>
        /// Updates all visual elements based on current robot state.
        /// Called by RobotAgent whenever state changes.
        /// </summary>
        public void UpdateState(RobotState state, bool isCarrying)
        {
            UpdateStatusLight(state);
            UpdateCarryIndicator(isCarrying);
            UpdatePathVisualization();
            lastState = state;
        }

        /// <summary>
        /// Changes the status light color and emission based on robot state.
        /// </summary>
        private void UpdateStatusLight(RobotState state)
        {
            if (statusMaterial == null) return;

            Color stateColor = GetStateColor(state);

            // Set both albedo and emission for glow effect
            statusMaterial.color = stateColor;
            statusMaterial.SetColor("_EmissionColor", stateColor * 2f);
        }

        /// <summary>
        /// Shows/hides the carrying indicator (visual box on top of robot).
        /// </summary>
        private void UpdateCarryIndicator(bool isCarrying)
        {
            if (carryIndicator != null)
            {
                carryIndicator.SetActive(isCarrying);
            }
        }

        /// <summary>
        /// Updates the path line renderer to show the NavMesh planned path.
        /// </summary>
        private void UpdatePathVisualization()
        {
            if (pathLine == null) return;

            var navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (navAgent != null && navAgent.hasPath)
            {
                var path = navAgent.path;
                pathLine.positionCount = path.corners.Length;
                pathLine.SetPositions(path.corners);
            }
            else
            {
                pathLine.positionCount = 0;
            }
        }

        // ──────────────────────────────────────────────
        // EFFECTS
        // ──────────────────────────────────────────────

        /// <summary>
        /// Plays a completion effect (particle burst) when a task is finished.
        /// </summary>
        public void PlayCompletionEffect()
        {
            // Create a quick particle burst
            GameObject effectObj = new GameObject("CompletionEffect");
            effectObj.transform.position = transform.position + Vector3.up * 1.5f;

            ParticleSystem ps = effectObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.startLifetime = 0.6f;
            main.startSpeed = 2f;
            main.startSize = 0.1f;
            main.startColor = Color.green;
            main.maxParticles = 15;
            main.loop = false;
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 15)
            });

            // Ensure particle system has a renderer
            var pRenderer = ps.GetComponent<ParticleSystemRenderer>();
            pRenderer.material = new Material(Shader.Find("Particles/Standard Unlit"));

            // Auto-destroy after effect completes
            Destroy(effectObj, 1.5f);
        }

        // ──────────────────────────────────────────────
        // UTILITY
        // ──────────────────────────────────────────────

        /// <summary>
        /// Maps robot state to its designated color.
        /// </summary>
        private Color GetStateColor(RobotState state)
        {
            switch (state)
            {
                case RobotState.Idle:       return WarehouseConstants.RobotIdle;
                case RobotState.Moving:     return WarehouseConstants.RobotMoving;
                case RobotState.Picking:    return WarehouseConstants.RobotPicking;
                case RobotState.Delivering: return WarehouseConstants.RobotDelivering;
                case RobotState.Restocking: return WarehouseConstants.RobotDelivering;
                case RobotState.Error:      return WarehouseConstants.RobotError;
                default:                    return Color.white;
            }
        }
    }
}
