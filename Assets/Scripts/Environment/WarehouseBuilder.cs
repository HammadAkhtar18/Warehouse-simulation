// WarehouseBuilder.cs - Procedural warehouse environment generator
// Creates the entire warehouse layout at runtime: floor, walls, shelves, zones, and NavMesh.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using WarehouseSimulation.Core;

namespace WarehouseSimulation.Environment
{
    /// <summary>
    /// Procedurally generates the warehouse environment including:
    /// - Floor and walls
    /// - Storage shelves in organized grid rows
    /// - Delivery zones (green platforms)
    /// - Restocking dock (blue platform)
    /// - NavMesh surface for robot pathfinding
    /// 
    /// The warehouse layout uses a grid-based design with clear aisles for navigation.
    /// Shelves are placed in rows with sufficient spacing (aisleWidth) for robots to pass.
    /// </summary>
    public class WarehouseBuilder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform warehouseParent;

        // Generated environment references (accessible by managers)
        public List<Shelf> Shelves { get; private set; } = new List<Shelf>();
        public List<DeliveryZone> DeliveryZones { get; private set; } = new List<DeliveryZone>();
        public DockZone Dock { get; private set; }
        public List<Vector3> RobotSpawnPoints { get; private set; } = new List<Vector3>();

        // NavMesh
        private NavMeshSurface navMeshSurface;

        // ──────────────────────────────────────────────
        // PUBLIC API
        // ──────────────────────────────────────────────

        /// <summary>
        /// Builds the entire warehouse environment procedurally.
        /// Call this from GameManager during initialization.
        /// </summary>
        public void BuildWarehouse()
        {
            if (warehouseParent == null)
            {
                warehouseParent = new GameObject("Warehouse").transform;
            }

            Shelves.Clear();
            DeliveryZones.Clear();
            RobotSpawnPoints.Clear();

            CreateFloor();
            CreateWalls();
            CreateShelves();
            CreateDeliveryZones();
            CreateDockZone();
            CreateRobotSpawnPoints();
            BakeNavMesh();

            Debug.Log($"[WarehouseBuilder] Warehouse built: {Shelves.Count} shelves, " +
                      $"{DeliveryZones.Count} delivery zones, {RobotSpawnPoints.Count} spawn points");
        }

        /// <summary>
        /// Destroys all generated warehouse objects. Used for reset.
        /// </summary>
        public void ClearWarehouse()
        {
            if (warehouseParent != null)
            {
                foreach (Transform child in warehouseParent)
                {
                    Destroy(child.gameObject);
                }
            }
            Shelves.Clear();
            DeliveryZones.Clear();
            RobotSpawnPoints.Clear();
        }

        // ──────────────────────────────────────────────
        // FLOOR & WALLS
        // ──────────────────────────────────────────────

        private void CreateFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(warehouseParent);
            floor.transform.position = new Vector3(
                WarehouseConstants.WarehouseWidth / 2f, 
                -0.05f, 
                WarehouseConstants.WarehouseLength / 2f
            );
            floor.transform.localScale = new Vector3(
                WarehouseConstants.WarehouseWidth, 
                0.1f, 
                WarehouseConstants.WarehouseLength
            );

            // Set floor material
            Renderer rend = floor.GetComponent<Renderer>();
            rend.material = CreateMaterial(WarehouseConstants.FloorColor);
            
