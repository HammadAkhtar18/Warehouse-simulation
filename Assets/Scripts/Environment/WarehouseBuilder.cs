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
            rend.material = CreateMaterial(WarehouseConstants.FloorColor, 0f, 0.3f);
            
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
            SafeSetTag(wall, "Obstacle");

            Renderer rend = wall.GetComponent<Renderer>();
            rend.material = CreateMaterial(WarehouseConstants.WallColor, 0.2f, 0.4f);
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
            SafeSetTag(shelf, "Shelf");

            // Set shelf material (dark metallic rack)
            Renderer shelfRend = shelf.GetComponent<Renderer>();
            shelfRend.material = CreateMaterial(new Color(0.35f, 0.3f, 0.25f), 0.4f, 0.3f);

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

            // Set indicator material (green by default — updated by Shelf component)
            Renderer indRend = indicator.GetComponent<Renderer>();
            indRend.material = CreateMaterial(WarehouseConstants.StockHighColor);
            
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
            SafeSetTag(zone, "Zone");

            Renderer rend = zone.GetComponent<Renderer>();
            Material mat = CreateMaterial(color);
            rend.material = mat;

            // Zone collider should be a trigger (robots don't physically bump into zones)
            zone.GetComponent<Collider>().isTrigger = true;

            return zone;
        }

        /// <summary>
        /// Safely sets a tag on a GameObject. If the tag doesn't exist in Unity's
        /// Tag Manager, logs a warning instead of crashing.
        /// </summary>
        private void SafeSetTag(GameObject obj, string tag)
        {
            try
            {
                obj.tag = tag;
            }
            catch (UnityException)
            {
                Debug.LogWarning($"[WarehouseBuilder] Tag '{tag}' not defined. Run Warehouse > Setup Scene first.");
            }
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

        // Cached reference material from a primitive (guaranteed to have correct URP shader)
        private static Material _cachedReferenceMaterial;

        /// <summary>
        /// Gets the default URP material by creating a temporary primitive.
        /// This avoids Shader.Find() which fails at runtime in URP.
        /// </summary>
        private static Material GetReferenceMaterial()
        {
            if (_cachedReferenceMaterial == null)
            {
                // Create a temporary primitive to capture its default material
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _cachedReferenceMaterial = new Material(temp.GetComponent<Renderer>().sharedMaterial);
                DestroyImmediate(temp);
            }
            return _cachedReferenceMaterial;
        }

        /// <summary>Creates a colored material by cloning the default URP material.</summary>
        private Material CreateMaterial(Color color, float metallic = 0f, float smoothness = 0.5f)
        {
            // Clone the reference material (has the correct URP shader)
            Material mat = new Material(GetReferenceMaterial());

            // Set color on both Standard and URP properties
            mat.color = color;
            mat.SetColor("_BaseColor", color);

            // Try setting metallic/smoothness (may not exist on all shaders)
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", smoothness);

            // Enable transparency if alpha < 1
            if (color.a < 1f)
            {
                if (mat.HasProperty("_Surface"))
                    mat.SetFloat("_Surface", 1); // Transparent

                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
            }

            return mat;
        }
    }
}
