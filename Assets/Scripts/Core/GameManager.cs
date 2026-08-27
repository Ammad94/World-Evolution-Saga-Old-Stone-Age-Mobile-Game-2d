using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrehistoricSurvival.Core
{
    /// <summary>
    /// Central singleton that orchestrates game initialization, pause/resume,
    /// scene transitions, and high-level game state.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Game Settings")]
        [Tooltip("Total in-game minutes per real second (controls day speed globally).")]
        public float timeScale = 1f;

        [Tooltip("Whether the game starts paused (useful for main-menu flow).")]
        public bool startPaused;

        [Header("Scene Names")]
        public string mainMenuScene = "MainMenu";
        public string gameplayScene = "GameplayWorld";

        // --- Public State ---
        public bool IsPaused { get; private set; }
        public bool IsGameOver { get; private set; }

        // --- Events ---
        public event Action OnGamePaused;
        public event Action OnGameResumed;
        public event Action OnGameOver;
        public event Action OnGameRestarted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        private void Start()
        {
            if (startPaused) PauseGame();
        }

        // ------------------------------------------------------------------
        // Pause / Resume
        // ------------------------------------------------------------------
        public void PauseGame()
        {
            if (IsPaused) return;
            IsPaused = true;
            Time.timeScale = 0f;
            OnGamePaused?.Invoke();
        }

        public void ResumeGame()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = 1f;
            OnGameResumed?.Invoke();
        }

        public void TogglePause()
        {
            if (IsPaused) ResumeGame();
            else PauseGame();
        }

        // ------------------------------------------------------------------
        // Game Over / Restart
        // ------------------------------------------------------------------
        public void TriggerGameOver()
        {
            if (IsGameOver) return;
            IsGameOver = true;
            PauseGame();
            OnGameOver?.Invoke();
        }

        public void RestartGame()
        {
            IsGameOver = false;
            IsPaused = false;
            Time.timeScale = 1f;
            OnGameRestarted?.Invoke();
            SceneManager.LoadScene(gameplayScene);
        }

        public void ReturnToMainMenu()
        {
            IsGameOver = false;
            IsPaused = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuScene);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        /// <summary>
        /// Converts in-game seconds to real seconds based on the current timeScale.
        /// </summary>
        public float InGameSecondsToReal(float inGameSeconds)
        {
            return timeScale > 0f ? inGameSeconds / timeScale : inGameSeconds;
        }
    }
}
