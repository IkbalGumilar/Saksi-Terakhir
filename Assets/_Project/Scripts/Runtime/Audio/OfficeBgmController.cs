using System.Collections;
using SaksiTerakhir.Persistence;
using UnityEngine;

namespace SaksiTerakhir.Audio
{
    /// <summary>
    /// Controls background music transitions specifically for the Office area:
    /// - Plays BGM_Sunny_Morning_Office while the main menu is active.
    /// - Smoothly transitions to BGM_Sunny_Morning_Office-2 when gameplay begins in the office.
    /// - Stops/fades out office BGM when leaving the office scene to prevent bleeding into other scenes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfficeBgmController : MonoBehaviour
    {
        [Header("Office BGM Clips")]
        [SerializeField] private AudioClip menuMusicClip;
        [SerializeField] private AudioClip officeGameplayMusicClip;

        [Header("Transition Settings")]
        [SerializeField] private float crossfadeDuration = 1.5f;

        private bool isOfficeActive;
        private bool isGameplayMusicActive;

        public AudioClip MenuMusicClip => menuMusicClip;
        public AudioClip OfficeGameplayMusicClip => officeGameplayMusicClip;

        private void Awake()
        {
#if UNITY_EDITOR
            EnsureEditorClipsLoaded();
#endif
        }

        private void OnEnable()
        {
            isOfficeActive = true;
            GameSessionController.MenuOpened -= OnMenuOpened;
            GameSessionController.MenuOpened += OnMenuOpened;

            GameSessionController.GameplayEntered -= OnGameplayEntered;
            GameSessionController.GameplayEntered += OnGameplayEntered;
        }

        private void OnDisable()
        {
            isOfficeActive = false;
            GameSessionController.MenuOpened -= OnMenuOpened;
            GameSessionController.GameplayEntered -= OnGameplayEntered;
        }

        private void Start()
        {
            StartCoroutine(InitializeMusicState());
        }

        private IEnumerator InitializeMusicState()
        {
            // Give 1 frame for GameSessionController.Bootstrap to initialize MenuOpen
            yield return null;

            if (!isOfficeActive) yield break;

            GameSessionController session = FindFirstObjectByType<GameSessionController>();
            bool isMenuOpen = session != null && session.MenuOpen;

            if (isMenuOpen)
            {
                PlayMenuMusic();
            }
            else
            {
                PlayOfficeGameplayMusic();
            }
        }

        private void OnMenuOpened()
        {
            if (!isOfficeActive) return;
            PlayMenuMusic();
        }

        private void OnGameplayEntered()
        {
            if (!isOfficeActive) return;
            PlayOfficeGameplayMusic();
        }

        public void PlayMenuMusic()
        {
            if (menuMusicClip == null)
            {
#if UNITY_EDITOR
                EnsureEditorClipsLoaded();
#endif
            }

            if (menuMusicClip != null)
            {
                isGameplayMusicActive = false;
                AudioManager.Instance.PlayMusic(menuMusicClip, loop: true, fadeDuration: crossfadeDuration);
            }
        }

        public void PlayOfficeGameplayMusic()
        {
            if (officeGameplayMusicClip == null)
            {
#if UNITY_EDITOR
                EnsureEditorClipsLoaded();
#endif
            }

            if (officeGameplayMusicClip != null)
            {
                isGameplayMusicActive = true;
                AudioManager.Instance.PlayMusic(officeGameplayMusicClip, loop: true, fadeDuration: crossfadeDuration);
            }
        }

        private void OnDestroy()
        {
            isOfficeActive = false;
            // Stop office music when leaving the office scene so the next scene can play its own audio
            if (AudioManager.HasInstance)
            {
                AudioManager.Instance.StopMusic(fadeDuration: 1.0f);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureEditorClipsLoaded();
        }

        public void EnsureEditorClipsLoaded()
        {
            if (menuMusicClip == null)
            {
                menuMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/_Project/Audio/Ambience/BGM_Sunny_Morning_Office.wav");
            }
            if (officeGameplayMusicClip == null)
            {
                officeGameplayMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/_Project/Audio/Ambience/BGM_Sunny_Morning_Office-2.wav");
            }
        }
#endif
    }
}
