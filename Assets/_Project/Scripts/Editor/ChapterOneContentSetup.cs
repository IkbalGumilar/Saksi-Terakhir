using System;
using System.Collections.Generic;
using System.Linq;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Localization;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Story;
using UnityEditor;
using UnityEngine;

namespace SaksiTerakhir.EditorTools
{
    public static class ChapterOneContentSetup
    {
        private const string Root = "Assets/_Project/Story";
        private const string Quests = Root + "/Quests";
        private const string Dialogues = Root + "/Dialogues";
        private const string Documents = "Assets/_Project/Items/Documents";
        private const string Profiles = "Assets/_Project/Npc/Profiles/";
        private const string LocalizationPath = "Assets/_Project/Localization/LocalizationTable.asset";

        [MenuItem("Saksi Terakhir/Ensure Chapter One Content")]
        public static void EnsureFromMenu() => Debug.Log(EnsureCanonicalContent());
        public static void EnsureFromBatch() => Debug.Log(EnsureCanonicalContent());
        public static string Ensure() => EnsureCanonicalContent();

        // This entry point updates content only. It never opens or saves a scene.
        public static string EnsureCanonicalContent()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Chapter-one content must be prepared outside Play Mode.");
            var manuscript = new ChapterOneManuscript();
            Dictionary<string, DialogueLine[]> speech = ReadCanonicalDialogues(manuscript);
            string agenda = ChapterOneManuscript.QuotedDocument(ChapterOneManuscript.Between(
                manuscript.Main, "## CATATAN AGENDA — 24 AGUSTUS 2026", "Ucup menutup buku."));
            // Validate source boundaries before creating any assets.
            foreach (KeyValuePair<string, DialogueLine[]> entry in speech)
            {
                var validation = ScriptableObject.CreateInstance<DialogueSequence>();
                validation.Configure(SequenceId(entry.Key), entry.Value);
                bool valid = validation.Validate(out string error);
                UnityEngine.Object.DestroyImmediate(validation);
                if (!valid) throw new InvalidOperationException(error);
            }
            EnsureFolder("Assets/_Project", "Story");
            EnsureFolder(Root, "Quests");
            EnsureFolder(Root, "Dialogues");
            EnsureFolder("Assets/_Project/Items", "Documents");
            var changed = new List<UnityEngine.Object>();
            var sequences = new Dictionary<string, DialogueSequence>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, DialogueLine[]> entry in speech)
                sequences.Add(entry.Key, Dialogue(entry.Key, SequenceId(entry.Key), entry.Value, changed));

