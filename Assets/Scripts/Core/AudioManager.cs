using UnityEngine;

namespace PrehistoricSurvival.Core
{
    /// <summary>Centralized, mobile-friendly audio playback and event-to-sound routing.</summary>
    [DefaultExecutionOrder(-400)]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }
        public AudioLibrary library;
        public bool playAmbientLoop = true;

        private AudioSource _effects;
        private AudioSource _music;
        private AudioClip _fallbackStep;
        private AudioClip _fallbackPickup;
        private AudioClip _fallbackCraft;
        private float _stepClock;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            library = library != null ? library : AudioLibrary.Instance;
            _effects = MakeSource("Effects", false, library != null ? library.effectsVolume : 0.8f);
            _music = MakeSource("Music", true, library != null ? library.musicVolume : 0.35f);
            RouteEvents(true);
            StartMusic();
        }

        private void Update()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            var controller = player != null ? player.GetComponent<PrehistoricSurvival.Player.PlayerController>() : null;
            if (controller != null && controller.IsMoving)
            {
                _stepClock += Time.unscaledDeltaTime;
                if (_stepClock >= 0.42f) { _stepClock = 0f; PlayFootstep(); }
            }
            else _stepClock = 0f;
        }

        private AudioSource MakeSource(string name, bool loop, float volume)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.name = name;
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.volume = volume;
            return source;
        }

        private void StartMusic()
        {
            if (!playAmbientLoop || library == null || library.music == null) return;
            var clip = Pick(library.music);
            if (clip != null) { _music.clip = clip; _music.Play(); }
        }

        public void PlayUI() => Play(Pick(library != null ? library.ui : null), 0.9f, _fallbackPickup);
        public void PlayFootstep() => Play(Pick(library != null ? library.footsteps : null), 0.5f, _fallbackStep);
        public void PlayPickup() => Play(Pick(library != null ? library.pickup : null), 1f, _fallbackPickup);
        public void PlayCraft() => Play(Pick(library != null ? library.craft : null), 1f, _fallbackCraft);
        public void PlayImpact() => Play(Pick(library != null ? library.impact : null), 0.9f, _fallbackCraft);
        public void PlayWater() => Play(Pick(library != null ? library.water : null), 0.75f, _fallbackStep);

        private void Play(AudioClip clip, float volume, AudioClip fallback)
        {
            if (clip == null) clip = fallback;
            if (clip == null) return;
            float min = library != null ? library.pitchMin : 0.94f;
            float max = library != null ? library.pitchMax : 1.06f;
            _effects.pitch = Random.Range(min, max);
            _effects.PlayOneShot(clip, volume);
        }

        private static AudioClip Pick(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[Random.Range(0, clips.Length)];
        }

        private void RouteEvents(bool subscribe)
        {
            if (subscribe)
            {
                EventManager.Subscribe(GameEvents.ItemCollected, OnPickup);
                EventManager.Subscribe(GameEvents.ItemConsumed, OnPickup);
                EventManager.Subscribe(GameEvents.ItemCrafted, OnCrafted);
                EventManager.Subscribe(GameEvents.TileDestroyed, OnImpact);
                EventManager.Subscribe(GameEvents.AnimalKilled, OnImpact);
                EventManager.Subscribe(GameEvents.PlayerEnteredWater, OnWater);
            }
            else
            {
                EventManager.Unsubscribe(GameEvents.ItemCollected, OnPickup);
                EventManager.Unsubscribe(GameEvents.ItemConsumed, OnPickup);
                EventManager.Unsubscribe(GameEvents.ItemCrafted, OnCrafted);
                EventManager.Unsubscribe(GameEvents.TileDestroyed, OnImpact);
                EventManager.Unsubscribe(GameEvents.AnimalKilled, OnImpact);
                EventManager.Unsubscribe(GameEvents.PlayerEnteredWater, OnWater);
            }
        }
        private void OnPickup(object _) => PlayPickup();
        private void OnCrafted(object _) => PlayCraft();
        private void OnImpact(object _) => PlayImpact();
        private void OnWater(object _) => PlayWater();

        private void OnDestroy()
        {
            if (Instance == this) { RouteEvents(false); Instance = null; }
        }

        private AudioClip Fallback(string name, float frequency, float duration)
        {
            int samples = Mathf.CeilToInt(44100f * duration);
            var clip = AudioClip.Create(name, samples, 1, 44100, false);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float envelope = 1f - i / (float)samples;
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / 44100f) * envelope * 0.18f;
            }
            clip.SetData(data, 0);
            return clip;
        }

        private void Start()
        {
            _fallbackStep = Fallback("StoneStep", 115f, 0.07f);
            _fallbackPickup = Fallback("PickupChime", 660f, 0.12f);
            _fallbackCraft = Fallback("CraftTap", 220f, 0.16f);
        }
    }
}
