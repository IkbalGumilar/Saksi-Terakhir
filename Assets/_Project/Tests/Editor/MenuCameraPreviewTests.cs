using System;
using System.Reflection;
using NUnit.Framework;
using SaksiTerakhir.Player;
using UnityEngine;

namespace SaksiTerakhir.Tests
{
    public sealed class MenuCameraPreviewTests
    {
        private GameObject fixture;
        private GameObject player;
        private CharacterController body;
        private Transform eye;
        private Mesh roofMesh;
        private CursorLockMode cursorLock;
        private bool cursorVisible;
        private static readonly Vector3 Origin = new Vector3(1000f, 0f, 1000f);

        [SetUp]
        public void SetUp()
        {
            cursorLock = Cursor.lockState;
            cursorVisible = Cursor.visible;
            fixture = new GameObject("Menu Preview Test Fixture");
            player = new GameObject("Player");
            player.transform.SetParent(fixture.transform);
            player.transform.position = Origin;
            body = player.AddComponent<CharacterController>();
            body.height = 1.75f;
            body.center = new Vector3(0f, 0.875f, 0f);
            body.radius = 0.2f;
            body.skinWidth = 0.02f;
            eye = new GameObject("Eye", typeof(Camera)).transform;
            eye.SetParent(player.transform, false);
            eye.localPosition = new Vector3(0f, 1.61f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(fixture);
            if (roofMesh != null) UnityEngine.Object.DestroyImmediate(roofMesh);
            Cursor.lockState = cursorLock;
            Cursor.visible = cursorVisible;
        }

        [Test]
        public void LevelPreviewKeepsSavedPitchAndRestoresExactPoseAndDisabledController()
        {
            PlayerCameraController camera = player.AddComponent<PlayerCameraController>();
            Set(camera, "body", body);
            Set(camera, "yawTransform", player.transform);
            Set(camera, "cameraTransform", eye);
            Quaternion authoredRotation = Quaternion.Euler(0f, 112f, 0f);
            player.transform.rotation = authoredRotation;
            body.enabled = false;
            camera.RestoreSavedPitch(-80f);
            Vector3 originalPosition = player.transform.position;
            Quaternion originalRotation = player.transform.rotation;

            camera.BeginMenuPreview();
            Assert.That(Mathf.Abs(eye.forward.y), Is.LessThan(0.001f));
            Assert.That(camera.SavedPitch, Is.EqualTo(-80f));
            camera.EndMenuPreview(false);
            Assert.That(player.transform.position, Is.EqualTo(originalPosition));
            Assert.That(player.transform.rotation, Is.EqualTo(originalRotation));
            Assert.That(body.enabled, Is.False);
            Assert.That(camera.SavedPitch, Is.EqualTo(-80f));
            Assert.That(eye.forward.y, Is.GreaterThan(0.9f));
            camera.EndMenuPreview(false);
            Assert.That(player.transform.position, Is.EqualTo(originalPosition), "Repeated cleanup must be harmless.");
        }

        [Test]
        public void FinishingPreviewRetainsAdjustedPoseAndLevelPitchWithoutContinuingScan()
        {
            Box("Wall", Origin + new Vector3(0f, 1.6f, 0.65f), new Vector3(5f, 3.2f, 0.15f));
            PlayerCameraController camera = player.AddComponent<PlayerCameraController>();
            Set(camera, "body", body);
            Set(camera, "yawTransform", player.transform);
            Set(camera, "cameraTransform", eye);
            camera.RestoreSavedPitch(-80f);
            camera.SetUiLookLocked(true);
            Physics.SyncTransforms();
            camera.BeginMenuPreview();
            // Exercise a nonzero scan phase, then the same handoff used by Continue.
            Set(camera, "menuStartedAt", Time.unscaledTime - 3f);
            typeof(PlayerCameraController).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(camera, null);
            Vector3 adjustedPosition = player.transform.position;
            Quaternion adjustedRotation = player.transform.rotation;
            Assert.That(Quaternion.Angle(adjustedRotation, Quaternion.identity), Is.GreaterThan(45f));

            camera.EndMenuPreview();

            Assert.That(player.transform.position, Is.EqualTo(adjustedPosition));
            Assert.That(Quaternion.Angle(player.transform.rotation, adjustedRotation), Is.LessThan(0.001f));
            Assert.That(camera.SavedPitch, Is.Zero, "Continue must not revive the saved steep pitch.");
            Assert.That(Mathf.Abs(eye.forward.y), Is.LessThan(0.001f));
            Assert.That(body.enabled, Is.True);
            Set(camera, "menuStartedAt", Time.unscaledTime - 6f);
            typeof(PlayerCameraController).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(camera, null);
            camera.EndMenuPreview(false);
            Assert.That(Quaternion.Angle(player.transform.rotation, adjustedRotation), Is.LessThan(0.001f),
                "Scanning and repeated cleanup must not change the committed view.");
        }

        [Test]
        public void FinishingPreviewKeepsAdjustedPositionInsteadOfReturningToTheSave()
        {
            Box("Floor", Origin + Vector3.down * 0.25f, new Vector3(6f, 0.5f, 6f));
            Box("Front", Origin + new Vector3(0f, 1.6f, 0.65f), new Vector3(3f, 3.2f, 0.15f));
            Box("Back", Origin + new Vector3(0f, 1.6f, -0.65f), new Vector3(3f, 3.2f, 0.15f));
            Box("Right", Origin + new Vector3(0.65f, 1.6f, 0f), new Vector3(0.15f, 3.2f, 1.3f));
            PlayerCameraController camera = player.AddComponent<PlayerCameraController>();
            Set(camera, "body", body);
            Set(camera, "yawTransform", player.transform);
            Set(camera, "cameraTransform", eye);
            Physics.SyncTransforms();
            camera.BeginMenuPreview();
            // Set a supported adjustment independently of the planner's view scoring.
            // This test guards the handoff, while the planner tests guard path safety.
            body.enabled = false;
            player.transform.position = Origin + Vector3.left * 0.45f;
            body.enabled = true;
            Physics.SyncTransforms();
            Vector3 adjusted = player.transform.position;
            Assert.That(Vector3.Distance(adjusted, Origin), Is.GreaterThan(0.1f));

            camera.EndMenuPreview();
            camera.EndMenuPreview(false);

            Assert.That(player.transform.position, Is.EqualTo(adjusted));
            Assert.That(body.enabled, Is.True);
        }

        [Test]
        public void BroadWallChoosesAClearDirectionWithoutTreatingPlayerChildrenAsWalls()
        {
            Box("Wall", Origin + new Vector3(0f, 1.6f, 0.65f), new Vector3(5f, 3.2f, 0.15f));
            GameObject self = Box("Self", Origin + new Vector3(0f, 1.6f, -0.55f), Vector3.one * 0.4f);
            self.transform.SetParent(player.transform, true);
            Physics.SyncTransforms();
            object view = Choose(false);
            float yaw = Field<float>(view, "Yaw");
            Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            float nearest = 12f;
            foreach (RaycastHit hit in Physics.RaycastAll(eye.position, direction, nearest,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(player.transform)) nearest = Mathf.Min(nearest, hit.distance);
            Assert.That(nearest, Is.GreaterThan(2f));
            Assert.That(Field<Vector3>(view, "Position"), Is.EqualTo(Origin));
        }

        [Test]
        public void NarrowPostAndLowRailInOneMeshKeepAWideOpenRooftopView()
        {
            Box("Rear", Origin + new Vector3(0f, 1.5f, -0.8f), new Vector3(5f, 3f, 0.15f));
            Box("Left", Origin + new Vector3(-0.8f, 1.5f, 0f), new Vector3(0.15f, 3f, 4f));
            Box("Right", Origin + new Vector3(0.8f, 1.5f, 0f), new Vector3(0.15f, 3f, 4f));
            GameObject source = Box("Mesh Source", Origin, Vector3.one);
            Mesh cube = source.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(source);
            roofMesh = new Mesh();
            roofMesh.CombineMeshes(new[] {
                new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(Origin + new Vector3(0f, 1.5f, 0.85f),
                    Quaternion.identity, new Vector3(0.1f, 3f, 0.1f)) },
                new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(Origin + new Vector3(0f, 1f, 0.85f),
                    Quaternion.identity, new Vector3(2f, 0.12f, 0.1f)) }
            });
            GameObject rail = new GameObject("Merged Rooftop Geometry", typeof(MeshCollider));
            rail.transform.SetParent(fixture.transform);
            rail.GetComponent<MeshCollider>().sharedMesh = roofMesh;
            Physics.SyncTransforms();

            float yaw = Field<float>(Choose(false), "Yaw");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, yaw)), Is.LessThan(10f),
                "A small turn around a thin post keeps the surrounding open view in frame.");
            int open = 0;
            for (int sample = -3; sample <= 3; sample++)
                if (!Physics.Raycast(eye.position, Quaternion.Euler(0f, yaw + sample * 5f, 0f) * Vector3.forward,
                    2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) open++;
            Assert.That(open, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void InvisibleQuestBarrierStopsSupportedMenuRelocation()
        {
            Box("Floor", Origin + Vector3.down * 0.25f, new Vector3(6f, 0.5f, 6f));
            GameObject barrier = Box("Boss Player Barrier", Origin + new Vector3(0f, 1.6f, 0.45f),
                new Vector3(3f, 3.2f, 0.1f));
            barrier.layer = 2; // Authored Ignore Raycast barrier still physically blocks the player.
            Physics.SyncTransforms();
            Type planner = typeof(PlayerCameraController).Assembly.GetType("SaksiTerakhir.Player.MenuViewPlanner");
            Assert.That(planner, Is.Not.Null);
            bool clear = (bool)planner.GetMethod("ClearPath", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { body, Origin, Origin + Vector3.forward * 0.9f });
            Assert.That(clear, Is.False,
                "An invisible quest barrier must stop temporary player relocation even though view rays ignore it.");
        }

        [Test]
        public void CrampedRooftopCannotRelocateBeyondSupportedCapsuleSpace()
        {
            Box("Roof", Origin + Vector3.down * 0.25f, new Vector3(0.9f, 0.5f, 0.9f));
            Box("Front", Origin + new Vector3(0f, 1.6f, 0.65f), new Vector3(3f, 3.2f, 0.15f));
            Box("Back", Origin + new Vector3(0f, 1.6f, -0.65f), new Vector3(3f, 3.2f, 0.15f));
            Box("Left", Origin + new Vector3(-0.65f, 1.6f, 0f), new Vector3(0.15f, 3.2f, 3f));
            Box("Right", Origin + new Vector3(0.65f, 1.6f, 0f), new Vector3(0.15f, 3.2f, 3f));
            Physics.SyncTransforms();
            object view = Choose(true);
            Assert.That(Field<Vector3>(view, "Position"), Is.EqualTo(Origin),
                "Every offset leaves part of the capsule unsupported, so the preview must keep its original floor.");
            Assert.That(Field<float>(view, "ScanAngle"), Is.LessThanOrEqualTo(6f));
        }

        private object Choose(bool relocate)
        {
            Type planner = typeof(PlayerCameraController).Assembly.GetType("SaksiTerakhir.Player.MenuViewPlanner");
            Assert.That(planner, Is.Not.Null);
            return planner.GetMethod("Choose", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { body, eye, 0f, 80f, 6f, relocate });
        }

        private GameObject Box(string name, Vector3 position, Vector3 size)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(fixture.transform);
            box.transform.position = position;
            box.transform.localScale = size;
            return box;
        }

        private static T Field<T>(object owner, string name) => (T)owner.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

        private static void Set(object owner, string name, object value) => owner.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    }
}