            Quest("morning-routine", "main.chapter1.morning", QuestCategory.Main,
                "quest.main.morning", new[] { "quest.main.morning.enter", "quest.main.morning.desk" }, changed);
            QuestDefinition rooftop = Quest("rooftop", "main.chapter1.rooftop", QuestCategory.Main,
                "quest.main.rooftop", new[] { "quest.main.rooftop.talk" }, changed);
            Quest("call", "main.chapter1.call", QuestCategory.Main,
                "quest.main.call", new[] { "quest.main.call.answer" }, changed);
            QuestDefinition bossFirst = Quest("boss-first", "main.chapter1.boss_first", QuestCategory.Main,
                "quest.main.boss_first", new[] { "quest.main.boss_first.talk" }, changed);
            QuestDefinition find = Quest("find-colleagues", "main.chapter1.find", QuestCategory.Main,
                "quest.main.find", new[] { "quest.main.find.raka", "quest.main.find.sinta" }, changed);
            QuestDefinition escort = Quest("escort", "main.chapter1.escort", QuestCategory.Main,
                "quest.main.escort", new[] { "quest.main.find.raka", "quest.main.find.sinta" }, changed);
            QuestDefinition bossFinal = Quest("boss-final", "main.chapter1.boss_final", QuestCategory.Main,
                "quest.main.boss_final", new[] { "quest.main.boss_final.listen" }, changed);
            QuestDefinition key = Quest("car-key", "main.chapter1.car_key", QuestCategory.Main,
                "quest.main.key", new[] { "quest.main.key.collect" }, changed);
            Quest("vehicle", "main.chapter1.vehicle", QuestCategory.Main,
                "quest.main.vehicle", new[] { "quest.main.vehicle.reach" }, changed);
            QuestDefinition breakfast = Quest("breakfast", "optional.chapter1.breakfast", QuestCategory.Optional,
                "quest.optional.breakfast", new[] { "quest.optional.breakfast.eat" }, changed, QuestProgressSource.Breakfast);
            QuestDefinition wrongRoute = Quest("wrong-route", "side.chapter1.wrong_route", QuestCategory.Side,
                "quest.side.wrong_route", new[] { "quest.side.wrong_route.talk" }, changed, QuestProgressSource.WrongRoute);
            QuestDefinition desk = Quest("desk-preparation", "optional.chapter1.desk_preparation", QuestCategory.Optional,
                "quest.optional.desk_preparation", new[] { "quest.optional.desk_preparation.finish" }, changed, QuestProgressSource.DeskPreparation);
            QuestDefinition supplies = Quest("supplies", "optional.chapter1.supplies", QuestCategory.Side,
                "quest.optional.supplies", new[] { "quest.optional.supplies.buy" }, changed, QuestProgressSource.Supplies);
            QuestDefinition meal = Quest("shared-meal", "optional.chapter1.shared_meal", QuestCategory.Optional,
                "quest.optional.shared_meal", new[] { "quest.optional.shared_meal.talk" }, changed, QuestProgressSource.SharedMeal);
            MergeQuestCatalog(new[] { breakfast, wrongRoute, desk, supplies, meal }, changed);
            Offer("NPC-021-Pekerja-03", new[] { rooftop }, new[] { sequences["lunch-invitation"], sequences["rooftop"] }, changed);
            Offer("NPC-022-Pekerja-04", new[] { rooftop }, new[] { sequences["lunch-invitation"], sequences["rooftop"] }, changed);
            Offer("NPC-003-Rekan-A", new[] { find, escort, meal },
                new[] { sequences["raka-first"], sequences["raka-last-intro"], sequences["raka-last"], sequences["shared-meal"] }, changed);
            Offer("NPC-004-Rekan-B", new[] { find, escort, meal },
                new[] { sequences["sinta-first"], sequences["sinta-last-intro"], sequences["sinta-last"], sequences["shared-meal"] }, changed);
            Offer("NPC-006-Bos", new[] { bossFirst, bossFinal },
                new[] { sequences["boss-first"], sequences["boss-seating"], sequences["boss-final"] }, changed);
            Offer("NPC-002-Resepsionis", new[] { key }, new[] { sequences["nadia-key"] }, changed);

