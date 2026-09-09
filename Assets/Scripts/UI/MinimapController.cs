using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(200)]
public class MinimapController : MonoBehaviour
{
    [Header("References")]
    public DungeonGenerator dungeon;
    public RawImage minimapImage;
    public RectTransform marker;
    public Transform target;

    [Header("Entity Markers")]
    [Tooltip("Layer tempat entitas (Player/Enemy/Boss/Neutral) berada. Marker dibuat untuk semua object di layer ini.")]
    public string entityLayerName = "Entity";

    [Header("Render")]
    [Tooltip("Resolusi RenderTexture minimap (persegi).")]
    public int renderTextureSize = 512;

    [Header("Colors")]
    [Tooltip("Warna lantai.")]
    public Color floorColor = new Color(0.20f, 0.20f, 0.22f, 1f);
    [Tooltip("Warna dinding.")]
    public Color wallColor = new Color(0.95f, 0.95f, 0.96f, 1f);

    [Header("View")]
    [Tooltip("Setengah lebar jendela minimap dalam sel grid. Mengontrol zoom.")]
    public float viewRadius = 5f;
    public float rotationSmoothing = 8f;

    [Header("Toggle")]
    public bool useToggle = true;
    public bool startVisible = true;

    private enum EntityKind { None, Player, Enemy, Boss, Neutral }

    private class EntityData
    {
        public GameObject go;
        public Transform marker;
        public EntityKind kind;
    }

    private Camera minimapCamera;
    private RenderTexture mapRT;
    private bool isVisible;
    private float currentRotation;
    private float pitch = 10f;
    private NPCSpawner spawner;
    private readonly List<EntityData> entities = new List<EntityData>();
    private static readonly Dictionary<EntityKind, Material> kindMaterials = new Dictionary<EntityKind, Material>();

    void Start()
    {
        if (minimapImage == null)
            minimapImage = GetComponentInChildren<RawImage>();

        if (dungeon != null)
            pitch = dungeon.TileSize + dungeon.TileGap;

        isVisible = startVisible;
        ApplyVisibility();

        SetupLiveCamera();
        UpdateView();

        SetupEntityTracking();
    }

    private void OnDestroy()
    {
        if (spawner != null)
        {
            spawner.OnNPCSpawned -= AddEntity;
            spawner.OnNPCDied -= RemoveEntity;
        }
    }

    void LateUpdate()
    {
        UpdateView();
        UpdateEntityMarkers();
        HandleToggle();
    }

