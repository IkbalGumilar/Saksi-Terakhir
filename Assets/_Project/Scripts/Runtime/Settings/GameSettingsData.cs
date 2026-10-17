using System;
using UnityEngine;

namespace SaksiTerakhir.Settings
{
    public enum SprintMode
    {
        Hold,
        Toggle
    }

    public enum CameraSpeedSetting
    {
        Slow,
        Normal,
        Fast
    }

    public enum ScreenModeSetting
    {
        Fullscreen,
        Windowed
    }

    public enum GraphicsPreset
    {
        Low,
        Medium,
        High
    }

    public enum ShadowQualitySetting
    {
        Off,
        Low,
        High
    }

    public enum ShadowDistanceSetting
    {
        Near15m,
        Medium25m,
        Far40m
    }

    public enum TextureQualitySetting
    {
        Low,
        Medium,
        High
    }

    public enum AntiAliasingSetting
    {
        Off,
        FXAA,
        SMAA
    }

    [Serializable]
    public sealed class GameSettingsData
    {
        // Kontrol
        public string inputDevice = "Keyboard & Mouse";
        public float mouseSensitivity = 50f; // 1 - 100
        public SprintMode sprintMode = SprintMode.Hold;
        public bool gamepadVibration = true;

        // Kamera
        public float horizontalSensitivity = 50f; // 1 - 100
        public float verticalSensitivity = 50f; // 1 - 100
        public bool invertVerticalAxis = false;
        public float fieldOfView = 75f; // 60 - 100
        public CameraSpeedSetting cameraSpeed = CameraSpeedSetting.Normal;

        // Permainan
        public bool showMainQuest = true;
        public bool showTrackedQuest = true;
        public bool interactionPrompt = true;

        // Tampilan
        public ScreenModeSetting screenMode = ScreenModeSetting.Fullscreen;
        public int resolutionIndex = 0; // 0 = 1920x1080, 1 = 1600x900, 2 = 1366x768, 3 = 1280x720
        public float brightness = 50f; // 0 - 100
        public bool vSync = true;

        // Grafis
        public GraphicsPreset qualityPreset = GraphicsPreset.Medium;
        public ShadowQualitySetting shadowQuality = ShadowQualitySetting.Low;
        public ShadowDistanceSetting shadowDistance = ShadowDistanceSetting.Medium25m;
        public TextureQualitySetting textureQuality = TextureQualitySetting.Medium;
        public AntiAliasingSetting antiAliasing = AntiAliasingSetting.FXAA;
        public int renderScalePercent = 100; // 70, 85, 100
        public int targetFrameRate = 60; // 30, 60, -1

        // Audio (saat ini disimpan untuk masa depan)
        public float masterVolume = 80f;
        public float musicVolume = 60f;
        public float sfxVolume = 90f;
        public float dialogueVolume = 100f;

        // Bahasa / Subtitle
        public SaksiTerakhir.Localization.GameLanguage language = SaksiTerakhir.Localization.GameLanguage.Indonesian;
        public bool showSpeakerName = true;
        public float textSpeed = 35f; // 10 - 100 karakter / detik
        public bool autoAdvance = true;
        public float autoAdvanceDelay = 1.5f; // 0.1 - 5.0 detik
        public bool textAnimation = true; // typewriter effect

        public GameSettingsData Clone()
        {
            return (GameSettingsData)MemberwiseClone();
        }

        public void ResetToDefaults()
        {
            var def = new GameSettingsData();
            inputDevice = def.inputDevice;
            mouseSensitivity = def.mouseSensitivity;
            sprintMode = def.sprintMode;
            gamepadVibration = def.gamepadVibration;

            horizontalSensitivity = def.horizontalSensitivity;
            verticalSensitivity = def.verticalSensitivity;
            invertVerticalAxis = def.invertVerticalAxis;
            fieldOfView = def.fieldOfView;
            cameraSpeed = def.cameraSpeed;

            showMainQuest = def.showMainQuest;
            showTrackedQuest = def.showTrackedQuest;
            interactionPrompt = def.interactionPrompt;

            screenMode = def.screenMode;
            resolutionIndex = def.resolutionIndex;
            brightness = def.brightness;
            vSync = def.vSync;

            qualityPreset = def.qualityPreset;
            shadowQuality = def.shadowQuality;
            shadowDistance = def.shadowDistance;
            textureQuality = def.textureQuality;
            antiAliasing = def.antiAliasing;
            renderScalePercent = def.renderScalePercent;
            targetFrameRate = def.targetFrameRate;

            masterVolume = def.masterVolume;
            musicVolume = def.musicVolume;
            sfxVolume = def.sfxVolume;
            dialogueVolume = def.dialogueVolume;

            language = def.language;
            showSpeakerName = def.showSpeakerName;
            textSpeed = def.textSpeed;
            autoAdvance = def.autoAdvance;
            autoAdvanceDelay = def.autoAdvanceDelay;
            textAnimation = def.textAnimation;
        }
    }
}
