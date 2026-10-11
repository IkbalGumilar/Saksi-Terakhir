using System;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Player;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class PlayerIdleBreathingTests
    {
        private GameObject root;
        private Transform chest;
        private Component breathing;
        private Type breathingType;
        private PlayerController player;

        [SetUp]
        public void SetUp()
        {
            breathingType = typeof(PlayerController).Assembly.GetType(
                "SaksiTerakhir.Player.PlayerIdleBreathing");
            Assert.That(breathingType, Is.Not.Null,
                "The player needs a separate procedural idle breathing component.");
            root = new GameObject("Idle Breathing Test Player");
            player = root.AddComponent<PlayerController>();
            breathing = root.AddComponent(breathingType);
            Invoke("Awake");
            chest = new GameObject("Chest With Authored Pose").transform;
            chest.SetParent(root.transform, false);
            chest.localRotation = Quaternion.Euler(21f, -14f, 8f);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void IdleFallbackMovesOnlyTheCameraOffsetWithoutAHumanoid()
        {
            Vector3 originalPosition = root.transform.position;
            Quaternion originalRotation = root.transform.rotation;
            Vector3 originalScale = root.transform.localScale;
            bool changed = false;
            for (int i = 0; i < 300; i++)
            {
                Step(1f / 60f);
                float offset = EyeOffset();
                Assert.That(Mathf.Abs(offset), Is.LessThanOrEqualTo(0.0081f));
                changed |= Mathf.Abs(offset) > 0.001f;
            }
            Assert.That(changed, Is.True, "Idle breathing should be visible without a mapped humanoid.");
            Assert.That(root.transform.position, Is.EqualTo(originalPosition));
            Assert.That(Quaternion.Angle(root.transform.rotation, originalRotation), Is.LessThan(0.01f));
            Assert.That(root.transform.localScale, Is.EqualTo(originalScale));
        }

        [Test]
        public void RepeatedFramesWithAFrozenAnimatorDoNotAccumulateBoneRotation()
        {
            SetField("breathingBone", chest);
            Quaternion authored = chest.localRotation;
            bool chestMoved = false;
            for (int i = 0; i < 900; i++)
            {
                Step(1f / 60f);
                float angle = Quaternion.Angle(authored, chest.localRotation);
                chestMoved |= angle > 0.05f;
                Assert.That(angle, Is.LessThan(0.5f),
                    "A frozen or culled Animator must not accumulate additive breathing offsets.");
            }
            Assert.That(chestMoved, Is.True, "Mapped chest motion should accompany the camera breath.");
            Invoke("OnDisable");
            Assert.That(Quaternion.Angle(authored, chest.localRotation), Is.LessThan(0.01f));
            Assert.That(EyeOffset(), Is.Zero);
        }

        [Test]
        public void ManualMotionStopsCameraBreathingAndRestoresTheAuthoredBonePose()
        {
            SetField("breathingBone", chest);
            Quaternion authored = chest.localRotation;
            for (int i = 0; i < 60; i++) Step(1f / 60f);
            Assert.That(Mathf.Abs(EyeOffset()), Is.GreaterThan(0.001f));
            typeof(PlayerController).GetField("horizontalVelocity", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(player, Vector3.forward);
            Step(1f / 60f);
            Assert.That(EyeOffset(), Is.Zero);
            Assert.That(Quaternion.Angle(authored, chest.localRotation), Is.LessThan(0.01f));
        }

        [Test]
        public void AutomaticMotionStopsBreathingEvenBeforeTheManualControllerUpdates()
        {
            for (int i = 0; i < 60; i++) Step(1f / 60f);
            player.SetCinematicMovementLocked(true);
            player.SetCinematicVelocity(Vector3.right * 2f);
            Step(1f / 60f);
            Assert.That(EyeOffset(), Is.Zero);
        }

        [Test]
        public void MotionAppliedDirectlyToTheRootAlsoStopsIdleBreathing()
        {
            for (int i = 0; i < 60; i++) Step(1f / 60f);
            root.transform.position += Vector3.forward * 0.05f;
            Step(1f / 60f);
            Assert.That(EyeOffset(), Is.Zero,
                "Scripted motion must not use idle sway simply because controller speed is zero.");
        }

        [Test]
        public void PausedMenuBreathesEvenIfACinematicVelocityWasPreviouslyRecorded()
        {
            player.SetCinematicMovementLocked(true);
            player.SetCinematicVelocity(Vector3.forward * 2f);
            player.SetUiMovementLocked(true);
            for (int i = 0; i < 60; i++) Step(1f / 60f);
            Assert.That(Mathf.Abs(EyeOffset()), Is.GreaterThan(0.001f),
                "A frozen main-menu player should breathe despite a saved cinematic velocity.");
        }

        [Test]
        public void RemovingBreathingPreservesANewerPoseWrittenByAnotherAnimationOwner()
        {
            SetField("breathingBone", chest);
            for (int i = 0; i < 60; i++) Step(1f / 60f);
            Quaternion newerPose = Quaternion.Euler(-38f, 4f, 12f);
            chest.localRotation = newerPose;
            Invoke("OnDisable");
            Assert.That(Quaternion.Angle(chest.localRotation, newerPose), Is.LessThan(0.01f),
                "Disabling the effect must not replace a newer seated or animated pose.");
        }

        [Test]
        public void EachNewAnimatedPoseRemainsTheBaseOfTheSubtleBreath()
        {
            SetField("breathingBone", chest);
            Quaternion seatedPose = Quaternion.Euler(55f, 12f, -9f);
            for (int i = 0; i < 60; i++) Step(1f / 60f);
            chest.localRotation = seatedPose;
            Step(1f / 60f);
            Assert.That(Quaternion.Angle(chest.localRotation, seatedPose), Is.LessThan(0.5f));
            Invoke("OnDisable");
            Assert.That(Quaternion.Angle(chest.localRotation, seatedPose), Is.LessThan(0.01f));
        }

        private float EyeOffset() => (float)breathingType.GetProperty("EyeOffset").GetValue(breathing);

        private void Step(float deltaTime) => breathingType.GetMethod(
            "StepBreathing", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(breathing, new object[] { deltaTime });

        private void Invoke(string name) => breathingType.GetMethod(
            name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(breathing, null);

        private void SetField(string name, object value) => breathingType.GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(breathing, value);
    }
}
