using UnityEngine;

namespace PrehistoricSurvival.Player
{
    /// <summary>
    /// Tracks carried weight (e.g., logs on shoulder) and applies speed penalties.
    /// Supports visual overlay sprites for carried items.
    /// </summary>
    public class WeightCarrySystem : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Speed reduction per log carried (as fraction, e.g. 0.15 = 15%).")]
        public float speedPenaltyPerLog = 0.15f;

        [Tooltip("Maximum number of logs that can be carried.")]
        public int maxLogs = 4;

        [Header("Visual")]
        [Tooltip("Overlay sprite renderer for shoulder-carried items.")]
        public SpriteRenderer overlayRenderer;
        [Tooltip("Sprite to show when carrying 1 log.")]
        public Sprite carry1Sprite;
        [Tooltip("Sprite to show when carrying 2 logs.")]
        public Sprite carry2Sprite;
        [Tooltip("Sprite to show when carrying 3+ logs.")]
        public Sprite carry3Sprite;

        private int _carriedLogs;

        /// <summary>Number of logs currently carried.</summary>
        public int CarriedLogs => _carriedLogs;

        /// <summary>Total speed penalty as a fraction (0..1).</summary>
        public float SpeedPenalty => Mathf.Clamp01(_carriedLogs * speedPenaltyPerLog);

        /// <summary>Total weight of carried logs (kg).</summary>
        public float CarriedWeight => _carriedLogs * 25f; // 25 kg per log

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>Pick up a log. Returns true if successful.</summary>
        public bool PickUpLog()
        {
            if (_carriedLogs >= maxLogs) return false;
            _carriedLogs++;
            UpdateOverlay();
            return true;
        }

        /// <summary>Drop one log.</summary>
        public void DropLog()
        {
            if (_carriedLogs <= 0) return;
            _carriedLogs--;
            UpdateOverlay();
        }

        /// <summary>Drop all logs.</summary>
        public void DropAll()
        {
            _carriedLogs = 0;
            UpdateOverlay();
        }

        private void UpdateOverlay()
        {
            if (overlayRenderer == null) return;

            if (_carriedLogs <= 0)
            {
                overlayRenderer.enabled = false;
            }
            else
            {
                overlayRenderer.enabled = true;
                if (_carriedLogs == 1) overlayRenderer.sprite = carry1Sprite;
                else if (_carriedLogs == 2) overlayRenderer.sprite = carry2Sprite;
                else overlayRenderer.sprite = carry3Sprite;
            }
        }
    }
}
