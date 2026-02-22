// DockZone.cs - Restocking dock where robots collect items for shelf restocking
// Acts as an infinite source of items for restocking operations.

using UnityEngine;
using WarehouseSimulation.Core;

namespace WarehouseSimulation.Environment
{
    /// <summary>
    /// Represents the restocking dock — the source of items for restocking shelves.
    /// Functions as an infinite supply point. Robots come here to pick up items
    /// before delivering them to low-stock shelves.
    /// </summary>
    public class DockZone : MonoBehaviour
    {
        [Header("Visual References")]
        [SerializeField] private Renderer zoneRenderer;

        /// <summary>Center position for robot navigation target.</summary>
        public Vector3 PickupPoint => transform.position;

        /// <summary>Total items dispatched from this dock (metrics).</summary>
        public int ItemsDispatched { get; private set; }

        private void Awake()
        {
            if (zoneRenderer == null)
                zoneRenderer = GetComponentInChildren<Renderer>();
        }

        private void Start()
        {
            UpdateVisuals();
        }

        /// <summary>
        /// Called when a robot picks up restock items from the dock.
        /// The dock has infinite supply, so no stock is consumed.
        /// </summary>
        public void OnItemPickedUp()
        {
            ItemsDispatched++;
        }

        /// <summary>
        /// Resets metrics for simulation restart.
        /// </summary>
        public void ResetStats()
        {
            ItemsDispatched = 0;
        }

        private void UpdateVisuals()
        {
            if (zoneRenderer == null) return;
            zoneRenderer.material.color = WarehouseConstants.DockZoneColor;
        }
    }
}
