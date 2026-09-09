using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Place on a "water system" GameObject that has a BoxCollider marking its water area (or a mesh to auto-fit
// one to). Any character (mob, NPC, player) that touches the box gets sunk so only SubmergedRatio of its own
// model height sits below the box's top face - measured from its actual collider bounds, not its transform
// pivot, so it works regardless of whether that pivot sits at the feet, center, or anywhere else - plus a
// small bounce on top. If the terrain under an agent rises above where floating would place it (eg a shore or
// shallow bank inside the water box), that agent is left alone to walk normally instead of getting stuck.
namespace NTGD124
{
    public class WaterFloater : MonoBehaviour
    {
        ///// Public Variables /////

        [Header("Bounce")]
        public float BounceAmplitude = 0.1f;
        public float BounceSpeed = 4f;

        [Header("Buoyancy")]
        [Range(0f, 1f)] public float SubmergedRatio = 0.8f; // fraction of the agent's own model height that sits below the water's top face (the rest pokes up above it)

        [Header("Water Collider")]
        public BoxCollider WaterCollider; // trigger collider agents must touch to float; leave unassigned to auto-fit one to this object's mesh bounds

        [Header("Terrain Safety")]
        public string TerrainTag = "GameTerrain"; // tag on the Terrain GameObject; lets an agent walk out onto a shore/shallow bank instead of getting stuck floating where the ground has risen above the water line
        public float TerrainClearance = 0.05f; // terrain must be at least this far above the intended float height before an agent is treated as grounded instead of floating


        ///// Private Variables /////

        private Terrain _targetTerrain;
        private readonly Dictionary<Transform, FloatingAgent> _floatingAgents = new Dictionary<Transform, FloatingAgent>();

        // Per-agent state tracked for as long as that agent stays in contact with WaterCollider
        private class FloatingAgent
        {
            public NavMeshAgent Agent;
            public float DefaultBaseOffset;
            public float Height; // the agent's own collider height, used to scale how far it sinks below the water's top face
            public float PivotOffsetFromBottom; // how far transform.position sits above the collider's own bottom (not always the feet - eg a CharacterController centered on its capsule)
            public float BouncePhase; // randomised per agent so multiple characters floating together don't bob in unison
        }


        ///// Unity Methods /////

        private void Awake()
        {
            EnsureWaterCollider();
            LocateTerrain();
        }

        // Collision-based trigger: any Collider (Player, NPC, mob) enters WaterCollider
        private void OnTriggerEnter(Collider other)
        {
            HandleColliderEntered(other);
        }

        // Collision-based trigger: any Collider (Player, NPC, mob) leaves WaterCollider
        private void OnTriggerExit(Collider other)
        {
            HandleColliderExited(other);
        }

        // Time-based trigger: every frame, keeps every currently-touching agent floating at the water surface
        private void LateUpdate()
        {
            foreach (KeyValuePair<Transform, FloatingAgent> entry in _floatingAgents)
            {
                ApplyFloat(entry.Key, entry.Value);
            }
        }


        ///// Trigger Methods /////

        // Trigger type: collision-based (OnTriggerEnter)
        private void HandleColliderEntered(Collider other)
        {
            Transform agentTransform = other.transform;
            if (_floatingAgents.ContainsKey(agentTransform)) return;

            NavMeshAgent agent = agentTransform.GetComponent<NavMeshAgent>();
            _floatingAgents[agentTransform] = new FloatingAgent
            {
                Agent = agent,
                DefaultBaseOffset = agent != null ? agent.baseOffset : 0f,
                Height = other.bounds.size.y,
                PivotOffsetFromBottom = agentTransform.position.y - other.bounds.min.y,
                BouncePhase = Random.Range(0f, Mathf.PI * 2f)
            };
        }

        // Trigger type: collision-based (OnTriggerExit)
        private void HandleColliderExited(Collider other)
        {
            if (!_floatingAgents.TryGetValue(other.transform, out FloatingAgent floating)) return;

            if (floating.Agent != null)
            {
                floating.Agent.baseOffset = floating.DefaultBaseOffset;
            }

            _floatingAgents.Remove(other.transform);
        }


        ///// Action Methods /////

        // Auto-fits WaterCollider (adding one if none exists) to this water system object's own mesh bounds
        private void EnsureWaterCollider()
        {
            if (WaterCollider == null)
            {
                WaterCollider = GetComponent<BoxCollider>();
                if (WaterCollider == null)
                {
                    WaterCollider = gameObject.AddComponent<BoxCollider>();
                }
            }

            WaterCollider.isTrigger = true;

            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogWarning($"{gameObject.name} has no mesh to fit WaterCollider to - using its current size/center as-is.");
                return;
            }

            Bounds meshBounds = meshFilter.sharedMesh.bounds;
            WaterCollider.center = meshBounds.center;
            WaterCollider.size = meshBounds.size;
        }

        // Finds the terrain using TerrainTag, used by the terrain-grounded safeguard in ApplyFloat
        private void LocateTerrain()
        {
            GameObject terrainObject = GameObject.FindGameObjectWithTag(TerrainTag);
            if (terrainObject == null)
            {
                Debug.LogWarning($"{gameObject.name} could not find a terrain tagged '{TerrainTag}'.");
                return;
            }

            _targetTerrain = terrainObject.GetComponent<Terrain>();
            if (_targetTerrain == null)
            {
                Debug.LogWarning($"{gameObject.name} found '{terrainObject.name}' but it has no Terrain component.");
            }
        }

        // Safeguard: if the terrain under the agent has risen above where floating would place it (eg a shore
        // or shallow bank inside the water box), it counts as grounded so it can walk on the terrain normally
        // instead of being pulled back down/up to float height and getting stuck
        private bool IsGroundedOnTerrain(Vector3 position, float floatY)
        {
            if (_targetTerrain == null) return false;

            float groundY = _targetTerrain.SampleHeight(position) + _targetTerrain.transform.position.y;
            return groundY >= floatY - TerrainClearance;
        }

        // Restores a grounded agent's NavMeshAgent baseOffset to normal so it walks at ground height instead of
        // staying raised from a previous frame of floating (transform-only agents need no action - leaving their
        // position alone lets normal movement/gravity settle them onto the terrain)
        private void ReleaseToTerrain(FloatingAgent floating)
        {
            if (floating.Agent != null)
            {
                floating.Agent.baseOffset = floating.DefaultBaseOffset;
            }
        }

        // Adjusts NavMeshAgent.baseOffset so the agent's transform pivot lands at floatY, visually placing it
        // there without disturbing its internal pathfinding
        private void ApplyAgentFloat(Transform agentTransform, NavMeshAgent agent, float floatY)
        {
            float navMeshSurfaceY = agentTransform.position.y - agent.baseOffset;
            agent.baseOffset = floatY - navMeshSurfaceY;
        }

        // Directly overrides the transform's height for characters with no NavMeshAgent (eg the player)
        private void ApplyTransformFloat(Transform agentTransform, float floatY)
        {
            Vector3 position = agentTransform.position;
            position.y = floatY;
            agentTransform.position = position;
        }

        // Floats a single tracked agent at the water box's top face, sunk by SubmergedRatio of its own model
        // height so most of it pokes up above the surface instead of standing fully on top, plus a small bounce
        private void ApplyFloat(Transform agentTransform, FloatingAgent floating)
        {
            if (agentTransform == null) return; // agent may have been destroyed while still touching the water

            float surfaceY = WaterCollider.bounds.max.y + GetBounce(floating.BouncePhase);
            float desiredBottomY = surfaceY - floating.Height * SubmergedRatio;
            float floatY = desiredBottomY + floating.PivotOffsetFromBottom;

            if (IsGroundedOnTerrain(agentTransform.position, floatY))
            {
                ReleaseToTerrain(floating);
                return;
            }

            if (floating.Agent != null)
            {
                ApplyAgentFloat(agentTransform, floating.Agent, floatY);
            }
            else
            {
                ApplyTransformFloat(agentTransform, floatY);
            }
        }

        private float GetBounce(float bouncePhase)
        {
            return Mathf.Sin(Time.time * BounceSpeed + bouncePhase) * BounceAmplitude;
        }
    }
}