            ItemDefinition original = ArchiveDocument(manuscript, changed);
            string assignment = speech["boss-final"].First(line => line.Text.StartsWith("Itu yang saya perlukan.", StringComparison.Ordinal)).Text;
            ItemDefinition letter = Document("Surat_Tugas_Verifikasi_Lapangan", "document.field_assignment",
                "Surat Tugas Verifikasi Lapangan", "Field Verification Assignment",
                "Tugas verifikasi lapangan untuk Ucup, Dadan, dan Rafi.", "Field verification assignment for Ucup, Dadan, and Rafi.",
                "Surat Tugas Verifikasi Lapangan\n24 Agustus 2026\n\n" + assignment, changed);
            ItemDefinition diary = Document("Agenda_24_Agustus_2026", "document.agenda_2026_08_24",
                "Agenda — 24 Agustus 2026", "Agenda — 24 August 2026",
                "Catatan agenda mengenai tugas verifikasi arsip Cikapohon.", "A diary entry about the Cikapohon archive assignment.", agenda, changed);
            // Dynamic receipts use their own InventoryEntry description. Cataloging
            // this template would replace the saved shop, items and payment details.
            Document("Struk_Pembayaran_Template", "document.travel_receipt", "Struk Pembayaran", "Payment Receipt",
                "Bukti pengeluaran perbekalan setelah penugasan.", "Proof of supply expenses after the assignment.",
                "Nama toko\nTanggal\nWaktu\nBarang\nJumlah pembayaran", changed);
            MergeItemCatalog(new[] { original, letter, diary }, changed);
            UpdateQuestTexts(changed);
            foreach (UnityEngine.Object asset in changed.Distinct()) AssetDatabase.SaveAssetIfDirty(asset);
            return $"Canonical Bab 1 content: {speech.Count} dialogues, 9 main quest stages, 5 optional/side quests, 3 readable documents.";
        }

        [MenuItem("Saksi Terakhir/Update Inscription Document")]
        public static void UpdateArchiveDocumentFromMenu() => Debug.Log(EnsureArchiveDocument().name);

        // A narrow update for the combined document, without regenerating dialogue
        // or modifying scenes while the author is working on the UI and manuscript.
        public static ItemDefinition EnsureArchiveDocument()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Update the inscription outside Play Mode.");
            var changed = new List<UnityEngine.Object>();
            ItemDefinition original = ArchiveDocument(new ChapterOneManuscript(), changed);
            MergeItemCatalog(new[] { original }, changed);
            foreach (string guid in AssetDatabase.FindAssets("t:QuestDefinition", new[] { Quests }))
            {
                QuestDefinition quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (!quest.RequiredItemIds.Contains(ItemDefinitionCatalog.LegacyWanasetaTranslationId)) continue;
                string[] ids = quest.RequiredItemIds.Select(ItemDefinitionCatalog.CanonicalId).Distinct().ToArray();
                var fields = new SerializedObject(quest);
                SerializedProperty required = fields.FindProperty("requiredItemIds");
                required.arraySize = ids.Length;
                for (int index = 0; index < ids.Length; index++) required.GetArrayElementAtIndex(index).stringValue = ids[index];
                fields.ApplyModifiedPropertiesWithoutUndo();
                changed.Add(quest);
            }
            foreach (UnityEngine.Object asset in changed.Distinct()) AssetDatabase.SaveAssetIfDirty(asset);
            AssetDatabase.DeleteAsset(Documents + "/Terjemahan_Arsip_Wanaseta.asset");
            return original;
        }

        private static ItemDefinition ArchiveDocument(ChapterOneManuscript manuscript, List<UnityEngine.Object> changed)
        {
            string originalText = ChapterOneManuscript.QuotedDocument(ChapterOneManuscript.Between(
                manuscript.Main, "## TEKS ASLI PRASASTI", "## TEKS TERJEMAHAN INDONESIA"));
            string translation = ChapterOneManuscript.QuotedDocument(ChapterOneManuscript.Between(
                manuscript.Main, "## TEKS TERJEMAHAN INDONESIA", "Terjemahan ini berupa catatan perjalanan"));
            ItemDefinition original = Document("Arsip_Wanaseta_Original", ItemDefinitionCatalog.WanasetaArchiveId,
                "Prasasti Cikapohon", "Cikapohon Inscription",
                "Salinan prasasti beserta terjemahan tim verifikasi. Bagian akhir prasasti terbelah.",
                "A copy of the inscription with the verification team's translation. Its ending is broken.", originalText, changed);
            var fields = new SerializedObject(original);
            fields.FindProperty("documentTranslationKey").stringValue = ItemDefinitionCatalog.WanasetaArchiveId + ".translation";
            fields.FindProperty("documentFont").objectReferenceValue = ArchiveDocumentFontSetup.Ensure(originalText);
            fields.ApplyModifiedPropertiesWithoutUndo();
            Text(original.DocumentTranslationKey, EnglishArchiveTranslation, translation, changed);
            Text("inventory.document.translate", "Translate", "Terjemahkan", changed);
            Text("inventory.document.original", "Original text", "Teks asli", changed);
            return original;
        }

        private const string EnglishArchiveTranslation = @"TRAVEL ACCOUNT — RADEN KIAN SANTANG

