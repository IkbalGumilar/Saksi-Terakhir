using System;
using System.Collections.Generic;
using SaksiTerakhir.Localization;
using SaksiTerakhir.Story;
using UnityEditor;
using UnityEngine;

namespace SaksiTerakhir.EditorTools
{
    public static class QuestJournalContentSetup
    {
        private const string QuestFolder = "Assets/_Project/Story/Quests";
        private const string LocalizationPath = "Assets/_Project/Localization/LocalizationTable.asset";

        // Canonical task context from bab-01/branch, with room names from the
        // current modular office and ChapterOneWorldSetup gameplay anchors.
        private sealed class Detail
        {
            public readonly string File, Id, KeyPrefix, EnglishDescription, IndonesianDescription, EnglishLocation, IndonesianLocation;
            public readonly string[] RequiredItems;
            public Detail(string file, string id, string prefix, string englishDescription, string indonesianDescription,
                string englishLocation, string indonesianLocation, string[] requiredItems = null)
            {
                File = file; Id = id; KeyPrefix = prefix;
                EnglishDescription = englishDescription; IndonesianDescription = indonesianDescription;
                EnglishLocation = englishLocation; IndonesianLocation = indonesianLocation;
                RequiredItems = requiredItems ?? Array.Empty<string>();
            }
        }

