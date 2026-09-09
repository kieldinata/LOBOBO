using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class EnemyTypeConfig
    {
        [Tooltip("Prefab enemy tipe ini.")]
        public GameObject prefab;
        [Tooltip("Bobot peluang muncul (besar = sering).")]
        public float weight = 1f;
    }

    [Header("References")]
    public DungeonGenerator dungeon;
    [Tooltip("Kosong = otomatis cari di dungeon.")]
    public DungeonNavMeshBake navMeshBake;
    public bool bakeNavMesh = true;

    [Header("Spawn")]
    [Tooltip("Daftar tipe enemy (dipilih per bobot).")]
    public List<EnemyTypeConfig> enemyTypes = new List<EnemyTypeConfig>();
    public int maxEnemies = 5;
    [Tooltip("Jarak min. spawn dari player (cell).")]
    public float minDistanceRooms = 8f;
    public bool spawnOnStart = true;

    [Header("Respawn")]
    [Tooltip("Jeda respawn setelah enemy mati (detik).")]
    public float respawnCooldownSeconds = 120f;

    [Header("Debug")]
    [Tooltip("Tekan K untuk membunuh enemy acak.")]
    public bool debugKillWithKey = true;

    public event System.Action<GameObject> OnEnemySpawned;
    public event System.Action<GameObject> OnEnemyDied;

    private readonly List<GameObject> aliveEnemies = new List<GameObject>();
    private readonly Queue<float> respawnDue = new Queue<float>();
    private Transform target;
    private float pitch = 10f;

    void Start()
    {
        if (dungeon == null)
        {
            Debug.LogWarning("EnemySpawner: dungeon reference belum di-assign.");
            return;
        }
        if (target == null)
            target = FindTargetTransform();
        if (target == null)
        {
            Debug.LogWarning("EnemySpawner: player tidak ditemukan. Pastikan ada objek bernama 'Player' atau ber-tag 'Player'.");
            return;
        }
        if (dungeon.Grid == null)
        {
            Debug.LogWarning("EnemySpawner: grid dungeon belum di-generate.");
            return;
        }
        if (!HasSpawnableType())
        {
            Debug.LogWarning("EnemySpawner: belum ada tipe enemy dengan prefab + weight > 0, spawn dilewati.");
            return;
        }

        pitch = dungeon.TileSize + dungeon.TileGap;

        if (bakeNavMesh)
            EnsureNavMeshBaked();

        if (spawnOnStart)
        {
            for (int i = 0; i < maxEnemies; i++)
                SpawnEnemy();
        }
    }

    private Transform FindTargetTransform()
    {
        GameObject obj = GameObject.Find("Player");
        if (obj != null) return obj.transform;

        CharacterController cc = FindFirstObjectByType<CharacterController>();
        return cc != null ? cc.transform : null;
    }

    private void EnsureNavMeshBaked()
    {
        if (navMeshBake == null)
            navMeshBake = dungeon.transform.GetComponent<DungeonNavMeshBake>();

        if (navMeshBake == null)
        {
            navMeshBake = dungeon.gameObject.AddComponent<DungeonNavMeshBake>();
            navMeshBake.dungeon = dungeon;
        }

        if (navMeshBake.dungeon == null)
            navMeshBake.dungeon = dungeon;

        navMeshBake.EnsureBaked();
        Debug.Log("EnemySpawner: NavMesh dungeon berhasil di-bake.");
    }

    void Update()
    {
        CleanupAlive();

        if (debugKillWithKey && Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
            KillRandomEnemy();

        while (respawnDue.Count > 0 && aliveEnemies.Count < maxEnemies)
        {
            if (respawnDue.Peek() > Time.time) break;
            respawnDue.Dequeue();
            if (!SpawnEnemy()) break;
        }
    }

    public void NotifyEnemyDied(GameObject enemy)
    {
        if (enemy == null) return;

        if (aliveEnemies.Remove(enemy))
        {
            respawnDue.Enqueue(Time.time + respawnCooldownSeconds);
            OnEnemyDied?.Invoke(enemy);
            Debug.Log($"EnemySpawner: 1 enemy mati. Rencana respawn dalam {respawnCooldownSeconds}s. (Alive {aliveEnemies.Count}/{maxEnemies})");
        }
    }

    private bool HasSpawnableType()
    {
        for (int i = 0; i < enemyTypes.Count; i++)
        {
            EnemyTypeConfig config = enemyTypes[i];
            if (config != null && config.prefab != null && config.weight > 0f)
                return true;
        }
        return false;
    }

    private EnemyTypeConfig PickSpawnType()
    {
        float total = 0f;
        for (int i = 0; i < enemyTypes.Count; i++)
        {
            EnemyTypeConfig config = enemyTypes[i];
            if (config != null && config.prefab != null && config.weight > 0f)
                total += config.weight;
        }

        if (total <= 0f) return null;

        float roll = Random.Range(0f, total);
        for (int i = 0; i < enemyTypes.Count; i++)
        {
            EnemyTypeConfig config = enemyTypes[i];
            if (config == null || config.prefab == null || config.weight <= 0f) continue;

            roll -= config.weight;
            if (roll <= 0f) return config;
        }

        return null;
    }

    private bool SpawnEnemy()
    {
        Vector2Int? cell = PickSpawnCell();
        if (cell == null)
        {
            Debug.Log("EnemySpawner: belum ada cell spawn valid berjarak cukup, respawn ditunda.");
            return false;
        }

        EnemyTypeConfig config = PickSpawnType();
        if (config == null)
        {
            Debug.LogWarning("EnemySpawner: belum ada tipe enemy dengan prefab + weight > 0, spawn ditunda.");
            return false;
        }

        Vector3 worldPos = dungeon.transform.TransformPoint(
            new Vector3(cell.Value.x * pitch, dungeon.FloorThickness / 2f, cell.Value.y * pitch)
        );

        GameObject enemy = Instantiate(config.prefab, worldPos, Quaternion.identity);
        enemy.name = "Enemy_" + config.prefab.name;

        enemy.transform.SetParent(transform, true);
        enemy.transform.localScale = Vector3.one * 0.3f;

        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        if (ai == null)
            ai = enemy.AddComponent<EnemyAI>();

        ai.Setup(target);
        ai.dungeon = dungeon;
        ai.WarpTo(worldPos);

        aliveEnemies.Add(enemy);
        OnEnemySpawned?.Invoke(enemy);
        return true;
    }

    private Vector2Int? PickSpawnCell()
    {
        int[,] grid = dungeon.Grid;
        int w = grid.GetLength(0);
        int h = grid.GetLength(1);

        Vector3 localTarget = dungeon.transform.InverseTransformPoint(target.position);
        int px = Mathf.RoundToInt(localTarget.x / pitch);
        int py = Mathf.RoundToInt(localTarget.z / pitch);

        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (grid[x, y] == 0) continue;

                int dist = Mathf.Max(Mathf.Abs(x - px), Mathf.Abs(y - py));
                if (dist < minDistanceRooms) continue;
                if (IsOccupied(x, y)) continue;

                candidates.Add(new Vector2Int(x, y));
            }
        }

        if (candidates.Count == 0) return null;

        return candidates[Random.Range(0, candidates.Count)];
    }

    private bool IsOccupied(int gridX, int gridY)
    {
        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            GameObject enemy = aliveEnemies[i];
            if (enemy == null) continue;

            Vector3 local = dungeon.transform.InverseTransformPoint(enemy.transform.position);
            int ex = Mathf.RoundToInt(local.x / pitch);
            int ey = Mathf.RoundToInt(local.z / pitch);
            if (ex == gridX && ey == gridY) return true;
        }
        return false;
    }

    private void CleanupAlive()
    {
        for (int i = aliveEnemies.Count - 1; i >= 0; i--)
        {
            if (aliveEnemies[i] == null)
                aliveEnemies.RemoveAt(i);
        }
    }

    private void KillRandomEnemy()
    {
        CleanupAlive();
        if (aliveEnemies.Count == 0) return;

        GameObject enemy = aliveEnemies[Random.Range(0, aliveEnemies.Count)];
        Destroy(enemy);
        NotifyEnemyDied(enemy);
    }
}