            // Floor is static for NavMesh baking
            floor.isStatic = true;
            floor.layer = LayerMask.NameToLayer("Default");
        }

        private void CreateWalls()
        {
            float w = WarehouseConstants.WarehouseWidth;
            float l = WarehouseConstants.WarehouseLength;
            float h = WarehouseConstants.WallHeight;
            float t = WarehouseConstants.WallThickness;

            // Four walls around the warehouse perimeter
            CreateWall("Wall_North", new Vector3(w / 2f, h / 2f, l),      new Vector3(w + t, h, t));
            CreateWall("Wall_South", new Vector3(w / 2f, h / 2f, 0),      new Vector3(w + t, h, t));
            CreateWall("Wall_East",  new Vector3(w, h / 2f, l / 2f),      new Vector3(t, h, l));
            CreateWall("Wall_West",  new Vector3(0, h / 2f, l / 2f),      new Vector3(t, h, l));
        }

        private void CreateWall(string name, Vector3 position, Vector3 scale)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(warehouseParent);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.isStatic = true;
            wall.tag = "Obstacle";

            Renderer rend = wall.GetComponent<Renderer>();
            rend.material = CreateMaterial(WarehouseConstants.WallColor);
        }

        // ──────────────────────────────────────────────
        // SHELVES
        // ──────────────────────────────────────────────

        /// <summary>
        /// Creates shelves in a grid pattern with aisles between rows.
        /// Layout: Shelves are centered in the warehouse with clear navigation paths.
        /// 
        /// Grid layout example (4 rows × 5 columns = 20 shelves):
        ///   [S] [S] [S] [S] [S]    <- Row 0
        ///        aisle
        ///   [S] [S] [S] [S] [S]    <- Row 1
        ///        aisle
        ///   [S] [S] [S] [S] [S]    <- Row 2
        ///        aisle
        ///   [S] [S] [S] [S] [S]    <- Row 3
        /// </summary>
        private void CreateShelves()
        {
            float startX = 5f;  // Left margin
            float startZ = 6f;  // Bottom margin (leave room for dock)

            float spacingX = (WarehouseConstants.WarehouseWidth - 2 * startX) /
                             Mathf.Max(1, WarehouseConstants.ShelfColumns - 1);
            float spacingZ = WarehouseConstants.ShelfDepth + WarehouseConstants.AisleWidth;

            int shelfIndex = 0;
            for (int row = 0; row < WarehouseConstants.ShelfRows; row++)
            {
                for (int col = 0; col < WarehouseConstants.ShelfColumns; col++)
                {
                    float x = startX + col * spacingX;
                    float z = startZ + row * spacingZ;

                    GameObject shelfObj = CreateShelfObject(x, z, shelfIndex);
                    Shelf shelf = shelfObj.GetComponent<Shelf>();
                    shelf.ShelfIndex = shelfIndex;

                    // Randomize initial stock for variety
                    float initialStock = Random.Range(20f, 100f);
                    shelf.SetStock(initialStock);

                    Shelves.Add(shelf);
                    shelfIndex++;
                }
            }
        }

        private GameObject CreateShelfObject(float x, float z, int index)
        {
            // Main shelf body
            GameObject shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shelf.name = $"Shelf_{index}";
            shelf.transform.SetParent(warehouseParent);
            shelf.transform.position = new Vector3(x, WarehouseConstants.ShelfHeight / 2f, z);
            shelf.transform.localScale = new Vector3(
                WarehouseConstants.ShelfWidth,
                WarehouseConstants.ShelfHeight,
                WarehouseConstants.ShelfDepth
            );
            shelf.isStatic = true;
            shelf.tag = "Shelf";

            // Add a NavMesh obstacle so robots path around shelves
            NavMeshObstacle obstacle = shelf.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.size = Vector3.one;

            // Add Shelf component
            Shelf shelfComponent = shelf.AddComponent<Shelf>();

            // Create stock level indicator (smaller cube on top that scales with stock)
            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.name = "StockIndicator";
            indicator.transform.SetParent(shelf.transform);
            indicator.transform.localPosition = new Vector3(0, 0.6f, 0);
            indicator.transform.localScale = new Vector3(0.8f, 0.3f, 0.8f);
            
            // Remove collider from indicator (visual only)
            Destroy(indicator.GetComponent<Collider>());

            return shelf;
        }

        // ──────────────────────────────────────────────
        // ZONES
        // ──────────────────────────────────────────────

        private void CreateDeliveryZones()
        {
            float w = WarehouseConstants.WarehouseWidth;
            float size = WarehouseConstants.ZoneSize;

            // Place delivery zones along the right side of the warehouse
            for (int i = 0; i < WarehouseConstants.DeliveryZoneCount; i++)
            {
                float x = w - size;
                float z = 4f + i * (size + 2f);

                GameObject zoneObj = CreateZoneObject($"DeliveryZone_{i}", x, z, size,
                    WarehouseConstants.DeliveryZoneColor);
                
                DeliveryZone zone = zoneObj.AddComponent<DeliveryZone>();
                zone.ZoneIndex = i;
                DeliveryZones.Add(zone);
            }
        }

        private void CreateDockZone()
        {
            float size = WarehouseConstants.ZoneSize + 1f;

            // Place dock on the left side of the warehouse, near the bottom
            float x = 2f;
            float z = 2f;

            GameObject dockObj = CreateZoneObject("DockZone", x, z, size,
                WarehouseConstants.DockZoneColor);

            Dock = dockObj.AddComponent<DockZone>();
        }

        private GameObject CreateZoneObject(string name, float x, float z, float size, Color color)
        {
            GameObject zone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zone.name = name;
            zone.transform.SetParent(warehouseParent);
            zone.transform.position = new Vector3(x, 0.05f, z);
            zone.transform.localScale = new Vector3(size, 0.1f, size);
            zone.tag = "Zone";

            Renderer rend = zone.GetComponent<Renderer>();
            Material mat = CreateMaterial(color);
            mat.SetFloat("_Mode", 3); // Transparent mode
            rend.material = mat;

            // Zone collider should be a trigger (robots don't physically bump into zones)
            zone.GetComponent<Collider>().isTrigger = true;

            return zone;
        }

        // ──────────────────────────────────────────────
        // ROBOT SPAWN POINTS
        // ──────────────────────────────────────────────

        private void CreateRobotSpawnPoints()
        {
            // Create spawn points in the open area near the dock
            float startX = 6f;
            float startZ = 2f;

            for (int i = 0; i < WarehouseConstants.MaxRobots; i++)
            {
                float x = startX + (i % 5) * 2.5f;
                float z = startZ + (i / 5) * 2.5f;
                RobotSpawnPoints.Add(new Vector3(x, 0f, z));
            }
        }

        // ──────────────────────────────────────────────
        // NAVMESH
        // ──────────────────────────────────────────────

        /// <summary>
        /// Bakes the NavMesh at runtime so robots can navigate.
        /// Requires the NavMeshSurface component from the AI Navigation package.
        /// </summary>
        private void BakeNavMesh()
        {
            // Add NavMeshSurface to the floor parent if not present
            navMeshSurface = warehouseParent.gameObject.GetComponent<NavMeshSurface>();
            if (navMeshSurface == null)
            {
                navMeshSurface = warehouseParent.gameObject.AddComponent<NavMeshSurface>();
            }

            navMeshSurface.collectObjects = CollectObjects.Children;
            navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            navMeshSurface.agentTypeID = 0; // Humanoid agent type
            
            navMeshSurface.BuildNavMesh();
            Debug.Log("[WarehouseBuilder] NavMesh baked successfully");
        }

        // ──────────────────────────────────────────────
        // UTILITIES
        // ──────────────────────────────────────────────

        /// <summary>Creates a simple colored material.</summary>
        private Material CreateMaterial(Color color)
        {
            // Use URP Lit shader if available, fallback to Standard
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            mat.color = color;

            // Enable transparency if alpha < 1
            if (color.a < 1f)
            {
                mat.SetFloat("_Surface", 1); // Transparent for URP
                mat.SetFloat("_Mode", 3);    // Transparent for Standard
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }

            return mat;
        }
    }
}
