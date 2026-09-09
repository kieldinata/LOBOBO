using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

public class DungeonNavMeshBake : MonoBehaviour
{
    public DungeonGenerator dungeon;

    [Header("Bake")]
    [Tooltip("Agent type yang dipakai untuk bake. -1 = mengikuti Project Settings > Navigation (default agent).")]
    public int agentTypeID = 0;

    private NavMeshSurface surface;

    public void EnsureBaked()
    {
        if (surface == null)
        {
            Transform host = dungeon != null ? dungeon.transform : transform;

            surface = host.GetComponent<NavMeshSurface>();
            if (surface == null)
                surface = host.gameObject.AddComponent<NavMeshSurface>();

            ConfigureSurface();
        }

        Build();
    }

    private void ConfigureSurface()
    {
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = -1;
        surface.agentTypeID = agentTypeID;
        surface.defaultArea = 0;

        EnsureCollider("Dungeon_Floor");
        MarkNotWalkable("Dungeon_Walls");
        MarkNotWalkable("Dungeon_Ceiling");
    }

    private void EnsureCollider(string childName)
    {
        if (dungeon == null) return;

        Transform child = dungeon.transform.Find(childName);
        if (child == null) return;

        if (child.GetComponent<MeshCollider>() == null)
            child.gameObject.AddComponent<MeshCollider>();
    }

    private void MarkNotWalkable(string childName)
    {
        if (dungeon == null) return;

        Transform child = dungeon.transform.Find(childName);
        if (child == null) return;

        MeshCollider whoseCollider = child.GetComponent<MeshCollider>();
        if (whoseCollider == null)
            child.gameObject.AddComponent<MeshCollider>();

        NavMeshModifier mod = child.GetComponent<NavMeshModifier>();
        if (mod == null)
            mod = child.gameObject.AddComponent<NavMeshModifier>();

        mod.overrideArea = true;
        mod.area = 1; // Not Walkable
    }

    public void Build()
    {
        if (surface != null)
            surface.BuildNavMesh();
    }
}