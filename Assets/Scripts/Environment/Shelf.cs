// Shelf.cs - Warehouse shelf with inventory tracking and visual stock indicators
// Each shelf maintains its own inventory level and provides color-coded feedback.

using UnityEngine;
using WarehouseSimulation.Core;

namespace WarehouseSimulation.Environment
{
    /// <summary>
    /// Represents a storage shelf in the warehouse.
    /// Tracks inventory level (0-100%) and updates visual appearance based on stock level.
    /// 
    /// Color coding:
    /// - Red:    Stock below 30% (low) — triggers restocking
    /// - Yellow: Stock between 30-70% (medium)
    /// - Green:  Stock above 70% (well stocked)
    /// </summary>
    public class Shelf : MonoBehaviour
    {
        [Header("Inventory Settings")]
        [SerializeField] private float currentStock = 80f;
        [SerializeField] private float maxStock = 100f;

        [Header("Visual References")]
        [SerializeField] private Renderer shelfRenderer;
        [SerializeField] private Transform stockIndicator;

        /// <summary>Unique shelf index for task management reference.</summary>
        public int ShelfIndex { get; set; }

        /// <summary>Current stock as a normalized percentage (0.0 to 1.0).</summary>
        public float StockPercentage => Mathf.Clamp01(currentStock / maxStock);

        /// <summary>Current absolute stock amount.</summary>
        public float CurrentStock => currentStock;

        /// <summary>Maximum stock capacity.</summary>
        public float MaxStock => maxStock;

        /// <summary>Whether the shelf has low stock (below threshold).</summary>
        public bool IsLowStock => StockPercentage < WarehouseConstants.LowStockThreshold;

        /// <summary>Whether this shelf already has a restock task pending.</summary>
        public bool HasPendingRestock { get; set; }

        /// <summary>
        /// The interaction point position for robots (slightly in front of shelf).
        /// </summary>
        public Vector3 InteractionPoint
        {
            get
            {
                // Position slightly in front of the shelf for robot access
                return transform.position + transform.forward * 1.5f;
            }
        }

        // ──────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────

        private void Awake()
        {
            if (shelfRenderer == null)
                shelfRenderer = GetComponentInChildren<Renderer>();
        }

        private void Start()
        {
            UpdateVisuals();
        }

        // ──────────────────────────────────────────────
        // INVENTORY OPERATIONS
        // ──────────────────────────────────────────────

        /// <summary>
        /// Remove items from the shelf (order fulfillment).
        /// Returns the actual amount removed (may be less than requested if stock is low).
        /// </summary>
        /// <param name="amount">Amount to pick from shelf.</param>
        /// <returns>Actual amount picked.</returns>
        public float PickItems(float amount)
        {
            float actualPick = Mathf.Min(amount, currentStock);
            currentStock -= actualPick;
            currentStock = Mathf.Max(0f, currentStock);
            UpdateVisuals();
            return actualPick;
        }

        /// <summary>
        /// Add items to the shelf (restocking operation).
        /// </summary>
        /// <param name="amount">Amount to add to shelf.</param>
        public void RestockItems(float amount)
        {
            currentStock = Mathf.Min(currentStock + amount, maxStock);
            HasPendingRestock = false;
            UpdateVisuals();
        }

        /// <summary>
        /// Set the stock level directly (used during initialization).
        /// </summary>
        public void SetStock(float amount)
        {
            currentStock = Mathf.Clamp(amount, 0f, maxStock);
            UpdateVisuals();
        }

        /// <summary>
        /// Get the current stock level category.
        /// </summary>
        public StockLevel GetStockLevel()
        {
            float pct = StockPercentage;
            if (pct < WarehouseConstants.LowStockThreshold)
                return StockLevel.Low;
            else if (pct < WarehouseConstants.MediumStockThreshold)
                return StockLevel.Medium;
            else
                return StockLevel.High;
        }

        // ──────────────────────────────────────────────
        // VISUALS
        // ──────────────────────────────────────────────

        /// <summary>
        /// Updates the shelf color and stock indicator based on current inventory level.
        /// Called automatically whenever stock changes.
        /// </summary>
        private void UpdateVisuals()
        {
            if (shelfRenderer == null) return;

            // Color-code the shelf based on stock level
            Color targetColor;
            switch (GetStockLevel())
            {
                case StockLevel.Low:
                    targetColor = WarehouseConstants.StockLowColor;
                    break;
                case StockLevel.Medium:
                    targetColor = WarehouseConstants.StockMediumColor;
                    break;
                case StockLevel.High:
                default:
                    targetColor = WarehouseConstants.StockHighColor;
                    break;
            }

            // Apply color to the shelf material
            if (shelfRenderer.material != null)
            {
                shelfRenderer.material.color = targetColor;
            }

            // Scale the stock indicator to visually show fill level
            if (stockIndicator != null)
            {
                Vector3 scale = stockIndicator.localScale;
                scale.y = Mathf.Lerp(0.1f, 1f, StockPercentage);
                stockIndicator.localScale = scale;
            }
        }

        /// <summary>
        /// Simulate gradual stock consumption for realism.
        /// Called periodically by InventoryManager.
        /// </summary>
        public void ConsumeStock(float amount)
        {
            currentStock = Mathf.Max(0f, currentStock - amount);
            UpdateVisuals();
        }
    }
}
