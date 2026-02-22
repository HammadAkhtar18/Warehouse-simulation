// PerformanceTracker.cs - Collects and stores performance metrics for the dashboard and learning graphs
// Tracks cumulative reward, task completion times, collision rates, and throughput over time.

using System.Collections.Generic;
using UnityEngine;
using WarehouseSimulation.Core;
using WarehouseSimulation.Managers;
using WarehouseSimulation.Tasks;
using WarehouseSimulation.Agents;

namespace WarehouseSimulation.UI
{
    /// <summary>
    /// Collects performance metrics over time for dashboard display and learning progress graphs.
    /// 
    /// Tracked metrics:
    /// - Orders completed (total and per hour)
    /// - Average delivery time
    /// - Robot utilization rate
    /// - Collision count
    /// - Warehouse throughput
    /// - Cumulative reward (for learning progress)
    /// - Time-series data for trend graphs
    /// </summary>
    public class PerformanceTracker : MonoBehaviour
    {
        // References
        private TaskManager taskManager;
        private RobotCoordinator coordinator;

        // ── Real-time metrics ──
        public int OrdersCompleted { get; private set; }
        public int RestocksCompleted { get; private set; }
        public float AverageDeliveryTime { get; private set; }
        public float RobotUtilization { get; private set; }     // 0-100%
        public int TotalCollisions { get; private set; }
        public float Throughput { get; private set; }            // Tasks per minute
        public int TasksInQueue { get; private set; }
        public float SimulationUptime { get; private set; }

        // ── Time-series data for graphs ──
        // Each list stores (time, value) pairs sampled periodically
        public List<Vector2> RewardHistory { get; private set; } = new List<Vector2>();
        public List<Vector2> CompletionTimeHistory { get; private set; } = new List<Vector2>();
        public List<Vector2> CollisionRateHistory { get; private set; } = new List<Vector2>();
        public List<Vector2> ThroughputHistory { get; private set; } = new List<Vector2>();

        // Sampling
        private float sampleInterval = 10f;  // Record data point every 10 seconds
        private float nextSampleTime;
        private float startTime;

        // Rolling window for rate calculations
        private int lastOrderCount;
        private int lastCollisionCount;
        private float lastSampleTime;

        // ──────────────────────────────────────────────
        // PUBLIC API
        // ──────────────────────────────────────────────

        /// <summary>
        /// Initialize with references to task and robot systems.
        /// </summary>
        public void Initialize(TaskManager tm, RobotCoordinator rc)
        {
            taskManager = tm;
            coordinator = rc;

            startTime = Time.time;
            nextSampleTime = Time.time + sampleInterval;
            lastSampleTime = Time.time;

            // Subscribe to task events for real-time tracking
            if (taskManager != null)
            {
                taskManager.OnTaskCompleted += OnTaskCompleted;
            }

            Debug.Log("[PerformanceTracker] Initialized");
        }

        /// <summary>
        /// Resets all metrics. Called on simulation restart.
        /// </summary>
        public void ResetMetrics()
        {
            OrdersCompleted = 0;
            RestocksCompleted = 0;
            AverageDeliveryTime = 0f;
            RobotUtilization = 0f;
            TotalCollisions = 0;
            Throughput = 0f;
            TasksInQueue = 0;
            SimulationUptime = 0f;

            RewardHistory.Clear();
            CompletionTimeHistory.Clear();
            CollisionRateHistory.Clear();
            ThroughputHistory.Clear();

            lastOrderCount = 0;
            lastCollisionCount = 0;
            startTime = Time.time;
            nextSampleTime = Time.time + sampleInterval;
            lastSampleTime = Time.time;
        }

        // ──────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────

        private void Update()
        {
            SimulationUptime = Time.time - startTime;

            // Update real-time metrics
            UpdateRealtimeMetrics();

            // Periodic sampling for graphs
            if (Time.time >= nextSampleTime)
            {
                SampleDataPoint();
                nextSampleTime = Time.time + sampleInterval;
            }
        }

        // ──────────────────────────────────────────────
        // METRIC CALCULATION
        // ──────────────────────────────────────────────

        /// <summary>
        /// Updates real-time metrics every frame (lightweight calculations).
        /// </summary>
        private void UpdateRealtimeMetrics()
        {
            if (taskManager != null)
            {
                OrdersCompleted = taskManager.TotalOrdersCompleted;
                RestocksCompleted = taskManager.TotalRestocksCompleted;
                AverageDeliveryTime = taskManager.AverageCompletionTime;
                TasksInQueue = taskManager.PendingTaskCount;
            }

            // Calculate robot utilization (% of robots actively working)
            if (coordinator != null && coordinator.RobotCount > 0)
            {
                int activeRobots = 0;
                foreach (var robot in coordinator.Robots)
                {
                    if (robot.CurrentState != RobotState.Idle)
                        activeRobots++;
                }
                RobotUtilization = (float)activeRobots / coordinator.RobotCount * 100f;

                // Sum total collisions across all robots
                TotalCollisions = 0;
                foreach (var robot in coordinator.Robots)
                {
                    TotalCollisions += robot.CollisionCount;
                }
            }

            // Calculate throughput (tasks per minute)
            if (SimulationUptime > 0)
            {
                int totalTasks = OrdersCompleted + RestocksCompleted;
                Throughput = totalTasks / (SimulationUptime / 60f);
            }
        }

        /// <summary>
        /// Records a data point for time-series graphs.
        /// Called every sampleInterval seconds.
        /// </summary>
        private void SampleDataPoint()
        {
            float t = SimulationUptime;

            // Cumulative reward (sum across all robots - approximated by task completions)
            float cumulativeReward = (OrdersCompleted + RestocksCompleted) * WarehouseConstants.RewardTaskComplete;
            RewardHistory.Add(new Vector2(t, cumulativeReward));

            // Completion time trend
            CompletionTimeHistory.Add(new Vector2(t, AverageDeliveryTime));

            // Collision rate (collisions per minute in the last interval)
            float dt = Time.time - lastSampleTime;
            int newCollisions = TotalCollisions - lastCollisionCount;
            float collisionRate = dt > 0 ? (newCollisions / dt * 60f) : 0f;
            CollisionRateHistory.Add(new Vector2(t, collisionRate));

            // Throughput trend
            ThroughputHistory.Add(new Vector2(t, Throughput));

            // Update rolling counters
            lastOrderCount = OrdersCompleted + RestocksCompleted;
            lastCollisionCount = TotalCollisions;
            lastSampleTime = Time.time;

            // Cap history size to prevent memory issues (keep last 100 points)
            TrimHistory(RewardHistory, 100);
            TrimHistory(CompletionTimeHistory, 100);
            TrimHistory(CollisionRateHistory, 100);
            TrimHistory(ThroughputHistory, 100);
        }

        private void TrimHistory(List<Vector2> history, int maxSize)
        {
            while (history.Count > maxSize)
            {
                history.RemoveAt(0);
            }
        }

        // ──────────────────────────────────────────────
        // EVENT HANDLERS
        // ──────────────────────────────────────────────

        private void OnTaskCompleted(TaskData task)
        {
            // Real-time metrics update on each completion
            // (already handled in UpdateRealtimeMetrics via TaskManager)
        }

        private void OnDestroy()
        {
            if (taskManager != null)
                taskManager.OnTaskCompleted -= OnTaskCompleted;
        }
    }
}
