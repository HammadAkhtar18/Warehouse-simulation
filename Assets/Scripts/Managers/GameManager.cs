// GameManager.cs - Central lifecycle manager for the warehouse simulation
// Handles initialization, simulation control (start/pause/reset), and system coordination.

using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using WarehouseSimulation.Core;
using WarehouseSimulation.Environment;
using WarehouseSimulation.Agents;
using WarehouseSimulation.UI;
using System.Collections.Generic;

namespace WarehouseSimulation.Managers
{
    /// <summary>
    /// Singleton manager that orchestrates the entire warehouse simulation.
    /// 
    /// Initialization order:
    /// 1. Build warehouse environment (WarehouseBuilder)
    /// 2. Initialize InventoryManager with shelf references
    /// 3. Initialize TaskManager with manager references
    /// 4. Spawn robots via RobotCoordinator
    /// 5. Initialize UI and performance tracking
    /// 6. Start simulation
    /// 
    /// Also handles:
    /// - Simulation state (running/paused)
    /// - Time scale control
    /// - Robot count adjustment
    /// - Full simulation reset
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // SINGLETON
        // ──────────────────────────────────────────────
        
        public static GameManager Instance { get; private set; }

        // ──────────────────────────────────────────────
        // COMPONENT REFERENCES
        // ──────────────────────────────────────────────

        [Header("Manager References")]
        [SerializeField] private WarehouseBuilder warehouseBuilder;
        [SerializeField] private InventoryManager inventoryManager;
        [SerializeField] private TaskManager taskManager;
        [SerializeField] private RobotCoordinator robotCoordinator;
        [SerializeField] private PerformanceTracker performanceTracker;
        [SerializeField] private UIManager uiManager;

        [Header("Simulation Settings")]
        [SerializeField] private int robotCount = WarehouseConstants.DefaultRobotCount;
        [SerializeField] private float timeScale = 1f;
        [SerializeField] private Core.SimulationMode simulationMode = Core.SimulationMode.Inference;

        [Header("Robot Prefab")]
        [SerializeField] private GameObject robotPrefab;

        // ── State ──
        private bool isRunning = false;
        private List<RobotAgent> spawnedRobots = new List<RobotAgent>();

        // ── Public Properties ──
        public bool IsRunning => isRunning;
        public int RobotCount => robotCount;
        public float TimeScale => timeScale;
        public Core.SimulationMode Mode => simulationMode;
        public WarehouseBuilder WarehouseBuilder => warehouseBuilder;
        public InventoryManager InventoryManager => inventoryManager;
        public TaskManager TaskManager => taskManager;
        public RobotCoordinator RobotCoordinator => robotCoordinator;
        public PerformanceTracker PerformanceTracker => performanceTracker;