        private static readonly Detail[] Details = {
            new Detail("morning-routine", "main.chapter1.morning", "quest.main.morning",
                "Your workday starts at ANRI. Enter the building and go to your desk before starting work.",
                "Hari kerja di ANRI dimulai. Masuk ke gedung lalu menuju meja kerja untuk memulai rutinitas pagi.",
                "ANRI: entrance and reception on floor 1; Ucup and Rafi's Workroom on floor 2.",
                "ANRI: pintu masuk dan resepsionis di lantai 1; Ruang Kerja Ucup dan Rafi di lantai 2."),
            new Detail("rooftop", "main.chapter1.rooftop", "quest.main.rooftop",
                "Two coworkers have brought lunch and invited you to eat on the rooftop. Meet them and finish the lunch conversation.",
                "Dua rekan kerja membawa makan siang dan mengajakmu makan di rooftop. Temui mereka lalu tuntaskan percakapan saat istirahat.",
                "ANRI rooftop, above floor 4, near the lunch table.",
                "Rooftop ANRI, di atas lantai 4, dekat meja makan saat istirahat."),
            new Detail("call", "main.chapter1.call", "quest.main.call",
                "Your phone rings as you leave the rooftop. Answer the boss's call to hear his request.",
                "Ponsel berdering saat kamu meninggalkan rooftop. Jawab panggilan bos untuk mendengar permintaannya.",
                "ANRI rooftop, near the exit.",
                "Rooftop ANRI, dekat pintu keluar."),
            new Detail("boss-first", "main.chapter1.boss_first", "quest.main.boss_first",
                "The boss has asked you to discuss a field verification file. Go to his office and speak with him.",
                "Bos memanggilmu untuk membahas berkas verifikasi lapangan. Datangi ruangannya lalu bicara dengannya.",
                "ANRI, floor 3: Director's Office. Wait at the double door until you are invited in.",
                "ANRI, lantai 3: Ruang Kepala (ruangan bos). Tunggu di pintu ganda sampai dipersilakan masuk."),
            new Detail("find-colleagues", "main.chapter1.find", "quest.main.find",
                "The boss needs both field colleagues for the briefing. Find Dadan and Rafi in either order. Send the first one straight to the boss, then find the other.",
                "Bos membutuhkan kedua rekan tim lapangan untuk briefing. Cari Dadan dan Rafi dalam urutan pilihanmu. Minta rekan pertama langsung menemui bos, lalu cari rekan yang satunya.",
                "ANRI, floor 2: Dadan is in Dadan's Workroom; Rafi is in Ucup and Rafi's Workroom. The boss waits on floor 3.",
                "ANRI, lantai 2: Dadan berada di Ruang Kerja Dadan; Rafi berada di Ruang Kerja Ucup dan Rafi. Bos menunggu di lantai 3."),
            new Detail("escort", "main.chapter1.escort", "quest.main.escort",
                "The first colleague has gone ahead. Accompany the last colleague to the boss's office; the conversation follows who you found last.",
                "Rekan pertama sudah berangkat lebih dulu. Dampingi rekan terakhir menuju ruangan bos; percakapan mengikuti siapa yang ditemukan terakhir.",
                "From the workrooms on ANRI floor 2 to the Director's Office on floor 3.",
                "Dari ruang kerja di lantai 2 ANRI menuju Ruang Kepala (ruangan bos) di lantai 3."),
            new Detail("boss-final", "main.chapter1.boss_final", "quest.main.boss_final",
                "The team is together. Sit down and listen to the Cikapohon archive briefing. Receive the original archive, its translation and the official field assignment.",
                "Tim sudah lengkap. Duduk lalu dengarkan briefing arsip Cikapohon. Terima arsip asli, terjemahannya, dan surat tugas verifikasi lapangan.",
                "ANRI, floor 3: Director's Office, at the briefing seats.",
                "ANRI, lantai 3: Ruang Kepala (ruangan bos), di tempat duduk briefing."),
            new Detail("car-key", "main.chapter1.car_key", "quest.main.key",
                "The field assignment is ready. Collect the office car key from reception. You may finish desk preparations, buy supplies or eat with Dadan and Rafi before collecting it.",
                "Tugas lapangan sudah diberikan. Ambil kunci mobil dinas di resepsionis. Sebelum mengambilnya, kamu dapat merapikan meja, membeli perbekalan, atau makan bersama Dadan dan Rafi.",
                "ANRI, floor 1: reception counter near the main entrance.",
                "ANRI, lantai 1: meja resepsionis dekat pintu masuk utama."),
            new Detail("vehicle", "main.chapter1.vehicle", "quest.main.vehicle",
                "You have the car key. Bring the assignment documents to the office car and meet Dadan and Rafi before leaving. The seating follows your earlier choice to eat together.",
                "Kunci mobil sudah diterima. Bawa berkas tugas ke mobil dinas lalu temui Dadan dan Rafi sebelum berangkat. Posisi duduk mengikuti pilihan makan bersama sebelumnya.",
                "ANRI parking area: the office car outside the main building.",
                "ANRI, area parkir: mobil dinas di luar gedung utama.", new[] { CarKeyInventory.CarKeyId, "document.wanaseta_original", "document.field_assignment" }),
            new Detail("travel-to-spbu", "chapter.travel_to_spbu", "story.quest.travel_spbu",
                "Leave ANRI with Dadan and Rafi and continue to the fuel station. The team will refuel and prepare reserve fuel before continuing the field trip.",
                "Tinggalkan ANRI bersama Dadan dan Rafi lalu lanjutkan perjalanan menuju SPBU. Tim akan mengisi bahan bakar dan menyiapkan cadangan sebelum meneruskan tugas lapangan.",
                "Road from ANRI to the fuel station; stop at the fuel station entrance.",
                "Jalan dari ANRI menuju SPBU; berhenti di area masuk SPBU.", new[] { CarKeyInventory.CarKeyId, "document.wanaseta_original", "document.field_assignment" }),
            new Detail("spbu-return", "chapter.spbu_return", "story.quest.spbu_return",
                "Dadan and Rafi have finished the vehicle preparations and called you back. Finish any activity you chose, then return to the parked office car. Extra shopping and the park conversation remain optional.",
                "Dadan dan Rafi sudah menuntaskan kebutuhan kendaraan dan memanggilmu kembali. Selesaikan kegiatan pilihanmu lalu kembali ke mobil dinas yang terparkir. Belanja tambahan dan percakapan di taman tetap opsional.",
                "Fuel station parking area: the office car beside the shop and park access.",
                "Area parkir SPBU: mobil dinas di dekat toko dan akses taman.", new[] { CarKeyInventory.CarKeyId, "document.wanaseta_original", "document.field_assignment" }),
            new Detail("spbu-departure", "chapter.spbu_departure", "story.quest.spbu_departure",
                "Rejoin Dadan and Rafi at the office car. Use the interaction on its right side to continue, finish the team conversation and agree on the next destination using only the information you obtained.",
                "Bergabung kembali dengan Dadan dan Rafi di mobil dinas. Gunakan interaksi di sisi kanan mobil untuk melanjutkan, tuntaskan percakapan tim, lalu sepakati tujuan berikutnya berdasarkan informasi yang sudah kamu peroleh.",
                "Fuel station parking area: the right side of the office car, then inside the car.",
                "Area parkir SPBU: sisi kanan mobil dinas, kemudian di dalam mobil.", new[] { CarKeyInventory.CarKeyId, "document.wanaseta_original", "document.field_assignment" }),
            new Detail("breakfast", "optional.chapter1.breakfast", "quest.optional.breakfast",
                "You may stop for breakfast before starting work. Order nasi uduk at the canteen and choose whether to pay now or owe the seller.",
                "Kamu dapat sarapan sebelum mulai bekerja. Pesan nasi uduk di kantin lalu pilih membayar sekarang atau berutang kepada penjual.",
                "ANRI, floor 1: canteen counter.",
                "ANRI, lantai 1: meja penjual di kantin."),
            new Detail("wrong-route", "side.chapter1.wrong_route", "quest.side.wrong_route",
                "A coworker has found a file in the wrong active shelf. Deliver it to the digitization staff and explain where it was found so they can continue scanning.",
                "Seorang pegawai menemukan berkas yang salah masuk rak aktif. Antar berkas ke petugas digitalisasi dan jelaskan tempat ditemukannya agar pemindaian dapat dilanjutkan.",
                "ANRI, floor 2: collect the file near the workroom corridor, then deliver it to the Document Processing room used by the digitization team scanning desk.",
                "ANRI, lantai 2: terima berkas di dekat koridor ruang kerja, lalu antar ke meja pemindaian di ruang Pengolahan Berkas (Tim Digitalisasi)."),
            new Detail("desk-preparation", "optional.chapter1.desk_preparation", "quest.optional.desk_preparation",
                "Return to your desk before departure and tidy the documents and personal belongings. If you choose to write today's agenda, the dated entry becomes a document you can keep.",
                "Kembali ke meja kerja sebelum berangkat lalu rapikan dokumen dan barang pribadi. Jika kamu memilih menulis agenda hari ini, catatan bertanggal itu menjadi dokumen yang dapat disimpan.",
                "ANRI, floor 2: your desk in Ucup and Rafi's Workroom.",
                "ANRI, lantai 2: meja Ucup di Ruang Kerja Ucup dan Rafi."),
            new Detail("supplies", "optional.chapter1.supplies", "quest.optional.supplies",
                "Prepare supplies after receiving the field assignment. Visit nearby shops and choose what you need; no fixed shopping list is required. Purchases produce receipts for your expenses.",
                "Siapkan perbekalan setelah menerima tugas lapangan. Kunjungi toko sekitar lalu pilih barang yang dibutuhkan; tidak ada daftar belanja wajib. Setiap pembelian menghasilkan struk pengeluaran.",
                "Shops around ANRI: convenience and general stores, electronics shop, hardware shop, pharmacy, bookshop and canteen.",
                "Toko di sekitar ANRI: minimarket dan toserba, toko elektronik, material, apotek, toko buku, dan kantin."),
            new Detail("shared-meal", "optional.chapter1.shared_meal", "quest.optional.shared_meal",
                "Visit the canteen before collecting the office car key. Sit with Dadan and Rafi, then finish the shared meal conversation to complete this optional quest.",
                "Datangi kantin sebelum mengambil kunci mobil dinas. Duduk bersama Dadan dan Rafi, lalu tuntaskan percakapan makan bersama untuk menyelesaikan quest opsional ini.",
                "ANRI, floor 1: the table where Dadan and Rafi are eating in the canteen.",
                "ANRI, lantai 1: meja tempat Dadan dan Rafi makan di kantin."),
        };

