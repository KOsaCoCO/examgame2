using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace NTGD124
{
    // South/West/North/East, fixed the same way for every slot in the 3x3 grid - matches
    // WallExample's wall1=South, wall2=West, wall3=North, wall4=East (derived from its
    // corner names: cornerWalls1+2/2+3/3+4/4+1 - each pair adjacent, confirming this cycle).
    public enum WallDirection { South, West, North, East }

    // Sits on one wall or corner segment of a spawned "wall kit" (see BaseSlotWallSpawner).
    // Fences in territory you've bought - INVISIBLE until its own slot is bought (not the
    // other way around), then visible for as long as at least one of its Directions either
    // falls off the 3x3 grid (a permanent map-edge side) or points at a still-locked neighbor.
    // Once its own slot AND every REAL neighbor in Directions are all bought, it hides again -
    // two owned plots don't need a fence between them. A straight wall segment has one
    // Direction; a corner segment has two (e.g. the SW corner uses [South, West]).
    public class BaseSlotWall : MonoBehaviour
    {
        [TextArea(3, 10)]
        [SerializeField] private string _componentDescription = "Invisible until its own slot is bought - becomes visible right when that happens (the reveal animation plays here), fencing in the newly claimed territory. Hides again only once every REAL neighbor slot in Directions is ALSO bought (two owned plots don't need a fence between them). A Direction with no real neighbor (a map-edge/perimeter side) makes this piece permanent once revealed - it can never hide again. Directions is set once on the wall-kit prefab per piece (wall1=South, wall2=West, wall3=North, wall4=East; a corner uses both of the directions in its name, e.g. cornerWalls1+2 = [South, West]) - it does NOT change per spawn. Initialize() (called by BaseSlotWallSpawner right after Instantiate) supplies the per-instance owner slot and BaseSlotExpander reference that Directions gets resolved against.";


        ///// Public Variables/Editor Properties /////

        [Tooltip("Which compass side(s) of the slot this piece sits on - 1 entry for a straight wall (wall1-4), 2 for a corner (the two directions in its name, e.g. cornerWalls1+2 = South+West).")]
        public WallDirection[] Directions;

        [Tooltip("How long (seconds) this piece scales up from nothing the first time it becomes visible, instead of popping in instantly. Only plays once per piece - after that, hiding/showing again (if it ever happens) is instant.")]
        public float RevealDuration = 0.3f;

        [Tooltip("Siege hits (from the boss and/or rallying mobs during a boss fight) this piece can take before DestroyPiece() runs - see StructureHealth.")]
        public int MaxHits = 50;


        ///// Private Variables /////

        private BaseSlotExpander _slotExpander;
        private Vector2Int _ownerGridCoord;
        private bool _initialized;
        private bool _hasRevealed;
        private Vector3 _authoredScale;

        private static readonly Vector2Int GridSpacing = new(50, 50);

        // Every currently-VISIBLE piece (SetActive(true), whether just revealed or already
        // showing) - a piece adds itself on OnEnable and removes itself on OnDisable, so this
        // naturally tracks only real, standing fences. Used by NightBossAi to find the nearest
        // wall segment to target without needing colliders sized for detection.
        private static readonly List<BaseSlotWall> ActivePieces = new();
        public static IReadOnlyList<BaseSlotWall> AllPieces => ActivePieces;


        ///// Unity Methods /////

        private void Awake()
        {
            _authoredScale = transform.localScale;
            SetUpNavMeshObstacle();
        }

        // Blocks NavMeshAgent pathing (mobs rallying on Center Orient / chasing the player)
        // while this piece is standing, on top of - not instead of - the physical Collider that
        // already blocks the player. NavMeshObstacle carves/un-carves live as this GameObject's
        // active state toggles (ShowPiece/HidePiece/DestroyPiece), so no NavMesh rebake is ever
        // needed - a wall opens up the instant it's destroyed, exactly like its visibility does.
        // Sized from this piece's own real combined renderer bounds (its actual fence mesh, not
        // a hand-guessed box) so straight walls and corner pieces alike fit correctly with no
        // per-piece tuning, the same "measure the real geometry" approach PlantSpawnerBase uses
        // for FX placement.
        private void SetUpNavMeshObstacle()
        {
            NavMeshObstacle obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = gameObject.AddComponent<NavMeshObstacle>();

            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.carving = true;

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            obstacle.center = transform.InverseTransformPoint(bounds.center);

            Vector3 lossyScale = transform.lossyScale;
            obstacle.size = new Vector3(
                lossyScale.x != 0f ? bounds.size.x / lossyScale.x : bounds.size.x,
                lossyScale.y != 0f ? bounds.size.y / lossyScale.y : bounds.size.y,
                lossyScale.z != 0f ? bounds.size.z / lossyScale.z : bounds.size.z);
        }

        private void OnEnable() => ActivePieces.Add(this);

        private void OnDisable() => ActivePieces.Remove(this);


        ///// Trigger Methods /////

        // Trigger type: called once by BaseSlotWallSpawner right after this piece is
        // Instantiate'd, since a prefab instance has no scene-specific slot reference of its
        // own - everything else (subscribing, the first visibility check) follows from this.
        public void Initialize(BaseSlotExpander slotExpander, Vector2Int ownerGridCoord)
        {
            _slotExpander = slotExpander;
            _ownerGridCoord = ownerGridCoord;
            _initialized = true;

            Debug.Log($"[BaseSlotWall] Initialized '{gameObject.name}' - owner grid {ownerGridCoord}, Directions=[{string.Join(",", Directions)}].");

            GameEvents.OnBaseSlotUnlocked += HandleSlotUnlocked;
            RefreshVisibility();
        }

        // Trigger type: fired whenever any slot finishes unlocking - cheap enough to just
        // re-check this piece's own (at most 3) bordering slots rather than filtering by index.
        private void HandleSlotUnlocked(int slotIndex)
        {
            Debug.Log($"[BaseSlotWall] '{gameObject.name}' (owner grid {_ownerGridCoord}) got OnBaseSlotUnlocked(slotIndex={slotIndex}) - re-checking visibility.");
            RefreshVisibility();
        }


        private void OnDestroy()
        {
            if (_initialized) GameEvents.OnBaseSlotUnlocked -= HandleSlotUnlocked;
        }


        ///// Action Methods /////

        // The night boss's "destroy a fence wall" attack (see NightBossAi) - unlike HidePiece(),
        // this is permanent: unsubscribing from OnBaseSlotUnlocked means RefreshVisibility never
        // runs again for this piece, so it can never be revealed again even if its own slot or a
        // neighbor later changes. Per this session's answer, destroyed walls do NOT auto-repair.
        public void DestroyPiece()
        {
            if (_initialized) GameEvents.OnBaseSlotUnlocked -= HandleSlotUnlocked;
            _initialized = false;
            gameObject.SetActive(false);
            Debug.Log($"[BaseSlotWall] '{gameObject.name}' was DESTROYED by the night boss - permanently gone.");
        }

        private void RefreshVisibility()
        {
            if (!_initialized) return;

            Transform ownerSlot = _slotExpander.GetSlotAtGridCoord(_ownerGridCoord);
            if (ownerSlot == null)
            {
                Debug.LogWarning($"[BaseSlotWall] '{gameObject.name}' found no slot at its own owner grid coord {_ownerGridCoord} - check BaseSlotExpander.Slots' GridCoord values. Staying hidden.");
                HidePiece();
                return;
            }
            if (!_slotExpander.IsSlotUnlocked(ownerSlot))
            {
                Debug.Log($"[BaseSlotWall] '{gameObject.name}' - own slot {_ownerGridCoord} not bought yet, staying hidden.");
                HidePiece();
                return;
            }

            bool allBorderingSlotsUnlocked = Directions.All(HasRealUnlockedNeighbor);
            Debug.Log($"[BaseSlotWall] '{gameObject.name}' - own slot {_ownerGridCoord} bought, all bordering neighbors real+bought={allBorderingSlotsUnlocked} -> {(allBorderingSlotsUnlocked ? "staying hidden (territory merged)" : "REVEALING (fencing new territory)")}.");
            if (allBorderingSlotsUnlocked) HidePiece();
            else ShowPiece();
        }

        // First time this piece becomes visible, scale it up from nothing instead of popping in
        // instantly - a piece only ever goes visible -> hidden afterward in this design (slots
        // don't relock), so this only ever plays once.
        private void ShowPiece()
        {
            if (_hasRevealed)
            {
                gameObject.SetActive(true);
                InitializeHealth();
                return;
            }

            _hasRevealed = true;
            transform.localScale = Vector3.zero;
            gameObject.SetActive(true);
            InitializeHealth();
            StartCoroutine(RevealScaleUp());
        }

        private void HidePiece() => gameObject.SetActive(false);

        // Adds (or resets) this piece's StructureHealth every time it becomes visible/
        // targetable, so a piece that's already been destroyed and somehow shows again later
        // starts back at full health rather than remembering old damage.
        private void InitializeHealth()
        {
            StructureHealth health = GetComponent<StructureHealth>();
            if (health == null) health = gameObject.AddComponent<StructureHealth>();

            health.OnDepleted = DestroyPiece;
            health.Initialize(MaxHits);
        }

        private IEnumerator RevealScaleUp()
        {
            float elapsed = 0f;
            while (elapsed < RevealDuration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(Vector3.zero, _authoredScale, elapsed / RevealDuration);
                yield return null;
            }
            transform.localScale = _authoredScale;
        }

        // A direction that falls off the 3x3 grid (a map-edge/perimeter side) has no neighbor
        // at all - that's never "satisfied", so a piece with any such direction can never fully
        // hide (it stays a permanent boundary wall). Only a direction with a REAL neighbor that
        // is itself unlocked counts toward hiding.
        private bool HasRealUnlockedNeighbor(WallDirection direction)
        {
            Transform neighbor = _slotExpander.GetSlotAtGridCoord(_ownerGridCoord + Offset(direction));
            return neighbor != null && _slotExpander.IsSlotUnlocked(neighbor);
        }

        private static Vector2Int Offset(WallDirection direction) => direction switch
        {
            WallDirection.South => new Vector2Int(0, -GridSpacing.y),
            WallDirection.West => new Vector2Int(-GridSpacing.x, 0),
            WallDirection.North => new Vector2Int(0, GridSpacing.y),
            WallDirection.East => new Vector2Int(GridSpacing.x, 0),
            _ => Vector2Int.zero,
        };
    }
}

// Implementation Steps:
// 1. On the wall-kit prefab (see BaseSlotWallSpawner for how it gets spawned per slot),
//    attach this script to each of the 4 straight walls and 4 corners and set Directions:
//    wall1 -> [South], wall2 -> [West], wall3 -> [North], wall4 -> [East]; cornerWalls1+2 ->
//    [South, West], cornerWalls2+3 -> [West, North], cornerWalls3+4 -> [North, East],
//    cornerWalls1+4 (aka 4+1) -> [East, South]. This only needs setting once, on the prefab -
//    every spawned instance reuses the same Directions.
// 2. No manual per-instance wiring beyond that - BaseSlotWallSpawner calls Initialize() on
//    every spawned piece right after Instantiate, which handles subscribing to
//    GameEvents.OnBaseSlotUnlocked and the first visibility check itself.
