// RobotCoordinator.cs - Multi-agent coordination layer for collision avoidance and deadlock resolution
// Manages robot priorities, detects deadlocks, and coordinates path negotiation.

using System.Collections.Generic;
using UnityEngine;
using WarehouseSimulation.Core;
using WarehouseSimulation.Agents;

namespace WarehouseSimulation.Managers
{
    /// <summary>
    /// Coordinates multiple robot agents to prevent collisions and resolve deadlocks.
    /// 
    /// Key algorithms:
    /// 1. PRIORITY-BASED NEGOTIATION: Robots with urgent tasks or closer to their targets
    ///    get higher navigation priority (lower NavMeshAgent avoidancePriority value).
    /// 
    /// 2. DEADLOCK DETECTION: Periodically checks if any robot has been stuck
    ///    (minimal movement) for longer than the stuck threshold. Stuck robots get
    ///    rerouted with temporary waypoints.
    /// 
    /// 3. PROXIMITY WARNINGS: Robots approaching each other too closely are flagged
    ///    so the RL agent can incorporate this into its observations.
    /// 
    /// 4. SPAWN MANAGEMENT: Registers and tracks all active robots for coordination.
    /// </summary>
    public class RobotCoordinator : MonoBehaviour
    {
        // All active robots in the simulation
        private List<RobotAgent> robots = new List<RobotAgent>();

        // Deadlock tracking: maps robot index → last recorded position
        private Dictionary<int, Vector3> lastPositions = new Dictionary<int, Vector3>();
        private Dictionary<int, float> stuckTimers = new Dictionary<int, float>();

        private float nextDeadlockCheck;

        /// <summary>Read-only access to all registered robots.</summary>
        public IReadOnlyList<RobotAgent> Robots => robots;

        /// <summary>Number of active robots.</summary>
        public int RobotCount => robots.Count;

        // ──────────────────────────────────────────────
        // PUBLIC API
        // ──────────────────────────────────────────────

        /// <summary>
        /// Register a robot with the coordination system.
        /// Called when a robot is spawned.
        /// </summary>
        public void RegisterRobot(RobotAgent robot)
        {
            if (!robots.Contains(robot))
            {
                robots.Add(robot);
                int idx = robots.Count - 1;
                lastPositions[idx] = robot.transform.position;
                stuckTimers[idx] = 0f;
                Debug.Log($"[RobotCoordinator] Registered robot {idx} (total: {robots.Count})");
            }
        }

        /// <summary>
        /// Unregister a robot. Called when a robot is removed.
        /// </summary>
        public void UnregisterRobot(RobotAgent robot)
        {
            int idx = robots.IndexOf(robot);
            if (idx >= 0)
            {
                robots.RemoveAt(idx);
                lastPositions.Remove(idx);
                stuckTimers.Remove(idx);
            }
        }

        /// <summary>
        /// Get the nearest other robot to the given position.
        /// Used by the RL observation space for nearby robot awareness.
        /// </summary>
        /// <param name="position">Reference position.</param>
        /// <param name="excludeRobot">Robot to exclude (self).</param>
        /// <returns>Nearest robot and distance, or null if no other robots.</returns>
        public (RobotAgent robot, float distance) GetNearestRobot(Vector3 position, RobotAgent excludeRobot)
        {
            RobotAgent nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var robot in robots)
            {
                if (robot == excludeRobot) continue;

                float dist = Vector3.Distance(position, robot.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = robot;
                }
            }

            return (nearest, nearestDist);
        }

        /// <summary>
        /// Get robots within a specified radius of a position.
        /// Used for proximity-based coordination decisions.
        /// </summary>
        public List<RobotAgent> GetRobotsInRadius(Vector3 position, float radius, RobotAgent excludeRobot = null)
        {
            List<RobotAgent> nearby = new List<RobotAgent>();
            float sqrRadius = radius * radius;

            foreach (var robot in robots)
            {
                if (robot == excludeRobot) continue;
                if ((robot.transform.position - position).sqrMagnitude <= sqrRadius)
                {
                    nearby.Add(robot);
                }
            }

            return nearby;
        }

