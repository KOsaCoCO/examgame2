using UnityEngine;

namespace NTGD124
{
    public class DayNightTimeCycle : MonoBehaviour
    {
        /////  Component Description/////
        [TextArea(3, 10)]
        [SerializeField] private string _componentDescription = "Tracks in-game day/night time. CurrentTime counts up in seconds through DayLengthSeconds then NightLengthSeconds, then wraps back to 0 (instead of growing forever) and increments CycleCount. Drive SkyDayNightRotation's LinkedDayNightCycle from this component.";


        /////  Public Variables/Editor Properties /////

        [Header("Cycle Length")]
        [Tooltip("How long daytime lasts, in seconds")]
        public float DayLengthSeconds = 240f; // 4 minutes

        [Tooltip("How long nighttime lasts, in seconds")]
        public float NightLengthSeconds = 240f; // 4 minutes

        [Header("Time")]
        [Tooltip("Current time within the current cycle, in seconds. 0 = start of day, DayLengthSeconds = start of night")]
        public float CurrentTime;

        [Header("Cycle Tracking")]
        [Tooltip("How many full day/night cycles have completed")]
        public int CycleCount;


        /////  Private Variables /////

        private float _fullCycleSeconds; // DayLengthSeconds + NightLengthSeconds
        private bool _wasDaytime; // tracks the previous frame's IsDaytime() so a day->night flip can be detected once


        /////  Unity Methods /////

        private void Start()
        {
            _fullCycleSeconds = DayLengthSeconds + NightLengthSeconds;
            CurrentTime = 0f;
            _wasDaytime = true; // CurrentTime starts at 0, which is always daytime
        }

        private void Update()
        {
            UpdateCycle();
        }


        /////  Action Methods /////

        // Advances the timer and, once the night ends, wraps back to 0 and counts the finished cycle
        private void UpdateCycle()
        {
            CurrentTime += Time.deltaTime;

            if (CurrentTime >= _fullCycleSeconds)
            {
                CurrentTime -= _fullCycleSeconds;
                CycleCount++;
            }

            bool isDaytimeNow = IsDaytime();
            if (_wasDaytime && !isDaytimeNow) GameEvents.RaiseNightBegan();
            if (!_wasDaytime && isDaytimeNow) GameEvents.RaiseDayBegan();
            _wasDaytime = isDaytimeNow;
        }


        /////  Getter Methods /////

        public bool IsDaytime()
        {
            return CurrentTime < DayLengthSeconds;
        }

        public float GetFullCycleSeconds()
        {
            return _fullCycleSeconds;
        }
    }
}

// Implementation Steps:
// 1. Create an empty GameObject in your scene (e.g. "DayNightTimeCycle") and attach this script to it.
// 2. Set DayLengthSeconds / NightLengthSeconds in the inspector (defaults to 1200 each = 20 minutes day, 20 minutes night).
// 3. On SkyDayNightRotation, drag this GameObject into "Linked Day Night Cycle" and set Full Cycle Minutes to (DayLengthSeconds + NightLengthSeconds) / 60 (default 40).
// 4. Press Play: CurrentTime counts up through the day then night, wraps back to 0 at the end of night, and CycleCount goes up by 1 each completed cycle.
