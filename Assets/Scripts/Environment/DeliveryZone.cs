// DeliveryZone.cs - Delivery drop-off zone for completed orders
// Robots deliver picked items here to complete order fulfillment tasks.

using UnityEngine;
using WarehouseSimulation.Core;

namespace WarehouseSimulation.Environment
{
    /// <summary>
    /// Represents a delivery zone where robots drop off items for order fulfillment.
    /// Visually highlighted (green) and provides trigger detection for delivery completion.
    /// </summary>
    public class DeliveryZone : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int zoneIndex;
        [SerializeField] private bool isActive = true;

        [Header("Visual References")]
        [SerializeField] private Renderer zoneRenderer;
        [SerializeField] private ParticleSystem completionParticles;

        /// <summary>Unique zone index for task routing.</summary>
        public int ZoneIndex { get => zoneIndex; set => zoneIndex = value; }

        /// <summary>Whether this zone is currently accepting deliveries.</summary>
        public bool IsActive { get => isActive; set { isActive = value; UpdateVisuals(); } }

        /// <summary>Center position for robot navigation target.</summary>
        public Vector3 DropOffPoint => transform.position;

        /// <summary>Number of deliveries completed at this zone (metrics).</summary>
        public int DeliveriesCompleted { get; private set; }

        private void Awake()
        {
            if (zoneRenderer == null)
                zoneRenderer = GetComponentInChildren<Renderer>();
        }

        private void Start()
        {
            UpdateVisuals();
            SetupCompletionParticles();
        }

        /// <summary>
        /// Called when a robot completes a delivery at this zone.
        /// Triggers visual feedback and updates metrics.
        /// </summary>
        public void OnDeliveryComplete()
        {
            DeliveriesCompleted++;

            // Play completion particle effect
            if (completionParticles != null)
            {
                completionParticles.Play();
            }

            // Brief visual highlight
            StartCoroutine(FlashZone());
        }

        /// <summary>
        /// Resets the delivery count (used on simulation reset).
        /// </summary>
        public void ResetStats()
        {
            DeliveriesCompleted = 0;
        }

        // ──────────────────────────────────────────────
        // VISUALS
        // ──────────────────────────────────────────────

        private void UpdateVisuals()
        {
            if (zoneRenderer == null) return;

            Color color = isActive
                ? WarehouseConstants.DeliveryZoneColor
                : new Color(0.5f, 0.5f, 0.5f, 0.3f);

            zoneRenderer.material.color = color;
        }

        private void SetupCompletionParticles()
        {
            // Create simple particle system for delivery completion feedback
            if (completionParticles == null)
            {
                GameObject particleObj = new GameObject("CompletionParticles");
                particleObj.transform.SetParent(transform);
                particleObj.transform.localPosition = Vector3.up * 0.5f;
                completionParticles = particleObj.AddComponent<ParticleSystem>();

                var main = completionParticles.main;
                main.duration = 1f;
                main.startLifetime = 0.8f;
                main.startSpeed = 3f;
                main.startSize = 0.15f;
                main.startColor = new Color(0.3f, 1f, 0.5f);
                main.maxParticles = 30;
                main.loop = false;
                main.playOnAwake = false;

                var emission = completionParticles.emission;
                emission.rateOverTime = 0;
                emission.SetBursts(new ParticleSystem.Burst[] {
                    new ParticleSystem.Burst(0f, 20)
                });

                var shape = completionParticles.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.5f;

                // Ensure particle system has a renderer
                var pRenderer = completionParticles.GetComponent<ParticleSystemRenderer>();
                pRenderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
            }
        }

        /// <summary>
        /// Brief color flash when delivery completes for visual feedback.
        /// </summary>
        private System.Collections.IEnumerator FlashZone()
        {
            if (zoneRenderer == null) yield break;

            Color originalColor = zoneRenderer.material.color;
            zoneRenderer.material.color = Color.white;
            yield return new WaitForSeconds(0.3f);
            zoneRenderer.material.color = originalColor;
        }
    }
}
