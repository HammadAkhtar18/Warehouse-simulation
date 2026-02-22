// InventoryManager.cs - Centralized inventory tracking and low-stock event management
// Monitors all shelves and triggers restocking when inventory drops below threshold.

using System.Collections.Generic;
using UnityEngine;
using WarehouseSimulation.Core;
using WarehouseSimulation.Environment;

namespace WarehouseSimulation.Managers
{
    /// <summary>
    /// Manages inventory across all warehouse shelves.
    /// 
    /// Responsibilities:
    /// - Register and track all shelf references
    /// - Periodically check for low-stock shelves
    /// - Fire events when restocking is needed
    /// - Provide shelf lookup for task assignment
    /// - Optionally simulate gradual stock consumption for realism
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float stockCheckInterval = 3f;
        [SerializeField] private bool simulateConsumption = true;
        [SerializeField] private float consumptionRate = 0.5f; // Stock consumed per check per shelf

        // All registered shelves in the warehouse
        private List<Shelf> shelves = new List<Shelf>();

        /// <summary>Read-only access to all shelves.</summary>
        public IReadOnlyList<Shelf> Shelves => shelves;

        /// <summary>Event fired when a shelf needs restocking.</summary>
        public event System.Action<Shelf> OnShelfNeedsRestock;

        private float nextStockCheck;

        // ──────────────────────────────────────────────
        // PUBLIC API
        // ──────────────────────────────────────────────

        /// <summary>
        /// Register all shelves from the warehouse builder.
        /// Called during initialization.
        /// </summary>
        public void RegisterShelves(List<Shelf> warehouseShelves)
        {
            shelves = new List<Shelf>(warehouseShelves);
            Debug.Log($"[InventoryManager] Registered {shelves.Count} shelves");
        }

        /// <summary>
        /// Get a shelf by its index.
        /// </summary>
        public Shelf GetShelf(int index)
        {
            if (index >= 0 && index < shelves.Count)
                return shelves[index];
            return null;
        }

        /// <summary>
        /// Find all shelves that currently have low stock and no pending restock task.
        /// </summary>
        public List<Shelf> GetLowStockShelves()
        {
            List<Shelf> lowStock = new List<Shelf>();
            foreach (var shelf in shelves)
            {
                if (shelf.IsLowStock && !shelf.HasPendingRestock)
                {
                    lowStock.Add(shelf);
                }
            }
            return lowStock;
        }

        /// <summary>
        /// Find the shelf with the highest stock for order fulfillment.
        /// Prefers shelves that are well-stocked to avoid depleting low shelves.
        /// </summary>
        public Shelf GetBestShelfForOrder()
        {
            Shelf best = null;
            float bestStock = 0f;

            foreach (var shelf in shelves)
            {
                if (shelf.CurrentStock >= WarehouseConstants.OrderPickAmount && 
                    shelf.CurrentStock > bestStock)
                {
                    best = shelf;
                    bestStock = shelf.CurrentStock;
                }
            }

            return best;
        }

        /// <summary>
        /// Get average stock level across all shelves (for metrics).
        /// </summary>
        public float GetAverageStockPercentage()
        {
            if (shelves.Count == 0) return 0f;

            float total = 0f;
            foreach (var shelf in shelves)
            {
                total += shelf.StockPercentage;
            }
            return total / shelves.Count;
        }

        // ──────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────

        private void Update()
        {
            if (Time.time >= nextStockCheck)
            {
                nextStockCheck = Time.time + stockCheckInterval;
                CheckInventoryLevels();

                if (simulateConsumption)
                {
                    SimulateConsumption();
                }
            }
        }

        // ──────────────────────────────────────────────
        // INTERNAL
        // ──────────────────────────────────────────────

        /// <summary>
        /// Periodically scans all shelves and fires restock events for low-stock shelves.
        /// This drives the automatic restocking system.
        /// </summary>
        private void CheckInventoryLevels()
        {
            foreach (var shelf in shelves)
            {
                if (shelf.IsLowStock && !shelf.HasPendingRestock)
                {
                    shelf.HasPendingRestock = true;
                    OnShelfNeedsRestock?.Invoke(shelf);
                }
            }
        }

        /// <summary>
        /// Simulates gradual stock consumption to keep the warehouse dynamic.
        /// Without this, shelves would only change when robots pick from them.
        /// </summary>
        private void SimulateConsumption()
        {
            foreach (var shelf in shelves)
            {
                // Random consumption to simulate customer demand
                if (Random.value < 0.3f) // 30% chance per check interval
                {
                    shelf.ConsumeStock(consumptionRate);
                }
            }
        }
    }
}
