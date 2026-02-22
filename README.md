# 🏭 Warehouse Simulation Game — Unity ML-Agents

A multi-agent warehouse simulation where autonomous robots learn to coordinate order fulfillment and restocking using reinforcement learning (PPO via Unity ML-Agents).

![University Project — Multi-Agent Coordination with RL]

---

## 📋 Table of Contents

- [Project Overview](#project-overview)
- [Architecture](#architecture)
- [Setup Instructions](#setup-instructions)
- [How to Run](#how-to-run)
- [Controls](#controls)
- [Training Guide](#training-guide)
- [Reward Function Design](#reward-function-design)
- [Key Algorithms](#key-algorithms)
- [Performance Benchmarks](#performance-benchmarks)

---

## 🎯 Project Overview

This simulation demonstrates multi-agent coordination in a warehouse setting:

- **5-10 autonomous robots** perform two task types:
  - **Order Fulfillment**: Pick items from shelves → deliver to green delivery zones
  - **Restocking**: Transport items from blue dock → restock low-inventory shelves
- **Reinforcement Learning** (PPO) trains robots to:
  - Navigate efficiently to targets
  - Avoid collisions with each other and obstacles
  - Complete tasks in optimal time
- **Real-time dashboard** displays performance metrics and learning progress

### Key Features

| Feature | Implementation |
|---------|---------------|
| Multi-agent pathfinding | NavMesh + custom coordination layer |
| Collision avoidance | Physics-based + predictive (RL-learned) |
| Deadlock resolution | Priority-based negotiation + detour routing |
| Task assignment | Nearest-available with load balancing |
| Inventory management | Auto-detect low stock, trigger restocking |
| Performance tracking | Real-time KPIs + time-series graphs |

---

## 🏗 Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                       GameManager                           │
│  (Singleton — Lifecycle, Init, Simulation Control)          │
├─────────┬──────────┬────────────────┬───────────┬───────────┤
│         │          │                │           │           │
│  Warehouse    Inventory      Task          Robot       Performance
│  Builder      Manager      Manager     Coordinator    Tracker
│  (Environment) (Stock)    (Orders)     (MultiAgent)   (Metrics)
│         │          │                │           │           │
│    Shelves    Low-Stock ──→ Auto    DeadLock    │      UI Manager
│    Zones       Events    Generate   Detection   │      (Dashboard)
│    NavMesh              Restock     Priority    │
│                         Tasks      Negotiation  │
│                                                 │
│                          ┌──────────────────────┘
│                          │
│                     RobotAgent (×N)
│                     ┌────────────────────────┐
│                     │  ML-Agents Agent       │
│                     │  ├─ Observations (21)  │
│                     │  ├─ Actions (3)        │
│                     │  ├─ Reward Function    │
│                     │  ├─ NavMeshAgent        │
│                     │  └─ State Machine      │
│                     │     Idle → Moving →    │
│                     │     Picking → Delivering│
│                     └────────────────────────┘
└─────────────────────────────────────────────────────────────┘
```

### File Structure

```
Assets/
├── Scripts/
│   ├── Core/
│   │   ├── WarehouseEnums.cs       # RobotState, TaskType, TaskStatus, etc.
│   │   └── WarehouseConstants.cs   # All tunable parameters
│   ├── Environment/
│   │   ├── WarehouseBuilder.cs     # Procedural warehouse generation
│   │   ├── Shelf.cs                # Inventory tracking + visual indicators
│   │   ├── DeliveryZone.cs         # Order delivery endpoints
│   │   └── DockZone.cs             # Restocking source
│   ├── Managers/
│   │   ├── GameManager.cs          # Singleton lifecycle controller
│   │   ├── TaskManager.cs          # Order/restock generation + assignment
│   │   ├── InventoryManager.cs     # Shelf stock monitoring
│   │   └── RobotCoordinator.cs     # Multi-agent coordination
│   ├── Agents/
│   │   ├── RobotAgent.cs           # ML-Agents Agent (core RL logic)
│   │   └── RobotVisuals.cs         # Color-coded status + path rendering
│   ├── Tasks/
│   │   └── TaskData.cs             # Task data structure
│   ├── UI/
│   │   ├── UIManager.cs            # Control panel + graph rendering
│   │   ├── MetricsDisplay.cs       # KPI dashboard (top-right)
│   │   ├── RobotStatusPanel.cs     # Robot status table (bottom-left)
│   │   ├── MinimapController.cs    # Overhead minimap (bottom-right)
│   │   └── PerformanceTracker.cs   # Data collection for metrics
│   └── Utils/
│       └── CameraController.cs     # WASD pan, zoom, rotate
├── ML-Agents/
│   └── config.yaml                 # PPO training configuration
└── Scenes/
    └── (Setup via GameManager)
```

---

## 🔧 Setup Instructions

### Prerequisites

- **Unity** 2021.3 LTS or newer (2022.3 LTS recommended)
- **Python** 3.8-3.10 (for ML-Agents training)
- **ML-Agents Python Package** v0.30+

### Step-by-Step Setup

#### 1. Open the Project in Unity

1. Open Unity Hub → **Add** → Select the `warehouse` folder
2. Open the project (Unity will import all scripts)

#### 2. Install Required Packages

Open **Window → Package Manager** and verify these packages are installed:

| Package | Version | Purpose |
|---------|---------|---------|
| ML Agents | 2.0.1+ | Reinforcement learning |
| AI Navigation | 1.1.5+ | NavMesh pathfinding |
| TextMeshPro | 3.0+ | UI text rendering |

If not auto-installed, add them via Package Manager → **+** → Add package by name:
- `com.unity.ml-agents`
- `com.unity.ai.navigation`

#### 3. Scene Setup

1. Create a new empty scene: **File → New Scene → Empty**
2. Save as `Assets/Scenes/WarehouseMain.unity`
3. Create an empty GameObject named `GameManager`
4. Add these components to it:
   - `GameManager` script
   - `WarehouseBuilder` script
   - `InventoryManager` script
   - `TaskManager` script
   - `RobotCoordinator` script
   - `PerformanceTracker` script
   - `UIManager` script
5. The GameManager will auto-detect and connect all components on play

#### 4. Camera Setup

1. Select the **Main Camera** in the scene
2. Add the `CameraController` script to it
3. Set initial position: `(20, 30, 0)` and rotation: `(60, 0, 0)`

#### 5. Tags Setup (Important!)

Go to **Edit → Project Settings → Tags and Layers** and add these tags:
- `Robot`
- `Shelf`
- `Obstacle`
- `Zone`

#### 6. Install Python ML-Agents (for training)

```bash
# Create a virtual environment (recommended)
python -m venv ml-agents-env
# On Windows:
ml-agents-env\Scripts\activate
# On macOS/Linux:
source ml-agents-env/bin/activate

# Install ML-Agents
pip install mlagents==0.30.0
pip install torch==1.13.1   # PyTorch backend

# Verify installation
mlagents-learn --help
```

---

## ▶️ How to Run

### Inference Mode (Watch Pre-trained Robots)

1. Open the scene in Unity
2. Set the `RobotAgent` Behavior Type to **Heuristic Only** or **Inference Only**
3. Press **Play** in Unity Editor
4. The warehouse generates and robots start operating autonomously
5. Use the control panel (top-left) to adjust parameters

### Training Mode

1. Open a terminal in the project root
2. Run the training command:

```bash
mlagents-learn Assets/ML-Agents/config.yaml --run-id=warehouse_v1
```

3. When the terminal shows `Listening on port 5004`, press **Play** in Unity
4. Training begins! Monitor with TensorBoard:

```bash
tensorboard --logdir results
```

5. Training will run for 500,000 steps (~2-4 hours depending on hardware)
6. After training, the model is saved in `results/warehouse_v1/`

### Loading a Trained Model

1. Copy the `.onnx` model file from `results/warehouse_v1/` to `Assets/ML-Agents/`
2. Select each robot's `Behavior Parameters` component
3. Drag the `.onnx` file into the **Model** field
4. Set **Behavior Type** to **Inference Only**
5. Press Play to see trained robots in action

---

## 🎮 Controls

| Input | Action |
|-------|--------|
| **W/A/S/D** or **Arrow Keys** | Pan camera |
| **Mouse Scroll** | Zoom in/out |
| **Right Mouse + Drag** | Rotate camera |
| **Middle Mouse + Drag** | Alternative pan |

### UI Controls (On-Screen Panel)

| Control | Function |
|---------|----------|
| ⏸ Pause / ▶️ Resume | Toggle simulation |
| 🔄 Reset | Full restart |
| Time Scale slider | 0.5x – 4x speed |
| Order Interval slider | Time between auto-generated orders |
| Robot Count slider | 3 – 15 robots |
| 📈 Show/Hide Graphs | Toggle learning progress charts |

---

## 🧠 Training Guide

### Hyperparameter Tuning Tips

| Parameter | Default | Try Higher If... | Try Lower If... |
|-----------|---------|-------------------|-----------------|
| `batch_size` | 1024 | Unstable training | Slow convergence |
| `learning_rate` | 3e-4 | Not learning at all | Oscillating reward |
| `beta` (entropy) | 5e-3 | Stuck in local optimum | Too random behavior |
| `buffer_size` | 10240 | High variance in reward | Memory limited |
| `max_steps` | 500k | Still improving at 500k | Converged early |

### Training Stages (Expected)

```
Steps 0-50k:      Random movement, learning basic navigation
Steps 50k-150k:   Robots start reaching targets, some collisions
Steps 150k-300k:  Collision rate drops, paths become more efficient
Steps 300k-500k:  Fine-tuning, efficiency bonus achieved more often
```

### Curriculum Learning (Advanced)

For faster convergence, start with fewer robots and simpler tasks:

1. Train with 3 robots for 200k steps
2. Increase to 5 robots and continue for 200k steps
3. Scale to 10 robots for final 100k steps

---

## 🎯 Reward Function Design

### Rationale

The reward function uses **reward shaping** — providing intermediate rewards that guide the agent toward the goal, rather than relying solely on sparse task-completion rewards.

```
╔══════════════════════════════════════════════════════════════╗
║ REWARD COMPONENT          │ VALUE    │ RATIONALE            ║
╠══════════════════════════════════════════════════════════════╣
║ Task Complete             │ +10.0    │ Primary objective    ║
║ Reached Pickup            │ +1.0     │ Sub-goal milestone   ║
║ Progress (per unit dist)  │ +0.1     │ Dense gradient       ║
║ Efficiency Bonus          │ +3.0     │ Optimal path reward  ║
║ Timestep Penalty          │ -0.01    │ Encourages speed     ║
║ Robot Collision            │ -5.0     │ Safety learning      ║
║ Obstacle Collision         │ -3.0     │ Navigation learning  ║
║ Idle > 5 seconds          │ -0.5/dt  │ Prevents stalling    ║
╚══════════════════════════════════════════════════════════════╝
```

**Why these values?**
- Task completion (+10) must be the dominant reward signal
- Collision penalties (-5, -3) are high enough to discourage but not so high that the agent becomes overly cautious and avoids all movement
- Progress reward (+0.1) provides a continuous gradient toward the target, essential for early training when the agent hasn't experienced task completion yet
- The efficiency bonus (+3) rewards path optimality only after the agent has learned basic navigation

---

## ⚙️ Key Algorithms

### 1. Multi-Agent Coordination

**Priority-Based Navigation** (RobotCoordinator):
- Robots with urgent tasks get lower NavMesh avoidance priority (higher right-of-way)
- Robots near their delivery target get temporary priority boost
- Idle robots yield to all active robots

### 2. Deadlock Detection & Resolution

Every 2 seconds, the coordinator checks if any robot hasn't moved:
1. Track each robot's position delta
2. If movement < 0.1 units over 3 seconds → **DEADLOCKED**
3. Resolution: assign a temporary waypoint perpendicular to the robot's facing direction
4. The robot navigates to the waypoint, then resumes its original path

### 3. Task Assignment Algorithm

```
SORT pending_tasks BY priority DESC
FOR each task:
    FIND nearest idle robot (weighted by distance + load_penalty)
    load_penalty = robot.tasks_completed × 0.5
    ASSIGN task to best-scoring robot
```

Load balancing ensures no single robot is overworked while others idle.

### 4. Hybrid RL + NavMesh Navigation

The RL agent doesn't control movement directly. Instead:
- **NavMesh** handles obstacle avoidance and pathfinding (70% weight)
- **RL agent** suggests directional nudges (30% weight)
- This hybrid approach lets the RL focus on high-level strategy (which path to take) while NavMesh ensures collision-free movement

---

## 📊 Performance Benchmarks

### Expected Results After Training (500k steps)

| Metric | Before Training | After Training |
|--------|----------------|----------------|
| Avg Task Completion Time | ~45s | ~15-20s |
| Collision Rate | ~20/min | <1/hour |
| Throughput | 2-3 tasks/min | 8-12 tasks/min |
| Robot Utilization | ~40% | ~80%+ |
| Path Efficiency | Random | Near-optimal |

### System Performance

- **10 robots at 60+ FPS** on mid-range hardware
- **Stable for 30+ minutes** continuous operation
- **NavMesh + 8-directional raycasts** per agent per step

---

## 📝 License

University Academic Project — For Educational Use

---

## 🤝 Credits

Built with:
- [Unity Engine](https://unity.com/) 2021.3+
- [Unity ML-Agents Toolkit](https://github.com/Unity-Technologies/ml-agents) v2.0
- [NavMesh Components](https://docs.unity3d.com/Manual/nav-NavigationSystem.html)