    private void SetupEntityTracking()
    {
        int entityLayer = LayerMask.NameToLayer(entityLayerName);
        if (entityLayer < 0)
        {
            Debug.LogWarning($"MinimapController: layer '{entityLayerName}' tidak ada. Marker entitas dilewati.");
            return;
        }

        if (spawner == null)
            spawner = FindFirstObjectByType<NPCSpawner>();

        if (spawner != null)
        {
            spawner.OnNPCSpawned += AddEntity;
            spawner.OnNPCDied -= RemoveEntity;
        }

        GameObject[] all = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].layer == entityLayer)
                AddEntity(all[i]);
        }
    }

    private void AddEntity(GameObject go)
    {
        if (go == null) return;
        if (HasEntity(go)) return;

        EntityKind kind = KindOf(go);
        if (kind == EntityKind.None) return;

        Transform markerT = CreateMarker(kind, go.name);

        EntityData data = new EntityData
        {
            go = go,
            marker = markerT,
            kind = kind
        };
        entities.Add(data);
    }

    private void RemoveEntity(GameObject go)
    {
        for (int i = entities.Count - 1; i >= 0; i--)
        {
            if (entities[i].go != go) continue;

            if (entities[i].marker != null)
                Destroy(entities[i].marker.gameObject);

            entities.RemoveAt(i);
        }
    }

    private bool HasEntity(GameObject go)
    {
        for (int i = 0; i < entities.Count; i++)
        {
            if (entities[i].go == go) return true;
        }
        return false;
    }

    private void UpdateEntityMarkers()
    {
        if (dungeon == null || minimapCamera == null) return;

        for (int i = entities.Count - 1; i >= 0; i--)
        {
            EntityData data = entities[i];

            if (data.go == null || data.marker == null)
            {
                if (data.marker != null) Destroy(data.marker.gameObject);
                entities.RemoveAt(i);
                continue;
            }

            Vector3 local = dungeon.transform.InverseTransformPoint(data.go.transform.position);
            data.marker.position = dungeon.transform.TransformPoint(
                new Vector3(dungeon.minimapCopyOffset + local.x, 1f, local.z)
            );
        }
    }

    private EntityKind KindOf(GameObject go)
    {
        if (go.CompareTag("Player")) return EntityKind.Player;
        if (go.CompareTag("Boss")) return EntityKind.Boss;
        if (go.CompareTag("Enemy")) return EntityKind.Enemy;
        if (go.CompareTag("Neutral")) return EntityKind.Neutral;

        return EntityKind.None;
    }

    private Transform CreateMarker(EntityKind kind, string entityName)
    {
        int minimapLayer = LayerMask.NameToLayer("Minimap");

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "MinimapMarker_" + entityName;
        if (minimapLayer >= 0) go.layer = minimapLayer;
        go.transform.SetParent(transform, false);

        Collider col = go.GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = GetKindMaterial(kind);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        go.transform.localScale = MarkerScale(kind);
        return go.transform;
    }

    private static Vector3 MarkerScale(EntityKind kind)
    {
        if (kind == EntityKind.Boss) return new Vector3(3f, 5f, 3f);
        return new Vector3(2f, 3.5f, 2f);
    }

    private static Material GetKindMaterial(EntityKind kind)
    {
        if (kindMaterials.TryGetValue(kind, out Material existing) && existing != null)
            return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        Material mat = new Material(shader)
        {
            color = MarkerColor(kind)
        };

        kindMaterials[kind] = mat;
        return mat;
    }

    private static Color MarkerColor(EntityKind kind)
    {
        switch (kind)
        {
            case EntityKind.Player: return Color.cyan;
            case EntityKind.Boss: return Color.magenta;
            case EntityKind.Enemy: return Color.red;
            default: return Color.green;
        }
    }

    private void SetupLiveCamera()
    {
        if (dungeon == null)
        {
            Debug.LogWarning("MinimapController: dungeon reference belum di-assign.");
            return;
        }

        Renderer floorCopy = FindChildRenderer(dungeon.transform, "Dungeon_Minimap_Floor");
        Renderer wallCopy = FindChildRenderer(dungeon.transform, "Dungeon_Minimap_Walls");
        if (floorCopy == null || wallCopy == null)
        {
            Debug.LogWarning("MinimapController: copy minimap (Dungeon_Minimap_Floor/Walls) tidak ditemukan. Regenerate dungeon.");
            return;
        }

        Material flatFloor = CreateFlatMaterial(floorColor);
        Material flatWall = CreateFlatMaterial(wallColor);
        if (flatFloor == null || flatWall == null) return;
        floorCopy.sharedMaterial = flatFloor;
        wallCopy.sharedMaterial = flatWall;

        int minimapLayer = LayerMask.NameToLayer("Minimap");
        int cullMask = 0;
        if (minimapLayer >= 0) cullMask |= 1 << minimapLayer;

        mapRT = new RenderTexture(renderTextureSize, renderTextureSize, 0, RenderTextureFormat.ARGB32);
        mapRT.name = "MinimapRT";

        GameObject camGO = new GameObject("Minimap_Camera");
        camGO.transform.SetParent(dungeon.transform, false);

        minimapCamera = camGO.AddComponent<Camera>();
        minimapCamera.enabled = true;
        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = viewRadius * pitch;
        minimapCamera.aspect = 1f;
        minimapCamera.nearClipPlane = 0.1f;
        minimapCamera.farClipPlane = 500f;
        minimapCamera.cullingMask = cullMask;
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = Color.clear;
        minimapCamera.allowHDR = false;
        minimapCamera.allowMSAA = false;
        minimapCamera.targetTexture = mapRT;
        minimapCamera.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        minimapCamera.transform.localPosition = new Vector3(dungeon.minimapCopyOffset, 150f, 0f);

        if (minimapImage != null)
            minimapImage.texture = mapRT;
    }

    private Material CreateFlatMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogWarning("MinimapController: tidak ada shader flat tersedia.");
            return null;
        }

        Material mat = new Material(shader);
        mat.color = color;
        return mat;
    }

    private Renderer FindChildRenderer(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
            {
                Renderer r = child.GetComponent<Renderer>();
                if (r != null) return r;
            }
        }
        return null;
    }

    private void UpdateView()
    {
        if (minimapImage == null || minimapCamera == null) return;

        if (target != null && dungeon != null)
        {
            Vector3 localTarget = dungeon.transform.InverseTransformPoint(target.position);
            minimapCamera.transform.localPosition = new Vector3(
                dungeon.minimapCopyOffset + localTarget.x,
                minimapCamera.orthographicSize + 50f,
                localTarget.z);

            float targetYaw = target.eulerAngles.y;
            currentRotation = Mathf.LerpAngle(currentRotation, targetYaw, Time.deltaTime * rotationSmoothing);
            minimapImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, currentRotation);

            if (marker != null)
                marker.localRotation = Quaternion.Euler(0f, 0f, -currentRotation);
        }
    }

    private void HandleToggle()
    {
        if (!useToggle) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.mKey.wasPressedThisFrame)
        {
            isVisible = !isVisible;
            ApplyVisibility();
        }
    }

    private void ApplyVisibility()
    {
        if (minimapImage != null)
            minimapImage.gameObject.SetActive(isVisible);

        if (marker != null)
            marker.gameObject.SetActive(isVisible);

        if (minimapCamera != null)
            minimapCamera.enabled = isVisible;
    }
}