I stopped in the village of Cikapohon at the foot of Mount Cikuray while searching for my father, Prabu Siliwangi, who had gone ahead to Sancang.

The village was beautiful and peaceful. I decided to stay for three days before continuing my journey.

The first day. I spent time among the villagers and helped with their work. Each night, the village fell silent. That night I woke on a large rock, far from where I had rested. I did not know how I had got there. I was overcome by sleepiness and fell asleep again.

Morning came. The village was beautiful and peaceful again, as though the night had been only a dream. Yet the dream felt too real to forget.

The second day. I had to leave soon. I still had to find my father. But my supplies were dwindling too quickly, as though someone took my food whenever I was not looking. To keep track, I recorded every meal and activity on white cloth with bamboo charcoal.

Tonight I read those notes again. My writing covered the cloth. It showed that I had been in this village for eight days.

That was impossible. I had only begun writing that morning.

Whatever was happening, I had to leave tomorrow.

The next night I woke among the bushes. I could not remember how I had got there. Something was wrong. I ran without knowing the way, wanting only to get away from the village.

As I remembered it, I had been here only three days. My notes showed the ninth day. My supplies were almost gone. The village seemed to prevent me from leaving.

I finally escaped. I was exhausted and nearly out of supplies, but still alive. I continued my journey to Sancang.

Do not trust what you see. Do not trust what you remember. The demons within this village are very powerful. What appears real may not be real. Get out p—

