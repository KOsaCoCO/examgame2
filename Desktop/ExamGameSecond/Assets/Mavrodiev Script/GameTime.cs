using UnityEngine;

namespace NTGD124
{
    // GameTime: Core timer system that can function as either a normal timer or countdown timer based on start/end times
    public class GameTime : MonoBehaviour
    {
        ///// Component Description /////
        [TextArea(3, 10)]
        [SerializeField] private string _componentDescription = "Core timer system that runs between start and end times. Set start > end for countdown, start < end for count up. Public CurrentTime for easy inspector debugging.";


        ///// Public Variables/Editor Properties /////
        [Header("Time")]
        [Tooltip("Current time value - viewable in inspector")]
        public float CurrentTime;
        
        [Header("Timer Configuration")]
        [Tooltip("Starting time in seconds")]
        public float StartTime = 60f;

        [Tooltip("Ending time in seconds")]
        public float EndTime = 0f;

        [Tooltip("Should timer stop when reaching end time")]
        public bool StopAtEnd = true;


        ///// Private Variables /////
        private bool _isRunning;


        ///// Unity Methods /////
        private void Start()
        {
            InitializeTimer();
        }

        private void Update()
        {
            UpdateTimer();
        }


        ///// Trigger Methods /////
        public void StartTimer()
        {
            _isRunning = true;
        }

        public void PauseTimer()
        {
            _isRunning = false;
        }

        public void ResumeTimer()
        {
            _isRunning = true;
        }

        public void ResetTimer()
        {
            InitializeTimer();
        }


        ///// Action Methods /////
        private void InitializeTimer()
        {
            CurrentTime = StartTime;
            _isRunning = true;
        }

        private void UpdateTimer()
        {
            if (!_isRunning) return;

            bool isCountdown = StartTime > EndTime;
            
            if (isCountdown)
            {
                CurrentTime -= Time.deltaTime;
                if (CurrentTime <= EndTime && StopAtEnd)
                {
                    CurrentTime = EndTime;
                    _isRunning = false;
                }
            }
            else
            {
                CurrentTime += Time.deltaTime;
                if (CurrentTime >= EndTime && StopAtEnd)
                {
                    CurrentTime = EndTime;
                    _isRunning = false;
                }
            }
        }


        ///// Getter Methods /////
        public float GetCurrentTime()
        {
            return CurrentTime;
        }
        
        public bool IsCountdown()
        {
            return StartTime > EndTime;
        }

        public bool IsRunning()
        {
            return _isRunning;
        }

        public bool HasFinished()
        {
            return StopAtEnd && (IsCountdown() ? 
                CurrentTime <= EndTime : 
                CurrentTime >= EndTime);
        }

        public float GetProgress()
        {
            return Mathf.Abs((CurrentTime - StartTime) / (EndTime - StartTime));
        }
    }
}

// Implementation Steps:
// 1. Create an empty GameObject in your scene
// 2. Name it "GameTime" or similar
// 3. Attach this GameTime script to it
// 4. Configure in inspector:
//    - For countdown: Set StartTime > EndTime (e.g., 60 to 0)
//    - For normal: Set StartTime < EndTime (e.g., 0 to 60)
//    - Set StopAtEnd as needed
// 5. Reference this GameTime component from other scripts (TimerTrigger, TimerDisplay, etc.)