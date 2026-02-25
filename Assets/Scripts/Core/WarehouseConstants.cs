// WarehouseConstants.cs - Central configuration constants for the warehouse simulation
// All tunable parameters are defined here for easy adjustment during development and testing.

using UnityEngine;

namespace WarehouseSimulation.Core
{
    /// <summary>
    /// Central repository of all simulation constants.
    /// Modify these values to tune simulation behavior without changing logic.
    /// </summary>
    public static class WarehouseConstants
    {
        // ──────────────────────────────────────────────
        // WAREHOUSE LAYOUT
        // ──────────────────────────────────────────────
        
        /// <summary>Total warehouse floor dimensions in Unity units (meters).</summary>
        public const float WarehouseWidth = 40f;
        public const float WarehouseLength = 30f;
        public const float WallHeight = 4f;
        public const float WallThickness = 0.3f;

        /// <summary>Shelf grid configuration.</summary>
        public const int ShelfRows = 4;
        public const int ShelfColumns = 5;
        public const float ShelfWidth = 2f;
        public const float ShelfHeight = 2.5f;
        public const float ShelfDepth = 1f;
        public const float AisleWidth = 3.5f;  // Space between shelf rows for robot navigation

        /// <summary>Number of delivery and dock zones.</summary>
        public const int DeliveryZoneCount = 3;
        public const int DockZoneCount = 1;
        public const float ZoneSize = 3f;

        // ──────────────────────────────────────────────
        // ROBOT MOVEMENT & PHYSICS
        // ──────────────────────────────────────────────
        
        /// <summary>Robot physical dimensions.</summary>
        public const float RobotRadius = 0.5f;
        public const float RobotHeight = 1.2f;

        /// <summary>Movement parameters (realistic warehouse robot speeds).</summary>
        public const float MaxSpeed = 3.0f;             // m/s (~10.8 km/h, typical AGV speed)
        public const float MinSpeed = 0.5f;             // m/s
        public const float Acceleration = 2.0f;         // m/s²
        public const float Deceleration = 3.0f;         // m/s²
        public const float AngularSpeed = 180f;         // degrees/second
        public const float StoppingDistance = 0.3f;      // meters

        /// <summary>NavMesh agent configuration.</summary>
        public const float NavMeshAgentRadius = 0.6f;
        public const float ObstacleAvoidanceRadius = 1.0f;
        public const int AvoidancePriority = 50;        // Default; lower = higher priority

        // ──────────────────────────────────────────────
        // TASK TIMING
        // ──────────────────────────────────────────────
        
        /// <summary>Time delays for pick and delivery operations (seconds).</summary>
        public const float PickupDuration = 2.0f;
        public const float DeliveryDuration = 1.5f;
        public const float RestockDuration = 2.0f;

        /// <summary>Interaction distance thresholds.</summary>
        public const float InteractionDistance = 1.5f;   // How close robot must be to interact
        public const float TaskCompletionDistance = 1.0f;

        // ──────────────────────────────────────────────
        // INVENTORY
        // ──────────────────────────────────────────────
        
        /// <summary>Shelf inventory parameters.</summary>
        public const float MaxInventory = 100f;
        public const float LowStockThreshold = 0.3f;    // 30% triggers restocking
        public const float MediumStockThreshold = 0.7f;  // Below 70% = medium
        public const float RestockAmount = 30f;          // Amount added per restock operation
        public const float OrderPickAmount = 10f;        // Amount removed per order pick

        // ──────────────────────────────────────────────
        // TASK GENERATION
        // ──────────────────────────────────────────────
        
        /// <summary>Order generation timing.</summary>
        public const float DefaultOrderInterval = 8f;    // Seconds between auto-generated orders
        public const float MinOrderInterval = 3f;
        public const float MaxOrderInterval = 20f;

        // ──────────────────────────────────────────────
        // ML-AGENTS / REINFORCEMENT LEARNING REWARDS
        // ──────────────────────────────────────────────
        
        /// <summary>
        /// Reward values for the RL reward function.
        /// Carefully tuned to encourage task completion while penalizing collisions and inefficiency.
        /// </summary>
        public const float RewardTaskComplete = 10f;
        public const float RewardProgressPerUnit = 0.1f;   // Per unit distance moved toward target
        public const float PenaltyTimestep = -0.005f;       // Per decision step (gentle urgency)
        public const float PenaltyRobotCollision = -2f;     // Proximity collision (reduced for exploration)
        public const float PenaltyObstacleCollision = -1f;  // Wall/shelf collision
        public const float PenaltyIdle = -0.5f;
        public const float IdleTimeThreshold = 3f;           // Seconds before idle penalty kicks in
        public const float RewardEfficiencyBonus = 3f;       // Bonus for completing in near-optimal time
        public const float RewardPickup = 2f;                // Reward for reaching pickup point
        
        /// <summary>Maximum episode length for ML training (steps).</summary>
        public const int MaxEpisodeSteps = 5000;

        // ──────────────────────────────────────────────
        // OBSERVATION SPACE PARAMETERS
        // ──────────────────────────────────────────────
        
        /// <summary>Raycast parameters for obstacle detection.</summary>
        public const float RaycastDistance = 10f;
        public const int RaycastDirections = 8;             // 8 directions for obstacle sensing

        // ──────────────────────────────────────────────
        // DEFAULT ROBOT COUNT
        // ──────────────────────────────────────────────
        
        public const int DefaultRobotCount = 5;
        public const int MinRobots = 3;
        public const int MaxRobots = 15;

        // ──────────────────────────────────────────────
        // COORDINATION
        // ──────────────────────────────────────────────
        
        /// <summary>Deadlock detection parameters.</summary>
        public const float DeadlockCheckInterval = 2f;       // Seconds between deadlock checks
        public const float StuckThreshold = 3f;              // Seconds of no movement to consider stuck
        public const float MinMovementThreshold = 0.1f;      // Minimum distance to not be "stuck"

        // ──────────────────────────────────────────────
        // COLORS
        // ──────────────────────────────────────────────
        
        public static readonly Color FloorColor = new Color(0.78f, 0.78f, 0.75f);     // Light concrete
        public static readonly Color WallColor = new Color(0.5f, 0.52f, 0.58f);      // Cool industrial gray
        
        public static readonly Color StockLowColor = new Color(0.95f, 0.25f, 0.2f);   // Red
        public static readonly Color StockMediumColor = new Color(1f, 0.75f, 0.1f);   // Amber
        public static readonly Color StockHighColor = new Color(0.15f, 0.85f, 0.35f); // Green

        public static readonly Color DeliveryZoneColor = new Color(0.15f, 0.9f, 0.45f, 0.5f);  // Bright green
        public static readonly Color DockZoneColor = new Color(0.2f, 0.55f, 0.95f, 0.5f);      // Bright blue

        // Robot state colors
        public static readonly Color RobotIdle = Color.green;
        public static readonly Color RobotMoving = new Color(0.2f, 0.5f, 1f);          // Blue
        public static readonly Color RobotPicking = Color.yellow;
        public static readonly Color RobotDelivering = new Color(1f, 0.6f, 0.2f);      // Orange
        public static readonly Color RobotError = Color.red;
    }
}
