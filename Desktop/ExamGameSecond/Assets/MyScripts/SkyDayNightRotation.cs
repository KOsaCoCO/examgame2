// Rotates this object's X axis continuously to simulate a day/night cycle over a set number of minutes.
using UnityEngine;
using System.Collections;

namespace NTGD124
{
    public class SkyDayNightRotation : MonoBehaviour
    {
        /////  Component Description/////
        [TextArea(3, 10)]
        public string ComponentDescription = "Rotates this GameObject's X rotation from 0 to 360 degrees, driven by a linked DayNightTimeCycle component. The full-cycle length is read directly from DayNightTimeCycle.GetFullCycleSeconds() every frame (instead of a separately configured duration), so the rotation can never fall out of sync with the timer and jump between angles. The rotation is calculated fresh from DayNightTimeCycle's CurrentTime every frame (wrapped with Mathf.Repeat), so it loops forever without growing into large numbers.";


        /////  Public Variables/Editor Properties /////

        [Header("Day Night Cycle Link")]
        [Tooltip("Drag the GameObject with the DayNightTimeCycle component here to link this rotation to it")]
        public DayNightTimeCycle LinkedDayNightCycle;

        [Header("Target Filter (Optional)")]
        public bool UseTagFilter = false; // If true, this component only runs on objects matching TargetTag
        public string TargetTag = "Untagged"; // Tag required on this GameObject for the rotation to run

        [Header("Trigger Delay")]
        public float StartDelay = 0f; // Seconds to wait before the rotation cycle begins


        /////  Private Variables /////

        private bool _isRotating; // Whether the rotation loop is currently active
        private float _baseY; // Y/Z euler angles captured once at Start and reused every frame -
        private float _baseZ; // reading them back from transform.localEulerAngles instead (as this
                               // used to) round-trips through Unity's quaternion->euler decomposition
                               // every frame, which isn't stable near the X axis's gimbal-adjacent
                               // angles and was the source of the sun's jitter.


        /////  Unity Methods /////

        // Unity Method: caches the constant Y/Z tilt and starts the rotation with the configured delay
        private void Start()
        {
            Vector3 initialEuler = transform.localEulerAngles;
            _baseY = initialEuler.y;
            _baseZ = initialEuler.z;

            StartRotationCycle(StartDelay);
        }


        /////  Trigger Methods /////

        // Trigger Method (time-based / editor-triggered): begins the rotation loop after an optional delay
        public void StartRotationCycle(float delay)
        {
            StartCoroutine(StartRotationCycleAfterDelay(delay));
        }


        /////  Action Methods /////

        // Action: reads DayNightTimeCycle's CurrentTime and converts it into a wrapped 0 to 360 rotation
        private void RotateOverTime()
        {
            if (LinkedDayNightCycle == null) return;

            // Read the full cycle length straight from DayNightTimeCycle every frame instead of
            // keeping a separate duration here, so the two components can never drift out of sync
            // and cause the rotation to jump between angles when their cycle lengths disagree.
            float fullCycleSeconds = LinkedDayNightCycle.GetFullCycleSeconds();
            if (fullCycleSeconds <= 0f) return;

            // Mathf.Repeat wraps CurrentTime into the 0 to fullCycleSeconds range no matter how
            // large DayNightTimeCycle's CurrentTime grows, so this value never accumulates or grows large
            float wrappedTime = Mathf.Repeat(LinkedDayNightCycle.CurrentTime, fullCycleSeconds);
            float rotationX = 360f * (wrappedTime / fullCycleSeconds);

            ApplyRotation(rotationX);
        }

        // Action: applies the calculated X rotation to this object's local rotation, always built
        // fresh from the cached Y/Z (see _baseY/_baseZ) rather than read back from the transform -
        // that read-back is what caused the jitter, since it fed each frame's (potentially
        // unstably-decomposed) Euler angles into the next frame's rotation.
        private void ApplyRotation(float rotationX)
        {
            transform.localRotation = Quaternion.Euler(rotationX, _baseY, _baseZ);
        }


        /////  Coroutines /////

        // Waits for the delay, checks the tag filter, then runs the rotation loop every frame
        private IEnumerator StartRotationCycleAfterDelay(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            // Skip rotating if a tag filter is set and this object does not match it
            if (UseTagFilter && !gameObject.CompareTag(TargetTag))
            {
                Debug.Log("SkyDayNightRotation: Tag filter active, object does not match TargetTag. Rotation skipped.");
                yield break;
            }

            _isRotating = true;
            Debug.Log("SkyDayNightRotation: Day/night rotation cycle started.");

            while (_isRotating)
            {
                RotateOverTime();
                yield return null;
            }
        }
    }
}

// Implementation Steps:
// 1. Attach this script to the object that should rotate to represent the sun/sky (e.g. a directional light or sky sphere).
// 2. Drag the GameObject that has the DayNightTimeCycle component into the "Linked Day Night Cycle" box in the Inspector.
// 3. Set DayNightTimeCycle's DayLengthSeconds + NightLengthSeconds to whatever full cycle length you want - this script reads that length directly, so there is no separate duration to keep in sync here.
// 4. Optionally enable "Use Tag Filter" and set "Target Tag" if this script is placed on a prefab/object shared across multiple objects and should only run on matching ones.
// 5. Optionally set "Start Delay" to have the rotation begin a few seconds after the scene starts.
// 6. Press Play: the object's X rotation will follow DayNightTimeCycle's CurrentTime, moving from 0 to 360 degrees and looping forever without growing into large numbers.
