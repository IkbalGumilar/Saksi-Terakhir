using System;
using System.Collections;
using SaksiTerakhir.Settings;
using UnityEngine;

namespace SaksiTerakhir.Audio
{
    /// <summary>
    /// Central manager for background music (BGM) playback, crossfading, and settings-driven volume.
    /// Completely zero-allocation in hot paths.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public sealed class AudioManager : MonoBehaviour
    {
        private static AudioManager instance;
        private static bool isQuitting;

        public static AudioManager Instance => EnsureInstance();
        public static bool HasInstance => instance != null;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource sourceA;
        [SerializeField] private AudioSource sourceB;

        private AudioSource activeSource;
        private AudioSource fadingOutSource;
        private Coroutine crossfadeRoutine;
        private Coroutine stopRoutine;

        private AudioClip currentClip;
        private float currentFadeNormalized = 1f;

        public static AudioManager EnsureInstance()
        {
            if (isQuitting) return null;
            if (instance != null) return instance;

            instance = FindFirstObjectByType<AudioManager>();
            if (instance == null)
            {
                GameObject root = new GameObject("[AudioManager]");
                instance = root.AddComponent<AudioManager>();
                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(root);
                }
            }
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (Application.isPlaying && transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            EnsureSources();
            ApplySettingsVolume();
        }

        private void OnEnable()
        {
            GameSettingsManager.SettingsChanged -= OnSettingsChanged;
            GameSettingsManager.SettingsChanged += OnSettingsChanged;
            ApplySettingsVolume();
        }

        private void OnDisable()
        {
            GameSettingsManager.SettingsChanged -= OnSettingsChanged;
        }

        private void OnApplicationQuit()
        {
            isQuitting = true;
        }

        private void EnsureSources()
        {
            if (sourceA == null)
            {
                GameObject childA = new GameObject("MusicSource_A");
                childA.transform.SetParent(transform, false);
                sourceA = childA.AddComponent<AudioSource>();
                ConfigureSource(sourceA);
            }
            if (sourceB == null)
            {
                GameObject childB = new GameObject("MusicSource_B");
                childB.transform.SetParent(transform, false);
                sourceB = childB.AddComponent<AudioSource>();
                ConfigureSource(sourceB);
            }

            if (activeSource == null)
            {
                activeSource = sourceA;
            }
        }

        private static void ConfigureSource(AudioSource src)
        {
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f; // 2D Sound for background music
            src.volume = 0f;
            src.ignoreListenerPause = true;
        }

        private float TargetMusicVolume
        {
            get
            {
                GameSettingsData settings = GameSettingsManager.Current;
                if (settings == null) return 0.6f;
                return Mathf.Clamp01(settings.musicVolume / 100f);
            }
        }

        private void OnSettingsChanged(GameSettingsData settings)
        {
            ApplySettingsVolume();
        }

        public void ApplySettingsVolume()
        {
            GameSettingsData settings = GameSettingsManager.Current;
            if (settings != null)
            {
                AudioListener.volume = Mathf.Clamp01(settings.masterVolume / 100f);
            }

            float target = TargetMusicVolume;
            if (crossfadeRoutine == null && stopRoutine == null && activeSource != null && activeSource.isPlaying)
            {
                activeSource.volume = target;
            }
            else if (activeSource != null && crossfadeRoutine != null)
            {
                activeSource.volume = target * currentFadeNormalized;
                if (fadingOutSource != null)
                {
                    fadingOutSource.volume = target * (1f - currentFadeNormalized);
                }
            }
        }

        /// <summary>
        /// Plays a music track with a smooth crossfade. If the same track is already playing,
        /// it continues smoothly without restarting.
        /// </summary>
        public void PlayMusic(AudioClip clip, bool loop = true, float fadeDuration = 1.0f)
        {
            if (clip == null) return;
            EnsureSources();

            // If same clip is already active and playing
            if (currentClip == clip && activeSource != null && activeSource.isPlaying && stopRoutine == null)
            {
                activeSource.loop = loop;
                return;
            }

            if (stopRoutine != null)
            {
                StopCoroutine(stopRoutine);
                stopRoutine = null;
            }

            if (crossfadeRoutine != null)
            {
                StopCoroutine(crossfadeRoutine);
                crossfadeRoutine = null;
            }

            // Swap active and fadingOut sources
            fadingOutSource = activeSource;
            activeSource = (activeSource == sourceA) ? sourceB : sourceA;

            currentClip = clip;
            activeSource.clip = clip;
            activeSource.loop = loop;
            activeSource.volume = 0f;
            activeSource.Play();

            if (fadeDuration <= 0f)
            {
                activeSource.volume = TargetMusicVolume;
                if (fadingOutSource != null)
                {
                    fadingOutSource.Stop();
                    fadingOutSource.volume = 0f;
                }
                currentFadeNormalized = 1f;
            }
            else
            {
                crossfadeRoutine = StartCoroutine(CrossfadeRoutine(fadeDuration));
            }
        }

        public void StopMusic(float fadeDuration = 1.0f)
        {
            if (crossfadeRoutine != null)
            {
                StopCoroutine(crossfadeRoutine);
                crossfadeRoutine = null;
            }
            if (stopRoutine != null)
            {
                StopCoroutine(stopRoutine);
                stopRoutine = null;
            }

            currentClip = null;
            if (activeSource == null || !activeSource.isPlaying) return;

            if (fadeDuration <= 0f)
            {
                activeSource.Stop();
                activeSource.volume = 0f;
                if (sourceA != null) { sourceA.Stop(); sourceA.volume = 0f; }
                if (sourceB != null) { sourceB.Stop(); sourceB.volume = 0f; }
            }
            else
            {
                stopRoutine = StartCoroutine(StopMusicRoutine(fadeDuration));
            }
        }

        private IEnumerator CrossfadeRoutine(float duration)
        {
            float elapsed = 0f;
            float startFadeOutVol = fadingOutSource != null ? fadingOutSource.volume : 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                currentFadeNormalized = Mathf.SmoothStep(0f, 1f, t);

                float target = TargetMusicVolume;
                if (activeSource != null)
                {
                    activeSource.volume = target * currentFadeNormalized;
                }
                if (fadingOutSource != null)
                {
                    fadingOutSource.volume = Mathf.Lerp(startFadeOutVol, 0f, currentFadeNormalized);
                }
                yield return null;
            }

            if (activeSource != null)
            {
                activeSource.volume = TargetMusicVolume;
            }
            if (fadingOutSource != null)
            {
                fadingOutSource.Stop();
                fadingOutSource.volume = 0f;
            }

            currentFadeNormalized = 1f;
            crossfadeRoutine = null;
        }

        private IEnumerator StopMusicRoutine(float duration)
        {
            float elapsed = 0f;
            float startVol = activeSource != null ? activeSource.volume : 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float k = Mathf.SmoothStep(0f, 1f, t);

                if (activeSource != null)
                {
                    activeSource.volume = Mathf.Lerp(startVol, 0f, k);
                }
                if (fadingOutSource != null && fadingOutSource.isPlaying)
                {
                    fadingOutSource.volume = Mathf.Lerp(fadingOutSource.volume, 0f, k);
                }
                yield return null;
            }

            if (activeSource != null)
            {
                activeSource.Stop();
                activeSource.volume = 0f;
            }
            if (fadingOutSource != null)
            {
                fadingOutSource.Stop();
                fadingOutSource.volume = 0f;
            }
            stopRoutine = null;
        }
    }
}