[The inscription is broken. The rest of the writing has not been found.]";

        private static Dictionary<string, DialogueLine[]> ReadCanonicalDialogues(ChapterOneManuscript book)
        {
            var result = new Dictionary<string, DialogueLine[]>(StringComparer.Ordinal);
            void Main(string file, string start, string end) => result.Add(file,
                ChapterOneManuscript.Speeches(ChapterOneManuscript.Between(book.Main, start, end)));
            void Branch(string file, string start, string end) => result.Add(file,
                ChapterOneManuscript.Speeches(ChapterOneManuscript.Between(book.Branches, start, end)));
            void Narration(string file, string content) => result.Add(file, new[] { new DialogueLine("Catatan", content) });
            Main("breakfast", "# 2. OPTIONAL QUEST — SARAPAN DULU", "### **BAYAR SEKARANG**");
            Main("breakfast-paid", "### **BAYAR SEKARANG**", "### **NGUTANG DULU**");
            Main("breakfast-owed", "### **NGUTANG DULU**", "# 3. AREA KANTOR — EKSPLORASI PAGI");
            Main("wrong-route-offer", "# SIDE QUEST 01 — SALAH JALUR", "Pemain dapat menerima atau menolak.");
            Main("wrong-route-accepted", "Jika menerima:", "**Side Quest:** Salah Jalur");
            Main("wrong-route-delivered", "Sesampainya di sana:", "Quest selesai.");
            Main("lunch-invitation", "## 12.08", "# MAIN QUEST 02 — JAM ISTIRAHAT");
            Main("rooftop", "# 5. ROOFTOP — AKHIR PEKAN YANG DITUNGGU", "**MAIN QUEST 02 — JAM ISTIRAHAT**");
            Main("boss-call", "**PONSEL BERGETAR**", "# MAIN QUEST 03 — PANGGILAN MENDADAK");
            Main("boss-first", "# 6. RUANGAN BOS", "# MAIN QUEST 04 — CARI DADAN DAN RAFI");
            Branch("raka-first", "# CABANG 01-A — DADAN DITEMUKAN LEBIH DULU", "# CABANG 01-B — RAFI DITEMUKAN TERAKHIR");
            Branch("sinta-first", "# CABANG 01-C — RAFI DITEMUKAN LEBIH DULU", "# CABANG 01-D — DADAN DITEMUKAN TERAKHIR");
            Branch("sinta-last-intro", "# CABANG 01-B — RAFI DITEMUKAN TERAKHIR", "Rafi memasukkan ponsel dan mendorong tasnya");
            Branch("sinta-last", "Rafi memasukkan ponsel dan mendorong tasnya", "# CABANG 01-C — RAFI DITEMUKAN LEBIH DULU");
            Branch("raka-last-intro", "# CABANG 01-D — DADAN DITEMUKAN TERAKHIR", "Ucup dan Dadan berjalan bersama menuju lantai tiga.");
            Branch("raka-last", "Ucup dan Dadan berjalan bersama menuju lantai tiga.", "# HASIL BRANCH 01");
            string briefing = ChapterOneManuscript.Between(book.Main, "# 7. BRIEFING ARSIP MISTERIUS", "# ITEM CERITA — ARSIP CIKAPOHON");
            string preparations = ChapterOneManuscript.Between(book.Main, "# 8. PERSIAPAN KEBERANGKATAN", "# MAIN QUEST 05 — PERSIAPAN LAPANGAN");
            result.Add("boss-final", ChapterOneManuscript.Speeches(briefing + preparations));
            string reception = ChapterOneManuscript.Between(book.Main, "# 10. RESEPSIONIS", "Jika pemain memiliki struk pembelian untuk tugas:");
            string handoff = ChapterOneManuscript.Between(book.Main, "Resepsionis mengambil sebuah kunci.", "## ITEM DIPEROLEH");
            result.Add("nadia-key", ChapterOneManuscript.Speeches(reception + handoff));
            Main("receipt-submitted", "Jika pemain memiliki struk pembelian untuk tugas:", "Semua pembelian setelah penugasan");
            Branch("shared-meal", "## ADEGAN OPTIONAL QUEST 09 — MAKAN BERSAMA DADAN DAN RAFI", "Adegan berakhir.");
            Branch("vehicle-meal-complete", "### Lanjutan Branch 02-A — Kedatangan di Mobil", "**MOBIL MENINGGALKAN ANRI — MENUJU SPBU**");
            Branch("vehicle-meal-skipped", "## BRANCH 02-B — OPTIONAL QUEST 09 DILEWATKAN", "**MOBIL MENINGGALKAN ANRI — MENUJU SPBU**");
            Narration("wrong-route-declined", "Pemain dapat menerima atau menolak.");
            Narration("desk-prepared", "Saat membersihkan meja, pemain menemukan buku agenda.");
            Narration("agenda-written", "Ucup membuka halaman baru.");
            Narration("shop-supplies", "Pemain bebas menentukan barang yang ingin dibeli.");
            Narration("bulletin", ChapterOneManuscript.QuotedDocument(ChapterOneManuscript.Between(
                book.Main, "# 3. AREA KANTOR — EKSPLORASI PAGI", "# SIDE QUEST 01 — SALAH JALUR")));
            // The seating prompt is an existing presentation cue, not a new plot event.
            result.Add("boss-seating", new[] { new DialogueLine("NPC-006", "Semua sudah hadir, silakan duduk.") });
            result.Add("boss-scold-raka", new[] { new DialogueLine("NPC-006", "Iya, cari dua-duanya. Berkasnya kita bahas setelah lengkap, supaya nanti kalian berangkat dengan informasi yang sama.") });
            result.Add("boss-scold-sinta", result["boss-scold-raka"]);
            result.Add("boss-scold-wait", new[] { new DialogueLine("NPC-006", "Yang ketemu duluan suruh langsung ke sini. Kamu lanjut cari satunya. Saya siapkan berkasnya sambil menunggu.") });
            return result;
        }

        private static string SequenceId(string file) => "chapter." + file.Replace('-', '_');

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        private static DialogueSequence Dialogue(string file, string id, DialogueLine[] lines, List<UnityEngine.Object> changed)
        {
            DialogueSequence asset = LoadOrCreate<DialogueSequence>(Dialogues + "/" + file + ".asset");
            asset.Configure(id, lines, asset.Conditions?.ToArray());
            EditorUtility.SetDirty(asset);
            changed.Add(asset);
            return asset;
        }

        private static QuestDefinition Quest(string file, string id, QuestCategory category, string title,
            string[] objectives, List<UnityEngine.Object> changed, QuestProgressSource source = QuestProgressSource.Journal)
        {
            QuestDefinition asset = LoadOrCreate<QuestDefinition>(Quests + "/" + file + ".asset");
            asset.Configure(id, category, title, objectives, asset.Conditions?.ToArray());
            asset.BindProgress(source);
            QuestJournalContentSetup.ConfigureDetails(asset);
            if (!asset.Validate(out string error)) throw new InvalidOperationException(error);
            EditorUtility.SetDirty(asset);
            changed.Add(asset);
            return asset;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void Offer(string file, QuestDefinition[] quests, DialogueSequence[] dialogues, List<UnityEngine.Object> changed)
        {
            NpcProfile profile = AssetDatabase.LoadAssetAtPath<NpcProfile>(Profiles + file + ".asset");
            if (profile == null) throw new InvalidOperationException("Missing NPC profile: " + file);
            profile.SetStoryOffers((profile.StoryQuests ?? Array.Empty<QuestDefinition>()).Concat(quests)
                    .Where(quest => quest != null).Distinct().ToArray(),
                (profile.StoryDialogues ?? Array.Empty<DialogueSequence>()).Concat(dialogues)
                    .Where(sequence => sequence != null).Distinct().ToArray());
            EditorUtility.SetDirty(profile);
            changed.Add(profile);
        }

        private static ItemDefinition Document(string file, string id, string title, string englishTitle,
            string description, string englishDescription, string content, List<UnityEngine.Object> changed)
        {
            ItemDefinition asset = LoadOrCreate<ItemDefinition>(Documents + "/" + file + ".asset");
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("nameKey").stringValue = id + ".name";
            serialized.FindProperty("descriptionKey").stringValue = id + ".description";
            serialized.FindProperty("type").enumValueIndex = (int)InventoryEntryType.Document;
            serialized.FindProperty("modelPrefab").objectReferenceValue = null;
            serialized.FindProperty("documentText").stringValue = content;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            changed.Add(asset);
            Text(id + ".name", englishTitle, title, changed);
            Text(id + ".description", englishDescription, description, changed);
            return asset;
        }

        private static void MergeItemCatalog(ItemDefinition[] additions, List<UnityEngine.Object> changed)
        {
            ItemDefinitionCatalog catalog = LoadOrCreate<ItemDefinitionCatalog>("Assets/_Project/Resources/ItemDefinitionCatalog.asset");
            ItemDefinition[] definitions = catalog.Definitions.Concat(additions).Where(item => item != null
                && item.Id != ItemDefinitionCatalog.LegacyWanasetaTranslationId).Distinct().ToArray();
            var serialized = new SerializedObject(catalog);
            SerializedProperty field = serialized.FindProperty("definitions");
            field.arraySize = definitions.Length;
            for (int i = 0; i < definitions.Length; i++) field.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            changed.Add(catalog);
        }

        private static void MergeQuestCatalog(QuestDefinition[] additions, List<UnityEngine.Object> changed)
        {
            QuestJournalCatalog catalog = LoadOrCreate<QuestJournalCatalog>("Assets/_Project/Resources/QuestJournalCatalog.asset");
            catalog.Configure(catalog.Definitions.Concat(additions).Where(quest => quest != null).Distinct().ToArray());
            EditorUtility.SetDirty(catalog);
            changed.Add(catalog);
        }

        private static void Text(string key, string english, string indonesian, List<UnityEngine.Object> changed)
        {
            LocalizationTable table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(LocalizationPath);
            if (table == null) throw new InvalidOperationException("Chapter-one content requires the project localization table.");
            var entries = new List<LocalizedTextEntry>(table.entries);
            LocalizedTextEntry entry = entries.FirstOrDefault(value => value != null && value.key == key);
            if (entry == null) { entry = new LocalizedTextEntry { key = key }; entries.Add(entry); }
            entry.english = english;
            entry.indonesian = indonesian;
            table.entries = entries.ToArray();
            table.ClearLookupCache();
            EditorUtility.SetDirty(table);
            changed.Add(table);
        }

        private static void UpdateQuestTexts(List<UnityEngine.Object> changed)
        {
            Text("interact.chapter.activity", "Interact", "Berinteraksi", changed);
            Text("interact.chapter.shop", "Shop", "Belanja", changed);
            Text("interact.chapter.bulletin", "Read the notice board", "Baca papan pengumuman", changed);
            Text("interact.door.locked", "Locked", "Terkunci", changed);
            Text("inventory.purchase_complete", "The item and receipt were added to inventory.", "Barang dan struk masuk inventory.", changed);
            Text("quest.main.morning", "Morning Routine", "Rutinitas Pagi", changed);
            Text("quest.main.morning.enter", "Enter the archive office", "Masuk ke gedung ANRI", changed);
            Text("quest.main.morning.desk", "Go to your workstation", "Menuju ruang kerja", changed);
            Text("quest.main.rooftop", "Lunch Break", "Jam Istirahat", changed);
            Text("quest.main.rooftop.talk", "Meet your colleagues on the rooftop", "Temui rekan kerja di rooftop", changed);
            Text("quest.main.call", "An Unexpected Call", "Panggilan Mendadak", changed);
            Text("quest.main.boss_first", "An Unexpected Call", "Panggilan Mendadak", changed);
            Text("quest.main.boss_first.talk", "Visit the boss on floor 3", "Pergi ke ruangan bos di lantai 3", changed);
            Text("quest.main.find", "Find Dadan and Rafi", "Cari Dadan dan Rafi", changed);
            Text("quest.main.boss_final.listen", "Listen to the archive briefing", "Dengarkan briefing arsip Cikapohon", changed);
            Text("quest.main.key", "Field Preparations", "Persiapan Lapangan", changed);
            Text("quest.main.key.collect", "Collect the office car key at reception", "Ambil kunci mobil dinas di resepsionis", changed);
            Text("quest.main.vehicle", "Departure", "Keberangkatan", changed);
            Text("quest.main.vehicle.reach", "Go to the office car", "Pergi ke mobil dinas", changed);
            Text("quest.optional.breakfast", "Morning Breakfast", "Sarapan Pagi", changed);
            Text("quest.optional.breakfast.eat", "Have breakfast before work", "Sarapan sebelum bekerja", changed);
            Text("quest.side.wrong_route", "Wrong Route", "Salah Jalur", changed);
            Text("quest.side.wrong_route.talk", "Deliver the file to digitization", "Antar berkas ke ruang digitalisasi", changed);
            Text("quest.optional.desk_preparation", "Before You Leave", "Sebelum Pergi", changed);
            Text("quest.optional.desk_preparation.finish", "Tidy the desk before leaving", "Rapikan meja sebelum pergi", changed);
            Text("quest.optional.supplies", "Field Supplies", "Perbekalan Lapangan", changed);
            Text("quest.optional.supplies.buy", "Buy the supplies you need", "Beli perbekalan yang dibutuhkan", changed);
            Text("quest.optional.shared_meal", "Have a Meal with Dadan and Rafi", "Makan Bersama Dadan dan Rafi", changed);
            Text("quest.optional.shared_meal.talk", "Sit with Dadan and Rafi before collecting the key", "Duduk bersama Dadan dan Rafi sebelum mengambil kunci", changed);
        }
    }
}
