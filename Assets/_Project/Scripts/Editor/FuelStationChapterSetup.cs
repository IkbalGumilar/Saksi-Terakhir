using System;
using System.Collections.Generic;
using System.Linq;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Localization;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using SaksiTerakhir.Vehicles;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SaksiTerakhir.EditorTools
{
    // Scene installation is explicit. Content is copied from the manuscript so
    // regenerating it never turns a partial conversation into complete knowledge.
    public static class FuelStationChapterSetup
    {
        private const string MainScene = "Assets/_Project/Scenes/Regional Archive Office.unity";
        private const string DialogueFolder = "Assets/_Project/Story/Dialogues/";
        private const string DocumentFolder = "Assets/_Project/Items/Documents/";
        private const string LocalizationPath = "Assets/_Project/Localization/LocalizationTable.asset";
        private const string FinalConversationId = "chapter.spbu_departure";

        [MenuItem("Saksi Terakhir/Connect Fuel Station Chapter Ending")]
        public static void ConfigureFromMenu() => Debug.Log(ConfigureActiveScene());

        public static string ConfigureActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != MainScene)
                throw new InvalidOperationException("Open the main scene outside Play Mode.");
            ChapterOneDirector chapter = FindComponent<ChapterOneDirector>(scene,
                candidate => candidate.enabled && candidate.gameObject.activeInHierarchy);
            VehicleKeyUse vehicle = FindComponent<VehicleKeyUse>(scene, candidate => candidate.name == "SUV_Fleet_Black");
            PlayerInputReader player = FindComponent<PlayerInputReader>(scene,
                candidate => candidate.enabled && candidate.gameObject.activeInHierarchy);
            if (chapter == null || vehicle == null || player == null)
                throw new InvalidOperationException("The active chapter, parked SUV and MC must exist.");

            var book = new ChapterOneManuscript();
            string grandfatherSection = ChapterOneManuscript.Between(book.Branches,
                "## ADEGAN — BANGKU DI TEMPAT TEDUH", "## GAMEPLAY — TIMER SPBU DAN PANGGILAN B");
            DialogueLine[] grandfatherLines = ChapterOneManuscript.Speeches(grandfatherSection);
            DialogueSequence grandfather = Dialogue("spbu-grandfather", "chapter.spbu_grandfather", grandfatherLines);
            DialogueSequence direct = Dialogue("spbu-ready-direct", "chapter.spbu_ready_direct",
                Speeches(book.Main, "### Panggilan langsung", "### Panggilan melalui telepon"));
            DialogueSequence phone = Dialogue("spbu-ready-phone", "chapter.spbu_ready_phone",
                Speeches(book.Main, "### Panggilan melalui telepon", "### Jika MC sedang berbicara dengan kakek"), phone: true);
            DialogueSequence farewell = Dialogue("spbu-grandfather-farewell", "chapter.spbu_grandfather_farewell",
                Speeches(book.Branches, "Jika pemain memilih berpamitan lebih awal, MC dapat berkata:",
                    "Jika pemain memilih menyelesaikan obrolan setelah menjawab telepon"));
            DialogueSequence photo = Dialogue("spbu-photo", "chapter.spbu_photo",
                Speeches(book.Main, "## OPTIONAL ACTIVITY — FOTO PERJALANAN", "**ITEM OPSIONAL:**"));
            DialogueSequence colleague = Dialogue("spbu-colleagues", "chapter.spbu_colleagues",
                new[] { Speeches(book.Main, "# 12. PERSINGGAHAN DI SPBU", "### Kebutuhan kendaraan").Last() });
            DialogueSequence[] departures = FinalConversations(book.Branches);

            FuelStationChapterDirector station = chapter.GetComponent<FuelStationChapterDirector>()
                ?? Undo.AddComponent<FuelStationChapterDirector>(chapter.gameObject);
            var settings = new SerializedObject(station);
            SetReference(settings, "chapter", chapter);
            SetReference(settings, "dialogue", chapter.GetComponent<StoryDialogueController>());
            SetReference(settings, "player", player.transform);
            SetReference(settings, "vehicle", vehicle);
            SetReference(settings, "grandfatherConversation", grandfather);
            SetInteger(settings, "grandfatherIntroEnd", LineEnding(grandfatherLines,
                "Oh, Cikuray. Dulu saya tinggal di Garut,"));
            SetInteger(settings, "reportLineEnd", LineEnding(grandfatherLines,
                "Mereka cerita ada laki-laki menebang pohon"));
            SetInteger(settings, "nightLineEnd", LineEnding(grandfatherLines,
                "Kalau kabar itu benar dan kalian ingin menemukan desa itu,"));
            SetInteger(settings, "morningLineEnd", LineEnding(grandfatherLines, "Kalau terlihat desa, berhenti di luar."));
            SetInteger(settings, "etiquetteLineEnd", LineEnding(grandfatherLines,
                "Mengikuti jalan untuk mencari letak desa berbeda"));
            SetReference(settings, "directReadyDialogue", direct);
            SetReference(settings, "phoneReadyDialogue", phone);
            SetReference(settings, "farewellDialogue", farewell);
            SetReference(settings, "photoDialogue", photo);
            SetReference(settings, "colleagueDialogue", colleague);
            SetReferences(settings.FindProperty("finalConversations"), departures);
            settings.ApplyModifiedProperties();
            Record(station);

            ConfigureDocuments();
            ConfigureQuests(chapter);
            ConfigureGrandfather(scene, chapter.transform, station);
            // The existing NpcInteractable on A/B routes station dialogue through
            // the director. A second interactable on their capsule would compete.
            VehicleJourneySetup.ConfigureStationDepartureActiveScene();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            return "Fuel-station chapter: canonical optional conversation, five departure variants, two documents, return/departure quests and park interaction installed. Scene left unsaved for review.";
        }

        private static DialogueSequence[] FinalConversations(string manuscript)
        {
            string branch = ChapterOneManuscript.Between(manuscript,
                "## BRANCH 04-A — INFORMASI KAKEK DICERITAKAN", "## BRANCH 04-B — TANPA INFORMASI KAKEK");
            DialogueLine[] beginning = Speeches(branch, "**Kondisi:**", "### Sisipan sesuai petunjuk yang telah didengar");
            DialogueLine[] ending = Speeches(branch, "### Lanjutan dialog bersama", "**State setelah percakapan:**");
            DialogueLine[] full = Speeches(branch, "**Jika `INFO_PETUNJUK_KE_DESA_LENGKAP = TRUE`:**",
                "**Jika `INFO_PETUNJUK_KE_DESA_LENGKAP = FALSE`:**");
            DialogueLine[] partialReply = Speeches(branch,
                "Setelah salah satu variasi parsial tersebut, A berkata:", "### Lanjutan dialog bersama");
            DialogueLine[] report = Partial("LAPORAN_GIRIAWAS", "PETUNJUK_MALAM");
            DialogueLine[] night = Partial("PETUNJUK_MALAM", "MENUNGGU_PAGI");
            DialogueLine[] morning = Partial("MENUNGGU_PAGI", null);
            DialogueLine[] noReport = Speeches(manuscript,
                "## BRANCH 04-B — TANPA INFORMASI KAKEK", "**State setelah percakapan:**");
            return new[]
            {
                Departure("spbu-departure-full", full, new[] { HasReport(), Condition(ChapterOneConditionId.GrandfatherDirectionsComplete) }),
                Departure("spbu-departure-morning", morning, new[] { HasReport(),
                    Condition(ChapterOneConditionId.GrandfatherDirectionsComplete, false),
                    Condition(ChapterOneConditionId.GrandfatherMorningAdviceHeard) }),
                Departure("spbu-departure-night", night, new[] { HasReport(),
                    Condition(ChapterOneConditionId.GrandfatherDirectionsComplete, false),
                    Condition(ChapterOneConditionId.GrandfatherMorningAdviceHeard, false),
                    Condition(ChapterOneConditionId.GrandfatherNightAdviceHeard) }),
                Departure("spbu-departure-report", report, new[] { HasReport(),
                    Condition(ChapterOneConditionId.GrandfatherDirectionsComplete, false),
                    Condition(ChapterOneConditionId.GrandfatherNightAdviceHeard, false),
                    Condition(ChapterOneConditionId.GrandfatherMorningAdviceHeard, false) }),
                Dialogue("spbu-departure-map", FinalConversationId, noReport,
                    new[] { Condition(ChapterOneConditionId.GrandfatherReportHeard, false) })
            };

            DialogueLine[] Partial(string stage, string next)
            {
                string section = ChapterOneManuscript.Between(branch,
                    "- Bila `BRANCH_03_TAHAP_DIALOG_TERAKHIR = " + stage + "`, MC berkata:",
                    next == null ? "Setelah salah satu variasi parsial tersebut" :
                        "- Bila `BRANCH_03_TAHAP_DIALOG_TERAKHIR = " + next + "`, MC berkata:");
                return new[] { new DialogueLine("PLAYER", ChapterOneManuscript.QuotedDocument(section)) }
                    .Concat(partialReply).ToArray();
            }

            DialogueSequence Departure(string file, DialogueLine[] insertion, ChapterOneStoryCondition[] conditions)
                => Dialogue(file, FinalConversationId, beginning.Concat(insertion).Concat(ending).ToArray(), conditions);
            ChapterOneStoryCondition HasReport() => Condition(ChapterOneConditionId.GrandfatherReportHeard);
        }

        private static ChapterOneStoryCondition Condition(ChapterOneConditionId id, bool expected = true)
            => new ChapterOneStoryCondition(id, expected);

        private static DialogueLine[] Speeches(string source, string start, string end)
            => ChapterOneManuscript.Speeches(ChapterOneManuscript.Between(source, start, end));

        private static int LineEnding(DialogueLine[] lines, string opening)
        {
            int index = Array.FindIndex(lines, line => line.Text.StartsWith(opening, StringComparison.Ordinal));
            if (index < 0) throw new InvalidOperationException("Missing canonical grandfather milestone: " + opening);
            return index;
        }

        private static DialogueSequence Dialogue(string file, string id, DialogueLine[] lines,
            ChapterOneStoryCondition[] conditions = null, bool phone = false)
        {
            DialogueSequence asset = Asset<DialogueSequence>(DialogueFolder + file + ".asset");
            Undo.RecordObject(asset, "Update canonical SPBU dialogue");
            asset.Configure(id, lines, conditions, phone);
            if (!asset.Validate(out string error)) throw new InvalidOperationException(error);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void ConfigureDocuments()
        {
            ItemDefinition receipt = Document("Struk_SPBU", "document.spbu_receipt",
                "Struk SPBU — Pengisian dan Perbekalan Kendaraan", "Fuel Station Receipt — Vehicle Fuel and Supplies",
                "Bukti pengisian tangki dan pembelian serta pengisian dua jerigen cadangan.",
                "Receipt for filling the vehicle and buying and filling two reserve fuel containers.",
                "Struk SPBU — Pengisian dan Perbekalan Kendaraan\n24 Agustus 2026\n\nTangki mobil dinas: terisi penuh\nJerigen khusus BBM: 2 buah, terisi\nPembayaran ditangani A dari dana dinas.");
            ItemDefinition photo = Document("Foto_Persinggahan_SPBU", "document.spbu_trip_photo",
                "Foto Perjalanan — Persinggahan SPBU", "Travel Photo — Fuel Station Stop",
                "Foto MC, A dan B di area parkir, dengan papan SPBU di belakang mereka.",
                "A photo of MC, A and B in the parking area with the fuel station sign behind them.",
                "Foto Perjalanan — Persinggahan SPBU\n\nMC, A dan B berdiri bersama di area parkir. Ketiganya sedang melihat kamera; papan SPBU terlihat di belakang mereka.");
            ItemDefinitionCatalog catalog = Asset<ItemDefinitionCatalog>("Assets/_Project/Resources/ItemDefinitionCatalog.asset");
            var serialized = new SerializedObject(catalog);
            SetReferences(serialized.FindProperty("definitions"), catalog.Definitions.Concat(new[] { receipt, photo }).Distinct().ToArray());
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog);
        }

        private static ItemDefinition Document(string file, string id, string title, string englishTitle,
            string description, string englishDescription, string contents)
        {
            ItemDefinition asset = Asset<ItemDefinition>(DocumentFolder + file + ".asset");
            var fields = new SerializedObject(asset);
            fields.FindProperty("id").stringValue = id;
            fields.FindProperty("nameKey").stringValue = id + ".name";
            fields.FindProperty("descriptionKey").stringValue = id + ".description";
            fields.FindProperty("type").enumValueIndex = (int)InventoryEntryType.Document;
            fields.FindProperty("modelPrefab").objectReferenceValue = null;
            fields.FindProperty("documentText").stringValue = contents;
            fields.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            Text(id + ".name", title, englishTitle);
            Text(id + ".description", description, englishDescription);
            return asset;
        }

        private static void ConfigureQuests(ChapterOneDirector chapter)
        {
            QuestDefinition returning = Asset<QuestDefinition>("Assets/_Project/Story/Quests/spbu-return.asset");
            returning.Configure("chapter.spbu_return", QuestCategory.Main, "story.quest.spbu_return", Array.Empty<string>());
            QuestDefinition leaving = Asset<QuestDefinition>("Assets/_Project/Story/Quests/spbu-departure.asset");
            leaving.Configure("chapter.spbu_departure", QuestCategory.Main, "story.quest.spbu_departure", Array.Empty<string>());
            EditorUtility.SetDirty(returning);
            EditorUtility.SetDirty(leaving);
            Text("story.quest.spbu_return", "Kembali ke mobil bersama Raka dan Sinta", "Return to the car with Raka and Sinta");
            Text("story.quest.spbu_departure", "Menuju Giriawas bersama Raka dan Sinta", "Leave for Giriawas with Raka and Sinta");
            var serialized = new SerializedObject(chapter);
            SerializedProperty quests = serialized.FindProperty("quests");
            if (quests.arraySize < 13) quests.arraySize = 13;
            quests.GetArrayElementAtIndex(10).objectReferenceValue = null;
            quests.GetArrayElementAtIndex(11).objectReferenceValue = returning;
            quests.GetArrayElementAtIndex(12).objectReferenceValue = leaving;
            serialized.ApplyModifiedProperties();
            Record(chapter);
        }

        private static void ConfigureGrandfather(Scene scene, Transform owner, FuelStationChapterDirector station)
        {
            Transform existing = owner.Find("SPBU Grandfather");
            if (existing != null)
            {
                BindGrandfather(existing.gameObject, station);
                return;
            }
            Transform park = FindNamed(scene, "10_Taman_Timur_Laut");
            if (park == null) throw new InvalidOperationException("The park beside the fuel station is missing.");
            NpcSpawnPoint[] reservedSeats = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<NpcSpawnPoint>(true))
                .Where(point => point.SeatAnchor != null).ToArray();
            Transform[] benches = park.GetComponentsInChildren<Transform>(true).Where(item => item.name == "Bangku_Taman").ToArray();
            Transform bench = benches.OrderBy(item => reservedSeats.Any(point =>
                    (point.SeatAnchor.position - item.position).sqrMagnitude < 2.25f) ? 1 : 0)
                .ThenBy(item => (item.position - new Vector3(46.5f, 0f, 283f)).sqrMagnitude).FirstOrDefault();
            if (bench == null) throw new InvalidOperationException("The fuel-station park has no usable bench.");
            GameObject root = new GameObject("SPBU Grandfather");
            Undo.RegisterCreatedObjectUndo(root, "Create the optional park conversation");
            root.transform.SetParent(owner, false);
            root.transform.SetPositionAndRotation(bench.position, bench.rotation);
            root.layer = 6;
            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, .98f, 0f);
            capsule.height = 1.15f;
            capsule.radius = .28f;
            Material bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Generated/Npc/NPC_Worker.mat");
            Primitive("Seated body", PrimitiveType.Capsule, root.transform,
                new Vector3(0f, .94f, 0f), new Vector3(.55f, .38f, .5f), bodyMaterial);
            Primitive("Head", PrimitiveType.Sphere, root.transform,
                new Vector3(0f, 1.4f, 0f), Vector3.one * .27f,
                AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Models/Materials/MC_ArchiveOfficer_Skin_Albedo.mat"));
            GameObject labelObject = new GameObject("Name", typeof(TextMeshPro));
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 1.7f, 0f);
            TextMeshPro label = labelObject.GetComponent<TextMeshPro>();
            label.text = "Kakek";
            label.fontSize = 2.4f;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(2f, .3f);
            BindGrandfather(root, station);
        }

        private static void BindGrandfather(GameObject owner, FuelStationChapterDirector station)
        {
            FuelStationInteractable target = owner.GetComponent<FuelStationInteractable>()
                ?? Undo.AddComponent<FuelStationInteractable>(owner);
            var serialized = new SerializedObject(target);
            SetReference(serialized, "station", station);
            serialized.FindProperty("kind").enumValueIndex = 0;
            serialized.ApplyModifiedProperties();
            Record(target);
        }

        private static void Primitive(string name, PrimitiveType type, Transform parent,
            Vector3 position, Vector3 scale, Material material)
        {
            GameObject visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = position;
            visual.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            if (material != null) visual.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Transform FindNamed(Scene scene, string name)
        {
            var pending = new Stack<Transform>(scene.GetRootGameObjects().Select(root => root.transform));
            while (pending.Count > 0)
            {
                Transform next = pending.Pop();
                if (next.name == name) return next;
                for (int index = 0; index < next.childCount; index++) pending.Push(next.GetChild(index));
            }
            return null;
        }

        private static T FindComponent<T>(Scene scene, Func<T, bool> predicate) where T : Component
            => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault(predicate);

        private static T Asset<T>(string path) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            T created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void SetReference(SerializedObject target, string field, UnityEngine.Object value)
            => target.FindProperty(field).objectReferenceValue = value;
        private static void SetInteger(SerializedObject target, string field, int value)
            => target.FindProperty(field).intValue = value;
        private static void SetReferences<T>(SerializedProperty field, T[] values) where T : UnityEngine.Object
        {
            field.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++) field.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        private static void Text(string key, string indonesian, string english)
        {
            LocalizationTable table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(LocalizationPath);
            if (table == null) throw new InvalidOperationException("The localization table is missing.");
            var entries = new List<LocalizedTextEntry>(table.entries);
            LocalizedTextEntry entry = entries.FirstOrDefault(candidate => candidate != null && candidate.key == key);
            if (entry == null) { entry = new LocalizedTextEntry { key = key }; entries.Add(entry); }
            entry.indonesian = indonesian;
            entry.english = english;
            table.entries = entries.ToArray();
            table.ClearLookupCache();
            EditorUtility.SetDirty(table);
        }

        private static void Record(UnityEngine.Object value)
        {
            EditorUtility.SetDirty(value);
            PrefabUtility.RecordPrefabInstancePropertyModifications(value);
        }
    }
}
