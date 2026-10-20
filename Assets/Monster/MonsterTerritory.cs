using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The monster's area. The BoxCollider (a trigger) marks the region. At start it bakes a NavMesh for just
/// this region, sized for the small monster, so the monster can walk around obstacles but can never leave.
/// The player being inside this box is what makes the monster chase.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class MonsterTerritory : MonoBehaviour
{
    [Header("Monster size (world units)")]
    public float agentRadius = 0.03f;
    public float agentHeight = 0.12f;
    [Tooltip("Highest step the monster can climb. Keep below the small push cube (0.0625).")]
    public float agentClimb = 0.03f;
    public float agentSlope = 40f;
    [Tooltip("NavMesh detail. Smaller = more accurate around obstacles, slower to build.")]
    public float voxelSize = 0.01f;

    // Runtime agent type ids can be any int (including negative), so track "built" separately.
    public int AgentTypeId { get; private set; }
    public bool IsBuilt { get; private set; }
    public Bounds Area => _box.bounds;

    BoxCollider _box;
    NavMeshDataInstance _instance;

    void Awake()
    {
        _box = GetComponent<BoxCollider>();
        _box.isTrigger = true;
        Build();
    }

    void OnDestroy()
    {
        if (_instance.valid) _instance.Remove();
    }

    public void Build()
    {
        if (_box == null) _box = GetComponent<BoxCollider>();
        var settings = NavMesh.CreateSettings();
        settings.agentRadius = agentRadius;
        settings.agentHeight = agentHeight;
        settings.agentClimb = agentClimb;
        settings.agentSlope = agentSlope;
        settings.overrideVoxelSize = true;
        settings.voxelSize = voxelSize;
        settings.minRegionArea = agentRadius * agentRadius * 4f;

        Bounds b = _box.bounds;
        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(b, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
        // Only static geometry: skip triggers, moving physics objects (handled by NavMeshObstacle) and characters.
        sources.RemoveAll(s =>
        {
            var c = s.component as Collider;
            return c != null && (c.isTrigger || c.attachedRigidbody != null || c is CharacterController);
        });

        var data = NavMeshBuilder.BuildNavMeshData(settings, sources, b, Vector3.zero, Quaternion.identity);
        if (data == null) { Debug.LogWarning("MonsterTerritory: NavMesh build failed."); return; }
        _instance = NavMesh.AddNavMeshData(data);
        AgentTypeId = settings.agentTypeID;
        IsBuilt = true;

        // Moving boxes etc. inside the area cut holes in the NavMesh while they sit still.
        if (!Application.isPlaying) return;
        foreach (var rb in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
        {
            if (rb.GetComponent<NavMeshObstacle>() != null || rb.GetComponent<MonsterAI>() != null) continue;
            if (!b.Contains(rb.position)) continue;
            var col = rb.GetComponent<BoxCollider>();
            if (col == null) continue;
            var obs = rb.gameObject.AddComponent<NavMeshObstacle>();
            obs.shape = NavMeshObstacleShape.Box;
            obs.center = col.center;
            obs.size = col.size;
            obs.carving = true;
            obs.carveOnlyStationary = true;
        }
    }

    public bool Contains(Vector3 worldPos)
    {
        Bounds b = _box != null ? _box.bounds : GetComponent<BoxCollider>().bounds;
        return worldPos.x >= b.min.x && worldPos.x <= b.max.x &&
               worldPos.z >= b.min.z && worldPos.z <= b.max.z &&
               worldPos.y >= b.min.y && worldPos.y <= b.max.y;
    }

    void OnDrawGizmos()
    {
        var bc = GetComponent<BoxCollider>();
        if (bc == null) return;
        Gizmos.color = new Color(1f, 0.15f, 0.1f, 0.6f);
        Gizmos.DrawWireCube(bc.bounds.center, bc.bounds.size);
    }
}