        [MenuItem("Saksi Terakhir/Ensure Quest Journal Content")]
        public static void EnsureFromMenu() => Debug.Log(Ensure());

        public static string Ensure()
        {
            const string path = "Assets/_Project/Resources/QuestJournalCatalog.asset";
            QuestJournalCatalog catalog = AssetDatabase.LoadAssetAtPath<QuestJournalCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<QuestJournalCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }
            var quests = new List<QuestDefinition>();
            foreach (QuestDefinition existing in catalog.Definitions)
                if (existing != null && !quests.Contains(existing)) quests.Add(existing);
            int configured = 0;
            foreach (Detail detail in Details)
            {
                QuestDefinition quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(QuestFolder + "/" + detail.File + ".asset");
                if (quest == null)
                {
                    Debug.LogWarning("Create canonical quest content before configuring journal details: " + detail.File);
                    continue;
                }
                if (!ConfigureDetails(quest)) continue;
                if (!quests.Contains(quest)) quests.Add(quest);
                configured++;
            }
            catalog.Configure(quests.ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return "Quest journal descriptions and locations: " + configured + "; existing IDs, objectives and scene preserved.";
        }

        // Also used by canonical content regeneration, which preserves the
        // authored quest identity and HUD objective keys.
        public static bool ConfigureDetails(QuestDefinition quest)
        {
            if (quest == null) return false;
            foreach (Detail detail in Details)
            {
                if (detail.Id != quest.Id) continue;
                string descriptionKey = detail.KeyPrefix + ".description";
                string locationKey = detail.KeyPrefix + ".location";
                quest.ConfigureDetails(descriptionKey, locationKey, detail.RequiredItems);
                AddText(descriptionKey, detail.EnglishDescription, detail.IndonesianDescription);
                AddText(locationKey, detail.EnglishLocation, detail.IndonesianLocation);
                AddText("quest.journal.location_label", "Location", "Lokasi");
                AddText("quest.journal.objectives_label", "Objectives", "Tujuan");
                EditorUtility.SetDirty(quest);
                return true;
            }
            return false;
        }

        private static void AddText(string key, string english, string indonesian)
        {
            LocalizationTable table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(LocalizationPath);
            if (table == null) return;
            var entries = new List<LocalizedTextEntry>(table.entries ?? Array.Empty<LocalizedTextEntry>());
            LocalizedTextEntry entry = entries.Find(value => value != null && value.key == key);
            if (entry == null)
            {
                entry = new LocalizedTextEntry { key = key };
                entries.Add(entry);
            }
            entry.english = english;
            entry.indonesian = indonesian;
            table.entries = entries.ToArray();
            table.ClearLookupCache();
            EditorUtility.SetDirty(table);
        }
    }
}
