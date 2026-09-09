using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

// One-time (but safely re-runnable) bake tool: the scene ships with exactly one baked
// NavMeshSurface (agent type "Monster07+20+31"), so every other monster species' NavMeshAgent -
// each pointed at its own dedicated agent type in ProjectSettings/NavMeshAreas.asset, presumably
// so each creature's real body size gets its own erosion/clearance margin - has never had any
// NavMesh to actually stand on. NavMeshAgent.Warp silently fails for all of them ("Failed to
// create agent because it is not close enough to the NavMesh"), which is both why most monster
// species never successfully spawn (MobSpawner destroys the mis-warped clone) and why the night
// boss (Monster35, agent type "Monster35Boss") stands frozen forever once spawned - its own
// SafeSetDestination guard (see NightBossAi.cs/Mobai.cs) silently no-ops on an agent that was
// never placed on any NavMesh, so it never moves and never leaves its Idle animation.
//
// This bakes one dedicated NavMeshSurface per missing agent type, covering the whole scene
// (CollectObjects.All, matching the one surface that already works) - not a shared/reused
// surface - so each species keeps the per-species clearance its agent type was actually set up
// for. Idempotent: re-running it reuses an existing surface for a given agent type and
// re-bakes/re-saves its NavMeshData in place instead of creating duplicates.
public static class MonsterNavMeshBaker
{
    private const string ContainerName = "Monster NavMesh Surfaces";
    private const string NavMeshAssetFolder = "Assets/Scenes/SampleScene";
    private const string TerrainTag = "GameTerrain";

    private struct AgentTypeTarget
    {
        public int AgentTypeID;
        public string Label;
    }

    // Every agent type referenced by a monster NavMeshAgent that has no baked surface yet,
    // per ProjectSettings/NavMeshAreas.asset's m_SettingNames. "-1372625422" (Monster07+20+31)
    // is deliberately excluded - it's the one already baked in the scene.
    private static readonly AgentTypeTarget[] Targets =
    {
        new AgentTypeTarget { AgentTypeID = 0, Label = "Humanoid" }, // Monster16_01 only - its _02/_04 siblings use the next row; giving 0 its own bake rather than editing the prefab
        new AgentTypeTarget { AgentTypeID = -334000983, Label = "Monster11" },
        new AgentTypeTarget { AgentTypeID = 1479372276, Label = "Monster15" },
        new AgentTypeTarget { AgentTypeID = -1923039037, Label = "Monster16" },
        new AgentTypeTarget { AgentTypeID = 287145453, Label = "Monster24" },
        new AgentTypeTarget { AgentTypeID = 658490984, Label = "Monster27" },
        new AgentTypeTarget { AgentTypeID = 65107623, Label = "Monster35Boss" },
    };

    [MenuItem("Tools/World/Bake Monster NavMeshes")]
    public static void BakeAll()
    {
        if (GameObject.FindGameObjectWithTag(TerrainTag) == null)
        {
            Debug.LogError($"[MonsterNavMeshBaker] No GameObject tagged '{TerrainTag}' found in the open scene - open SampleScene first.");
            return;
        }

        GameObject container = GameObject.Find(ContainerName);
        if (container == null) container = new GameObject(ContainerName);

        int baked = 0;
        foreach (AgentTypeTarget target in Targets)
        {
            NavMeshSurface surface = FindSurfaceForAgentType(target.AgentTypeID);
            if (surface == null) surface = container.AddComponent<NavMeshSurface>();

            surface.agentTypeID = target.AgentTypeID;
            surface.collectObjects = CollectObjects.All;
            surface.BuildNavMesh();

            SaveNavMeshDataAsset(surface, target.Label);
            baked++;
        }

        EditorUtility.SetDirty(container);
        EditorSceneManager.MarkSceneDirty(container.scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[MonsterNavMeshBaker] Baked {baked} NavMesh surface(s) under '{ContainerName}'. Every monster species and the boss now have real NavMesh data for their own agent type.");
    }

    private static NavMeshSurface FindSurfaceForAgentType(int agentTypeID)
    {
        foreach (NavMeshSurface surface in Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None))
        {
            if (surface.agentTypeID == agentTypeID) return surface;
        }
        return null;
    }

    // BuildNavMesh() only produces an in-memory NavMeshData - without this it would vanish the
    // moment the scene closes, unlike the one surface that's already a real saved asset. Deletes
    // and recreates the file on a re-run so re-baking (e.g. after terrain changes) stays safe to
    // repeat rather than erroring on an asset that already exists at that path.
    private static void SaveNavMeshDataAsset(NavMeshSurface surface, string label)
    {
        if (surface.navMeshData == null) return;

        string path = $"{NavMeshAssetFolder}/NavMesh-{label}.asset";
        if (AssetDatabase.LoadAssetAtPath<NavMeshData>(path) != null) AssetDatabase.DeleteAsset(path);

        AssetDatabase.CreateAsset(surface.navMeshData, path);
        EditorUtility.SetDirty(surface);
    }
}

// Implementation Steps:
// 1. Open SampleScene.unity in the Editor, then run Tools > World > Bake Monster NavMeshes once.
//    It bakes the whole terrain 7 times (once per missing agent type) so it may take a little
//    while - watch the Console for the final summary log.
// 2. Save the scene afterward (it's marked dirty automatically, but Unity won't auto-save it).
// 3. Re-test: every monster species should now spawn and move without "Failed to create agent"/
//    "warped but is not on the NavMesh" warnings, and the night boss should walk, animate, attack,
//    and siege base slots/walls normally once triggered.
// 4. Safe to re-run any time the terrain/navigation geometry changes - it re-bakes and
//    overwrites each NavMesh-*.asset in place rather than duplicating surfaces.
