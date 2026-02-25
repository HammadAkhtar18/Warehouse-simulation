// RobotAgent.cs - ML-Agents reinforcement learning robot agent
// This is the CORE ML component: inherits from Unity.MLAgents.Agent
// Contains the observation space, action space, and reward function.

using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using WarehouseSimulation.Core;
using WarehouseSimulation.Tasks;
using WarehouseSimulation.Managers;
using WarehouseSimulation.Environment;

namespace WarehouseSimulation.Agents
{
    /// <summary>
    /// Individual robot agent using ML-Agents for reinforcement learning.
    /// 
    /// ╔══════════════════════════════════════════════════════════════╗
    /// ║  REINFORCEMENT LEARNING DESIGN                             ║
    /// ╠══════════════════════════════════════════════════════════════╣
    /// ║                                                            ║
    /// ║  OBSERVATIONS (~15 floats):                                ║
    /// ║  • Robot position (x, z)                       [2 floats]  ║
    /// ║  • Target position (x, z)                      [2 floats]  ║
    /// ║  • Distance to target (normalized)             [1 float]   ║
    /// ║  • Current velocity (x, z)                     [2 floats]  ║
    /// ║  • Obstacle raycasts (8 directions)            [8 floats]  ║
    /// ║  • Task type (one-hot: order/restock)           [2 floats]  ║
    /// ║  • Has item (boolean as float)                 [1 float]   ║
    /// ║  • Nearest robot distance (normalized)         [1 float]   ║
    /// ║  • Nearest robot relative direction (x, z)     [2 floats]  ║
    /// ║  Total: ~21 observations                                   ║
    /// ║                                                            ║
    /// ║  ACTIONS (continuous):                                     ║
    /// ║  • Move direction X      [-1, 1]                           ║
    /// ║  • Move direction Z      [-1, 1]                           ║
    /// ║  • Speed scalar          [0, 1]                            ║
    /// ║                                                            ║
    /// ║  REWARD FUNCTION:                                          ║
    /// ║  +10.0  Task completed                                     ║
    /// ║  +1.0   Reached pickup point                               ║
    /// ║  +0.1   Per unit moved toward target                       ║
    /// ║  +3.0   Efficiency bonus (near-optimal time)               ║
    /// ║  -0.01  Per timestep (encourages speed)                    ║
    /// ║  -5.0   Collision with another robot                       ║
    /// ║  -3.0   Collision with obstacle                            ║
    /// ║  -0.5   Idle for >5 seconds                                ║
    /// ║                                                            ║
    /// ╚══════════════════════════════════════════════════════════════╝
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class RobotAgent : Agent
    {
        // ──────────────────────────────────────────────
        // CONFIGURATION
        // ──────────────────────────────────────────────

        [Header("Robot Identity")]
        public int RobotIndex;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;

        // ── Component References ──
        private NavMeshAgent navAgent;
        private RobotVisuals visuals;
        private Rigidbody rb;

        // ── Manager References ──
        private TaskManager taskManager;
        private RobotCoordinator coordinator;
        private InventoryManager inventoryManager;

        // ── State ──
        private RobotState currentState = RobotState.Idle;
        private TaskData currentTask;
        private Vector3 currentTarget;
        private float previousDistanceToTarget;
        private float idleTimer;
        private int tasksCompleted;
        private bool isCarryingItem;
        private float episodeStartTime;
        private int collisionCount;
        private Vector3 temporaryWaypoint;
        private bool hasTemporaryWaypoint;
        private float waypointTimeout;
        private bool isInteracting; // True during pick/delivery delay

        // ── Public Properties ──
        public RobotState CurrentState => currentState;
        public TaskData CurrentTask => currentTask;
        public NavMeshAgent NavAgent => navAgent;
        public int TasksCompleted => tasksCompleted;
        public bool IsCarryingItem => isCarryingItem;
        public int CollisionCount => collisionCount;

        /// <summary>Distance to current navigation target.</summary>
        public float DistanceToTarget => Vector3.Distance(transform.position, currentTarget);

        // ──────────────────────────────────────────────
        // INITIALIZATION
        // ──────────────────────────────────────────────

        /// <summary>
        /// Called by GameManager after spawning.
        /// Sets up references to manager systems.
        /// </summary>
        public void Initialize(TaskManager tm, RobotCoordinator rc, InventoryManager im)
        {
            taskManager = tm;
            coordinator = rc;
            inventoryManager = im;
        }

        public override void Initialize()
        {
            base.Initialize();

            navAgent = GetComponent<NavMeshAgent>();
            visuals = GetComponent<RobotVisuals>();
            rb = GetComponent<Rigidbody>();

            // Set MaxStep so ML-Agents auto-ends episodes
            // This is the NATIVE way to handle episode length limits
            MaxStep = WarehouseConstants.MaxEpisodeSteps;

            // Configure NavMeshAgent for realistic warehouse robot movement
            ConfigureNavAgent();
        }

        /// <summary>
        /// Configures the NavMeshAgent with realistic warehouse robot parameters.
        /// These imitate the movement characteristics of an actual AGV (Automated Guided Vehicle).
        /// </summary>
        private void ConfigureNavAgent()
        {
            if (navAgent == null) return;

            navAgent.speed = WarehouseConstants.MaxSpeed;
            navAgent.acceleration = WarehouseConstants.Acceleration;
            navAgent.angularSpeed = WarehouseConstants.AngularSpeed;
            navAgent.stoppingDistance = WarehouseConstants.StoppingDistance;
            navAgent.radius = WarehouseConstants.NavMeshAgentRadius;
            navAgent.height = WarehouseConstants.RobotHeight;
            navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            navAgent.avoidancePriority = WarehouseConstants.AvoidancePriority;
            navAgent.autoBraking = true;
            navAgent.autoRepath = true;
        }

        // ──────────────────────────────────────────────
        // ML-AGENTS: EPISODE MANAGEMENT
        // ──────────────────────────────────────────────

        /// <summary>
        /// Called at the start of each training episode.
        /// Resets the robot to a random spawn position and clears all state.
        /// </summary>
        public override void OnEpisodeBegin()
        {
            Debug.Log($"[Robot {RobotIndex}] Episode BEGIN (StepCount was {StepCount})");

            // Reset state
            currentState = RobotState.Idle;
            currentTask = null;
            isCarryingItem = false;
            isInteracting = false;
            idleTimer = 0f;
            collisionCount = 0;
            hasTemporaryWaypoint = false;
            episodeStartTime = Time.time;
            previousDistanceToTarget = float.MaxValue;

            // Randomize spawn position for training generalization
            RandomizePosition();

            // Reset NavAgent
            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.ResetPath();
                navAgent.isStopped = false;
            }

            // Proactively request a task so the robot starts working immediately
            if (taskManager != null)
            {
                taskManager.RequestTaskForRobot(this);
            }

            // Update visuals
            UpdateVisuals();
        }

