using System.IO;
using UnityEditor;
using UnityEngine;
using NTGD124;

// One-time batch tool: adds Mobai (with the correct DifficultyLevel) to every KEPT monster
// prefab from the Stylized3DMonster package, per the Easy/Medium/Hard roster decided in
// GameplayRoadmap.md's Combat section (Monster07-35, halved per tier, Monster35 doubling as
// the tower-defense final boss). Deliberately does NOT touch MobHealth (needs a real Collider
// per monster, deferred to the same later pass as animations/NavMeshAgent tuning) or
// MobSpawner's prefab lists (scene integration, also deferred). Idempotent - safe to re-run.
public static class MonsterRosterSetup
{
    private const string PackageRoot = "Assets/FreeAssets/Stylized3DMonster";
    private const int FinalBossNumber = 35;

    // Kept after halving, then trimmed by two more per tier - same alternating rule applied
    // twice (see GameplayRoadmap.md Section 6). Hard's alternation deliberately runs top-down
    // from 35 so the final boss survives both cuts.
    private static readonly int[] EasyNumbers = { 7, 11, 15 };
    private static readonly int[] MediumNumbers = { 16, 20, 24 };
    private static readonly int[] HardNumbers = { 27, 31, 35 };

    [MenuItem("Tools/Monster Roster/Wire Up Kept Prefabs")]
    public static void WireUpKeptPrefabs()
    {
        int wired = 0;
        int alreadyPresent = 0;
        int foldersMissing = 0;

        WireTier(EasyNumbers, MobDifficultyLevel.Easy, ref wired, ref alreadyPresent, ref foldersMissing);
        WireTier(MediumNumbers, MobDifficultyLevel.Medium, ref wired, ref alreadyPresent, ref foldersMissing);
        WireTier(HardNumbers, MobDifficultyLevel.Hard, ref wired, ref alreadyPresent, ref foldersMissing);

        AssetDatabase.SaveAssets();
        Debug.Log($"Monster roster wiring done - {wired} prefab(s) newly got Mobai, {alreadyPresent} already had it (DifficultyLevel re-confirmed), {foldersMissing} folder(s) not found.");
    }

    private static void WireTier(int[] numbers, MobDifficultyLevel difficulty, ref int wired, ref int alreadyPresent, ref int foldersMissing)
    {
        foreach (int number in numbers)
        {
            string folder = $"{PackageRoot}/Monster{number:D2}_FreeTrial/Prefab";
            if (!Directory.Exists(folder))
            {
                Debug.LogWarning($"Monster roster: folder not found - {folder}");
                foldersMissing++;
                continue;
            }

            bool isFinalBoss = number == FinalBossNumber;
            foreach (string prefabPath in Directory.GetFiles(folder, "*.prefab"))
            {
                string assetPath = prefabPath.Replace('\\', '/');
                if (WirePrefab(assetPath, difficulty, isFinalBoss)) wired++;
                else alreadyPresent++;
            }
        }
    }

    // Returns true if Mobai was newly added, false if it already existed (DifficultyLevel is
    // still re-applied either way, in case a prior run or manual edit left it mismatched).
    private static bool WirePrefab(string assetPath, MobDifficultyLevel difficulty, bool isFinalBoss)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
        bool wasAlreadyPresent;

        try
        {
            Mobai mobai = root.GetComponent<Mobai>();
            wasAlreadyPresent = mobai != null;
            if (!wasAlreadyPresent)
            {
                mobai = root.AddComponent<Mobai>(); // also auto-adds a default, untuned NavMeshAgent (Mobai's RequireComponent)
            }

            mobai.DifficultyLevel = difficulty;
            if (isFinalBoss) Debug.Log($"Monster roster: '{assetPath}' flagged as the tower-defense final boss.");

            PrefabUtility.SaveAsPrefabAsset(root, assetPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return !wasAlreadyPresent;
    }
}

// Implementation Steps:
// 1. In Unity, use the menu Tools > Monster Roster > Wire Up Kept Prefabs. Check the Console
//    for the summary line and the final-boss confirmation log.
// 2. This only adds Mobai (DifficultyLevel set correctly) to the 9 kept packages' prefabs
//    (27 prefab assets total: Monster07/11/15, 16/20/24, 27/31/35, x3 color variants each).
//    It's safe to re-run - re-running just re-confirms DifficultyLevel and skips prefabs that
//    already have Mobai.
// 3. Deliberately NOT done here (per this session's staging): MobHealth (needs a real Collider
//    per monster), NavMeshAgent tuning (a default one now exists on each kept prefab via
//    Mobai's RequireComponent, but completely untuned), Animator/animation wiring, and dragging
//    any of these into MobSpawner's EasyMobPrefabs/MediumMobPrefabs/HardMobPrefabs lists or the
//    scene. All of that is the next pass.
// 4. Dropped packages (08/09/10/12/13/14, 17/18/19/21/22/23/25, 26/28/29/30/32/33/34) and
//    Monster36_FreeTrial are untouched on disk - still in the project, just outside the roster.
