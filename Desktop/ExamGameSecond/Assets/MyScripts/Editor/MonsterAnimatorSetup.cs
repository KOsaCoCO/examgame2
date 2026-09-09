using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using NTGD124;

// One-time batch tool: wires every kept monster package's AnimatorController
// (MonsterNN_AC.controller) to react to real movement instead of auto-looping Idle<->Walk on an
// unconditioned exit-time transition (the raw demo state as every package ships it - see
// GameplayRoadmap.md Section 6's Animator wiring blind spot). Adds an "IsMoving" bool parameter
// (Mobai.IsMovingAnimatorParam - Mobai.Update() drives it every frame from the NavMeshAgent's
// actual velocity, regardless of which script is currently setting that agent's destination) and
// makes the existing Idle->Walk / Walk->Idle transitions condition-driven instead. Also adds an
// Animator to each kept prefab's wrapper root if it doesn't already have one there (most of them
// don't - confirmed by this tool's own first run - a few already do, sibling to
// Mobai/MobHealth/NavMeshAgent/Collider, e.g. Monster16_01.prefab) and (re)assigns its
// runtimeAnimatorController to that package's own controller. Deliberately separate from
// MonsterRosterSetup.cs (Mobai/DifficultyLevel) - this tool only touches Animator/
// AnimatorController. Idempotent - safe to re-run.
public static class MonsterAnimatorSetup
{
    private const string PackageRoot = "Assets/FreeAssets/Stylized3DMonster";

    // The full kept roster (Easy 07/11/15, Medium 16/20/24, Hard 27/31/35 - see
    // MonsterRosterSetup.cs) - animator wiring applies the same way regardless of difficulty tier.
    private static readonly int[] KeptNumbers = { 7, 11, 15, 16, 20, 24, 27, 31, 35 };

    [MenuItem("Tools/Monster Roster/Wire Up Animators")]
    public static void WireUpAnimators()
    {
        int controllersWired = 0;
        int prefabsAssigned = 0;
        int missing = 0;

        foreach (int number in KeptNumbers)
        {
            string folder = $"{PackageRoot}/Monster{number:D2}_FreeTrial";
            string controllerPath = $"{folder}/Anim/Monster{number:D2}_AC.controller";

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                Debug.LogWarning($"Monster animator setup: controller not found - {controllerPath}");
                missing++;
                continue;
            }

            if (WireController(controller)) controllersWired++;
            prefabsAssigned += AssignControllerToPrefabs(folder, controller);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Monster animator wiring done - {controllersWired} controller(s) newly wired with IsMoving conditions, {prefabsAssigned} prefab(s) got their Animator assigned/corrected, {missing} controller(s) not found.");
    }

    // Separate step, run after WireUpAnimators (and after each package's .fbx has been
    // reimported with Avatar Setup = "Create From This Model" instead of "No Avatar" - every
    // kept package shipped with avatarSetup: 0, which is the actual reason mobs move but never
    // animate: a Generic-rig Animator with no Avatar assigned can't play any Mecanim clip at
    // all, regardless of how correctly its Controller/parameters/transitions are wired).
    // WireUpAnimators/AssignControllerToPrefabs never touched Animator.avatar, only
    // runtimeAnimatorController, so every prefab's Avatar has been sitting unassigned this
    // whole time. Finds each package's own generated Avatar sub-asset (inside its .fbx, once
    // reimported) and assigns it to every kept prefab variant's Animator, mirroring
    // AssignControllerToPrefabs' own load/modify/save-as-prefab-asset shape.
    [MenuItem("Tools/Monster Roster/Assign Avatars")]
    public static void AssignAvatars()
    {
        int avatarsAssigned = 0;
        int missing = 0;

        foreach (int number in KeptNumbers)
        {
            string folder = $"{PackageRoot}/Monster{number:D2}_FreeTrial";
            string fbxPath = Directory.GetFiles(folder, "*.fbx").FirstOrDefault()?.Replace('\\', '/');
            if (fbxPath == null)
            {
                Debug.LogWarning($"Monster animator setup: no .fbx found in '{folder}'.");
                missing++;
                continue;
            }

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null)
            {
                Debug.LogWarning($"Monster animator setup: '{fbxPath}' has no Avatar sub-asset yet - select it, set Rig > Avatar Definition to 'Create From This Model', Apply, then re-run this.");
                missing++;
                continue;
            }

            avatarsAssigned += AssignAvatarToPrefabs($"{folder}/Prefab", avatar);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Monster animator avatar assignment done - {avatarsAssigned} prefab(s) got their Animator.avatar set, {missing} package(s) skipped (fbx or Avatar not found - see warnings above).");
    }

