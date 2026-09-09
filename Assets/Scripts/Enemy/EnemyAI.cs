using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

sealed class DebugReadOnlyAttribute : PropertyAttribute
{
}

public class EnemyAI : MonoBehaviour
{
    [Header("References")]
    public Transform target;
    [Tooltip("Mask raycast yang memblokir LoS.")]
    public LayerMask obstructionMask = -1;

    [Header("Sensing")]
    [Tooltip("Jarak maks. enemy melihat target.")]
    public float sightRange = 20f;
    [Tooltip("Radius area mencurigakan (pemicu Suspect).")]
    public float suspicionRadius = 12f;
    [Tooltip("Kecepatan target yang dianggap sprint.")]
    public float suspicionSpeed = 6f;
    [Tooltip("Tinggi mata untuk raycast LoS.")]
    public float eyeHeight = 1.2f;

    [Header("Patrol")]
    public float patrolWaitTime = 0.5f;
    private readonly float patrolRangeCells = 8f;

    [Header("Suspect")]
    [Tooltip("Durasi curiga sebelum kembali ke Patrol.")]
    public float suspectTimeout = 15f;
    [Tooltip("Interval refresh tujuan Suspect.")]
    public float suspectDestinationRefresh = 2f;

    [Header("Movement")]
    public float patrolSpeed = 2.5f;
    public float chaseSpeed = 7f;
    public float angularSpeed = 360f;
    public float acceleration = 8f;
    public float stoppingDistance = 0.5f;

    [Header("Chase")]
    [Tooltip("Jarak kontak untuk memicu battle.")]
    public float contactRange = 5f;
    [Tooltip("Jeda retry trigger battle.")]
    public float battleRetryInterval = 5f;

    [Header("Collider Setup")]
    [Tooltip("Tinggi CapsuleCollider otomatis.")]
    public float capsuleHeight = 2f;
    public float capsuleRadius = 0.3f;

    [Header("Debug")]
    [Tooltip("Log & gizmo LoS di Scene.")]
    public bool debug = false;
    [Tooltip("Tipe ruangan tempat enemy berdiri.")]
    [SerializeField, DebugReadOnly]
    private string debugCurrentRoom = "None";

    public enum BehaviorState { Patrol, Suspect, Chase }

