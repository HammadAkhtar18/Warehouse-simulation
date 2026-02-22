// WarehouseEnums.cs - All enumeration types for the warehouse simulation
// These enums define the possible states and categories used throughout the system.

namespace WarehouseSimulation.Core
{
    /// <summary>
    /// Current operational state of a robot.
    /// Used for visual feedback and task management decisions.
    /// </summary>
    public enum RobotState
    {
        Idle,           // Green  - Waiting for a task
        Moving,         // Blue   - Navigating to target
        Picking,        // Yellow - Collecting item from shelf
        Delivering,     // Orange - Dropping off item at zone
        Restocking,     // Orange - Placing item on shelf
        Error           // Red    - Stuck or collision detected
    }

    /// <summary>
    /// Type of warehouse task.
    /// Determines the robot's workflow: pickup source and delivery destination.
    /// </summary>
    public enum TaskType
    {
        OrderFulfillment,   // Pick from shelf → Deliver to delivery zone
        Restocking          // Pick from dock  → Restock low-inventory shelf
    }

    /// <summary>
    /// Lifecycle status of a task in the task management pipeline.
    /// Tasks progress linearly through these states.
    /// </summary>
    public enum TaskStatus
    {
        Created,        // Task generated, waiting for assignment
        Assigned,       // Robot assigned but hasn't started moving
        InProgress,     // Robot is actively working on this task
        Completed,      // Task finished successfully
        Failed          // Task could not be completed
    }

    /// <summary>
    /// Priority level for task scheduling.
    /// Higher priority tasks are assigned first.
    /// </summary>
    public enum TaskPriority
    {
        Low = 0,
        Standard = 1,
        Urgent = 2
    }

    /// <summary>
    /// Inventory level category for shelf stock visualization.
    /// Drives the color coding of shelf displays.
    /// </summary>
    public enum StockLevel
    {
        Low,        // Red    - Below 30%
        Medium,     // Yellow - 30% to 70%
        High        // Green  - Above 70%
    }

    /// <summary>
    /// Phase of the simulation for training vs inference mode.
    /// </summary>
    public enum SimulationMode
    {
        Training,       // ML-Agents training mode (fast, no fancy visuals)
        Inference       // Normal play mode with full visualization
    }
}
