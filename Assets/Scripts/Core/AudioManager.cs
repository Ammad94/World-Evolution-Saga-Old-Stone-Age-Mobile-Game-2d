using System.Collections;
using UnityEngine;

namespace PrehistoricSurvival.Core
{
    /// <summary>
    /// Centralized audio playback with four mixer buses (Music / SFX / Ambience / UI),
    /// material-aware footsteps, 3D creature voices, and resource-based clip loading.
    /// Clips live under <c>Assets/Resources/Audio/</c>:
    ///   Audio/music/&lt;track&gt;, Audio/ambience/&lt;loop&gt;, Audio/sfx/&lt;effect&gt;.
    /// Volumes persist to PlayerPrefs and are surfaced by the settings menus.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }
        public AudioLibrary library;
        public bool playAmbientLoop = true;

        private AudioSource _sfx;
        private AudioSource _music;
        private AudioSource _ambience;
        private AudioSource _ui;
        private AudioSource[] _voice3d = new AudioSource[4];
        private int _voiceIndex;
        private AudioClip _fallbackStep;
        private AudioClip _fallbackPickup;
        private AudioClip _fallbackCraft;
        private float _stepClock;

        // Bus volumes (persisted)
        private const string KEY_MUSIC = "vol_music";
        private const string KEY_SFX = "vol_sfx";
        private const string KEY_AMB = "vol_amb";
        private const string KEY_UI = "vol_ui";
        private float _musicVol = 0.55f, _sfxVol = 0.8f, _ambVol = 0.65f, _uiVol = 0.9f;

        // Footstep material detection
        private string _stepMaterial = "dirt";
        private float _materialClock;

        public float MusicVolume => _musicVol;
        public float SfxVolume => _sfxVol;
        public float AmbienceVolume => _ambVol;
        public float UiVolume => _uiVol;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            library = library != null ? library : AudioLibrary.Instance;

            _musicVol = PlayerPrefs.GetFloat(KEY_MUSIC, 0.55f);
            _sfxVol = PlayerPrefs.GetFloat(KEY_SFX, 0.8f);
            _ambVol = PlayerPrefs.GetFloat(KEY_AMB, 0.65f);
            _uiVol = PlayerPrefs.GetFloat(KEY_UI, 0.9f);