        /// <summary>
        /// Teleports robot to a random valid NavMesh position within the warehouse.
        /// Ensures diverse training experiences across different starting locations.
        /// </summary>
        private void RandomizePosition()
        {
            float w = WarehouseConstants.WarehouseWidth;
            float l = WarehouseConstants.WarehouseLength;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector3 randomPos = new Vector3(
                    Random.Range(3f, w - 3f),
                    0f,
                    Random.Range(3f, l - 3f)
                );

                if (NavMesh.SamplePosition(randomPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                {
                    if (navAgent != null)
                    {
                        navAgent.Warp(hit.position);
                    }
                    else
                    {
                        transform.position = hit.position + Vector3.up * (WarehouseConstants.RobotHeight / 2f);
                    }
                    return;
                }
            }
        }

        // ──────────────────────────────────────────────
        // ML-AGENTS: OBSERVATION COLLECTION
        // ──────────────────────────────────────────────

        /// <summary>
        /// Collects observations for the neural network.
        /// 
        /// Observation design rationale:
        /// - Position data is normalized to warehouse dimensions for faster learning
        /// - Raycasts provide spatial awareness of nearby obstacles
        /// - Task info (one-hot encoded) tells the agent what behavior is expected
        /// - Nearby robot info enables cooperative behavior learning
        /// </summary>
        public override void CollectObservations(VectorSensor sensor)
        {
            try
            {
                float wNorm = WarehouseConstants.WarehouseWidth;
                float lNorm = WarehouseConstants.WarehouseLength;

                // ── 1. Robot position (normalized) [2 floats] ──
                sensor.AddObservation(transform.position.x / wNorm);
                sensor.AddObservation(transform.position.z / lNorm);

                // ── 2. Target position (normalized) [2 floats] ──
                sensor.AddObservation(currentTarget.x / wNorm);
                sensor.AddObservation(currentTarget.z / lNorm);

                // ── 3. Distance to target (normalized) [1 float] ──
                float maxDist = Mathf.Sqrt(wNorm * wNorm + lNorm * lNorm);
                float dist = Vector3.Distance(transform.position, currentTarget);
                sensor.AddObservation(dist / maxDist);

                // ── 4. Current velocity (normalized) [2 floats] ──
                Vector3 velocity = navAgent != null ? navAgent.velocity : Vector3.zero;
                sensor.AddObservation(velocity.x / WarehouseConstants.MaxSpeed);
                sensor.AddObservation(velocity.z / WarehouseConstants.MaxSpeed);

                // ── 5. Obstacle raycasts (8 directions, normalized distance) [8 floats] ──
                float rayDist = WarehouseConstants.RaycastDistance;
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * 45f;
                    Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                    
                    if (Physics.Raycast(transform.position + Vector3.up * 0.5f, direction, out RaycastHit hit, rayDist))
                    {
                        sensor.AddObservation(hit.distance / rayDist);
                    }
                    else
                    {
                        sensor.AddObservation(1f);
                    }
                }

                // ── 6. Task type (one-hot encoded) [2 floats] ──
                if (currentTask != null)
                {
                    sensor.AddObservation(currentTask.Type == TaskType.OrderFulfillment ? 1f : 0f);
                    sensor.AddObservation(currentTask.Type == TaskType.Restocking ? 1f : 0f);
                }
                else
                {
                    sensor.AddObservation(0f);
                    sensor.AddObservation(0f);
                }

                // ── 7. Carrying item status [1 float] ──
                sensor.AddObservation(isCarryingItem ? 1f : 0f);

                // ── 8. Nearest robot info [3 floats] ──
                if (coordinator != null)
                {
                    var (nearestRobot, nearestDist) = coordinator.GetNearestRobot(transform.position, this);
                    if (nearestRobot != null)
                    {
                        sensor.AddObservation(nearestDist / maxDist);
                        Vector3 dirToRobot = (nearestRobot.transform.position - transform.position).normalized;
                        sensor.AddObservation(dirToRobot.x);
                        sensor.AddObservation(dirToRobot.z);
                    }
                    else
                    {
                        sensor.AddObservation(1f);
                        sensor.AddObservation(0f);
                        sensor.AddObservation(0f);
                    }
                }
                else
                {
                    sensor.AddObservation(1f);
                    sensor.AddObservation(0f);
                    sensor.AddObservation(0f);
                }

                // Total observations: 2 + 2 + 1 + 2 + 8 + 2 + 1 + 3 = 21
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Robot {RobotIndex}] CollectObservations EXCEPTION: {e.Message}\n{e.StackTrace}");
                // Can't easily pad to exact count, but the error message is what matters
            }
        }

        // ──────────────────────────────────────────────
        // ML-AGENTS: ACTION EXECUTION
        // ──────────────────────────────────────────────

        /// <summary>
        /// Receives actions from the neural network and applies them.
        /// 
        /// Action space design:
        /// - actions[0]: X direction [-1, 1] — lateral movement
        /// - actions[1]: Z direction [-1, 1] — forward/backward movement
        /// - actions[2]: Speed scalar [0, 1] — how fast to move (mapped to min-max speed)
        /// 
        /// The RL agent learns to control the robot's movement direction and speed
        /// while the NavMeshAgent handles pathfinding and obstacle avoidance.
        /// This hybrid approach lets the agent focus on high-level navigation decisions
        /// while NavMesh handles low-level collision avoidance.
        /// </summary>
        public override void OnActionReceived(ActionBuffers actions)
        {
            // ── DIAGNOSTIC: Log step count periodically for Robot 0 ──
            if (RobotIndex == 0 && StepCount % 200 == 0)
            {
                Debug.Log($"[Robot 0] StepCount={StepCount}, MaxStep={MaxStep}, isInteracting={isInteracting}, hasTask={currentTask != null}");
            }

            // ══════════════════════════════════════════════
            // EPISODE TERMINATION — MUST run before any early returns!
            // ══════════════════════════════════════════════

            // ── Episode time limit (failsafe — 60 sim seconds) ──
            float episodeElapsed = Time.time - episodeStartTime;
            if (episodeElapsed > 60f)
            {
                Debug.Log($"[Robot {RobotIndex}] TIME LIMIT (60s sim). StepCount={StepCount}. Ending episode.");
                AddReward(-2f);
                EndEpisode();
                return;
            }

            // ── Episode step limit ──
            if (StepCount >= WarehouseConstants.MaxEpisodeSteps)
            {
                Debug.Log($"[Robot {RobotIndex}] MAX STEPS reached ({StepCount}). Ending episode.");
                AddReward(-2f);
                EndEpisode();
                return;
            }

            // ══════════════════════════════════════════════

            if (isInteracting) return; // Don't move during pick/delivery

            // ── Extract actions ──
            float moveX = actions.ContinuousActions[0];
            float moveZ = actions.ContinuousActions[1];
            float speedScalar = Mathf.Clamp01((actions.ContinuousActions[2] + 1f) / 2f); // Map [-1,1] to [0,1]

            // ── Apply movement ──
            if (currentTask != null && navAgent != null && navAgent.isOnNavMesh)
            {
                // Determine the actual target (temporary waypoint or task target)
                Vector3 target = hasTemporaryWaypoint ? temporaryWaypoint : currentTarget;

                // Use the RL action to influence the NavMesh target
                // Blend between direct-to-target path and RL-suggested direction
                Vector3 rlDirection = new Vector3(moveX, 0, moveZ).normalized;
                Vector3 directDirection = (target - transform.position).normalized;
                
                // The RL agent can nudge the path (30% influence) while NavMesh handles main navigation
                Vector3 blendedDirection = Vector3.Lerp(directDirection, rlDirection, 0.3f).normalized;
                
                // Set NavMesh destination slightly ahead in the blended direction
                Vector3 navTarget = transform.position + blendedDirection * 3f;
                
                // For close-range, go directly to the actual target
                float distToTarget = Vector3.Distance(transform.position, target);
                if (distToTarget < 3f)
                {
                    navTarget = target;
                }

                navAgent.SetDestination(navTarget);

                // Adjust speed based on RL action
                float speed = Mathf.Lerp(WarehouseConstants.MinSpeed, WarehouseConstants.MaxSpeed, speedScalar);
                navAgent.speed = speed;
            }

            // ── Calculate rewards ──
            CalculateStepRewards();

            // ── Proximity-based collision detection ──
            // (OnCollisionEnter doesn't work with kinematic Rigidbodies + NavMeshAgent)
            CheckProximityCollisions();

            // ── Check for task interactions ──
            CheckInteractions();

            // ── Handle temporary waypoint timeout ──
            if (hasTemporaryWaypoint)
            {
                waypointTimeout -= Time.deltaTime;
                float distToWaypoint = Vector3.Distance(transform.position, temporaryWaypoint);
                if (distToWaypoint < 1.5f || waypointTimeout <= 0f)
                {
                    hasTemporaryWaypoint = false;
                }
            }

            // ── Update state and visuals ──
            UpdateState();
            UpdateVisuals();
        }

        /// <summary>
        /// Heuristic input for manual testing (arrow keys / WASD equivalent).
        /// Only active when running without a trained model (behavior = Heuristic).
        /// </summary>
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var continuousActions = actionsOut.ContinuousActions;

            // Use simple direction-to-target heuristic for testing
            if (currentTask != null)
            {
                Vector3 dir = (currentTarget - transform.position).normalized;
                continuousActions[0] = dir.x;
                continuousActions[1] = dir.z;
                continuousActions[2] = 1f; // Full speed
            }
            else
            {
                continuousActions[0] = 0f;
                continuousActions[1] = 0f;
                continuousActions[2] = 0f;
            }
        }

        // ──────────────────────────────────────────────
        // REWARD CALCULATION
        // ──────────────────────────────────────────────

        /// <summary>
        /// Calculates per-step rewards based on the current state.
        /// 
        /// Reward shaping strategy:
        /// - DENSE REWARDS (per-step progress) enable faster learning by providing
        ///   continuous gradient signal. Without these, the agent would only learn
        ///   from sparse task-completion rewards.
        /// - PENALTY ESCALATION: idle/collision penalties increase with duration
        ///   to strongly discourage unproductive behavior.
        /// </summary>
        private void CalculateStepRewards()
        {
            // ── Time penalty (encourages efficiency) ──
            AddReward(WarehouseConstants.PenaltyTimestep);

            if (currentTask != null)
            {
                // ── Progress reward (dense reward signal) ──
                float currentDist = Vector3.Distance(transform.position, currentTarget);
                float progress = previousDistanceToTarget - currentDist;

                if (progress > 0)
                {
                    // Moving closer to target → positive reward
                    AddReward(progress * WarehouseConstants.RewardProgressPerUnit);
                }

                previousDistanceToTarget = currentDist;
                idleTimer = 0f; // Reset idle timer when we have a task
            }
            else
            {
                // ── Idle penalty (discourages doing nothing) ──
                idleTimer += Time.deltaTime;
                if (idleTimer > WarehouseConstants.IdleTimeThreshold)
                {
                    AddReward(WarehouseConstants.PenaltyIdle * Time.deltaTime);
                }
            }
        }

        // ──────────────────────────────────────────────
        // TASK INTERACTION
        // ──────────────────────────────────────────────

        /// <summary>
        /// Checks if the robot is close enough to interact with its current target.
        /// Handles pickup and delivery actions with appropriate delays.
        /// </summary>
        private void CheckInteractions()
        {
            if (currentTask == null || isInteracting) return;

            float distToTarget = Vector3.Distance(transform.position, currentTarget);

            if (distToTarget <= WarehouseConstants.InteractionDistance)
            {
                if (!currentTask.ItemPickedUp)
                {
                    // At pickup location → start picking
                    StartCoroutine(PerformPickup());
                }
                else
                {
                    // At delivery location → complete delivery
                    StartCoroutine(PerformDelivery());
                }
            }
        }

        /// <summary>
        /// Simulates item pickup with a delay.
        /// Changes state to Picking, waits, then transitions to delivery phase.
        /// </summary>
        private IEnumerator PerformPickup()
        {
            isInteracting = true;
            currentState = RobotState.Picking;
            UpdateVisuals();

            // Stop movement during pickup
            if (navAgent != null && navAgent.isOnNavMesh)
                navAgent.isStopped = true;

            // Pickup delay (simulates forklift operation)
            yield return new WaitForSeconds(WarehouseConstants.PickupDuration);

            // Execute pickup logic
            if (currentTask.Type == TaskType.OrderFulfillment)
            {
                // Pick item from shelf
                Shelf shelf = currentTask.TargetShelf?.GetComponent<Shelf>();
                if (shelf != null)
                {
                    shelf.PickItems(WarehouseConstants.OrderPickAmount);
                }
            }
            else if (currentTask.Type == TaskType.Restocking)
            {
                // Pick item from dock
                DockZone dock = currentTask.TargetZone?.GetComponent<DockZone>();
                if (dock != null)
                {
                    dock.OnItemPickedUp();
                }
            }

            currentTask.ItemPickedUp = true;
            isCarryingItem = true;
            currentTask.StartedTime = Time.time;

            // Reward for reaching pickup point
            AddReward(WarehouseConstants.RewardPickup);

            // Set new target to delivery position
            currentTarget = currentTask.DeliveryPosition;
            previousDistanceToTarget = Vector3.Distance(transform.position, currentTarget);

            // Resume movement
            if (navAgent != null && navAgent.isOnNavMesh)
                navAgent.isStopped = false;

            currentState = RobotState.Moving;
            isInteracting = false;
            UpdateVisuals();
        }

        /// <summary>
        /// Simulates item delivery with a delay.
        /// Completes the task, grants rewards, and returns robot to idle.
        /// </summary>
        private IEnumerator PerformDelivery()
        {
            isInteracting = true;
            currentState = currentTask.Type == TaskType.Restocking
                ? RobotState.Restocking
                : RobotState.Delivering;
            UpdateVisuals();

            // Stop movement during delivery
            if (navAgent != null && navAgent.isOnNavMesh)
                navAgent.isStopped = true;

            // Delivery delay
            float duration = currentTask.Type == TaskType.Restocking
                ? WarehouseConstants.RestockDuration
                : WarehouseConstants.DeliveryDuration;

            yield return new WaitForSeconds(duration);

            // Execute delivery logic
            if (currentTask.Type == TaskType.OrderFulfillment)
            {
                DeliveryZone zone = currentTask.TargetZone?.GetComponent<DeliveryZone>();
                if (zone != null)
                {
                    zone.OnDeliveryComplete();
                }
            }
            else if (currentTask.Type == TaskType.Restocking)
            {
                Shelf shelf = currentTask.TargetShelf?.GetComponent<Shelf>();
                if (shelf != null)
                {
                    shelf.RestockItems(WarehouseConstants.RestockAmount);
                }
            }

            // ── REWARD: Task completion ──
            AddReward(WarehouseConstants.RewardTaskComplete);

            // ── REWARD: Efficiency bonus (completed close to optimal time) ──
            float actualTime = Time.time - currentTask.StartedTime;
            float optimalTime = currentTask.OptimalDistance / WarehouseConstants.MaxSpeed;
            if (optimalTime > 0 && actualTime <= optimalTime * 1.5f)
            {
                AddReward(WarehouseConstants.RewardEfficiencyBonus);
            }

            // Report completion to TaskManager
            if (taskManager != null)
            {
                taskManager.ReportTaskComplete(currentTask);
            }

            // Reset robot state
            tasksCompleted++;
            currentTask = null;
            isCarryingItem = false;
            currentState = RobotState.Idle;

            // Resume NavMeshAgent
            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = false;
                navAgent.ResetPath();
            }

            isInteracting = false;
            UpdateVisuals();

            // End episode after task completion so PPO can compute proper returns
            Debug.Log($"[Robot {RobotIndex}] TASK COMPLETED. Ending episode. Reward={GetCumulativeReward():F2}");
            EndEpisode();
        }

        // ──────────────────────────────────────────────
        // TASK ASSIGNMENT
        // ──────────────────────────────────────────────

        /// <summary>
        /// Called by TaskManager to assign a new task to this robot.
        /// Sets the initial target and transitions from Idle to Moving.
        /// </summary>
        public void AssignTask(TaskData task)
        {
            currentTask = task;
            task.Status = Core.TaskStatus.InProgress;

            // First go to pickup position
            currentTarget = task.PickupPosition;
            previousDistanceToTarget = Vector3.Distance(transform.position, currentTarget);

            currentState = RobotState.Moving;

            // Set NavMesh destination
            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = false;
                navAgent.SetDestination(currentTarget);
            }

            UpdateVisuals();
            Debug.Log($"[Robot {RobotIndex}] Assigned: {task}");
        }

        /// <summary>
        /// Sets a temporary detour waypoint for deadlock resolution.
        /// The robot navigates to this point before continuing to its task target.
        /// </summary>
        public void SetTemporaryWaypoint(Vector3 waypoint)
        {
            temporaryWaypoint = waypoint;
            hasTemporaryWaypoint = true;
            waypointTimeout = 3f; // Timeout after 3 seconds

            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.SetDestination(waypoint);
            }
        }

        // ──────────────────────────────────────────────
        // STATE MANAGEMENT
        // ──────────────────────────────────────────────

        private void UpdateState()
        {
            if (isInteracting) return;

            if (currentTask == null)
            {
                currentState = RobotState.Idle;
            }
            else if (navAgent != null && navAgent.velocity.magnitude < 0.1f &&
                     currentState == RobotState.Moving)
            {
                // Check if stuck
                // (handled by RobotCoordinator deadlock detection)
            }
        }

        private void UpdateVisuals()
        {
            if (visuals != null)
            {
                visuals.UpdateState(currentState, isCarryingItem);
            }
        }

        // ──────────────────────────────────────────────
        // COLLISION HANDLING (Proximity-based)
        // ──────────────────────────────────────────────

        private float lastCollisionCheckTime;
        private const float CollisionCheckInterval = 0.2f; // Check 5x/sec to save perf
        private const float RobotCollisionRadius = 1.2f;   // Slightly larger than physical radius

        /// <summary>
        /// Proximity-based collision detection since kinematic Rigidbodies + NavMeshAgent
        /// don't trigger OnCollisionEnter reliably. Checks distance to nearby robots
        /// and obstacles, applying penalties when too close.
        /// </summary>
        private void CheckProximityCollisions()
        {
            if (Time.time - lastCollisionCheckTime < CollisionCheckInterval) return;
            lastCollisionCheckTime = Time.time;

            // Check robot-to-robot proximity
            if (coordinator != null)
            {
                var (nearestRobot, nearestDist) = coordinator.GetNearestRobot(transform.position, this);
                if (nearestRobot != null && nearestDist < RobotCollisionRadius)
                {
                    AddReward(WarehouseConstants.PenaltyRobotCollision);
                    collisionCount++;
                }
            }

            // Check obstacle proximity via short raycasts
            float checkDist = WarehouseConstants.RobotRadius + 0.3f;
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 dir = Quaternion.Euler(0, i * 90f, 0) * Vector3.forward;
                if (Physics.Raycast(origin, dir, out RaycastHit hit, checkDist))
                {
                    if (hit.collider.CompareTag("Obstacle") || hit.collider.CompareTag("Shelf"))
                    {
                        AddReward(WarehouseConstants.PenaltyObstacleCollision * 0.1f); // Scaled per-check
                        collisionCount++;
                        break; // One penalty per check interval
                    }
                }
            }
        }

        // ──────────────────────────────────────────────
        // DEBUG VISUALIZATION
        // ──────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;

            // Draw line to current target
            if (currentTask != null)
            {
                Gizmos.color = isCarryingItem ? Color.yellow : Color.cyan;
                Gizmos.DrawLine(transform.position, currentTarget);
                Gizmos.DrawWireSphere(currentTarget, 0.5f);
            }

            // Draw temporary waypoint
            if (hasTemporaryWaypoint)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(temporaryWaypoint, 0.3f);
            }
        }
    }
}