        /// <summary>
        /// Updates navigation priorities based on current task urgency and proximity to target.
        /// Robots closer to completing their task or with higher priority tasks get right-of-way.
        /// 
        /// This uses NavMeshAgent's built-in avoidance priority system:
        /// - Lower value = higher priority = other agents avoid this one more
        /// - Range: 0 (highest) to 99 (lowest)
        /// </summary>
        public void UpdateNavigationPriorities()
        {
            foreach (var robot in robots)
            {
                if (robot == null || robot.NavAgent == null) continue;

                int priority = 50; // Default

                // Robots with urgent tasks get higher priority
                if (robot.CurrentTask != null)
                {
                    switch (robot.CurrentTask.Priority)
                    {
                        case TaskPriority.Urgent:  priority = 20; break;
                        case TaskPriority.Standard: priority = 40; break;
                        case TaskPriority.Low:      priority = 60; break;
                    }

                    // Robots closer to target get slightly higher priority
                    // (prevents blocking at intersections)
                    float distToTarget = robot.DistanceToTarget;
                    if (distToTarget < 3f)
                        priority -= 10;
                }
                else
                {
                    // Idle robots have lowest priority
                    priority = 70;
                }

                priority = Mathf.Clamp(priority, 0, 99);
                robot.NavAgent.avoidancePriority = priority;
            }
        }

        // ──────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────

        private void Update()
        {
            // Periodically check for deadlocks
            if (Time.time >= nextDeadlockCheck)
            {
                nextDeadlockCheck = Time.time + WarehouseConstants.DeadlockCheckInterval;
                DetectAndResolveDeadlocks();
                UpdateNavigationPriorities();
            }
        }

        // ──────────────────────────────────────────────
        // DEADLOCK DETECTION & RESOLUTION
        // ──────────────────────────────────────────────

        /// <summary>
        /// Deadlock detection algorithm:
        /// 
        /// 1. For each robot with an active task, check if it has moved
        ///    more than MinMovementThreshold since the last check.
        /// 2. If not, increment its stuck timer.
        /// 3. If stuck timer exceeds StuckThreshold, the robot is considered deadlocked.
        /// 4. Resolution: Assign a temporary detour waypoint to break the deadlock.
        ///    The waypoint is perpendicular to the robot's current facing direction
        ///    to route it around the obstacle.
        /// </summary>
        private void DetectAndResolveDeadlocks()
        {
            for (int i = 0; i < robots.Count; i++)
            {
                var robot = robots[i];
                if (robot == null || robot.CurrentState == RobotState.Idle || 
                    robot.CurrentState == RobotState.Picking || 
                    robot.CurrentState == RobotState.Delivering)
                    continue;

                Vector3 currentPos = robot.transform.position;
                
                if (!lastPositions.ContainsKey(i))
                {
                    lastPositions[i] = currentPos;
                    stuckTimers[i] = 0f;
                    continue;
                }

                float movedDistance = Vector3.Distance(currentPos, lastPositions[i]);

                if (movedDistance < WarehouseConstants.MinMovementThreshold)
                {
                    // Robot hasn't moved significantly
                    stuckTimers[i] += WarehouseConstants.DeadlockCheckInterval;

                    if (stuckTimers[i] >= WarehouseConstants.StuckThreshold)
                    {
                        ResolveDeadlock(robot, i);
                        stuckTimers[i] = 0f;
                    }
                }
                else
                {
                    // Robot is moving fine, reset timer
                    stuckTimers[i] = 0f;
                }

                lastPositions[i] = currentPos;
            }
        }

        /// <summary>
        /// Resolves a detected deadlock by assigning a temporary detour waypoint.
        /// The waypoint is placed to the side of the robot to route it around
        /// whatever is blocking it.
        /// </summary>
        private void ResolveDeadlock(RobotAgent robot, int robotIndex)
        {
            Debug.LogWarning($"[RobotCoordinator] Deadlock detected for robot {robotIndex}, rerouting");

            // Calculate a detour point perpendicular to current forward direction
            Vector3 right = robot.transform.right;
            
            // Randomly pick left or right detour to avoid symmetric deadlocks
            if (Random.value > 0.5f)
                right = -right;

            Vector3 detourPoint = robot.transform.position + right * 3f;

            // Clamp detour within warehouse bounds
            detourPoint.x = Mathf.Clamp(detourPoint.x, 2f, WarehouseConstants.WarehouseWidth - 2f);
            detourPoint.z = Mathf.Clamp(detourPoint.z, 2f, WarehouseConstants.WarehouseLength - 2f);

            robot.SetTemporaryWaypoint(detourPoint);
        }

        /// <summary>
        /// Clear all tracking data. Used on simulation reset.
        /// </summary>
        public void Reset()
        {
            robots.Clear();
            lastPositions.Clear();
            stuckTimers.Clear();
        }
    }
}
