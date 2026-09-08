using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

[System.Serializable]
public class RoomData
{
    public List<Vector2Int> tiles;
    public Vector2Int centerGrid;
    public Vector3 centerWorld;
    public int gridValue;

    public RoomData()
    {
        tiles = new List<Vector2Int>();
    }
}

[DefaultExecutionOrder(-100)]
public class DungeonGenerator : MonoBehaviour
{
    [Header("Map Settings")]
    public int size = 20;
    public int totalRooms = 15;

    [Header("Tile Dimensions")]
    public float tileSize = 10f;
    public float floorThickness = 0.2f;
    public float wallHeight = 4f;
    public float wallThickness = 0.5f;

    [Header("Door Settings")]
    public float doorWidth = 3f;
    public float doorHeight = 2.5f;

    [Header("Gap / Offset Settings")]
    public float tileGap = 2f;

    [Header("Minimap")]
    [Tooltip("Offset world copy minimap. Copy dipindah jauh dari dungeon asli supaya tidak terlihat dari main camera.")]
    public float minimapCopyOffset = 2000f;
    [Tooltip("Pengali tinggi wall untuk copy minimap. Copy dipotong di ketinggian ini supaya lintel pintu tidak menutup jalur.")]
    [Range(0.1f, 1f)]
    public float minimapCopyHeightFactor = 0.5f;

    [Header("Chance Settings")]
    [Range(0f, 1f)]
    public float bigRoomChance = 0.3f;

    [Header("Ceiling Settings")]
    public float ceilingThickness = 0.2f;

    [Header("Texture Settings")]
    [Tooltip("Ukuran texture per meter. Semakin kecil nilainya, semakin besar tile texture yang terlihat.")]
    public float textureMetersPerTile = 2f;

    [Header("Materials")]
    public Material floorMaterial;
    public Material wallMaterial;
    public Material ceilingMaterial;

    private int[,] grid;
    private HashSet<Vector2Int> roomPositions = new HashSet<Vector2Int>();

    public int[,] Grid => grid;
    public float TileSize => tileSize;
    public float TileGap => tileGap;
    public Vector3 Origin => transform.position;
    public float WallHeight => wallHeight;
    public float CeilingThickness => ceilingThickness;
    public float FloorThickness => floorThickness;
    public bool IsGenerated { get; private set; }
    private List<ProBuilderMesh> floorMeshes = new List<ProBuilderMesh>();
    private List<ProBuilderMesh> wallMeshes = new List<ProBuilderMesh>();
    private List<ProBuilderMesh> ceilingMeshes = new List<ProBuilderMesh>();
    private HashSet<EdgeKey> passages = new HashSet<EdgeKey>();
    private HashSet<Vector2Int> generatedPillars = new HashSet<Vector2Int>();

    private readonly Vector2Int[] directions = new Vector2Int[]
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    private struct EdgeKey
    {
        public Vector2Int a;
        public Vector2Int b;

        public EdgeKey(Vector2Int p1, Vector2Int p2)
        {
            if (p1.x < p2.x || (p1.x == p2.x && p1.y < p2.y))
            {
                a = p1;
                b = p2;
            }
            else
            {
                a = p2;
                b = p1;
            }
        }

        public override bool Equals(object obj)
        {
            if (!(obj is EdgeKey)) return false;
            EdgeKey other = (EdgeKey)obj;
            return a.Equals(other.a) && b.Equals(other.b);
        }

        public override int GetHashCode()
        {
            return a.GetHashCode() ^ (b.GetHashCode() * 397);
        }
    }

    void Start()
    {
        GenerateDungeon();
    }

