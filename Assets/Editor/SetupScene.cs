using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SetupScene
{
    [MenuItem("Tools/Repair Batter Scene")]
    public static void RepairScene()
    {
        const string scenePath = "Assets/Scenes/Main.unity";
        const string batterPath = "Assets/Prefabs/Batter.prefab";
        const string scriptPath = "Assets/Scripts/BatterSpawner.cs";

        // Make a fresh scene using Unity APIs so all serialized references are valid.
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        layers.GetArrayElementAtIndex(8).stringValue = "PanLayer";
        tagManager.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetPositionAndRotation(new Vector3(0, 7, -4.5f), Quaternion.Euler(57, 0, 0));
        var camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 2.65f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.16f, 0.19f);

        var pan = new GameObject("Tawa");
        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Silver Pan Body";
        body.transform.SetParent(pan.transform, false);
        body.transform.localPosition = new Vector3(0, 0.06f, 0);
        body.transform.localScale = new Vector3(2.8f, 0.06f, 2.8f);
        int panLayer = LayerMask.NameToLayer("PanLayer");
        if (panLayer < 0)
        {
            Debug.LogError("PanLayer is missing in Project Settings > Tags and Layers.");
            return;
        }
        pan.layer = panLayer;
        body.layer = panLayer;

        // Give the cylinder a simple steel outer rim and a dark, flat cooking face.
        var steel = MakeMaterial("Tawa Steel", new Color(0.22f, 0.27f, 0.30f), 0.72f, 0.28f);
        var iron = MakeMaterial("Seasoned Iron", new Color(0.055f, 0.065f, 0.065f), 0.22f, 0.72f);
        body.GetComponent<Renderer>().sharedMaterial = steel;
        var cookingSurface = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cookingSurface.name = "Nonstick Cooking Surface";
        cookingSurface.transform.SetParent(pan.transform, false);
        cookingSurface.transform.localPosition = new Vector3(0, 0.14f, 0);
        cookingSurface.transform.localScale = new Vector3(2.46f, 0.02f, 2.46f);
        cookingSurface.GetComponent<Renderer>().sharedMaterial = iron;
        var surfaceCollider = cookingSurface.GetComponent<Collider>();
        if (surfaceCollider != null) Object.DestroyImmediate(surfaceCollider);

        // A thin, bright rolled lip makes the shallow pan read clearly from above.
        var lip = new GameObject("Polished Pan Lip");
        lip.transform.SetParent(pan.transform, false);
        lip.transform.localPosition = new Vector3(0, 0.14f, 0);
        var lipFilter = lip.AddComponent<MeshFilter>();
        lipFilter.sharedMesh = CreateLipMesh();
        lip.AddComponent<MeshRenderer>().sharedMaterial = steel;

        // Handle extends away from the player so it stays visible above the pan.
        var neck = GameObject.CreatePrimitive(PrimitiveType.Cube);
        neck.name = "Handle Neck";
        neck.transform.SetParent(pan.transform, false);
        neck.transform.localPosition = new Vector3(0, 0.11f, 1.38f);
        neck.transform.localScale = new Vector3(0.3f, 0.075f, 0.48f);
        neck.GetComponent<Renderer>().sharedMaterial = steel;
        Object.DestroyImmediate(neck.GetComponent<Collider>());
        var grip = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        grip.name = "Black Heat-Resistant Handle";
        grip.transform.SetParent(pan.transform, false);
        grip.transform.localPosition = new Vector3(0, 0.13f, 2.02f);
        grip.transform.localRotation = Quaternion.Euler(90, 0, 0);
        grip.transform.localScale = new Vector3(0.2f, 0.75f, 0.2f);
        grip.GetComponent<Renderer>().sharedMaterial = MakeMaterial("BlackHandle", new Color(0.035f, 0.04f, 0.045f), 0.08f, 0.3f);
        Object.DestroyImmediate(grip.GetComponent<Collider>());

        var lightObject = new GameObject("Directional Light", typeof(Light));
        var light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.4f;
        lightObject.transform.rotation = Quaternion.Euler(45, -25, 0);

        var batterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(batterPath);
        var spawnerScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
        if (batterPrefab == null || spawnerScript == null)
        {
            Debug.LogError("Batter prefab or BatterSpawner script was not imported.");
            return;
        }
        var spawner = cameraObject.AddComponent(spawnerScript.GetClass()) as MonoBehaviour;
        var serializedSpawner = new SerializedObject(spawner);
        serializedSpawner.FindProperty("mainCamera").objectReferenceValue = camera;
        serializedSpawner.FindProperty("panLayer").intValue = 1 << panLayer;
        serializedSpawner.FindProperty("batterMaterial").objectReferenceValue = MakeMaterial("Golden Batter", new Color(0.96f, 0.64f, 0.16f), 0.08f, 0.38f);
        serializedSpawner.FindProperty("minimumPointSpacing").floatValue = 0.035f;
        serializedSpawner.FindProperty("ribbonWidth").floatValue = 0.13f;
        serializedSpawner.FindProperty("surfaceOffset").floatValue = 0.045f;
        serializedSpawner.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, scenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("Batter drawing scene rebuilt and saved.");
    }

    private static Material MakeMaterial(string name, Color color, float metallic, float smoothness)
    {
        string path = "Assets/Materials/" + name.Replace(" ", "") + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var material = new Material(shader) { name = name, color = color };
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        System.IO.Directory.CreateDirectory("Assets/Materials");
        AssetDatabase.Refresh();
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Mesh CreateLipMesh()
    {
        const int segments = 64;
        const int sides = 12;
        const float majorRadius = 1.32f;
        const float radialRadius = 0.055f;
        const float verticalRadius = 0.028f;
        var vertices = new Vector3[segments * sides];
        var triangles = new int[segments * sides * 6];
        for (int i = 0; i < segments; i++)
        {
            float around = (i / (float)segments) * Mathf.PI * 2f;
            for (int j = 0; j < sides; j++)
            {
                float tube = (j / (float)sides) * Mathf.PI * 2f;
                float radius = majorRadius + Mathf.Cos(tube) * radialRadius;
                vertices[i * sides + j] = new Vector3(Mathf.Cos(around) * radius,
                    Mathf.Sin(tube) * verticalRadius, Mathf.Sin(around) * radius);
                int nextI = (i + 1) % segments;
                int nextJ = (j + 1) % sides;
                int t = (i * sides + j) * 6;
                int a = i * sides + j;
                int b = nextI * sides + j;
                int c = i * sides + nextJ;
                int d = nextI * sides + nextJ;
                triangles[t] = a; triangles[t + 1] = c; triangles[t + 2] = b;
                triangles[t + 3] = c; triangles[t + 4] = d; triangles[t + 5] = b;
            }
        }
        var mesh = new Mesh { name = "Rolled Silver Pan Lip" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