            _sfx = MakeSource("SFX", false, _sfxVol);
            _music = MakeSource("Music", true, _musicVol * (library != null ? library.musicVolume / 0.35f : 1f));
            _ambience = MakeSource("Ambience", true, _ambVol);
            _ui = MakeSource("UI", false, _uiVol);
            for (int i = 0; i < _voice3d.Length; i++)
            {
                var s = MakeSource("Voice3D_" + i, false, 1f);
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 4f;
                s.maxDistance = 45f;
                _voice3d[i] = s;
            }
            RouteEvents(true);
            if (playAmbientLoop) PlayMusic("music/menu_theme", 0f);
        }

        private void Start()
        {
            _fallbackStep = Fallback("StoneStep", 115f, 0.07f);
            _fallbackPickup = Fallback("PickupChime", 660f, 0.12f);
            _fallbackCraft = Fallback("CraftTap", 220f, 0.16f);
        }

        private void OnDestroy()
        {
            if (Instance == this) { RouteEvents(false); Instance = null; }
        }

        // ------------------------------------------------------------------
        // Clip loading
        // ------------------------------------------------------------------
        public static AudioClip Clip(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;
            return Resources.Load<AudioClip>("Audio/" + resourcePath);
        }

        private static AudioClip PickClip(string prefix, int variants)
        {
            int i = Random.Range(0, variants);
            return Clip("sfx/" + prefix + "_" + i);
        }

        // ------------------------------------------------------------------
        // Public one-shots
        // ------------------------------------------------------------------
        public void PlayUI() => PlayOnBus(_ui, PickClip("ui_click", 1), 0.9f, _fallbackPickup);
        public void PlayBack() => PlayOnBus(_ui, Clip("sfx/ui_back"), 0.9f, _fallbackPickup);
        public void PlayHover() => PlayOnBus(_ui, Clip("sfx/ui_hover"), 0.5f, null);
        public void PlayError() => PlayOnBus(_ui, Clip("sfx/ui_error"), 0.8f, null);
        public void PlayPage() => PlayOnBus(_ui, Clip("sfx/ui_page"), 0.7f, null);

        public void PlayFootstep() => PlayOnBus(_sfx, FootstepForMaterial(_stepMaterial), 0.5f, _fallbackStep);
        public void PlayFootstep(string material) => PlayOnBus(_sfx, FootstepForMaterial(material), 0.5f, _fallbackStep);

        public void PlayPickup(string itemId = null)
        {
            AudioClip clip = null;
            if (!string.IsNullOrEmpty(itemId)) clip = Clip("sfx/pickup_" + itemId);
            if (clip == null) clip = PickClip("pickup_generic", 3);
            PlayOnBus(_sfx, clip, 0.9f, _fallbackPickup);
        }
        public void PlayCraft() => PlayOnBus(_sfx, Clip("sfx/craft_hammer_" + Random.Range(0, 3)), 0.9f, _fallbackCraft);
        public void PlayCraftComplete() => PlayOnBus(_ui, Clip("sfx/craft_complete"), 0.8f, null);
        public void PlayImpact() => PlayOnBus(_sfx, PickClip("hit_stone", 2), 0.9f, _fallbackCraft);
        public void PlayWater() => PlayOnBus(_sfx, Clip("sfx/splash_small"), 0.75f, _fallbackStep);
        public void Play(string sfxName, float volume = 1f)
        {
            if (string.IsNullOrEmpty(sfxName)) return;
            PlayOnBus(_sfx, Clip("sfx/" + sfxName), volume, null);
        }
        public void PlayUiSound(string sfxName, float volume = 1f)
            => PlayOnBus(_ui, Clip("sfx/" + sfxName), volume, null);

        /// <summary>3D positional voice (creature call, body fall, splash...).</summary>
        public void PlayVoiceAt(string sfxName, Vector3 position, float volume = 1f)
        {
            var clip = Clip("sfx/" + sfxName);
            if (clip == null) return;
            var src = _voice3d[_voiceIndex];
            _voiceIndex = (_voiceIndex + 1) % _voice3d.Length;
            src.transform.position = position;
            src.pitch = Random.Range(0.92f, 1.08f);
            src.PlayOneShot(clip, volume);
        }

        // ------------------------------------------------------------------
        // Music
        // ------------------------------------------------------------------
        private string _currentTrack;
        public string CurrentTrack => _currentTrack;

        public void PlayMusic(string trackPath, float fade = 1.5f)
        {
            if (_currentTrack == trackPath) return;
            var clip = Clip(trackPath);
            if (clip == null) return;
            _currentTrack = trackPath;
            if (_musicFade != null) StopCoroutine(_musicFade);
            _musicFade = StartCoroutine(FadeToTrack(clip, fade));
        }

        public void StopMusic(float fade = 1f)
        {
            _currentTrack = null;
            if (_musicFade != null) StopCoroutine(_musicFade);
            StartCoroutine(FadeOut(_music, fade));
        }

        private Coroutine _musicFade;

        private IEnumerator FadeToTrack(AudioClip clip, float fade)
        {
            float start = _music.volume;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.05f, fade))
            {
                _music.volume = Mathf.Lerp(start, 0f, t);
                yield return null;
            }
            _music.clip = clip;
            _music.Play();
            float target = _musicVol * (library != null ? library.musicVolume / 0.35f : 1f);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.05f, fade))
            {
                _music.volume = Mathf.Lerp(0f, target, t);
                yield return null;
            }
            _music.volume = target;
        }

        private IEnumerator FadeOut(AudioSource src, float fade)
        {
            float start = src.volume;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.05f, fade))
            {
                src.volume = Mathf.Lerp(start, 0f, t);
                yield return null;
            }
            src.Stop();
        }

        // ------------------------------------------------------------------
        // Ambience (biome loops + weather layer)
        // ------------------------------------------------------------------
        private Coroutine _ambRoutine;
        public void CrossfadeAmbience(string ambiencePath, float fade = 3f)
        {
            if (_ambRoutine != null) StopCoroutine(_ambRoutine);
            _ambRoutine = StartCoroutine(FadeAmbience(Clip(ambiencePath), fade));
        }

        private IEnumerator FadeAmbience(AudioClip clip, float fade)
        {
            if (_ambience.isPlaying)
            {
                float start = _ambience.volume;
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / fade)
                { _ambience.volume = Mathf.Lerp(start, 0f, t); yield return null; }
            }
            _ambience.clip = clip;
            if (clip != null) _ambience.Play();
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / fade)
            { _ambience.volume = Mathf.Lerp(0f, _ambVol, t); yield return null; }
            _ambience.volume = _ambVol;
        }

        // ------------------------------------------------------------------
        // Bus volumes (settings API)
        // ------------------------------------------------------------------
        public void SetBusVolume(string bus, float v)
        {
            v = Mathf.Clamp01(v);
            switch (bus)
            {
                case "music":
                    _musicVol = v;
                    if (!_musicTweening) _music.volume = v * (library != null ? library.musicVolume / 0.35f : 1f);
                    PlayerPrefs.SetFloat(KEY_MUSIC, v);
                    break;
                case "sfx": _sfxVol = v; _sfx.volume = v; PlayerPrefs.SetFloat(KEY_SFX, v); break;
                case "ambience": _ambVol = v; _ambience.volume = v; PlayerPrefs.SetFloat(KEY_AMB, v); break;
                case "ui": _uiVol = v; _ui.volume = v; PlayerPrefs.SetFloat(KEY_UI, v); break;
            }
        }
        private bool _musicTweening;

        // ------------------------------------------------------------------
        // Internals
        // ------------------------------------------------------------------
        private void Update()
        {
            // Auto footstep cadence + material from the ground under the player.
            var player = GameObject.FindGameObjectWithTag("Player");
            var controller = player != null ? player.GetComponent<PrehistoricSurvival.Player.PlayerController>() : null;
            if (controller != null && controller.IsMoving)
            {
                _stepClock += Time.unscaledDeltaTime;
                float cadence = controller.CurrentSpeed > 6.5f ? 0.3f : 0.42f;
                if (_stepClock >= cadence) { _stepClock = 0f; PlayFootstep(); }
            }
            else _stepClock = 0f;

            _materialClock -= Time.unscaledDeltaTime;
            if (_materialClock <= 0f && player != null)
            {
                _materialClock = 0.5f;
                _stepMaterial = DetectStepMaterial(player.transform.position);
            }
        }

        private static string DetectStepMaterial(Vector3 pos)
        {
            var swimming = pos;
            var chunkMgr = PrehistoricSurvival.World.ChunkManager.Instance;
            if (chunkMgr == null) return "dirt";
            var biome = chunkMgr.GetBiomeAt(swimming);
            var sample = PrehistoricSurvival.World.WorldMap.Instance != null
                ? PrehistoricSurvival.World.WorldMap.Instance.SampleWorld(pos) : default;
            if (sample.isWater || sample.isRiver) return "shallow";
            switch (biome)
            {
                case PrehistoricSurvival.World.BiomeType.Glacier:
                case PrehistoricSurvival.World.BiomeType.SnowPeak:
                case PrehistoricSurvival.World.BiomeType.Tundra: return "snow";
                case PrehistoricSurvival.World.BiomeType.Desert:
                case PrehistoricSurvival.World.BiomeType.Beach: return "sand";
                case PrehistoricSurvival.World.BiomeType.Mountain: return "stone";
                case PrehistoricSurvival.World.BiomeType.Swamp: return "mud";
                case PrehistoricSurvival.World.BiomeType.Taiga:
                case PrehistoricSurvival.World.BiomeType.TemperateForest:
                case PrehistoricSurvival.World.BiomeType.TropicalRainforest: return "grass";
                case PrehistoricSurvival.World.BiomeType.Grassland:
                case PrehistoricSurvival.World.BiomeType.Savannah:
                case PrehistoricSurvival.World.BiomeType.Steppe: return "grass";
                default: return "dirt";
            }
        }

        private AudioClip FootstepForMaterial(string material)
        {
            var clip = PickClip("step_" + material, 3);
            return clip != null ? clip : _fallbackStep;
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

        private void PlayOnBus(AudioSource bus, AudioClip clip, float volume, AudioClip fallback)
        {
            if (clip == null) clip = fallback;
            if (clip == null) return;
            float min = library != null ? library.pitchMin : 0.94f;
            float max = library != null ? library.pitchMax : 1.06f;
            bus.pitch = Random.Range(min, max);
            bus.PlayOneShot(clip, volume);
        }

        private void RouteEvents(bool subscribe)
        {
            if (subscribe)
            {
                EventManager.Subscribe(GameEvents.ItemCollected, OnPickup);
                EventManager.Subscribe(GameEvents.ItemConsumed, OnPickup);
                EventManager.Subscribe(GameEvents.ItemCrafted, OnCrafted);
                EventManager.Subscribe(GameEvents.TileDestroyed, OnImpact);
                EventManager.Subscribe(GameEvents.AnimalKilled, OnAnimalKilled);
                EventManager.Subscribe(GameEvents.PlayerEnteredWater, OnWater);
                EventManager.Subscribe(GameEvents.PlayerExitedWater, OnWater);
            }
            else
            {
                EventManager.Unsubscribe(GameEvents.ItemCollected, OnPickup);
                EventManager.Unsubscribe(GameEvents.ItemConsumed, OnPickup);
                EventManager.Unsubscribe(GameEvents.ItemCrafted, OnCrafted);
                EventManager.Unsubscribe(GameEvents.TileDestroyed, OnImpact);
                EventManager.Unsubscribe(GameEvents.AnimalKilled, OnAnimalKilled);
                EventManager.Unsubscribe(GameEvents.PlayerEnteredWater, OnWater);
                EventManager.Unsubscribe(GameEvents.PlayerExitedWater, OnWater);
            }
        }
        private void OnPickup(object payload)
        {
            string itemId = payload as string;
            PlayPickup(itemId);
        }
        private void OnCrafted(object _) { PlayCraft(); PlayUiSound("craft_complete", 0.6f); }
        private void OnImpact(object _) => PlayImpact();
        private void OnWater(object _) => PlayWater();
        private void OnAnimalKilled(object payload)
        {
            var ai = payload as PrehistoricSurvival.AI.AnimalAI;
            if (ai != null)
            {
                PlayVoiceAt(VoiceNameFor(ai.animalName, "death"), ai.transform.position, 0.9f);
                Play("body_fall_" + Random.Range(0, 2), 0.7f);
            }
            PlayImpact();
        }

        /// <summary>Map an animal display name + call type to an sfx name.</summary>
        public static string VoiceNameFor(string animalName, string kind)
        {
            animalName = (animalName ?? "").ToLowerInvariant();
            if (animalName.Contains("mammoth")) return "mammoth_" + (kind == "death" ? "death" : kind == "growl" ? "growl" : "call_0");
            if (animalName.Contains("sabertooth")) return "sabertooth_" + kind;
            if (animalName.Contains("bear")) return "bear_" + kind;
            if (animalName.Contains("bison")) return "bison_" + kind;
            if (animalName.Contains("wolf")) return "wolf_" + kind;
            if (animalName.Contains("lion")) return "lion_" + kind;
            if (animalName.Contains("boar")) return "boar_" + kind;
            if (animalName.Contains("elk")) return "elk_" + kind;
            if (animalName.Contains("reindeer")) return "reindeer_bellow";
            if (animalName.Contains("hyena")) return "hyena_0";
            if (animalName.Contains("rhino")) return "rhino_snort";
            if (animalName.Contains("musk")) return "muskox_grunt";
            if (animalName.Contains("hare")) return "hare_squeal";
            if (animalName.Contains("ptarmigan")) return "ptarmigan_0";
            if (animalName.Contains("auk")) return "auk_0";
            return "hit_flesh_0";
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
    }
}
