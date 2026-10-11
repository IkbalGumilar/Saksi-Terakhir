using System;
using System.Linq;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using SaksiTerakhir.Vehicles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SaksiTerakhir.EditorTools
{
    public static class VehicleItemUseSetup
    {
        private const string MainScene = "Assets/_Project/Scenes/Regional Archive Office.unity";
        private const string SignalMaterial = "Assets/_Project/Art/Models/Materials/Vehicle_KeylessAmber.mat";

        [MenuItem("Saksi Terakhir/Inventory/Connect Car Key In Active Scene")]
        public static void ConfigureFromMenu() => Debug.Log(ConfigureActiveScene());

        // Uses the open Editor and never opens, reloads or saves a scene.
        public static string ConfigureActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != MainScene)
                throw new InvalidOperationException("Open the main scene outside Play Mode before connecting the car key.");
            GameObject[] roots = scene.GetRootGameObjects().Where(g => g.activeInHierarchy).ToArray();
            GameObject car = roots.FirstOrDefault(g => g.name == "SUV_Fleet_Black");
            PlayerInputReader player = roots.SelectMany(g => g.GetComponentsInChildren<PlayerInputReader>(true))
                .FirstOrDefault(c => c.gameObject.activeInHierarchy);
            ChapterOneDirector chapter = roots.SelectMany(g => g.GetComponentsInChildren<ChapterOneDirector>(true))
                .FirstOrDefault(c => c.gameObject.activeInHierarchy);
            ItemDefinitionCatalog catalog = ItemDefinitionCatalog.LoadDefault();
            ItemDefinition key = catalog != null ? catalog.Find(CarKeyInventory.CarKeyId) : null;
            if (car == null || player == null || chapter == null || key == null)
                throw new InvalidOperationException("Car, player, active chapter or car-key definition is missing.");
            // This FBX's steering wheel is on car-local +X; the imported FL/FR
            // names do not identify that side reliably. Use the physical door bounds.
            Transform door = car.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.name == "SUV_Fleet_Door_FR" || r.name == "SUV_Fleet_Door_FL")
                .OrderByDescending(r => car.transform.InverseTransformPoint(r.bounds.center).x)
                .Select(r => r.transform).FirstOrDefault();
            Renderer detail = car.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "SUV_Fleet_Detail");
            if (door == null || detail == null) throw new InvalidOperationException("SUV driver door or indicator renderer is missing.");
            int amberSlot = Array.FindIndex(detail.sharedMaterials, m => m != null
                && (m.name == "Vehicle_LampAmber" || m.name == "Vehicle_KeylessAmber"));
            if (amberSlot < 0) throw new InvalidOperationException("SUV amber indicator material is missing.");

            AudioSource source = Ensure<AudioSource>(car);
            Undo.RecordObject(source, "Configure car key audio");
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 1f;
            source.maxDistance = 12f;
            Record(source);
            ItemKeyLock keyLock = Ensure<ItemKeyLock>(car);
            Bind(keyLock, "requiredKey", key);
            Bind(keyLock, "audioSource", source);
            VehicleSignalFeedback feedback = Ensure<VehicleSignalFeedback>(car);
            Bind(feedback, "audioSource", source);

            // Only this SUV receives the emission-capable copy; source materials stay untouched.
            Material amber = AssetDatabase.LoadAssetAtPath<Material>(SignalMaterial);
            if (amber == null)
            {
                amber = new Material(detail.sharedMaterials[amberSlot]);
                amber.name = "Vehicle_KeylessAmber";
                amber.EnableKeyword("_EMISSION");
                amber.SetColor("_EmissionColor", Color.black);
                AssetDatabase.CreateAsset(amber, SignalMaterial);
            }
            // URP strips the keyword from a black baked-emission material on import.
            // Realtime emissive permits a black resting state and animated property blocks.
            Undo.RecordObject(amber, "Enable animated SUV emission");
            amber.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            amber.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(amber);
            Undo.RecordObject(detail, "Connect SUV signal material");
            Material[] slots = detail.sharedMaterials;
            slots[amberSlot] = amber;
            detail.sharedMaterials = slots;
            Record(detail);
            var signal = new SerializedObject(feedback);
            signal.FindProperty("indicators").arraySize = 1;
            SerializedProperty surface = signal.FindProperty("indicators").GetArrayElementAtIndex(0);
            surface.FindPropertyRelative("renderer").objectReferenceValue = detail;
            surface.FindPropertyRelative("materialSlot").intValue = amberSlot;
            signal.ApplyModifiedProperties();
            Record(feedback);

            // Imported door meshes have a 100x scale and a rotated FBX basis.
            // Keep the prompt in the vehicle's metre-based coordinate frame.
            Transform interaction = EnsureChild(car.transform, "Driver Door Interaction");
            BoxCollider trigger = Ensure<BoxCollider>(interaction.gameObject);
            Undo.RecordObject(trigger, "Configure driver door prompt");
            trigger.isTrigger = true;
            trigger.size = new Vector3(0.42f, 1.5f, 1.12f);
            trigger.center = Vector3.zero;
            Renderer doorRenderer = door.GetComponent<Renderer>();
            if (doorRenderer == null) throw new InvalidOperationException("Driver door renderer is missing.");
            interaction.localPosition = car.transform.InverseTransformPoint(doorRenderer.bounds.center);
            interaction.localRotation = Quaternion.identity;
            interaction.localScale = Vector3.one;
            interaction.gameObject.layer = 6;
            Record(trigger);
            var vehicle = Ensure<VehicleKeyUse>(car);
            Bind(vehicle, "keyLock", keyLock);
            Bind(vehicle, "signalFeedback", feedback);
            Bind(vehicle, "driverDoorTrigger", trigger);
            Bind(vehicle, "chapter", chapter);

            float driverX = Mathf.Sign(car.transform.InverseTransformPoint(doorRenderer.bounds.center).x) * 0.36f;
            Bind(vehicle, "driverSeat", Anchor(car.transform, "Car Driver Seat", new Vector3(driverX, 0.24f, 0.18f)));
            Bind(vehicle, "frontPassengerSeat", Anchor(car.transform, "Car Front Passenger Seat", new Vector3(-driverX, 0.24f, 0.18f)));
            Bind(vehicle, "rearPlayerSeat", Anchor(car.transform, "Car Rear Player Seat", new Vector3(driverX, 0.24f, -0.95f)));
            Bind(vehicle, "rearColleagueSeat", Anchor(car.transform, "Car Rear Colleague Seat", new Vector3(-driverX, 0.24f, -0.95f)));
            Vector3 exitPosition = new Vector3(Mathf.Sign(driverX) * 1.95f, 0f, 0.18f);
            Vector3 worldExit = car.transform.TransformPoint(exitPosition);
            if (Physics.Raycast(worldExit + Vector3.up * 3f, Vector3.down, out RaycastHit ground, 5f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                exitPosition = car.transform.InverseTransformPoint(ground.point + Vector3.up * 0.04f);
            Bind(vehicle, "exitPoint", Anchor(car.transform, "Car Exit Point", exitPosition));
            OfficeNpcAgent[] actors = roots.SelectMany(g => g.GetComponentsInChildren<OfficeNpcAgent>(true))
                .Where(a => a.gameObject.activeInHierarchy).ToArray();
            Bind(vehicle, "colleagueA", actors.FirstOrDefault(a => a.Profile != null && a.Profile.Id == "NPC-003"));
            Bind(vehicle, "colleagueB", actors.FirstOrDefault(a => a.Profile != null && a.Profile.Id == "NPC-004"));
            VehicleDriverDoorInteractable target = Ensure<VehicleDriverDoorInteractable>(interaction.gameObject);
            Bind(target, "vehicle", vehicle);
            VehicleRemoteItemUse handler = Ensure<VehicleRemoteItemUse>(player.gameObject);
            Bind(handler, "vehicle", vehicle);
            Bind(handler, "input", player);
            Ensure<StoryPlayerSeating>(player.gameObject);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            return "Car key connected: 5m remote, driver-door lock, branch seats, optional audio. Scene not saved; Play Mode not run.";
        }

        private static T Ensure<T>(GameObject owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(owner);
        }

        private static Transform EnsureChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) return child;
            GameObject created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, "Create car interaction anchor");
            created.transform.SetParent(parent, false);
            return created.transform;
        }

        private static Transform Anchor(Transform parent, string name, Vector3 position)
        {
            Transform anchor = EnsureChild(parent, name);
            Undo.RecordObject(anchor, "Place car seat anchor");
            anchor.localPosition = position;
            anchor.localRotation = Quaternion.identity;
            Record(anchor);
            return anchor;
        }

        private static void Bind(UnityEngine.Object component, string field, UnityEngine.Object value)
        {
            Undo.RecordObject(component, "Connect car item use");
            var serialized = new SerializedObject(component);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
            Record(component);
        }

        private static void Record(UnityEngine.Object target)
        {
            EditorUtility.SetDirty(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }
}
