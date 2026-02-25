// SceneSetup.cs - Editor utility to rebuild the WarehouseMain scene
// Place in Assets/Scripts/Editor/ and run via menu: Warehouse > Setup Scene

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.EventSystems;
using WarehouseSimulation.Managers;
using WarehouseSimulation.Environment;
using WarehouseSimulation.UI;

public class SceneSetup : Editor
{
    [MenuItem("Warehouse/Setup Scene")]
    public static void SetupWarehouseScene()
    {
        // ─────────────────────────────────
        // 1. MAIN CAMERA
        // ─────────────────────────────────
        var cameraObj = new GameObject("Main Camera");
        cameraObj.tag = "MainCamera";
        var cam = cameraObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 60f;
        cameraObj.transform.position = new Vector3(25f, 30f, -10f);
        cameraObj.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
        cameraObj.AddComponent<AudioListener>();

        // ─────────────────────────────────
        // 2. DIRECTIONAL LIGHT
        // ─────────────────────────────────
        var lightObj = new GameObject("Directional Light");
        var light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.956f, 0.839f);
        light.intensity = 1f;
        light.shadows = LightShadows.Soft;
        lightObj.transform.position = new Vector3(0f, 10f, 0f);
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ─────────────────────────────────
        // 3. GAME MANAGER (with all sub-components)
        // ─────────────────────────────────
        var gameManagerObj = new GameObject("GameManager");
        var gameManager = gameManagerObj.AddComponent<GameManager>();

        // Add all manager components to the same GameObject
        var warehouseBuilder = gameManagerObj.AddComponent<WarehouseBuilder>();
        var inventoryManager = gameManagerObj.AddComponent<InventoryManager>();
        var taskManager = gameManagerObj.AddComponent<TaskManager>();
        var robotCoordinator = gameManagerObj.AddComponent<RobotCoordinator>();
        var performanceTracker = gameManagerObj.AddComponent<PerformanceTracker>();

        // Wire up serialized references via SerializedObject
        var so = new SerializedObject(gameManager);
        so.FindProperty("warehouseBuilder").objectReferenceValue = warehouseBuilder;
        so.FindProperty("inventoryManager").objectReferenceValue = inventoryManager;
        so.FindProperty("taskManager").objectReferenceValue = taskManager;
        so.FindProperty("robotCoordinator").objectReferenceValue = robotCoordinator;
        so.FindProperty("performanceTracker").objectReferenceValue = performanceTracker;
        so.ApplyModifiedProperties();

        // ─────────────────────────────────
        // 4. UI CANVAS
        // ─────────────────────────────────
        var canvasObj = new GameObject("UICanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // UIManager
        var uiManager = canvasObj.AddComponent<UIManager>();

        // Wire UIManager reference
        so = new SerializedObject(gameManager);
        so.FindProperty("uiManager").objectReferenceValue = uiManager;
        so.ApplyModifiedProperties();

        // ─────────────────────────────────
        // 5. EVENT SYSTEM (required for UI)
        // ─────────────────────────────────
        var eventSystemObj = new GameObject("EventSystem");
        eventSystemObj.AddComponent<EventSystem>();
        eventSystemObj.AddComponent<StandaloneInputModule>();

        // ─────────────────────────────────
        // 6. ADD TAGS (if they don't exist)
        // ─────────────────────────────────
        AddTag("Robot");
        AddTag("Shelf");
        AddTag("Obstacle");
        AddTag("DropOffZone");
        AddTag("ChargingStation");
        AddTag("PickupZone");
        AddTag("Zone");

        // ─────────────────────────────────
        // 7. MARK SCENE DIRTY
        // ─────────────────────────────────
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[SceneSetup] WarehouseMain scene rebuilt successfully! Don't forget to save (Ctrl+S).");
        EditorUtility.DisplayDialog("Scene Setup Complete",
            "The WarehouseMain scene has been rebuilt with:\n" +
            "• Main Camera\n" +
            "• Directional Light\n" +
            "• GameManager (with all managers)\n" +
            "• UI Canvas\n" +
            "• Event System\n\n" +
            "Press Ctrl+S to save the scene!",
            "OK");
    }

    private static void AddTag(string tag)
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tagsProp = tagManager.FindProperty("tags");

        // Check if tag already exists
        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
                return;
        }

        // Add the tag
        tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
        tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
        tagManager.ApplyModifiedProperties();
        Debug.Log($"[SceneSetup] Added tag: {tag}");
    }
}
#endif