    // Same load/modify/save-as-prefab-asset shape as AssignControllerToPrefabs, just setting
    // Animator.avatar instead of runtimeAnimatorController.
    private static int AssignAvatarToPrefabs(string prefabFolder, Avatar avatar)
    {
        if (!Directory.Exists(prefabFolder)) return 0;

        int assigned = 0;
        foreach (string prefabPath in Directory.GetFiles(prefabFolder, "*.prefab"))
        {
            string assetPath = prefabPath.Replace('\\', '/');
            GameObject root = PrefabUtility.LoadPrefabContents(assetPath);

            try
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null) continue;

                if (animator.avatar != avatar)
                {
                    animator.avatar = avatar;
                    PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                    assigned++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return assigned;
    }

    // Adds the IsMoving bool parameter (if missing) and makes the existing Idle<->Walk
    // transitions condition-driven instead of exit-time-driven. Returns true if anything changed.
    private static bool WireController(AnimatorController controller)
    {
        bool changed = false;

        if (!controller.parameters.Any(p => p.name == Mobai.IsMovingAnimatorParam))
        {
            controller.AddParameter(Mobai.IsMovingAnimatorParam, AnimatorControllerParameterType.Bool);
            changed = true;
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = FindStateContaining(stateMachine, "Idle");
        AnimatorState walkState = FindStateContaining(stateMachine, "Walk");

        if (idleState == null || walkState == null)
        {
            Debug.LogWarning($"Monster animator setup: could not find both an Idle and a Walk state in '{controller.name}' - skipping condition wiring.");
            return changed;
        }

        if (WireTransition(idleState, walkState, AnimatorConditionMode.If)) changed = true;
        if (WireTransition(walkState, idleState, AnimatorConditionMode.IfNot)) changed = true;

        if (changed) EditorUtility.SetDirty(controller);
        return changed;
    }

    private static AnimatorState FindStateContaining(AnimatorStateMachine stateMachine, string nameContains)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            if (childState.state.name.Contains(nameContains)) return childState.state;
        }
        return null;
    }

    // Finds the existing transition between the two states (or creates one if missing), strips
    // its exit-time auto-loop, and makes sure it carries exactly one IsMoving condition in the
    // given mode. Returns true if anything changed.
    private static bool WireTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode)
    {
        bool changed = false;

        AnimatorStateTransition transition = from.transitions.FirstOrDefault(t => t.destinationState == to);
        if (transition == null)
        {
            transition = from.AddTransition(to);
            changed = true;
        }

        if (transition.hasExitTime)
        {
            transition.hasExitTime = false;
            changed = true;
        }

        bool hasCorrectCondition = transition.conditions.Any(c => c.parameter == Mobai.IsMovingAnimatorParam && c.mode == mode);
        if (!hasCorrectCondition)
        {
            // Drop any stale IsMoving condition in the wrong mode before adding the right one
            foreach (AnimatorCondition stale in transition.conditions.Where(c => c.parameter == Mobai.IsMovingAnimatorParam))
            {
                transition.RemoveCondition(stale);
            }
            transition.AddCondition(mode, 0, Mobai.IsMovingAnimatorParam);
            changed = true;
        }

        return changed;
    }

    // Assigns this package's controller to every kept prefab variant's Animator - found anywhere
    // in the hierarchy if one already exists (a few prefabs already have one hand-added directly
    // on the wrapper root, sibling to Mobai/MobHealth/NavMeshAgent/Collider - confirmed by
    // inspecting Monster16_01.prefab), added on that same wrapper root to match if none exists
    // yet (most prefabs, confirmed by the first run of this tool). Returns how many prefabs were
    // newly given an Animator or had their controller corrected.
    private static int AssignControllerToPrefabs(string packageFolder, AnimatorController controller)
    {
        string prefabFolder = $"{packageFolder}/Prefab";
        if (!Directory.Exists(prefabFolder)) return 0;

        int assigned = 0;
        foreach (string prefabPath in Directory.GetFiles(prefabFolder, "*.prefab"))
        {
            string assetPath = prefabPath.Replace('\\', '/');
            GameObject root = PrefabUtility.LoadPrefabContents(assetPath);

            try
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null) animator = root.AddComponent<Animator>();

                if (animator.runtimeAnimatorController != controller)
                {
                    animator.runtimeAnimatorController = controller;
                    PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                    assigned++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return assigned;
    }
}

// Implementation Steps:
// 1. In Unity, use the menu Tools > Monster Roster > Wire Up Animators. Check the Console for the
//    summary line.
// 2. This only touches each package's AnimatorController (adds an "IsMoving" bool parameter,
//    makes the Idle<->Walk transitions condition-driven instead of exit-time-driven) and each
//    kept prefab's Animator.runtimeAnimatorController (assigned if missing/wrong). It does NOT
//    touch Mobai/MobHealth/Collider/NavMeshAgent - that's MonsterRosterSetup.cs / whatever you've
//    already done by hand.
// 3. Mobai.cs drives the IsMoving parameter itself every frame (Update() -> UpdateAnimator()),
//    based on the NavMeshAgent's actual velocity - nothing else to wire at runtime, on the night
//    boss (Monster35) included, since NightBossAi shares the same NavMeshAgent Mobai is watching.
// 4. Safe to re-run - already-correct parameters/conditions/assignments are detected and skipped.
// 5. Every kept package's .fbx shipped with Rig > Avatar Definition set to "No Avatar" - the
//    actual reason mobs move (NavMeshAgent driving the root transform fine) but never visibly
//    animate: a Generic-rig Animator with no Avatar assigned can't play any Mecanim clip at all,
//    no matter how correctly its Controller/parameters/transitions/clips are wired. For each
//    Monster##_FreeTrial.fbx: select it, Rig tab, set Avatar Definition to "Create From This
//    Model", Apply. Once every package's .fbx has been reimported that way, run
//    Tools > Monster Roster > Assign Avatars to wire the newly-generated Avatar into every kept
//    prefab's Animator (mirrors step 1's controller wiring) - check the Console for which
//    packages it skipped if a .fbx wasn't reimported yet.