    private NavMeshAgent agent;
    public DungeonGenerator dungeon;
    private StructureGenerator structure;
    private List<RoomData> trackedRooms;
    private BehaviorState state = BehaviorState.Patrol;
    private Vector3 lastSeenPosition;
    private float patrolWaitEnd;
    private float suspectTimer;
    private float suspectRefreshTimer;
    private bool waitingAtPatrolPoint;
    private bool battleTriggered;
    private float nextBattleAttempt;
    private float debugRoomRefreshTimer;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 2f;
        }

        CapsuleCollider col = GetComponent<CapsuleCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<CapsuleCollider>();
            col.height = capsuleHeight;
            col.radius = capsuleRadius;
            col.center = new Vector3(0f, capsuleHeight * 0.5f, 0f);
        }

        if (obstructionMask.value == 0)
            obstructionMask = -1;
    }

    public void Setup(Transform targetRef)
    {
        target = targetRef;
    }

    public void WarpTo(Vector3 position)
    {
        if (agent == null) return;

        Vector3 finalPos = position;
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 6f, NavMesh.AllAreas))
            finalPos = hit.position;

        agent.Warp(finalPos);
        agent.speed = patrolSpeed;
        agent.angularSpeed = angularSpeed;
        agent.acceleration = acceleration;
        agent.stoppingDistance = contactRange;

        state = BehaviorState.Patrol;
        waitingAtPatrolPoint = false;
        agent.ResetPath();
    }

    public void TriggerSuspicion(Vector3 approxPosition)
    {
        if (!enabled) return;
        EnterSuspect(approxPosition);
    }

    private void Update()
    {
        if (agent == null) return;

        if (!agent.isOnNavMesh)
        {
            WarpTo(transform.position);
            return;
        }

        bool seen = HasLineOfSight();

        RefreshDebugRoom();

        switch (state)
        {
            case BehaviorState.Patrol:
                UpdatePatrol(seen);
                break;
            case BehaviorState.Suspect:
                UpdateSuspect(seen);
                break;
            case BehaviorState.Chase:
                UpdateChase(seen);
                break;
        }

        TryTriggerBattleAtContact();
    }

    private void TryTriggerBattleAtContact()
    {
        if (battleTriggered || target == null || agent == null) return;
        if (Time.time < nextBattleAttempt) return;

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > contactRange)
            return;

        LockInPlace();

        if (!HasLineOfSight())
        {
            if (debug)
                Debug.Log($"[EnemyAI] {name} dalam radius ({dist:F2}m) tapi LOS terhalang -> diam, belum battle.");
            return;
        }

        if (debug)
            Debug.Log($"[EnemyAI] {name} kontak kelihatan, memicu battle...");

        if (BattleManager.Instance != null && BattleManager.Instance.RequestBattle(this))
        {
            battleTriggered = true;
            if (debug) Debug.Log($"[EnemyAI] {name}: battle dimulai.");
        }
        else
        {
            nextBattleAttempt = Time.time + battleRetryInterval;
        }
    }

    private void LockInPlace()
    {
        state = BehaviorState.Chase;
        agent.ResetPath();
        agent.isStopped = true;
    }

    private void UpdatePatrol(bool seen)
    {
        if (seen)
        {
            EnterChase();
            return;
        }

        if (IsSuspiciousActionDetected())
        {
            EnterSuspect(target.position);
            return;
        }

        if (waitingAtPatrolPoint)
        {
            if (Time.time >= patrolWaitEnd)
            {
                waitingAtPatrolPoint = false;
                agent.speed = patrolSpeed;

                Vector3 p = PickPatrolPoint();
                if (float.IsPositiveInfinity(p.x))
                {
                    waitingAtPatrolPoint = true;
                    patrolWaitEnd = Time.time + 0.5f;
                }
                else if (!SetPatrolDestination(p))
                {
                    waitingAtPatrolPoint = true;
                    patrolWaitEnd = Time.time + 1f;
                }
            }
            return;
        }

        if (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            waitingAtPatrolPoint = true;
            patrolWaitEnd = Time.time + patrolWaitTime;
            agent.ResetPath();
        }
    }

    private void UpdateSuspect(bool seen)
    {
        if (seen)
        {
            EnterChase();
            return;
        }

        suspectTimer += Time.deltaTime;
        if (suspectTimer >= suspectTimeout)
        {
            EnterPatrol();
            return;
        }

        suspectRefreshTimer -= Time.deltaTime;
        if (suspectRefreshTimer <= 0f)
        {
            Vector3 basePos = target != null ? target.position : lastSeenPosition;
            Vector3 dest = basePos + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));

            if (NavMesh.SamplePosition(dest, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                dest = hit.position;

            if (SetDestinationSafe(dest))
            {
                agent.speed = patrolSpeed;
                suspectRefreshTimer = suspectDestinationRefresh;
            }
        }
    }

    private void UpdateChase(bool seen)
    {
        if (seen)
        {
            lastSeenPosition = target.position;
            agent.speed = chaseSpeed;
        }

        float dist = target != null
            ? Vector3.Distance(transform.position, target.position)
            : float.MaxValue;

        if (dist <= contactRange)
        {
            LockInPlace();
            return;
        }

        agent.isStopped = false;

        if (seen)
            SetDestinationSafe(target.position);
        else
            EnterSuspect(lastSeenPosition);
    }

    public void ResetToHome()
    {
        battleTriggered = false;
        nextBattleAttempt = 0f;
        if (agent != null)
            agent.isStopped = false;
        EnterPatrol();
    }

    private bool IsSuspiciousActionDetected()
    {
        if (target == null) return false;
        if (Vector3.Distance(transform.position, target.position) > suspicionRadius) return false;
        return PlayerSpeed() >= suspicionSpeed;
    }

    private float PlayerSpeed()
    {
        if (target == null) return 0f;

        CharacterController cc = target.GetComponent<CharacterController>();
        if (cc != null)
            return new Vector3(cc.velocity.x, 0f, cc.velocity.z).magnitude;

        return 0f;
    }

    private bool HasLineOfSight()
    {
        if (target == null) return false;

        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3 dest = target.position + Vector3.up * eyeHeight;
        Vector3 dir = dest - origin;
        float dist = dir.magnitude;

        if (dist > sightRange) return false;
        if (dist < 0.01f) return true;

        RaycastHit[] hits = Physics.RaycastAll(origin, dir.normalized, dist, obstructionMask);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Transform h = hits[i].collider.transform;

            if (h == transform || h.IsChildOf(transform))
                continue;

            return IsPlayerHit(hits[i]);
        }

        return true;
    }

    private bool IsPlayerHit(RaycastHit hit)
    {
        Transform p = target;
        if (p == null) return false;

        Transform h = hit.collider.transform;
        return h == p || h.IsChildOf(p) || p.IsChildOf(h);
    }

    private Vector3 PickPatrolPoint()
    {
        EnsureDungeon();
        EnsureStructure();
        EnsureRooms();

        if (dungeon == null || trackedRooms == null || trackedRooms.Count == 0)
            return Vector3.positiveInfinity;

        Vector2Int selfCell = WorldToCell(transform.position);
        RoomData selfRoom = FindRoomAtCell(selfCell);
        RoomData chosen = ChoosePatrolRoom(selfCell, selfRoom);
        if (chosen == null)
            return Vector3.positiveInfinity;

        float pitch = dungeon.TileSize + dungeon.TileGap;

        for (int i = 0; i < 8; i++)
        {
            Vector2Int cell = chosen.tiles[Random.Range(0, chosen.tiles.Count)];
            Vector3 world = dungeon.transform.TransformPoint(
                new Vector3(cell.x * pitch, dungeon.FloorThickness * 0.5f, cell.y * pitch)
            );

            if (NavMesh.SamplePosition(world, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                return hit.position;
        }

        return Vector3.positiveInfinity;
    }

    private void EnsureDungeon()
    {
        if (dungeon == null)
            dungeon = FindFirstObjectByType<DungeonGenerator>();
    }

    private void EnsureStructure()
    {
        if (structure != null) return;

        if (dungeon != null)
            structure = dungeon.GetComponent<StructureGenerator>();

        if (structure == null)
            structure = FindFirstObjectByType<StructureGenerator>();
    }

    private void EnsureRooms()
    {
        if (trackedRooms != null) return;

        List<RoomData> rooms = dungeon != null && dungeon.Grid != null
            ? dungeon.GetRooms()
            : new List<RoomData>();

        if (rooms.Count > 0)
            trackedRooms = rooms;
    }

    private RoomData ChoosePatrolRoom(Vector2Int selfCell, RoomData selfRoom)
    {
        List<RoomData> near = new List<RoomData>();
        List<RoomData> any = new List<RoomData>();

        for (int i = 0; i < trackedRooms.Count; i++)
        {
            RoomData room = trackedRooms[i];
            if (room == selfRoom) continue;
            if (IsForbiddenRoom(room)) continue;

            any.Add(room);

            int dist = Mathf.Max(
                Mathf.Abs(room.centerGrid.x - selfCell.x),
                Mathf.Abs(room.centerGrid.y - selfCell.y)
            );

            if (dist <= patrolRangeCells)
                near.Add(room);
        }

        List<RoomData> pool = near.Count > 0 ? near : any;
        if (pool.Count == 0) return null;

        return pool[Random.Range(0, pool.Count)];
    }

    private Vector2Int WorldToCell(Vector3 worldPos)
    {
        Vector3 local = dungeon.transform.InverseTransformPoint(worldPos);
        float pitch = dungeon.TileSize + dungeon.TileGap;
        return new Vector2Int(Mathf.RoundToInt(local.x / pitch), Mathf.RoundToInt(local.z / pitch));
    }

    private RoomData FindRoomAtCell(Vector2Int cell)
    {
        for (int i = 0; i < trackedRooms.Count; i++)
        {
            if (RoomContainsCell(trackedRooms[i], cell))
                return trackedRooms[i];
        }

        return null;
    }

    private bool IsForbiddenRoom(RoomData room)
    {
        if (structure == null) return false;

        return room == structure.SpawnRoom
            || room == structure.EndRoom
            || room == structure.ShopRoom;
    }

    private string DescribeRoom(RoomData room)
    {
        if (room == null || structure == null) return "Corridor";
        if (room == structure.SpawnRoom) return "Spawn";
        if (room == structure.EndRoom) return "End";
        if (room == structure.ShopRoom) return "Shop";
        return "Room ";
    }

    private void RefreshDebugRoom()
    {
        debugRoomRefreshTimer -= Time.deltaTime;
        if (debugRoomRefreshTimer > 0f) return;

        debugRoomRefreshTimer = 0.2f;
        debugCurrentRoom = "None";
        if (dungeon == null || trackedRooms == null || trackedRooms.Count == 0) return;
        debugCurrentRoom = DescribeRoom(FindRoomAtCell(WorldToCell(transform.position)));
    }

    private bool RoomContainsCell(RoomData room, Vector2Int cell)
    {
        return room != null && room.tiles != null && room.tiles.Contains(cell);
    }

    private bool SetPatrolDestination(Vector3 destination)
    {
        if (agent == null || !agent.isOnNavMesh) return false;

        NavMeshPath path = new NavMeshPath();
        if (agent.CalculatePath(destination, path) && path.status != NavMeshPathStatus.PathInvalid)
        {
            agent.SetDestination(destination);
            return true;
        }

        return false;
    }

    private bool SetDestinationSafe(Vector3 destination)
    {
        if (agent == null || !agent.isOnNavMesh) return false;

        NavMeshPath path = new NavMeshPath();
        if (agent.CalculatePath(destination, path) && path.status == NavMeshPathStatus.PathComplete)
        {
            agent.SetDestination(destination);
            return true;
        }

        return false;
    }

    private void EnterChase()
    {
        state = BehaviorState.Chase;
        lastSeenPosition = target != null ? target.position : transform.position;
        agent.speed = chaseSpeed;
    }

    private void EnterSuspect(Vector3 approxPosition)
    {
        state = BehaviorState.Suspect;
        lastSeenPosition = approxPosition;
        suspectTimer = 0f;
        suspectRefreshTimer = 0f;
        agent.speed = patrolSpeed;
        agent.isStopped = false;
    }

    private void EnterPatrol()
    {
        state = BehaviorState.Patrol;
        waitingAtPatrolPoint = false;
        agent.speed = patrolSpeed;
        agent.isStopped = false;
        agent.ResetPath();
    }

    private void OnDrawGizmos()
    {
        if (!debug || target == null) return;

        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3 dest = target.position + Vector3.up * eyeHeight;

        Gizmos.color = HasLineOfSight() ? Color.green : Color.red;
        Gizmos.DrawLine(origin, dest);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, contactRange);
    }
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(DebugReadOnlyAttribute))]
public class DebugReadOnlyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginDisabledGroup(true);
        EditorGUI.PropertyField(position, property, label);
        EditorGUI.EndDisabledGroup();
    }
}
#endif