// Implementation Steps:
// 1. Add this WaterFloater script to each "water system" GameObject in the scene (eg the Bitgem water volume
//    objects, which already have a MeshFilter/MeshRenderer).
// 2. Leave Water Collider unassigned to auto-fit a trigger BoxCollider to that object's own mesh bounds on
//    Awake, or assign your own BoxCollider beforehand if you want manual control over the floatable area.
// 3. Make sure every character that should float (Player, NPC, mob prefabs) has either a Rigidbody or a
//    CharacterController, and that this GameObject has a Rigidbody set to Is Kinematic if agents use plain
//    Colliders with no Rigidbody of their own - Unity only fires trigger events when at least one side of the
//    overlap has a Rigidbody.
// 4. Adjust Bounce Amplitude/Speed per water body if you want a different float feel.
// 5. Floating height comes entirely from WaterCollider's own top face (plus each agent's Submerged Ratio and
//    the bounce) - there is no separate wave/surface simulation involved, so make sure the box actually covers
//    the visible water surface for each water body.
// 6. Terrain Tag (default "GameTerrain") must match the Tag on your scene's Terrain GameObject - it feeds the
//    terrain-grounded safeguard so agents standing on a shore/shallow bank inside the water box walk normally
//    instead of being pulled to float height. If the terrain isn't found, this safeguard is skipped gracefully
//    (a warning is logged) and agents just float everywhere inside the box as before.
