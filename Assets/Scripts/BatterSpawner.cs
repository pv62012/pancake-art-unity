using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns a finger or mouse drag into one continuous, flat ribbon of batter.
/// The ribbon is one lightweight mesh per drag, rather than hundreds of tiny objects.
/// </summary>
[RequireComponent(typeof(Camera))]
public class BatterSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask panLayer;
    [SerializeField] private Material batterMaterial;

    [Header("Batter drawing")]
    [SerializeField, Min(0.01f)] private float minimumPointSpacing = 0.055f;
    [SerializeField, Min(0.01f)] private float ribbonWidth = 0.16f;
    [SerializeField, Min(0f)] private float surfaceOffset = 0.025f;

    private readonly List<Vector3> points = new List<Vector3>();
    private readonly List<Vector3> normals = new List<Vector3>();
    private GameObject activeRibbon;
    private Mesh activeMesh;
    private Vector3 lastPoint;
    private bool drawing;
    private string statusMessage = "Drag across the dark pan to pour batter.";

    private void Awake()
    {
        if (mainCamera == null) mainCamera = GetComponent<Camera>();
        if (mainCamera == null) mainCamera = Camera.main;
    }

    private void Update()
    {
        // Read touch on Android, or mouse input in the Editor for easy iteration.
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began) BeginStroke(touch.position);
            else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                ContinueStroke(touch.position);
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                EndStroke();
        }
        else
        {
            if (Input.GetMouseButtonDown(0)) BeginStroke(Input.mousePosition);
            else if (Input.GetMouseButton(0)) ContinueStroke(Input.mousePosition);
            else if (Input.GetMouseButtonUp(0)) EndStroke();
        }
    }

    private void BeginStroke(Vector2 screenPosition)
    {
        EndStroke();
        drawing = true;
        statusMessage = "Touch detected — drag across the pan.";
        points.Clear();
        normals.Clear();
        activeRibbon = new GameObject("Batter Stroke");
        // Mesh vertices below are expressed in camera-local coordinates, so keep the
        // child object at that coordinate system's origin (worldPositionStays=false).
        activeRibbon.transform.SetParent(transform, false);
        MeshFilter filter = activeRibbon.AddComponent<MeshFilter>();
        MeshRenderer renderer = activeRibbon.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = batterMaterial != null ? batterMaterial : CreateFallbackMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        activeMesh = new Mesh { name = "Batter Ribbon" };
        activeMesh.MarkDynamic();
        filter.sharedMesh = activeMesh;
        TryAddPoint(screenPosition, true);
    }

    private void ContinueStroke(Vector2 screenPosition)
    {
        if (drawing) TryAddPoint(screenPosition, false);
    }

    private void EndStroke()
    {
        if (drawing)
            statusMessage = points.Count > 0
                ? "Stroke saved. Drag again to continue your design."
                : "No pan hit. Touch inside the dark cooking surface.";
        drawing = false;
        activeRibbon = null;
        activeMesh = null;
    }

    private void TryAddPoint(Vector2 screenPosition, bool firstPoint)
    {
        if (mainCamera == null || activeMesh == null)
        {
            statusMessage = "Camera setup missing — check the scene references.";
            return;
        }
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, panLayer))
        {
            statusMessage = "Touch the dark pan surface to pour.";
            return;
        }

        statusMessage = "Pouring batter...";
        Vector3 point = hit.point + hit.normal * surfaceOffset;
        if (!firstPoint && Vector3.Distance(point, lastPoint) < minimumPointSpacing) return;
        points.Add(point);
        normals.Add(hit.normal);
        lastPoint = point;
        RebuildRibbon();
    }

    private void RebuildRibbon()
    {
        int count = points.Count;
        if (count == 0) return;
        // A small quad makes the very first touch visible before the drag has length.
        if (count == 1)
        {
            float radius = ribbonWidth * 0.5f;
            Vector3 center = transform.InverseTransformPoint(points[0]);
            Vector3 right = transform.InverseTransformVector(Vector3.right * radius);
            Vector3 forward = transform.InverseTransformVector(Vector3.forward * radius);
            activeMesh.Clear();
            activeMesh.vertices = new[] { center - right - forward, center - right + forward, center + right - forward, center + right + forward };
            activeMesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.right, Vector2.one };
            activeMesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            activeMesh.RecalculateNormals();
            activeMesh.RecalculateBounds();
            return;
        }

        Vector3[] vertices = new Vector3[count * 2];
        Vector2[] uv = new Vector2[count * 2];
        int[] triangles = new int[(count - 1) * 6];

        for (int i = 0; i < count; i++)
        {
            Vector3 forward;
            if (count == 1) forward = Vector3.forward;
            else if (i == 0) forward = points[1] - points[0];
            else if (i == count - 1) forward = points[i] - points[i - 1];
            else forward = points[i + 1] - points[i - 1];

            Vector3 side = Vector3.Cross(normals[i], forward).normalized * (ribbonWidth * 0.5f);
            vertices[i * 2] = transform.InverseTransformPoint(points[i] - side);
            vertices[i * 2 + 1] = transform.InverseTransformPoint(points[i] + side);
            uv[i * 2] = new Vector2(0, i);
            uv[i * 2 + 1] = new Vector2(1, i);

            if (i < count - 1)
            {
                int t = i * 6;
                int v = i * 2;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
        }

        activeMesh.Clear();
        activeMesh.vertices = vertices;
        activeMesh.uv = uv;
        activeMesh.triangles = triangles;
        activeMesh.RecalculateNormals();
        activeMesh.RecalculateBounds();
    }

    private static Material CreateFallbackMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { color = new Color(0.96f, 0.68f, 0.22f) };
        return material;
    }

    // A simple, readable on-screen prompt also confirms whether a touch reached the pan.
    private void OnGUI()
    {
        float scale = Mathf.Clamp(Screen.height / 900f, 1f, 3f);
        float width = Screen.width * 0.9f;
        float height = 112f * scale;
        Rect panel = new Rect((Screen.width - width) * 0.5f, 24f * scale, width, height);

        Color oldColor = GUI.color;
        GUI.color = new Color(0.035f, 0.045f, 0.05f, 0.88f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = oldColor;

        GUIStyle title = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt(24f * scale),
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.73f, 0.3f) }
        };
        GUIStyle instruction = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt(15f * scale),
            normal = { textColor = Color.white }
        };
        GUI.Label(new Rect(panel.x, panel.y + 5f * scale, width, 36f * scale), "PANCAKE ART", title);
        GUI.Label(new Rect(panel.x + 8f * scale, panel.y + 42f * scale, width - 16f * scale, 28f * scale),
            "DRAG YOUR FINGER ON THE PAN TO DRAW", instruction);
        GUI.Label(new Rect(panel.x + 8f * scale, panel.y + 72f * scale, width - 16f * scale, 28f * scale),
            statusMessage, instruction);
    }
}
