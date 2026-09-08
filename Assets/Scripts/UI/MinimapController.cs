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

    [Header("Render")]
    [Tooltip("Resolusi RenderTexture minimap (persegi).")]
    public int renderTextureSize = 512;

    [Header("Colors")]
    [Tooltip("Warna lantai (abu gelap).")]
    public Color floorColor = new Color(0.20f, 0.20f, 0.22f, 1f);
    [Tooltip("Warna dinding (putih).")]
    public Color wallColor = new Color(0.95f, 0.95f, 0.96f, 1f);

    [Header("View")]
    [Tooltip("Setengah lebar jendela minimap dalam sel grid. Mengontrol zoom.")]
    public float viewRadius = 5f;
    public float rotationSmoothing = 8f;

    [Header("Toggle")]
    public bool useToggle = true;
    public bool startVisible = true;

    private Camera minimapCamera;
    private RenderTexture mapRT;
    private bool isVisible;
    private float currentRotation;
    private float pitch = 10f;

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
    }

    void LateUpdate()
    {
        UpdateView();
        HandleToggle();
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