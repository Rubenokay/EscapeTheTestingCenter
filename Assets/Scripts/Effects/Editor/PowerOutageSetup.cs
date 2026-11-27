using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: Tools > Escape The Testing Center > Set Up Power Outage
/// Adds the outage controller, 4 emergency lights around the room, an on-screen
/// warning message and dust particles to the open scene. Run it once, then move
/// the lights wherever you like.
/// </summary>
public static class PowerOutageSetup
{
    private const string MaterialFolder = "Assets/Materials";
    private const string BulbMaterialPath = MaterialFolder + "/EmergencyBulb.mat";
    private const string UrpParticleMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

    [MenuItem("Tools/Escape The Testing Center/Set Up Power Outage")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Escape The Testing Center", "Stop Play mode first, then run this again.", "OK");
            return;
        }
        if (Object.FindFirstObjectByType<PowerOutageController>() != null)
        {
            EditorUtility.DisplayDialog("Power Outage", "This scene already has a PowerOutageController.", "OK");
            return;
        }

        // ----- figure out where the room is -----
        Bounds furniture = new Bounds();
        bool hasFurniture = false;
        float floorY = float.MaxValue;
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (r is ParticleSystemRenderer) continue;
            string n = r.gameObject.name.ToLower();
            bool isFloor = n.Contains("plane") || n.Contains("ground") || n.Contains("floor");
            bool isPlayer = n.Contains("player") || n.Contains("capsule");
            if (isFloor) { floorY = Mathf.Min(floorY, r.bounds.max.y); continue; }
            if (isPlayer) continue;
            if (!hasFurniture) { furniture = r.bounds; hasFurniture = true; }
            else furniture.Encapsulate(r.bounds);
        }
        if (!hasFurniture) furniture = new Bounds(Vector3.zero, new Vector3(10f, 2f, 10f));
        if (floorY == float.MaxValue) floorY = furniture.min.y;
        float lightY = floorY + 3f;
        Vector3 c = furniture.center;
        Vector3 e = furniture.extents + new Vector3(1f, 0f, 1f);

        // ----- controller -----
        var root = new GameObject("Power Outage System");
        Undo.RegisterCreatedObjectUndo(root, "Set Up Power Outage");
        var controller = root.AddComponent<PowerOutageController>();

        // ----- emergency lights -----
        var lightsParent = new GameObject("Emergency Lights");
        lightsParent.transform.SetParent(root.transform);
        Material bulbMat = GetOrCreateBulbMaterial();

        Vector3[] spots =
        {
            new Vector3(c.x - e.x, lightY, c.z - e.z),
            new Vector3(c.x + e.x, lightY, c.z - e.z),
            new Vector3(c.x - e.x, lightY, c.z + e.z),
            new Vector3(c.x + e.x, lightY, c.z + e.z),
        };
        EmergencyLight.Mode[] modes =
        {
            EmergencyLight.Mode.Pulse, EmergencyLight.Mode.Rotate,
            EmergencyLight.Mode.Pulse, EmergencyLight.Mode.Flicker,
        };

        for (int i = 0; i < spots.Length; i++)
        {
            var go = new GameObject($"Emergency Light {i + 1} ({modes[i]})");
            go.transform.SetParent(lightsParent.transform);
            go.transform.position = spots[i];

            var light = go.AddComponent<Light>();
            if (modes[i] == EmergencyLight.Mode.Rotate)
            {
                light.type = LightType.Spot;
                light.spotAngle = 70f;
                go.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
            }
            else
            {
                light.type = LightType.Point;
            }
            light.shadows = LightShadows.Soft;
            light.color = new Color(1f, 0.08f, 0.04f);

            var em = go.AddComponent<EmergencyLight>();
            em.mode = modes[i];
            if (modes[i] == EmergencyLight.Mode.Rotate) { em.intensity = 15f; em.range = 18f; }

            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "Bulb";
            Object.DestroyImmediate(bulb.GetComponent<Collider>());
            bulb.transform.SetParent(go.transform, false);
            bulb.transform.localScale = Vector3.one * 0.15f;
            var br = bulb.GetComponent<MeshRenderer>();
            br.sharedMaterial = bulbMat;
            br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            em.bulbRenderer = br;

            controller.emergencyLights.Add(em);
        }

        // ----- warning message on the existing HUD -----
        var interaction = Object.FindFirstObjectByType<PlayerInteraction>();
        Canvas canvas = null;
        if (interaction != null && interaction.objectiveText != null) canvas = interaction.objectiveText.canvas;
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            var msgGo = new GameObject("Outage Message", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(msgGo, "Set Up Power Outage");
            msgGo.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)msgGo.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -80f);
            rt.sizeDelta = new Vector2(900f, 140f);
            var tmp = msgGo.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 42;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(1f, 0.2f, 0.15f);
            tmp.text = controller.outageMessage;
            tmp.raycastTarget = false;
            controller.messageText = tmp;
        }

        // ----- dust on the camera -----
        Camera cam = Camera.main;
        if (cam != null && cam.GetComponent<DustMotes>() == null)
        {
            var dust = Undo.AddComponent<DustMotes>(cam.gameObject);
            dust.particleMaterial = AssetDatabase.LoadAssetAtPath<Material>(UrpParticleMaterial);
        }

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("Power outage set up. Move the Emergency Lights where you want them, then press Play. Press O in Play mode to toggle the outage.");
    }

    [MenuItem("Tools/Escape The Testing Center/Add Horror Ground Fog")]
    public static void AddGroundFog()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Escape The Testing Center", "Stop Play mode first, then run this again.", "OK");
            return;
        }
        Camera cam = Camera.main;
        if (cam == null)
        {
            EditorUtility.DisplayDialog("Ground Fog", "No camera tagged MainCamera found in this scene.", "OK");
            return;
        }
        var fog = cam.GetComponent<GroundFog>();
        if (fog == null) fog = Undo.AddComponent<GroundFog>(cam.gameObject);
        fog.particleMaterial = AssetDatabase.LoadAssetAtPath<Material>(UrpParticleMaterial);
        Selection.activeGameObject = cam.gameObject;
        EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
        Debug.Log("Ground fog added to the Main Camera. It fades in when the power goes out.");
    }

    private static Material GetOrCreateBulbMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(BulbMaterialPath);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets", "Materials");
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        var mat = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));
        mat.name = "EmergencyBulb";
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1f, 0.08f, 0.04f));
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(1f, 0.08f, 0.04f));
        AssetDatabase.CreateAsset(mat, BulbMaterialPath);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
