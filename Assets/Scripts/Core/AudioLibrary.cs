using UnityEngine;

namespace PrehistoricSurvival.Core
{
    /// <summary>Data-driven sound bank. Assign imported clips in the inspector; the audio manager also supplies quiet procedural fallbacks.</summary>
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "PrehistoricSurvival/Audio Library")]
    public class AudioLibrary : ScriptableObject
    {
        public const string ResourcePath = "AudioLibrary";

        [Header("Music")]
        public AudioClip[] music;
        [Range(0f, 1f)] public float musicVolume = 0.35f;

        [Header("World")]
        public AudioClip[] ambience;
        public AudioClip[] footsteps;
        public AudioClip[] water;
        public AudioClip[] weather;

        [Header("Actions")]
        public AudioClip[] ui;
        public AudioClip[] pickup;
        public AudioClip[] craft;
        public AudioClip[] impact;
        public AudioClip[] combat;

        [Range(0f, 1f)] public float effectsVolume = 0.8f;
        [Range(0.8f, 1.2f)] public float pitchMin = 0.94f;
        [Range(0.8f, 1.2f)] public float pitchMax = 1.06f;

        private static AudioLibrary _instance;
        public static AudioLibrary Instance
        {
            get
            {
                if (_instance == null) _instance = Resources.Load<AudioLibrary>(ResourcePath);
                return _instance;
            }
        }
    }
}
