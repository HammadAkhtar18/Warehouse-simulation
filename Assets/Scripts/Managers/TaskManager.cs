// TaskManager.cs - Order and restocking task management system
// Generates, assigns, and tracks warehouse tasks for autonomous robot operation.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WarehouseSimulation.Core;
using WarehouseSimulation.Environment;
using WarehouseSimulation.Agents;
using WarehouseSimulation.Tasks;

namespace WarehouseSimulation.Managers
{
    /// <summary>
    /// Central task management system for the warehouse simulation.
    /// 
    /// Key responsibilities:
    /// 1. AUTO-GENERATE ORDERS: Creates order fulfillment tasks at a configurable rate.
    /// 2. AUTO-GENERATE RESTOCKS: Responds to low-stock events from InventoryManager.
    /// 3. ASSIGN TASKS: Uses nearest-available-robot algorithm with priority and load balancing.
    /// 4. TRACK LIFECYCLE: Manages task state from Created through Completed/Failed.
    /// 
    /// Task Assignment Algorithm:
    /// - Sort pending tasks by priority (Urgent > Standard > Low)
    /// - For each task, find the nearest idle robot
    /// - Balance load so no single robot is overworked
    /// - Prevent duplicate restock assignments to the same shelf
    /// </summary>
    public class TaskManager : MonoBehaviour
    {
        [Header("Order Generation")]
        [SerializeField] private float orderInterval = WarehouseConstants.DefaultOrderInterval;
        [SerializeField] private bool autoGenerateOrders = true;

        // Task queues
        private List<TaskData> pendingTasks = new List<TaskData>();
        private List<TaskData> activeTasks = new List<TaskData>();
        private List<TaskData> completedTasks = new List<TaskData>();

        // References (set by GameManager)
        private InventoryManager inventoryManager;
        private RobotCoordinator robotCoordinator;
        private WarehouseBuilder warehouseBuilder;

        private float nextOrderTime;

        // ── Events ──
        public event System.Action<TaskData> OnTaskCreated;
        public event System.Action<TaskData> OnTaskAssigned;
        public event System.Action<TaskData> OnTaskCompleted;

        // ── Metrics ──
        public int TotalOrdersCreated { get; private set; }
        public int TotalOrdersCompleted { get; private set; }
        public int TotalRestocksCompleted { get; private set; }
        public int PendingTaskCount => pendingTasks.Count;
        public int ActiveTaskCount => activeTasks.Count;
        public float AverageCompletionTime { get; private set; }

        /// <summary>Current order generation interval. Adjustable via UI.</summary>
        public float OrderInterval
        {
            get => orderInterval;
            set => orderInterval = Mathf.Clamp(value, WarehouseConstants.MinOrderInterval,
                                               WarehouseConstants.MaxOrderInterval);
        }

        // ──────────────────────────────────────────────
        // INITIALIZATION
        // ──────────────────────────────────────────────

        /// <summary>
        /// Initialize with references to other managers.
        /// Called by GameManager during setup.
        /// </summary>
        public void Initialize(InventoryManager invMgr, RobotCoordinator coordinator, WarehouseBuilder builder)
        {
            inventoryManager = invMgr;
            robotCoordinator = coordinator;
            warehouseBuilder = builder;

            // Subscribe to low-stock events for automatic restocking
            inventoryManager.OnShelfNeedsRestock += OnShelfNeedsRestock;

            nextOrderTime = Time.time + orderInterval;

            Debug.Log("[TaskManager] Initialized");
        }

        // ──────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────

        private void Update()
        {
            // Auto-generate orders at configured interval
            if (autoGenerateOrders && Time.time >= nextOrderTime)
            {
                GenerateRandomOrder();
                nextOrderTime = Time.time + orderInterval;
            }

            // Try to assign any pending tasks to available robots
            AssignPendingTasks();
        }

        // ──────────────────────────────────────────────
        // ORDER GENERATION
        // ──────────────────────────────────────────────