    public void GenerateDungeon()
    {
        grid = new int[size, size];
        roomPositions.Clear();
        floorMeshes.Clear();
        wallMeshes.Clear();
        ceilingMeshes.Clear();
        passages.Clear();
        generatedPillars.Clear();

        Vector2Int currentPosition = new Vector2Int(size / 2, size / 2);
        grid[currentPosition.x, currentPosition.y] = 1;
        roomPositions.Add(currentPosition);

        List<Vector2Int> visitedList = new List<Vector2Int> { currentPosition };

        while (roomPositions.Count < totalRooms)
        {
            Vector2Int randomDirection = directions[Random.Range(0, directions.Length)];
            Vector2Int nextPosition = currentPosition + randomDirection;

            if (IsInsideBounds(nextPosition))
            {
                currentPosition = nextPosition;
                if (grid[currentPosition.x, currentPosition.y] == 0)
                {
                    grid[currentPosition.x, currentPosition.y] = 1;
                    roomPositions.Add(currentPosition);
                    visitedList.Add(currentPosition);

                    Vector2Int prev = currentPosition - randomDirection;
                    passages.Add(new EdgeKey(prev, currentPosition));
                }
            }
            else
            {
                currentPosition = visitedList[Random.Range(0, visitedList.Count)];
            }
        }

        ProcessRoomTypes();
        GenerateFloorTiles();
        GenerateCeilingTiles();
        GenerateGapFillers();
        GenerateCeilingGapFillers();
        GenerateWallsAndPillars();
        CombineAllMeshes();
        IsGenerated = true;
    }

    private bool IsInsideBounds(Vector2Int position)
    {
        return position.x > 0 && position.x < size - 1 &&
               position.y > 0 && position.y < size - 1;
    }

    private void ProcessRoomTypes()
    {
        for (int x = 1; x < size - 1; x++)
        {
            for (int y = 1; y < size - 1; y++)
            {
                if (grid[x, y] == 1 && grid[x + 1, y] == 1 &&
                    grid[x, y + 1] == 1 && grid[x + 1, y + 1] == 1)
                {
                    if (Random.value < bigRoomChance)
                    {
                        grid[x, y] = 3; grid[x + 1, y] = 3;
                        grid[x, y + 1] = 3; grid[x + 1, y + 1] = 3;

                        passages.Add(new EdgeKey(new Vector2Int(x, y), new Vector2Int(x + 1, y)));
                        passages.Add(new EdgeKey(new Vector2Int(x, y), new Vector2Int(x, y + 1)));
                        passages.Add(new EdgeKey(new Vector2Int(x + 1, y), new Vector2Int(x + 1, y + 1)));
                        passages.Add(new EdgeKey(new Vector2Int(x, y + 1), new Vector2Int(x + 1, y + 1)));
                    }
                }
            }
        }

        for (int x = 1; x < size - 1; x++)
        {
            for (int y = 1; y < size - 1; y++)
            {
                if (grid[x, y] == 1 && grid[x + 1, y] == 1 && grid[x + 2, y] == 1)
                {
                    grid[x, y] = 2; grid[x + 1, y] = 2; grid[x + 2, y] = 2;
                    passages.Add(new EdgeKey(new Vector2Int(x, y), new Vector2Int(x + 1, y)));
                    passages.Add(new EdgeKey(new Vector2Int(x + 1, y), new Vector2Int(x + 2, y)));
                }
                else if (grid[x, y] == 1 && grid[x, y + 1] == 1 && grid[x, y + 2] == 1)
                {
                    grid[x, y] = 2; grid[x, y + 1] = 2; grid[x, y + 2] = 2;
                    passages.Add(new EdgeKey(new Vector2Int(x, y), new Vector2Int(x, y + 1)));
                    passages.Add(new EdgeKey(new Vector2Int(x, y + 1), new Vector2Int(x, y + 2)));
                }
            }
        }
    }

