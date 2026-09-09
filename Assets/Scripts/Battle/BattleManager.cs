using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class BattleManager : MonoBehaviour
{
    private static BattleManager _instance;
    public static BattleManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<BattleManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("BattleManager");
                    _instance = go.AddComponent<BattleManager>();
                    DontDestroyOnLoad(go);
                    Debug.LogWarning("BattleManager: objek tidak ditemukan di scene, dibuat otomatis. Sebaiknya tambahkan GameObject 'BattleManager' di scene World.");
                }
            }
            return _instance;
        }
    }

    [Header("World References (auto-find)")]
    public DungeonGenerator dungeon;
    public Transform player;
    public EnemySpawner spawner;

    [Header("Battle Scene")]
    [Tooltip("Nama scene battle (daftar di Build Settings).")]
    public string battleSceneName = "BattleScene";

    [Header("Combat Result")]
    [Tooltip("Jarak min. kabur (cell). 0 = otomatis.")]
    public float fleeMinCellDistance = 0f;

    private EnemyAI activeEnemy;
    private bool battleActive;
    private Camera dungeonCamera;
    private Quaternion dungeonCameraRotation;
    private bool introLooking;
    private Quaternion introLookRotation;
    private const float battleIntroDuration = 0.5f;

    public string ActiveEnemyDisplayName
    {
        get { return activeEnemy != null ? activeEnemy.gameObject.name : "?"; }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void Start()
    {
        Autofind();
    }

    public bool RequestBattle(EnemyAI enemy)
    {
        if (battleActive || enemy == null) return false;

        Autofind();

        if (string.IsNullOrEmpty(battleSceneName) || !SceneInBuild(battleSceneName))
        {
            Debug.LogError($"BattleManager: scene '{battleSceneName}' tidak ditemukan di Build Settings. Tambahkan via File > Build Settings.");
            return false;
        }

        activeEnemy = enemy;
        battleActive = true;
        StartCoroutine(BattleIntroSequence(enemy));
        return true;
    }

    private IEnumerator BattleIntroSequence(EnemyAI enemy)
    {
        Time.timeScale = 0f;

        Camera mainCamera = Camera.main;
        if (mainCamera == null && player != null)
            mainCamera = player.GetComponentInChildren<Camera>();

        if (mainCamera != null)
        {
            dungeonCamera = mainCamera;
            dungeonCameraRotation = mainCamera.transform.rotation;

            Vector3 dir = (enemy.transform.position + Vector3.up * enemy.eyeHeight) - mainCamera.transform.position;
            if (dir.sqrMagnitude > 0.0001f)
            {
                introLookRotation = Quaternion.LookRotation(dir);
                introLooking = true;
            }
        }

        yield return new WaitForSecondsRealtime(battleIntroDuration);

        introLooking = false;
        SceneManager.LoadScene(battleSceneName, LoadSceneMode.Additive);

        Debug.Log($"BattleManager: battle dimulai melawan {enemy.name}.");
    }

    private void LateUpdate()
    {
        if (introLooking && dungeonCamera != null)
            dungeonCamera.transform.rotation = introLookRotation;
    }

    public void EndBattle(bool enemyDefeated)
    {
        if (!battleActive) return;

        battleActive = false;

        if (enemyDefeated)
        {
            if (activeEnemy != null)
            {
                GameObject enemyGo = activeEnemy.gameObject;
                activeEnemy = null;
                if (spawner != null) spawner.NotifyEnemyDied(enemyGo);
                if (enemyGo != null) Destroy(enemyGo);
            }
            Debug.Log("BattleManager: menang. Enemy dihancurkan, respawn cooldown berjalan.");
        }
        else
        {
            EnemyAI enemy = activeEnemy;
            activeEnemy = null;
            TeleportPlayerAwayFrom(enemy);
            if (enemy != null)
                enemy.ResetToHome();
            Debug.Log("BattleManager: kabur. Enemy kembali ke Patrol, player diteleport ke tempat aman.");
        }

        CleanupBattleAssets();
    }

    private void CleanupBattleAssets()
    {
        Time.timeScale = 1f;

        if (dungeonCamera != null)
        {
            dungeonCamera.transform.rotation = dungeonCameraRotation;
            dungeonCamera.gameObject.SetActive(true);
            dungeonCamera = null;
        }

        if (!string.IsNullOrEmpty(battleSceneName))
            SceneManager.UnloadSceneAsync(battleSceneName);
    }

    private void TeleportPlayerAwayFrom(EnemyAI enemy)
    {
        if (dungeon == null || dungeon.Grid == null)
        {
            Debug.LogWarning("BattleManager: dungeon belum siap, player tidak dipindah.");
            return;
        }
        if (player == null)
        {
            Debug.LogWarning("BattleManager: player tidak ditemukan (cari bernama/tag 'Player'), kabur tanpa teleport.");
            return;
        }

        int[,] grid = dungeon.Grid;
        int w = grid.GetLength(0);
        int h = grid.GetLength(1);
        float pitch = dungeon.TileSize + dungeon.TileGap;

        int ex = 0, ey = 0;
        if (enemy != null)
        {
            Vector3 localEnemy = dungeon.transform.InverseTransformPoint(enemy.transform.position);
            ex = Mathf.RoundToInt(localEnemy.x / pitch);
            ey = Mathf.RoundToInt(localEnemy.z / pitch);
        }

        float suspicion = enemy != null ? enemy.suspicionRadius : 12f;
        float minChebyshev = fleeMinCellDistance > 0f
            ? fleeMinCellDistance
            : Mathf.CeilToInt(suspicion / pitch) + 1;

        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (grid[x, y] == 0) continue;

                int dist = Mathf.Max(Mathf.Abs(x - ex), Mathf.Abs(y - ey));
                if (dist < minChebyshev) continue;

                candidates.Add(new Vector2Int(x, y));
            }
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning("BattleManager: tidak ada cell valid untuk kabur, player tidak dipindah.");
            return;
        }

        Vector2Int c = candidates[Random.Range(0, candidates.Count)];
        Vector3 worldPos = dungeon.transform.TransformPoint(
            new Vector3(c.x * pitch, dungeon.FloorThickness / 2f, c.y * pitch)
        );

        if (NavMesh.SamplePosition(worldPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            worldPos = hit.position;

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.position = worldPos;
        if (cc != null) cc.enabled = true;

        Debug.Log($"BattleManager: player diteleport ke cell ({c.x}, {c.y}).");
    }

    private void Autofind()
    {
        if (dungeon == null)
            dungeon = FindFirstObjectByType<DungeonGenerator>();

        if (player == null)
        {
            player = FindPlayerTransform();
            if (player == null)
            {
                CharacterController cc = FindFirstObjectByType<CharacterController>();
                if (cc != null) player = cc.transform;
            }
        }

        if (spawner == null)
            spawner = FindFirstObjectByType<EnemySpawner>();
    }

    private Transform FindPlayerTransform()
    {
        GameObject obj = GameObject.Find("Player");
        if (obj == null)
            obj = GameObject.FindGameObjectWithTag("Player");
        return obj != null ? obj.transform : null;
    }

    private bool SceneInBuild(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (Path.GetFileNameWithoutExtension(path) == sceneName)
                return true;
        }
        return false;
    }
}