        /// <summary>
        /// Generates a random order fulfillment task.
        /// Picks a well-stocked shelf as source and a random delivery zone as destination.
        /// </summary>
        public void GenerateRandomOrder()
        {
            // Find a shelf with sufficient stock
            Shelf sourceShelf = inventoryManager.GetBestShelfForOrder();
            if (sourceShelf == null)
            {
                Debug.LogWarning("[TaskManager] No shelf with sufficient stock for order");
                return;
            }

            // Pick a random delivery zone
            if (warehouseBuilder.DeliveryZones.Count == 0) return;
            DeliveryZone targetZone = warehouseBuilder.DeliveryZones[
                Random.Range(0, warehouseBuilder.DeliveryZones.Count)];

            // Randomize priority (mostly standard, some urgent)
            TaskPriority priority = TaskPriority.Standard;
            float roll = Random.value;
            if (roll < 0.15f) priority = TaskPriority.Urgent;
            else if (roll < 0.35f) priority = TaskPriority.Low;

            // Create the task
            TaskData task = new TaskData(TaskType.OrderFulfillment, priority)
            {
                PickupPosition = sourceShelf.InteractionPoint,
                DeliveryPosition = targetZone.DropOffPoint,
                TargetShelf = sourceShelf.gameObject,
                TargetZone = targetZone.gameObject
            };

            // Calculate optimal distance for efficiency metrics
            task.OptimalDistance = Vector3.Distance(task.PickupPosition, task.DeliveryPosition);

            pendingTasks.Add(task);
            TotalOrdersCreated++;

            OnTaskCreated?.Invoke(task);
        }

        /// <summary>
        /// Event handler: creates a restock task when a shelf reports low stock.
        /// Called automatically via the InventoryManager event system.
        /// </summary>
        private void OnShelfNeedsRestock(Shelf shelf)
        {
            // Check for duplicate restock assignments
            bool alreadyPending = pendingTasks.Any(t =>
                t.Type == TaskType.Restocking &&
                t.TargetShelf == shelf.gameObject);

            bool alreadyActive = activeTasks.Any(t =>
                t.Type == TaskType.Restocking &&
                t.TargetShelf == shelf.gameObject);

            if (alreadyPending || alreadyActive) return;

            if (warehouseBuilder.Dock == null) return;

            TaskData task = new TaskData(TaskType.Restocking, TaskPriority.Standard)
            {
                PickupPosition = warehouseBuilder.Dock.PickupPoint,
                DeliveryPosition = shelf.InteractionPoint,
                TargetShelf = shelf.gameObject,
                TargetZone = warehouseBuilder.Dock.gameObject
            };

            task.OptimalDistance = Vector3.Distance(task.PickupPosition, task.DeliveryPosition);

            pendingTasks.Add(task);
            OnTaskCreated?.Invoke(task);

            Debug.Log($"[TaskManager] Restock task created for shelf {shelf.ShelfIndex}");
        }

        // ──────────────────────────────────────────────
        // TASK ASSIGNMENT ALGORITHM
        // ──────────────────────────────────────────────

        /// <summary>
        /// Assigns pending tasks to available robots using the following algorithm:
        /// 
        /// 1. Sort pending tasks by priority (highest first).
        /// 2. For each task, find the nearest idle robot.
        /// 3. Assign the task to that robot.
        /// 4. Move task from pending to active queue.
        /// 
        /// Load balancing: prefers robots with fewer completed tasks to distribute work evenly.
        /// </summary>
        private void AssignPendingTasks()
        {
            if (pendingTasks.Count == 0) return;

            // Sort by priority (highest first)
            pendingTasks.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            // Get all idle robots
            List<RobotAgent> availableRobots = new List<RobotAgent>();
            foreach (var robot in robotCoordinator.Robots)
            {
                if (robot.CurrentState == RobotState.Idle && robot.CurrentTask == null)
                {
                    availableRobots.Add(robot);
                }
            }

            if (availableRobots.Count == 0) return;

            // Assign tasks to nearest available robots
            List<TaskData> tasksToRemove = new List<TaskData>();

            foreach (var task in pendingTasks)
            {
                if (availableRobots.Count == 0) break;

                // Find nearest available robot to the pickup position
                RobotAgent bestRobot = null;
                float bestScore = float.MaxValue;

                foreach (var robot in availableRobots)
                {
                    float distance = Vector3.Distance(robot.transform.position, task.PickupPosition);
                    
                    // Load balancing: penalize robots that have completed more tasks
                    float loadPenalty = robot.TasksCompleted * 0.5f;
                    float score = distance + loadPenalty;

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestRobot = robot;
                    }
                }

                if (bestRobot != null)
                {
                    // Assign the task
                    task.Status = Core.TaskStatus.Assigned;
                    task.AssignedRobot = bestRobot.gameObject;
                    task.AssignedTime = Time.time;

                    bestRobot.AssignTask(task);

                    activeTasks.Add(task);
                    tasksToRemove.Add(task);
                    availableRobots.Remove(bestRobot);

                    OnTaskAssigned?.Invoke(task);

                    Debug.Log($"[TaskManager] Assigned {task} to Robot {bestRobot.RobotIndex}");
                }
            }