    private void GenerateFloorTiles()
    {
        float pitch = tileSize + tileGap;
        Vector3 tileSizeVec = new Vector3(tileSize, floorThickness, tileSize);

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                if (grid[x, y] == 0) continue;

                Vector3 spawnPos = transform.position + new Vector3(x * pitch, 0, y * pitch);
                floorMeshes.Add(CreateBlockMesh(spawnPos, tileSizeVec));
            }
        }
    }

    private void GenerateCeilingTiles()
    {
        float pitch = tileSize + tileGap;
        float ceilingY = wallHeight + (ceilingThickness / 2f) + (floorThickness / 2f);
        Vector3 ceilingSizeVec = new Vector3(tileSize, ceilingThickness, tileSize);

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                if (grid[x, y] == 0) continue;

                Vector3 spawnPos = transform.position + new Vector3(x * pitch, ceilingY, y * pitch);
                ceilingMeshes.Add(CreateBlockMesh(spawnPos, ceilingSizeVec));
            }
        }
    }

    private void GenerateGapFillers()
    {
        if (tileGap <= 0) return;

        float pitch = tileSize + tileGap;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                if (grid[x, y] == 0) continue;

                if (x + 1 < size && grid[x + 1, y] > 0)
                {
                    Vector3 centerPos = transform.position + new Vector3((x * pitch) + (tileSize / 2f) + (tileGap / 2f), 0, y * pitch);
                    Vector3 gapDimensions = new Vector3(tileGap, floorThickness, tileSize);
                    floorMeshes.Add(CreateBlockMesh(centerPos, gapDimensions));
                }

                if (y + 1 < size && grid[x, y + 1] > 0)
                {
                    Vector3 centerPos = transform.position + new Vector3(x * pitch, 0, (y * pitch) + (tileSize / 2f) + (tileGap / 2f));
                    Vector3 gapDimensions = new Vector3(tileSize, floorThickness, tileGap);
                    floorMeshes.Add(CreateBlockMesh(centerPos, gapDimensions));
                }

                bool right = (x + 1 < size && grid[x + 1, y] > 0);
                bool top = (y + 1 < size && grid[x, y + 1] > 0);
                bool topRight = (x + 1 < size && y + 1 < size && grid[x + 1, y + 1] > 0);

                if (right || top || topRight)
                {
                    Vector3 centerPos = transform.position + new Vector3((x * pitch) + (tileSize / 2f) + (tileGap / 2f), 0, (y * pitch) + (tileSize / 2f) + (tileGap / 2f));
                    Vector3 gapDimensions = new Vector3(tileGap, floorThickness, tileGap);
                    floorMeshes.Add(CreateBlockMesh(centerPos, gapDimensions));
                }
            }
        }
    }

    private void GenerateCeilingGapFillers()
    {
        if (tileGap <= 0) return;

        float pitch = tileSize + tileGap;
        float ceilingY = wallHeight + (ceilingThickness / 2f) + (floorThickness / 2f);

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                if (grid[x, y] == 0) continue;

                if (x + 1 < size && grid[x + 1, y] > 0)
                {
                    Vector3 centerPos = transform.position + new Vector3((x * pitch) + (tileSize / 2f) + (tileGap / 2f), ceilingY, y * pitch);
                    Vector3 gapDimensions = new Vector3(tileGap, ceilingThickness, tileSize);
                    ceilingMeshes.Add(CreateBlockMesh(centerPos, gapDimensions));
                }

                if (y + 1 < size && grid[x, y + 1] > 0)
                {
                    Vector3 centerPos = transform.position + new Vector3(x * pitch, ceilingY, (y * pitch) + (tileSize / 2f) + (tileGap / 2f));
                    Vector3 gapDimensions = new Vector3(tileSize, ceilingThickness, tileGap);
                    ceilingMeshes.Add(CreateBlockMesh(centerPos, gapDimensions));
                }

                bool right = (x + 1 < size && grid[x + 1, y] > 0);
                bool top = (y + 1 < size && grid[x, y + 1] > 0);
                bool topRight = (x + 1 < size && y + 1 < size && grid[x + 1, y + 1] > 0);

                if (right || top || topRight)
                {
                    Vector3 centerPos = transform.position + new Vector3((x * pitch) + (tileSize / 2f) + (tileGap / 2f), ceilingY, (y * pitch) + (tileSize / 2f) + (tileGap / 2f));
                    Vector3 gapDimensions = new Vector3(tileGap, ceilingThickness, tileGap);
                    ceilingMeshes.Add(CreateBlockMesh(centerPos, gapDimensions));
                }
            }
        }
    }

    private void GenerateWallsAndPillars()
    {
        float pitch = tileSize + tileGap;
        float wallPosY = wallHeight / 2f + floorThickness / 2f;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                if (grid[x, y] == 0) continue;

                Vector2Int curr = new Vector2Int(x, y);

                Vector2Int right = new Vector2Int(x + 1, y);
                if (x + 1 < size && grid[x + 1, y] > 0)
                {
                    bool hasPassage = passages.Contains(new EdgeKey(curr, right));
                    bool isInternalCorridor = (grid[x, y] == 2 && grid[x + 1, y] == 2);
                    bool isInternalBigRoom = (grid[x, y] == 3 && grid[x + 1, y] == 3);

                    if (!isInternalCorridor && !isInternalBigRoom)
                    {
                        if (hasPassage)
                        {
                            Vector3 wallCenter = transform.position + new Vector3((x * pitch) + (tileSize / 2f) + (tileGap / 2f), 0, y * pitch);
                            GenerateDoorWall(wallCenter, true);
                        }
                        else
                        {
                            Vector3 wallPos = transform.position + new Vector3((x * pitch) + (tileSize / 2f) + (tileGap / 2f), wallPosY, y * pitch);
                            Vector3 wallDim = new Vector3(wallThickness, wallHeight, tileSize);
                            wallMeshes.Add(CreateBlockMesh(wallPos, wallDim));
                        }
                    }
                }
                else
                {
                    Vector3 wallPos = transform.position + new Vector3((x * pitch) + (tileSize / 2f) + (wallThickness / 2f), wallPosY, y * pitch);
                    Vector3 wallDim = new Vector3(wallThickness, wallHeight, tileSize);
                    wallMeshes.Add(CreateBlockMesh(wallPos, wallDim));
                }

                Vector2Int top = new Vector2Int(x, y + 1);
                if (y + 1 < size && grid[x, y + 1] > 0)
                {
                    bool hasPassage = passages.Contains(new EdgeKey(curr, top));
                    bool isInternalCorridor = (grid[x, y] == 2 && grid[x, y + 1] == 2);
                    bool isInternalBigRoom = (grid[x, y] == 3 && grid[x, y + 1] == 3);

                    if (!isInternalCorridor && !isInternalBigRoom)
                    {
                        if (hasPassage)
                        {
                            Vector3 wallCenter = transform.position + new Vector3(x * pitch, 0, (y * pitch) + (tileSize / 2f) + (tileGap / 2f));
                            GenerateDoorWall(wallCenter, false);
                        }
                        else
                        {
                            Vector3 wallPos = transform.position + new Vector3(x * pitch, wallPosY, (y * pitch) + (tileSize / 2f) + (tileGap / 2f));
                            Vector3 wallDim = new Vector3(tileSize, wallHeight, wallThickness);
                            wallMeshes.Add(CreateBlockMesh(wallPos, wallDim));
                        }
                    }
                }
                else
                {
                    Vector3 wallPos = transform.position + new Vector3(x * pitch, wallPosY, (y * pitch) + (tileSize / 2f) + (wallThickness / 2f));
                    Vector3 wallDim = new Vector3(tileSize, wallHeight, wallThickness);
                    wallMeshes.Add(CreateBlockMesh(wallPos, wallDim));
                }

                if (x - 1 < 0 || grid[x - 1, y] == 0)
                {
                    Vector3 wallPos = transform.position + new Vector3((x * pitch) - (tileSize / 2f) - (wallThickness / 2f), wallPosY, y * pitch);
                    Vector3 wallDim = new Vector3(wallThickness, wallHeight, tileSize);
                    wallMeshes.Add(CreateBlockMesh(wallPos, wallDim));
                }

                if (y - 1 < 0 || grid[x, y - 1] == 0)
                {
                    Vector3 wallPos = transform.position + new Vector3(x * pitch, wallPosY, (y * pitch) - (tileSize / 2f) - (wallThickness / 2f));
                    Vector3 wallDim = new Vector3(tileSize, wallHeight, wallThickness);
                    wallMeshes.Add(CreateBlockMesh(wallPos, wallDim));
                }

                if (tileGap > 0)
                {
                    TryGenerateCornerPillar(x, y, 1, 1, pitch, wallPosY);
                    TryGenerateCornerPillar(x, y, -1, 1, pitch, wallPosY);
                    TryGenerateCornerPillar(x, y, 1, -1, pitch, wallPosY);
                    TryGenerateCornerPillar(x, y, -1, -1, pitch, wallPosY);
                }
            }
        }
    }

    private void TryGenerateCornerPillar(int gridX, int gridY, int offsetX, int offsetY, float pitch, float wallPosY)
    {
        int pillarGridX = gridX * 2 + (offsetX > 0 ? 1 : 0);
        int pillarGridY = gridY * 2 + (offsetY > 0 ? 1 : 0);
        Vector2Int pillarKey = new Vector2Int(pillarGridX, pillarGridY);

        if (generatedPillars.Contains(pillarKey)) return;

        Vector3 pillarPos = transform.position + new Vector3(
            (gridX * pitch) + (offsetX * (tileSize / 2f + tileGap / 2f)),
            wallPosY,
            (gridY * pitch) + (offsetY * (tileSize / 2f + tileGap / 2f))
        );

        Vector3 pillarDim = new Vector3(tileGap, wallHeight, tileGap);
        wallMeshes.Add(CreateBlockMesh(pillarPos, pillarDim));
        generatedPillars.Add(pillarKey);
    }

    private void GenerateDoorWall(Vector3 centerPosition, bool isVerticalWall)
    {
        float actualDoorWidth = tileSize * 0.4f;
        float actualDoorHeight = wallHeight * 0.7f;
        float sideWidth = (tileSize - actualDoorWidth) / 2f;
        float topHeight = wallHeight - actualDoorHeight;

        float baseY = centerPosition.y + floorThickness / 2f;

        if (isVerticalWall)
        {
            if (topHeight > 0)
            {
                float topPosY = baseY + actualDoorHeight + (topHeight / 2f);
                Vector3 topPos = new Vector3(centerPosition.x, topPosY, centerPosition.z);
                Vector3 topDim = new Vector3(wallThickness, topHeight, tileSize);
                wallMeshes.Add(CreateBlockMesh(topPos, topDim));
            }

            if (sideWidth > 0)
            {
                float sidePosY = baseY + (actualDoorHeight / 2f);

                Vector3 sidePos1 = new Vector3(centerPosition.x, sidePosY, centerPosition.z + (actualDoorWidth / 2f) + (sideWidth / 2f));
                Vector3 sideDim1 = new Vector3(wallThickness, actualDoorHeight, sideWidth);
                wallMeshes.Add(CreateBlockMesh(sidePos1, sideDim1));

                Vector3 sidePos2 = new Vector3(centerPosition.x, sidePosY, centerPosition.z - (actualDoorWidth / 2f) - (sideWidth / 2f));
                Vector3 sideDim2 = new Vector3(wallThickness, actualDoorHeight, sideWidth);
                wallMeshes.Add(CreateBlockMesh(sidePos2, sideDim2));
            }
        }
        else
        {
            if (topHeight > 0)
            {
                float topPosY = baseY + actualDoorHeight + (topHeight / 2f);
                Vector3 topPos = new Vector3(centerPosition.x, topPosY, centerPosition.z);
                Vector3 topDim = new Vector3(tileSize, topHeight, wallThickness);
                wallMeshes.Add(CreateBlockMesh(topPos, topDim));
            }

            if (sideWidth > 0)
            {
                float sidePosY = baseY + (actualDoorHeight / 2f);

                Vector3 sidePos1 = new Vector3(centerPosition.x + (actualDoorWidth / 2f) + (sideWidth / 2f), sidePosY, centerPosition.z);
                Vector3 sideDim1 = new Vector3(sideWidth, actualDoorHeight, wallThickness);
                wallMeshes.Add(CreateBlockMesh(sidePos1, sideDim1));

                Vector3 sidePos2 = new Vector3(centerPosition.x - (actualDoorWidth / 2f) - (sideWidth / 2f), sidePosY, centerPosition.z);
                Vector3 sideDim2 = new Vector3(sideWidth, actualDoorHeight, wallThickness);
                wallMeshes.Add(CreateBlockMesh(sidePos2, sideDim2));
            }
        }
    }

    private ProBuilderMesh CreateBlockMesh(Vector3 centerPosition, Vector3 dimensions)
    {
        ProBuilderMesh mesh = ShapeGenerator.CreateShape(ShapeType.Cube, PivotLocation.Center);
        mesh.transform.SetParent(transform, false);
        mesh.transform.position = centerPosition;
        mesh.transform.localScale = dimensions;
        return mesh;
    }

    private void FixUVs(Mesh mesh, Vector3 scale)
    {
        Vector2[] uvs = mesh.uv;
        if (uvs == null || uvs.Length == 0) return;

        Vector3[] normals = mesh.normals;
        float perMeter = 1f / Mathf.Max(textureMetersPerTile, 0.001f);

        for (int i = 0; i < uvs.Length; i++)
        {
            Vector3 n = normals[i].normalized;
            float uScale, vScale;

            if (Mathf.Abs(n.x) > 0.5f) { uScale = scale.z; vScale = scale.y; }
            else if (Mathf.Abs(n.y) > 0.5f) { uScale = scale.x; vScale = scale.z; }
            else { uScale = scale.x; vScale = scale.y; }

            uvs[i] = new Vector2(uvs[i].x * uScale * perMeter, uvs[i].y * vScale * perMeter);
        }

        mesh.uv = uvs;
    }

    private void CombineAllMeshes()
    {
        CombineGroup(floorMeshes, "Dungeon_Floor", floorMaterial);
        CombineGroup(wallMeshes, "Dungeon_Walls", wallMaterial);
        CombineGroup(ceilingMeshes, "Dungeon_Ceiling", ceilingMaterial);
    }

    private void CombineGroup(List<ProBuilderMesh> meshes, string childName, Material material)
    {
        if (meshes == null || meshes.Count == 0) return;

        List<CombineInstance> combineList = new List<CombineInstance>();
        bool wantsCopy = childName == "Dungeon_Floor" || childName == "Dungeon_Walls";
        List<CombineInstance> copyInstances = wantsCopy ? new List<CombineInstance>() : null;

        for (int i = 0; i < meshes.Count; i++)
        {
            ProBuilderMesh pbMesh = meshes[i];
            if (pbMesh == null) continue;

            pbMesh.ToMesh();
            pbMesh.Refresh();

            MeshFilter filter = pbMesh.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                Mesh meshCopy = Instantiate(filter.sharedMesh);
                FixUVs(meshCopy, pbMesh.transform.localScale);

                CombineInstance instance = new CombineInstance();
                instance.mesh = meshCopy;
                instance.transform = transform.worldToLocalMatrix * pbMesh.transform.localToWorldMatrix;
                combineList.Add(instance);
                if (wantsCopy) copyInstances.Add(instance);
            }
        }

        for (int i = 0; i < meshes.Count; i++)
        {
            if (meshes[i] != null)
            {
                DestroyImmediate(meshes[i].gameObject);
            }
        }
        meshes.Clear();

        if (combineList.Count == 0) return;

        Mesh combinedMesh = new Mesh();
        combinedMesh.indexFormat = IndexFormat.UInt32;
        combinedMesh.CombineMeshes(combineList.ToArray(), true, true);
        combinedMesh.RecalculateBounds();
        combinedMesh.RecalculateNormals();

        GameObject childObj = new GameObject(childName);
        childObj.transform.SetParent(transform, false);
        childObj.transform.localPosition = Vector3.zero;

        MeshFilter meshFilter = childObj.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = combinedMesh;

        MeshRenderer meshRenderer = childObj.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material != null ? material : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        meshRenderer.receiveShadows = true;

        MeshCollider meshCollider = childObj.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = combinedMesh;

        if (childName == "Dungeon_Floor" || childName == "Dungeon_Walls")
            CreateMinimapCopy(copyInstances, childName);
    }

    private void CreateMinimapCopy(List<CombineInstance> instances, string sourceName)
    {
        if (instances == null || instances.Count == 0) return;

        int layer = LayerMask.NameToLayer("Minimap");
        if (layer < 0) return;

        string copyName = sourceName == "Dungeon_Floor" ? "Dungeon_Minimap_Floor" : "Dungeon_Minimap_Walls";

        Shader flatShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (flatShader == null) flatShader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (flatShader == null) flatShader = Shader.Find("Standard");
        if (flatShader == null)
        {
            Debug.LogWarning("DungeonGenerator: shader flat tidak ditemukan, copy minimap dilewati.");
            return;
        }

        float cutY = Mathf.Clamp(minimapCopyHeightFactor, 0.1f, 1f) * wallHeight;
        Vector3 copyOffset = new Vector3(minimapCopyOffset, 0f, 0f);

        List<Mesh> tempMeshes = new List<Mesh>();
        List<CombineInstance> clippedList = new List<CombineInstance>();

        foreach (CombineInstance inst in instances)
        {
            Mesh src = inst.mesh;
            if (src == null) continue;

            Vector3[] verts = src.vertices;
            if (verts.Length == 0) continue;

            Vector3[] baked = new Vector3[verts.Length];
            float yMin = float.MaxValue, yMax = float.MinValue;
            for (int i = 0; i < verts.Length; i++)
            {
                baked[i] = inst.transform.MultiplyPoint3x4(verts[i]);
                if (baked[i].y < yMin) yMin = baked[i].y;
                if (baked[i].y > yMax) yMax = baked[i].y;
            }

            if (yMin >= cutY) continue;

            Mesh clipped = new Mesh();
            tempMeshes.Add(clipped);

            for (int i = 0; i < baked.Length; i++)
            {
                if (baked[i].y > cutY) baked[i].y = cutY;
                verts[i] = baked[i] + copyOffset;
            }

            clipped.vertices = verts;
            clipped.uv = src.uv;
            clipped.normals = src.normals;
            clipped.triangles = src.triangles;

            clippedList.Add(new CombineInstance { mesh = clipped, transform = Matrix4x4.identity });
        }

        if (clippedList.Count == 0)
        {
            foreach (Mesh m in tempMeshes) Destroy(m);
            return;
        }

        Mesh combinedMesh = new Mesh();
        combinedMesh.indexFormat = IndexFormat.UInt32;
        combinedMesh.CombineMeshes(clippedList.ToArray(), true, true);
        combinedMesh.RecalculateBounds();
        combinedMesh.RecalculateNormals();

        foreach (Mesh m in tempMeshes) Destroy(m);

        GameObject copy = new GameObject(copyName);
        copy.transform.SetParent(transform, false);
        copy.transform.localPosition = Vector3.zero;
        copy.layer = layer;

        MeshFilter filter = copy.AddComponent<MeshFilter>();
        filter.sharedMesh = combinedMesh;

        MeshRenderer renderer = copy.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = new Material(flatShader);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    public List<RoomData> GetRooms()
    {
        List<RoomData> rooms = new List<RoomData>();
        if (grid == null) return rooms;

        int w = grid.GetLength(0);
        int h = grid.GetLength(1);
        bool[,] visited = new bool[w, h];
        float pitch = tileSize + tileGap;
        float centerY = floorThickness / 2f;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                int val = grid[x, y];
                if ((val != 1 && val != 3) || visited[x, y]) continue;

                RoomData room = new RoomData();
                room.gridValue = val;

                Queue<Vector2Int> queue = new Queue<Vector2Int>();
                queue.Enqueue(new Vector2Int(x, y));
                visited[x, y] = true;

                int sumX = 0, sumY = 0;

                while (queue.Count > 0)
                {
                    Vector2Int cell = queue.Dequeue();
                    room.tiles.Add(cell);
                    sumX += cell.x;
                    sumY += cell.y;

                    for (int d = 0; d < directions.Length; d++)
                    {
                        Vector2Int nxt = cell + directions[d];
                        if (nxt.x < 0 || nxt.x >= w || nxt.y < 0 || nxt.y >= h) continue;
                        if (visited[nxt.x, nxt.y]) continue;

                        int nVal = grid[nxt.x, nxt.y];
                        if (nVal == 1 || nVal == 3)
                        {
                            visited[nxt.x, nxt.y] = true;
                            queue.Enqueue(nxt);
                        }
                    }
                }

                room.centerGrid = new Vector2Int(
                    Mathf.RoundToInt((float)sumX / room.tiles.Count),
                    Mathf.RoundToInt((float)sumY / room.tiles.Count)
                );

                room.centerWorld = Origin + new Vector3(
                    room.centerGrid.x * pitch,
                    centerY,
                    room.centerGrid.y * pitch
                );

                rooms.Add(room);
            }
        }

        return rooms;
    }
}
