using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NTGD124
{
    // Hit-counter + billboarded health bar for a siege-destructible structure (a BaseSlotWall
    // piece or a BaseSlotExpander slot) - the exact same two-quad bar MobHealth already builds
    // for mobs, reused here rather than duplicated per structure type. Owner-agnostic on
    // purpose: RegisterHit() only counts down and fires OnDepleted once, the owner
    // (BaseSlotWall/BaseSlotExpander) decides what actually happens on depletion
    // (DestroyPiece()/DestroySlot()) via that callback - this component has no opinion about
    // walls vs slots, or about who's attacking (the boss and every rallying mob during a siege
    // both just call RegisterHit through SiegeTargeting.DamageStructure).
    public class StructureHealth : MonoBehaviour
    {
        [Header("Health Bar")]
        public float barHeightOffset = 2.2f;
        public float barWidth = 1f;
        public float barThickness = 0.12f;
        public Color healthyColor = Color.green;
        public Color lowHealthColor = Color.red;

        public int MaxHits { get; private set; }

        // Assigned fresh (not +=) by the owner every time Initialize runs - a structure can
        // cycle destroyed -> rebuilt -> destroyed again (slots specifically: DestroySlot reverts
        // IsUnlocked, letting the player re-buy and re-destroy the same slot later), and plain
        // assignment guarantees only the current owner call is ever invoked, never a stale one
        // stacked from a previous life.
        public Action OnDepleted;

        private int hitsTaken;
        private bool depleted;
        private Camera mainCamera;
        private Transform healthBarRoot;
        private Transform fillTransform;
        private Renderer fillRenderer;

        // Called by the owner every time this structure becomes active/targetable (a wall's
        // ShowPiece, a slot's unlock) - resets hitsTaken to 0 and (re)builds the bar, so a
        // structure that's been rebuilt after a prior destruction always starts back at full
        // health rather than remembering damage from before it was last destroyed.
        public void Initialize(int maxHits)
        {
            MaxHits = maxHits;
            hitsTaken = 0;
            depleted = false;

            mainCamera = Camera.main;
            if (healthBarRoot == null) BuildHealthBar();
            UpdateHealthBar();
        }

        void LateUpdate()
        {
            if (healthBarRoot == null) return;
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;

            healthBarRoot.SetPositionAndRotation(transform.position + Vector3.up * barHeightOffset, mainCamera.transform.rotation);
        }

        public void RegisterHit(int amount)
        {
            if (depleted) return;

            hitsTaken += amount;
            UpdateHealthBar();

            if (hitsTaken >= MaxHits)
            {
                depleted = true;
                OnDepleted?.Invoke();
            }
        }

        // Builds a simple green->red bar from two unlit Quads (background + fill) - identical
        // shape to MobHealth.BuildHealthBar, parented to this structure so it's cleaned up
        // automatically if the structure's own GameObject is ever destroyed outright.
        private void BuildHealthBar()
        {
            healthBarRoot = new GameObject("HealthBar").transform;
            healthBarRoot.SetParent(transform, worldPositionStays: false);
            healthBarRoot.localPosition = Vector3.up * barHeightOffset;

            Transform background = CreateBarQuad("Background", Color.black);
            background.SetParent(healthBarRoot, worldPositionStays: false);
            background.localPosition = new Vector3(0f, 0f, 0.02f);
            background.localScale = new Vector3(barWidth, barThickness, 1f);

            fillTransform = CreateBarQuad("Fill", healthyColor);
            fillTransform.SetParent(healthBarRoot, worldPositionStays: false);
            fillTransform.localPosition = Vector3.zero;
            fillTransform.localScale = new Vector3(barWidth, barThickness, 1f);
            fillRenderer = fillTransform.GetComponent<Renderer>();
        }

        private Transform CreateBarQuad(string quadName, Color color)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = quadName;

            Collider quadCollider = quad.GetComponent<Collider>();
            if (quadCollider != null) Destroy(quadCollider);

            Renderer renderer = quad.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            Material material = new Material(shader) { color = color };
            renderer.material = material;

            return quad.transform;
        }

        // Shrinks the fill from its right edge (left edge pinned) and lerps green -> red as
        // hitsTaken approaches MaxHits - identical math to MobHealth.UpdateHealthBar.
        private void UpdateHealthBar()
        {
            if (fillTransform == null || fillRenderer == null) return;

            float remainingFraction = MaxHits > 0 ? Mathf.Clamp01(1f - (float)hitsTaken / MaxHits) : 0f;

            fillTransform.localScale = new Vector3(barWidth * remainingFraction, barThickness, 1f);
            fillTransform.localPosition = new Vector3(-(barWidth * (1f - remainingFraction)) / 2f, 0f, 0f);
            fillRenderer.material.color = Color.Lerp(lowHealthColor, healthyColor, remainingFraction);
        }
    }
}

// Implementation Steps:
// 1. Nothing to place manually - BaseSlotWall.ShowPiece() and BaseSlotExpander.RefreshSlotVisual()
//    both add this component to themselves/their slot GameObject automatically the moment they
//    become active, and call Initialize() with their own MaxHits/SlotMaxHits value.
// 2. Damage only ever comes from SiegeTargeting.DamageStructure (the boss's/rallying mobs'
//    siege attacks) - there's no player-facing way to hit a wall or slot directly.