            // Remove assigned tasks from pending queue
            foreach (var task in tasksToRemove)
            {
                pendingTasks.Remove(task);
            }
        }

        /// <summary>
        /// Called by RobotAgent at the start of each episode to immediately get a task.
        /// Finds the nearest pending task and assigns it without waiting for the Update cycle.
        /// </summary>
        public void RequestTaskForRobot(RobotAgent robot)
        {
            if (pendingTasks.Count == 0) return;
            if (robot.CurrentTask != null) return;

            // Find nearest pending task
            TaskData bestTask = null;
            float bestDist = float.MaxValue;

            foreach (var task in pendingTasks)
            {
                float dist = Vector3.Distance(robot.transform.position, task.PickupPosition);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestTask = task;
                }
            }

            if (bestTask != null)
            {
                bestTask.Status = Core.TaskStatus.Assigned;
                bestTask.AssignedRobot = robot.gameObject;
                bestTask.AssignedTime = Time.time;

                robot.AssignTask(bestTask);

                activeTasks.Add(bestTask);
                pendingTasks.Remove(bestTask);

                OnTaskAssigned?.Invoke(bestTask);
            }
        }

        // ──────────────────────────────────────────────
        // TASK COMPLETION
        // ──────────────────────────────────────────────

        /// <summary>
        /// Called by RobotAgent when it completes a task.
        /// Moves the task from active to completed, updates metrics.
        /// </summary>
        public void ReportTaskComplete(TaskData task)
        {
            task.Status = Core.TaskStatus.Completed;
            task.CompletedTime = Time.time;

            activeTasks.Remove(task);
            completedTasks.Add(task);

            if (task.Type == TaskType.OrderFulfillment)
                TotalOrdersCompleted++;
            else
                TotalRestocksCompleted++;

            // Update average completion time (running average)
            UpdateAverageCompletionTime(task);

            OnTaskCompleted?.Invoke(task);
        }

        /// <summary>
        /// Called when a task fails (e.g., robot gets permanently stuck).
        /// Returns the task to the pending queue for reassignment.
        /// </summary>
        public void ReportTaskFailed(TaskData task)
        {
            task.Status = Core.TaskStatus.Created;
            task.AssignedRobot = null;
            task.ItemPickedUp = false;

            activeTasks.Remove(task);
            pendingTasks.Add(task);

            Debug.LogWarning($"[TaskManager] Task {task.TaskId} failed, returning to queue");
        }

        private void UpdateAverageCompletionTime(TaskData task)
        {
            float duration = task.CompletedTime - task.CreatedTime;
            int total = TotalOrdersCompleted + TotalRestocksCompleted;

            if (total <= 1)
                AverageCompletionTime = duration;
            else
                AverageCompletionTime = AverageCompletionTime + (duration - AverageCompletionTime) / total;
        }

        // ──────────────────────────────────────────────
        // RESET
        // ──────────────────────────────────────────────

        /// <summary>
        /// Resets all task data for simulation restart.
        /// </summary>
        public void ResetAll()
        {
            pendingTasks.Clear();
            activeTasks.Clear();
            completedTasks.Clear();

            TotalOrdersCreated = 0;
            TotalOrdersCompleted = 0;
            TotalRestocksCompleted = 0;
            AverageCompletionTime = 0f;

            TaskData.ResetIdCounter();
            nextOrderTime = Time.time + orderInterval;
        }

        private void OnDestroy()
        {
            if (inventoryManager != null)
                inventoryManager.OnShelfNeedsRestock -= OnShelfNeedsRestock;
        }
    }
}
