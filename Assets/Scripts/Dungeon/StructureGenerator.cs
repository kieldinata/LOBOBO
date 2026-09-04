using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class StructureGenerator : MonoBehaviour
{
    public DungeonGenerator dungeon;

    public RoomData SpawnRoom;
    public RoomData EndRoom;
    public RoomData ShopRoom;

    public Vector3 SpawnPosition => SpawnRoom != null ? SpawnRoom.centerWorld : Vector3.zero;
    public Vector3 EndPosition => EndRoom != null ? EndRoom.centerWorld : Vector3.zero;
    public Vector3 ShopPosition => ShopRoom != null ? ShopRoom.centerWorld : Vector3.zero;

    [Header("Lamp Settings")]
    public bool autoPlaceLights = true;
    public float lightRange = 15f;
    public float lightIntensity = 75f;
    public Color lightColor = Color.white;
    [Tooltip("Jarak lampu di bawah ceiling.")]
    public float lightYFromCeiling = 1f;

    void Start()
    {
        if (dungeon == null)
        {
            Debug.LogWarning("StructureGenerator: dungeon reference belum di-assign di inspector.");
            return;
        }

        if (!dungeon.IsGenerated)
        {
            dungeon.GenerateDungeon();
        }

        List<RoomData> rooms = dungeon.GetRooms();
        if (rooms.Count < 3)
        {
            Debug.LogWarning("StructureGenerator: ruangan yang tersedia kurang dari 3, tidak cukup untuk spawn/end/shop.");
            return;
        }

        SpawnRoom = rooms[Random.Range(0, rooms.Count)];
        EndRoom = FindFarthestRoom(rooms, SpawnRoom);
        ShopRoom = FindFarthestRoom(rooms, SpawnRoom, EndRoom);

        PlaceLights();
    }

    private void PlaceLights()
    {
        if (!autoPlaceLights) return;
        PlaceLight(SpawnRoom, "Spawn");
        PlaceLight(EndRoom, "End");
        PlaceLight(ShopRoom, "Shop");
    }

    private void PlaceLight(RoomData room, string label)
    {
        if (room == null) return;

        float lightY = dungeon.Origin.y + dungeon.WallHeight + dungeon.CeilingThickness - lightYFromCeiling;
        float centerY = dungeon.FloorThickness / 2f;

        GameObject lightObj = new GameObject("Light_" + label);
        lightObj.transform.SetParent(transform, false);
        lightObj.transform.position = new Vector3(room.centerWorld.x, lightY, room.centerWorld.z);

        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = lightRange;
        light.intensity = lightIntensity;
        light.color = lightColor;
        light.shadows = LightShadows.Soft;
    }

    private RoomData FindFarthestRoom(List<RoomData> rooms, params RoomData[] excluded)
    {
        RoomData best = null;
        float bestDist = -1f;

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomData candidate = rooms[i];
            if (System.Array.IndexOf(excluded, candidate) >= 0) continue;

            float total = 0f;
            for (int j = 0; j < excluded.Length; j++)
            {
                if (excluded[j] == null) continue;
                total += Vector3.Distance(candidate.centerWorld, excluded[j].centerWorld);
            }

            if (total > bestDist)
            {
                bestDist = total;
                best = candidate;
            }
        }

        return best;
    }
}
