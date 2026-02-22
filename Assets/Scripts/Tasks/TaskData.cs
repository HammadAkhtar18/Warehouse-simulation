// TaskData.cs - Data class representing a warehouse task (order or restock)
// Tasks are the fundamental unit of work assigned to robots.

using UnityEngine;
using WarehouseSimulation.Core;

namespace WarehouseSimulation.Tasks
{
    /// <summary>
    /// Represents a single warehouse task — either an order fulfillment (pick → deliver)
    /// or a restocking operation (dock → shelf).
    /// 
    /// Lifecycle: Created → Assigned → InProgress → Completed/Failed
    /// </summary>
    public class TaskData
    {
        // ──────────────────────────────────────────────
        // IDENTITY
        // ──────────────────────────────────────────────
        
        private static int _nextId = 0;
        
        /// <summary>Unique task identifier.</summary>
        public int TaskId { get; private set; }
        
        /// <summary>Type of task (OrderFulfillment or Restocking).</summary>
        public TaskType Type { get; private set; }
        
        /// <summary>Current lifecycle status.</summary>
        public Core.TaskStatus Status { get; set; }
        
        /// <summary>Priority level for scheduling.</summary>
        public TaskPriority Priority { get; set; }

        // ──────────────────────────────────────────────
        // LOCATIONS
        // ──────────────────────────────────────────────
        
        /// <summary>Where the robot picks up the item (shelf for orders, dock for restocking).</summary>
        public Vector3 PickupPosition { get; set; }
        
        /// <summary>Where the robot delivers the item (delivery zone for orders, shelf for restocking).</summary>
        public Vector3 DeliveryPosition { get; set; }
        
        /// <summary>Reference to the shelf involved in this task.</summary>
        public GameObject TargetShelf { get; set; }
        
        /// <summary>Reference to the delivery/dock zone involved.</summary>
        public GameObject TargetZone { get; set; }

        // ──────────────────────────────────────────────
        // ASSIGNMENT
        // ──────────────────────────────────────────────
        
        /// <summary>The robot currently assigned to this task (null if unassigned).</summary>
        public GameObject AssignedRobot { get; set; }
        
        /// <summary>Index of the assigned robot for display purposes.</summary>
        public int AssignedRobotIndex { get; set; } = -1;

        // ──────────────────────────────────────────────
        // TIMING (for performance metrics)
        // ──────────────────────────────────────────────
        
        /// <summary>Simulation time when the task was created.</summary>
        public float CreatedTime { get; private set; }
        
        /// <summary>Simulation time when the task was assigned to a robot.</summary>
        public float AssignedTime { get; set; }
        
        /// <summary>Simulation time when the task was started (robot began moving).</summary>
        public float StartedTime { get; set; }
        
        /// <summary>Simulation time when the task was completed.</summary>
        public float CompletedTime { get; set; }
        
        /// <summary>Total time from creation to completion (seconds).</summary>
        public float TotalDuration => (Status == Core.TaskStatus.Completed) 
            ? CompletedTime - CreatedTime 
            : Time.time - CreatedTime;

        /// <summary>Whether the robot has picked up the item for this task.</summary>
        public bool ItemPickedUp { get; set; }

        /// <summary>Optimal straight-line distance for efficiency calculation.</summary>
        public float OptimalDistance { get; set; }

        // ──────────────────────────────────────────────
        // CONSTRUCTOR
        // ──────────────────────────────────────────────
        
        /// <summary>
        /// Creates a new task with the given type and priority.
        /// Automatically assigns a unique ID and records creation time.
        /// </summary>
        public TaskData(TaskType type, TaskPriority priority = TaskPriority.Standard)
        {
            TaskId = _nextId++;
            Type = type;
            Priority = priority;
            Status = Core.TaskStatus.Created;
            CreatedTime = Time.time;
            ItemPickedUp = false;
        }

        /// <summary>
        /// Resets the static ID counter. Call when resetting the simulation.
        /// </summary>
        public static void ResetIdCounter()
        {
            _nextId = 0;
        }

        public override string ToString()
        {
            return $"Task#{TaskId} [{Type}] {Status} Priority:{Priority}";
        }
    }
}