        // ──────────────────────────────────────────────
        // UNITY LIFECYCLE
        // ──────────────────────────────────────────────

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Ensure all managers exist on this GameObject if not assigned
            EnsureManagerComponents();
        }

        private void Start()
        {
            InitializeSimulation();
        }

        // ──────────────────────────────────────────────
        // INITIALIZATION
        // ──────────────────────────────────────────────

        /// <summary>
        /// Full initialization sequence. Called on Start and on Reset.
        /// </summary>
        private void InitializeSimulation()
        {
            Debug.Log("[GameManager] === Initializing Warehouse Simulation ===");

            // Step 1: Build the warehouse environment
            warehouseBuilder.BuildWarehouse();

            // Step 2: Register shelves with inventory manager
            inventoryManager.RegisterShelves(warehouseBuilder.Shelves);

            // Step 3: Initialize task manager with cross-references
            taskManager.Initialize(inventoryManager, robotCoordinator, warehouseBuilder);

            // Step 4: Spawn robots
            SpawnRobots(robotCount);

            // Step 5: Initialize performance tracking
            if (performanceTracker != null)
            {
                performanceTracker.Initialize(taskManager, robotCoordinator);
            }

            // Step 6: Initialize UI
            if (uiManager != null)
            {
                uiManager.Initialize(this);
            }

            // Start simulation
            isRunning = true;
            Time.timeScale = timeScale;

            Debug.Log("[GameManager] === Simulation Started ===");
        }

        /// <summary>
        /// Ensures all required manager components are attached to this GameObject.
        /// Creates missing components automatically for ease of setup.
        /// </summary>
        private void EnsureManagerComponents()
        {
            if (warehouseBuilder == null)
                warehouseBuilder = GetComponentInChildren<WarehouseBuilder>() ?? gameObject.AddComponent<WarehouseBuilder>();
            if (inventoryManager == null)
                inventoryManager = GetComponentInChildren<InventoryManager>() ?? gameObject.AddComponent<InventoryManager>();
            if (taskManager == null)
                taskManager = GetComponentInChildren<TaskManager>() ?? gameObject.AddComponent<TaskManager>();
            if (robotCoordinator == null)
                robotCoordinator = GetComponentInChildren<RobotCoordinator>() ?? gameObject.AddComponent<RobotCoordinator>();
            if (performanceTracker == null)
                performanceTracker = GetComponentInChildren<PerformanceTracker>() ?? gameObject.AddComponent<PerformanceTracker>();
        }

        // ──────────────────────────────────────────────
        // ROBOT SPAWNING
        // ──────────────────────────────────────────────

        /// <summary>
        /// Spawns the specified number of robots at designated spawn points.
        /// If no prefab is assigned, creates robots procedurally.
        /// </summary>
        private void SpawnRobots(int count)
        {
            count = Mathf.Clamp(count, WarehouseConstants.MinRobots, WarehouseConstants.MaxRobots);

            for (int i = 0; i < count; i++)
            {
                Vector3 spawnPos = Vector3.zero;
                if (i < warehouseBuilder.RobotSpawnPoints.Count)
                {
                    spawnPos = warehouseBuilder.RobotSpawnPoints[i];
                }
                else
                {
                    // Fallback spawn position
                    spawnPos = new Vector3(5f + i * 2f, 0f, 3f);
                }

                GameObject robotObj;

                if (robotPrefab != null)
                {
                    robotObj = Instantiate(robotPrefab, spawnPos, Quaternion.identity);
                }
                else
                {
                    // Create robot procedurally if no prefab assigned
                    robotObj = CreateProceduralRobot(spawnPos, i);
                }

                robotObj.name = $"Robot_{i}";

                // IMPORTANT: Configure BehaviorParameters BEFORE adding RobotAgent
                // Agent.Awake() reads BehaviorParameters during initialization,
                // so it must be configured first with the correct action/observation spec.
                var behaviorParams = robotObj.AddComponent<BehaviorParameters>();
                behaviorParams.BehaviorName = "RobotAgent";
                behaviorParams.BrainParameters.VectorObservationSize = 21;
                behaviorParams.BrainParameters.NumStackedVectorObservations = 1;
                behaviorParams.BrainParameters.ActionSpec = new Unity.MLAgents.Actuators.ActionSpec(3, new int[0]);
                behaviorParams.BehaviorType = BehaviorType.Default;

                // Add DecisionRequester before Agent so it's ready when Agent initializes
                var decisionRequester = robotObj.AddComponent<DecisionRequester>();
                decisionRequester.DecisionPeriod = 5;

                // NOW add RobotAgent — it will find the pre-configured BehaviorParameters
                RobotAgent agent = robotObj.AddComponent<RobotAgent>();

                agent.RobotIndex = i;
                agent.Initialize(taskManager, robotCoordinator, inventoryManager);

                // Register with coordinator
                robotCoordinator.RegisterRobot(agent);
                spawnedRobots.Add(agent);
            }

            Debug.Log($"[GameManager] Spawned {count} robots");
        }

        /// <summary>
        /// Creates a robot visually from primitives (no prefab needed).
        /// The robot is a capsule body with a colored status light on top.
        /// </summary>
        private GameObject CreateProceduralRobot(Vector3 position, int index)
        {
            // Main body (capsule)
            GameObject robot = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            robot.transform.position = position + Vector3.up * (WarehouseConstants.RobotHeight / 2f);
            robot.transform.localScale = new Vector3(
                WarehouseConstants.RobotRadius * 2f,
                WarehouseConstants.RobotHeight / 2f,
                WarehouseConstants.RobotRadius * 2f
            );

            // Set robot layer for collision detection
            robot.tag = "Robot";

            // Apply initial material
            Renderer rend = robot.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.3f, 0.3f, 0.35f); // Dark metallic base
            rend.material = mat;

            // Status light (small sphere on top)
            GameObject statusLight = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            statusLight.name = "StatusLight";
            statusLight.transform.SetParent(robot.transform);
            statusLight.transform.localPosition = new Vector3(0, 0.7f, 0);
            statusLight.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
            Destroy(statusLight.GetComponent<Collider>());

            // Carrying indicator (small cube, hidden initially)
            GameObject carryIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            carryIndicator.name = "CarryIndicator";
            carryIndicator.transform.SetParent(robot.transform);
            carryIndicator.transform.localPosition = new Vector3(0, 0.9f, 0.3f);
            carryIndicator.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            Destroy(carryIndicator.GetComponent<Collider>());
            carryIndicator.SetActive(false);

            // Add Rigidbody for physics
            Rigidbody rb = robot.AddComponent<Rigidbody>();
            rb.isKinematic = true; // NavMeshAgent controls movement
            rb.useGravity = false;

            // Add RobotVisuals component
            RobotVisuals visuals = robot.AddComponent<RobotVisuals>();

            return robot;
        }

        // ──────────────────────────────────────────────
        // SIMULATION CONTROL
        // ──────────────────────────────────────────────

        /// <summary>Toggle simulation pause state.</summary>
        public void TogglePause()
        {
            isRunning = !isRunning;
            Time.timeScale = isRunning ? timeScale : 0f;
            Debug.Log($"[GameManager] Simulation {(isRunning ? "resumed" : "paused")}");
        }

        /// <summary>Start the simulation if not running.</summary>
        public void StartSimulation()
        {
            isRunning = true;
            Time.timeScale = timeScale;
        }

        /// <summary>Pause the simulation.</summary>
        public void PauseSimulation()
        {
            isRunning = false;
            Time.timeScale = 0f;
        }

        /// <summary>
        /// Set the simulation time scale (0.5x - 4x).
        /// Used for speeding up or slowing down during training observation.
        /// </summary>
        public void SetTimeScale(float scale)
        {
            timeScale = Mathf.Clamp(scale, 0.5f, 4f);
            if (isRunning)
                Time.timeScale = timeScale;
        }

        /// <summary>
        /// Change the number of active robots.
        /// Destroys all current robots and respawns with new count.
        /// </summary>
        public void SetRobotCount(int count)
        {
            robotCount = Mathf.Clamp(count, WarehouseConstants.MinRobots, WarehouseConstants.MaxRobots);
            
            // Destroy existing robots
            foreach (var robot in spawnedRobots)
            {
                if (robot != null)
                    Destroy(robot.gameObject);
            }
            spawnedRobots.Clear();
            robotCoordinator.Reset();

            // Respawn with new count
            SpawnRobots(robotCount);
        }

        /// <summary>
        /// Full simulation reset: clears everything and reinitializes.
        /// </summary>
        public void ResetSimulation()
        {
            Debug.Log("[GameManager] === Resetting Simulation ===");

            // Destroy robots
            foreach (var robot in spawnedRobots)
            {
                if (robot != null)
                    Destroy(robot.gameObject);
            }
            spawnedRobots.Clear();

            // Reset all systems
            robotCoordinator.Reset();
            taskManager.ResetAll();
            warehouseBuilder.ClearWarehouse();

            if (performanceTracker != null)
                performanceTracker.ResetMetrics();

            // Reinitialize
            InitializeSimulation();
        }
    }
}
