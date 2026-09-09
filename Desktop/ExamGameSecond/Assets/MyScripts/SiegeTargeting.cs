using UnityEngine;

namespace NTGD124
{
    // Shared "find and destroy the nearest wall piece or unlocked base slot" logic - used by
    // both NightBossAi (the boss's own siege behaviour) and Mobai (regular mobs joining the
    // siege once the boss appears, see Mobai's boss-support mode) so the two don't carry
    // separate copies of the same targeting/destroy logic.
    public static class SiegeTargeting
    {
        // Picks whichever of the nearest wall piece / nearest unlocked slot is closer - either
        // order is fine mechanically, both go through the same nearest-first scan.
        public static Transform FindNearestStructure(Vector3 fromPosition, float detectionRange, BaseSlotExpander slotExpander)
        {
            Transform nearestWall = FindNearestWallPiece(fromPosition, detectionRange);
            Transform nearestSlot = FindNearestUnlockedSlot(fromPosition, detectionRange, slotExpander);

            if (nearestWall == null) return nearestSlot;
            if (nearestSlot == null) return nearestWall;

            float wallDistance = Vector3.Distance(fromPosition, nearestWall.position);
            float slotDistance = Vector3.Distance(fromPosition, nearestSlot.position);
            return wallDistance <= slotDistance ? nearestWall : nearestSlot;
        }

        private static Transform FindNearestWallPiece(Vector3 fromPosition, float detectionRange)
        {
            BaseSlotWall nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (BaseSlotWall piece in BaseSlotWall.AllPieces)
            {
                if (piece == null) continue;

                float distance = Vector3.Distance(fromPosition, piece.transform.position);
                if (distance > detectionRange || distance >= nearestDistance) continue;

                nearestDistance = distance;
                nearest = piece;
            }

            return nearest != null ? nearest.transform : null;
        }

        private static Transform FindNearestUnlockedSlot(Vector3 fromPosition, float detectionRange, BaseSlotExpander slotExpander)
        {
            if (slotExpander == null) return null;

            Transform nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (Transform slotTransform in slotExpander.GetUnlockedSlotTransforms())
            {
                float distance = Vector3.Distance(fromPosition, slotTransform.position);
                if (distance > detectionRange || distance >= nearestDistance) continue;

                nearestDistance = distance;
                nearest = slotTransform;
            }

            return nearest;
        }

        // Registers one siege hit against whichever structure this target is (wall piece or
        // slot) - both BaseSlotWall.ShowPiece and BaseSlotExpander.RefreshSlotVisual add a
        // StructureHealth component the moment their structure becomes targetable, so this is
        // normally just a RegisterHit call; actual destruction (DestroyPiece/DestroySlot) is
        // handled by that component's own OnDepleted callback once its health runs out, not
        // decided here.
        public static void DamageStructure(Transform target, BaseSlotExpander slotExpander, int amount)
        {
            StructureHealth health = target.GetComponent<StructureHealth>();
            if (health != null)
            {
                health.RegisterHit(amount);
                return;
            }

            // Defensive fallback only - StructureHealth should always be present by the time
            // anything can target this structure. Destroys outright rather than leaving a
            // structure with no health to register hits against.
            BaseSlotWall wall = target.GetComponent<BaseSlotWall>();
            if (wall != null)
            {
                wall.DestroyPiece();
                return;
            }

            slotExpander?.DestroySlot(target);
        }
    }
}
