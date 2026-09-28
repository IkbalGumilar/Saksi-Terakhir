using System;
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
        private const string Profiles = "Assets/_Project/Npc/Profiles/";

        [MenuItem("Saksi Terakhir/Ensure Chapter One Content")]
        public static void EnsureFromMenu() => Debug.Log(Ensure());

        public static void EnsureFromBatch() => Debug.Log(Ensure());

        public static string Ensure()
        {
            EnsureFolder("Assets/_Project", "Story");
            EnsureFolder(Root, "Quests");
            EnsureFolder(Root, "Dialogues");

            QuestDefinition rooftop = Quest("rooftop", "main.chapter1.rooftop",
                "quest.main.rooftop", "quest.main.rooftop.talk");
            QuestDefinition call = Quest("call", "main.chapter1.call", "quest.main.call",
                "quest.main.call.answer");
            QuestDefinition bossFirst = Quest("boss-first", "main.chapter1.boss_first",
                "quest.main.boss_first", "quest.main.boss_first.talk");
            QuestDefinition find = Quest("find-colleagues", "main.chapter1.find",
                "quest.main.find", "quest.main.find.raka", "quest.main.find.sinta");
            QuestDefinition escort = Quest("escort", "main.chapter1.escort",
                "quest.main.escort", "quest.main.find.raka", "quest.main.find.sinta");
            QuestDefinition bossFinal = Quest("boss-final", "main.chapter1.boss_final",
                "quest.main.boss_final", "quest.main.boss_final.listen");
            QuestDefinition key = Quest("car-key", "main.chapter1.car_key",
                "quest.main.key", "quest.main.key.collect");
            QuestDefinition vehicle = Quest("vehicle", "main.chapter1.vehicle",
                "quest.main.vehicle", "quest.main.vehicle.reach");

            DialogueSequence opening = Dialogue("rooftop", "chapter.rooftop",
                L("NPC-021", "Akhir pekan ini jadi ke pantai, kan? Aku sudah menunggu kabarnya sejak Senin."),
                L("NPC-022", "Jadi, kalau laporan hari Jumat selesai. Aku tidak mau membawa map arsip ke pasir."),
                L("PLAYER", "Kalian sudah memilih pantainya?"),
                L("NPC-021", "Belum resmi. Aku menemukan teluk kecil yang katanya tenang saat pagi."),
                L("NPC-022", "Bima, terakhir kali kamu bilang tenang, kita malah tersesat mencari parkir."),
                L("NPC-021", "Itu karena peta lama. Kali ini aku bawa petunjuk yang lebih baik."),
                L("PLAYER", "Kedengarannya seperti awal dari pekerjaan baru."),
                L("NPC-022", "Jangan bercanda. Ini liburan kantor, bukan survei lapangan."),
                L("NPC-021", "Kita berangkat Sabtu pagi. Yang lain bisa menyusul setelah sarapan."),
                L("NPC-022", "Aku akan menghubungi Raka dan Sinta. Mereka belum memberi jawaban."),
                L("PLAYER", "Kalau bos bagaimana?"),
                L("NPC-021", "Pak Arya juga diundang. Katanya ia akan ikut kalau meja kerjanya kosong."),
                L("NPC-022", "Aku belum pernah melihat meja itu benar-benar kosong."),
                L("PLAYER", "Berarti kita perlu alasan yang cukup kuat untuk menariknya keluar kantor."),
                L("NPC-021", "Pemandangan laut biasanya lebih meyakinkan daripada rapat."),
                L("NPC-022", "Aku bawa makanan. Tolong jangan biarkan Bima memegang peta sendirian."),
                L("PLAYER", "Baik. Tapi sekarang kita selesaikan urusan hari ini dulu."),
                L("NPC-021", "Setuju. Nanti sore kita pastikan siapa saja yang ikut."));

            DialogueSequence phone = Dialogue("boss-call", "chapter.boss_call",
                L("NPC-006", "Halo, ini Arya. Bisa datang ke ruangan saya sekarang?"),
                L("PLAYER", "Ada yang perlu diperiksa, Pak?"),
                L("NPC-006", "Ada catatan arsip yang mengganggu pikiran saya. Lebih baik kita bicara langsung."),
                L("PLAYER", "Saya segera ke sana."),
                L("NPC-006", "Terima kasih. Saya menunggu di ruang kerja."));

            DialogueSequence firstBriefing = Dialogue("boss-first", "chapter.boss_first",
                L("NPC-006", "Terima kasih sudah datang. Ada berkas yang ingin saya tunjukkan."),
                L("PLAYER", "Berkas apa, Pak?"),
                L("NPC-006", "Daftar permukiman dari arsip lama. Satu entri tidak cocok dengan catatan kita sekarang."),
                L("PLAYER", "Mungkin nama tempatnya sudah berubah."),
                L("NPC-006", "Itu dugaan pertama saya. Tetapi nomor berkas dan rujukannya masih konsisten."),
                L("PLAYER", "Apakah ada orang yang pernah memeriksanya langsung?"),
                L("NPC-006", "Belum. Semua laporan setelahnya hanya mengutip lembar yang sama."),
                L("PLAYER", "Jadi keberadaan tempat itu belum terkonfirmasi."),
                L("NPC-006", "Tepat. Saya tidak mau menandainya salah sebelum kita melihat kenyataan di lapangan."),
                L("PLAYER", "Apa yang harus saya lakukan?"),
                L("NPC-006", "Cari Raka dan Sinta. Kalian bertiga akan memeriksa petunjuknya bersama."),
                L("PLAYER", "Mereka tahu soal berkas ini?"),
                L("NPC-006", "Belum. Saya sengaja menunggu kamu tiba sebelum memanggil mereka."),
                L("PLAYER", "Saya akan menemukan mereka dulu."),
                L("NPC-006", "Ajak mereka ke sini. Setelah semua lengkap, saya jelaskan alamat yang tercatat."),
                L("PLAYER", "Baik, Pak. Saya kembali bersama mereka."));

            DialogueSequence rakaFirst = Dialogue("raka-first", "chapter.raka_first",
                L("PLAYER", "Raka, Pak Arya meminta kita berkumpul di ruangannya."),
                L("NPC-003", "Sekarang? Aku sedang menutup laporan pagi."),
                L("PLAYER", "Ada arsip tempat yang belum bisa dikonfirmasi. Ia ingin kita memeriksanya."),
                L("NPC-003", "Kalau begitu laporan ini bisa menunggu beberapa menit."),
                L("PLAYER", "Aku masih harus mencari Sinta."),
                L("NPC-003", "Pergilah. Aku jalan dulu ke lantai empat dan menunggu di sana."),
                L("PLAYER", "Hati-hati di tangga. Kita bertemu di ruang bos."));

            DialogueSequence sintaFirst = Dialogue("sinta-first", "chapter.sinta_first",
                L("PLAYER", "Sinta, Pak Arya memanggil kita ke ruangannya."),
                L("NPC-004", "Apakah jadwal akhir pekan berubah?"),
                L("PLAYER", "Bukan soal liburan. Ada arsip lama yang perlu kita periksa bersama."),
                L("NPC-004", "Baik. Aku rapikan meja ini sebentar lalu berangkat."),
                L("PLAYER", "Aku harus mencari Raka juga."),
                L("NPC-004", "Aku ke ruangan bos lebih dulu. Beri tahu Raka agar tidak berlama-lama."),
                L("PLAYER", "Sampai bertemu di lantai empat."));

            DialogueSequence rakaLastIntro = Dialogue("raka-last-intro", "chapter.raka_last_intro",
                L("PLAYER", "Raka, Pak Arya menunggu kita. Sinta sudah lebih dulu ke ruangannya."),
                L("NPC-003", "Baik. Beri aku sebentar untuk menutup laporan ini."),
                L("PLAYER", "Sudah siap?"),
                L("NPC-003", "Sudah. Kita jalan pelan saja; ada hal yang ingin kuceritakan."));

            DialogueSequence sintaLastIntro = Dialogue("sinta-last-intro", "chapter.sinta_last_intro",
                L("PLAYER", "Sinta, Pak Arya menunggu kita. Raka sudah lebih dulu ke ruangannya."),
                L("NPC-004", "Baik. Aku rapikan catatan ini sebentar."),
                L("PLAYER", "Kita bisa berangkat?"),
                L("NPC-004", "Bisa. Sambil jalan, mari bahas rencana liburan setelah tugas ini."));

            DialogueSequence rakaLast = Dialogue("raka-last", "chapter.raka_last",
                L("PLAYER", "Raka, kita harus kembali ke Pak Arya. Sinta sudah menunggu."),
                L("NPC-003", "Aku ikut. Kita jalan sambil bicara saja."),
                L("NPC-003", "Sejujurnya, aku sedang memikirkan sesuatu di luar pekerjaan ini."),
                L("PLAYER", "Sesuatu yang besar?"),
                L("NPC-003", "Aku ingin keluar dan membuka usaha sendiri."),
                L("PLAYER", "Kamu sudah menentukan waktunya?"),
                L("NPC-003", "Belum. Ide itu matang, tapi melangkah keluar tetap menakutkan."),
                L("PLAYER", "Wajar. Kamu tidak harus memutuskan semuanya hari ini."),
                L("NPC-003", "Aku tahu. Itulah sebabnya aku belum bercerita kepada banyak orang."),
                L("PLAYER", "Terima kasih sudah percaya padaku."),
                L("NPC-003", "Tugas dari Pak Arya ini mungkin pekerjaan lapangan terakhirku di sini."),
                L("PLAYER", "Kalau begitu kita kerjakan dengan baik."),
                L("NPC-003", "Setuju. Sesudahnya aku akan memikirkan langkah berikutnya dengan tenang."),
                L("NPC-003", "Kita sudah sampai. Mari dengar apa yang sebenarnya ada di berkas itu."));

            DialogueSequence sintaLast = Dialogue("sinta-last", "chapter.sinta_last",
                L("PLAYER", "Sinta, Pak Arya menunggu kita. Raka sudah menuju ke sana."),
                L("NPC-004", "Aku ikut. Semoga ini tidak merusak rencana akhir pekan."),
                L("PLAYER", "Bima dan Yuni sedang membahas pantai di rooftop tadi."),
                L("NPC-004", "Aku tahu. Mereka berdebat soal siapa yang membawa peta."),
                L("PLAYER", "Yuni tidak mau Bima memegangnya sendirian."),
                L("NPC-004", "Keputusan yang bijak. Aku akan mengurus kendaraan dan bekal."),
                L("PLAYER", "Setelah pekerjaan dari bos selesai?"),
                L("NPC-004", "Tentu. Aku tidak mau memesan apa pun sebelum kita tahu berapa lama tugas ini."),
                L("PLAYER", "Semoga hanya pemeriksaan singkat."),
                L("NPC-004", "Kalau selesai tepat waktu, kita bisa berangkat Sabtu sebelum jalan ramai."),
                L("PLAYER", "Aku ingin ikut melihat laut saat matahari terbit."),
                L("NPC-004", "Bagus. Nanti aku hubungi semua orang, termasuk Pak Arya."),
                L("PLAYER", "Ia benar-benar akan ikut?"),
                L("NPC-004", "Kita tanyakan setelah pengarahan. Nah, ruangannya sudah di depan."));

            DialogueSequence finalBriefing = Dialogue("boss-final", "chapter.boss_final",
                L("NPC-006", "Sekarang kalian sudah lengkap. Perhatikan lembar arsip di meja ini."),
                L("NPC-003", "Tulisan pinggirnya berbeda dari catatan utama."),
                L("NPC-006", "Benar. Catatan itu ditambahkan setelah berkas pertama disimpan."),
                L("NPC-004", "Nama desanya tidak ada di peta wilayah yang kita pakai sekarang."),
                L("NPC-006", "Dan tidak muncul dalam daftar administrasi modern mana pun yang saya periksa."),
                L("PLAYER", "Namun berkas lama ini mencatat alamatnya?"),
                L("NPC-006", "Ada petunjuk: Provinsi Arunika, di ujung selatan Kecamatan Tanjung Sagara."),
                L("NPC-003", "Bagian selatan kecamatan itu luas. Apakah ada penanda lain?"),
                L("NPC-006", "Sebuah jalan lama dan aliran sungai, tetapi keduanya mungkin sudah berubah."),
                L("NPC-004", "Bisa jadi tempat itu berganti nama atau ditinggalkan."),
                L("NPC-006", "Itulah yang harus kalian pastikan. Jangan menulis kesimpulan sebelum melihatnya."),
                L("PLAYER", "Kami mulai dari alamat di arsip, lalu memeriksa keadaan di lapangan."),
                L("NPC-006", "Catat apa yang kalian temukan, bahkan bila tampaknya tidak berhubungan."),
                L("NPC-003", "Kalau jalur lama sudah tertutup, kami cari akses dari kecamatan."),
                L("NPC-006", "Gunakan penilaian kalian. Keselamatan lebih penting daripada mengejar jadwal."),
                L("NPC-004", "Apakah kendaraan kantor tersedia?"),
                L("NPC-006", "Ya. Kunci mobil ada pada Nadia di meja resepsionis."),
                L("PLAYER", "Kami ambil kuncinya dan menyiapkan perjalanan."),
                L("NPC-006", "Kabari saya sebelum berangkat jauh. Saya ingin tahu rute yang kalian pilih."),
                L("NPC-003", "Baik, Pak. Kami mulai dari petunjuk paling jelas."));

            DialogueSequence bossSeating = Dialogue("boss-seating", "chapter.boss_seating",
                L("NPC-006", "Semua sudah hadir, silakan duduk."));

            DialogueSequence nadia = Dialogue("nadia-key", "chapter.nadia_key",
                L("PLAYER", "Nadia, Pak Arya meminta kunci mobil kantor."),
                L("NPC-002", "Saya sudah menyiapkannya. Mobil hitam terparkir di depan gedung."),
                L("PLAYER", "Kami akan memakainya untuk pemeriksaan arsip di lapangan."),
                L("NPC-002", "Ini kuncinya. Tolong catat keadaan mobil sebelum berangkat."),
                L("PLAYER", "Terima kasih. Saya akan menjaganya."),
                L("NPC-002", "Hati-hati di jalan, dan beri kabar jika jadwal kembali berubah."));

            DialogueSequence scoldRaka = Dialogue("boss-scold-raka", "chapter.boss_scold_raka",
                L("NPC-006", "Jangan kembali sendirian dulu. Raka belum kamu ajak bicara."),
                L("PLAYER", "Baik, Pak. Saya cari Raka dan kembali bersama mereka."));
            DialogueSequence scoldSinta = Dialogue("boss-scold-sinta", "chapter.boss_scold_sinta",
                L("NPC-006", "Sinta masih belum datang. Temukan dia sebelum kita mulai."),
                L("PLAYER", "Baik. Saya akan mengajaknya ke sini."));
            DialogueSequence scoldWait = Dialogue("boss-scold-wait", "chapter.boss_scold_wait",
                L("NPC-006", "Tunggu sampai kedua rekanmu berada di sini."),
                L("PLAYER", "Saya tunggu mereka masuk, Pak."));

            Offer("NPC-021-Pekerja-03", new[] { rooftop }, new[] { opening });
            Offer("NPC-022-Pekerja-04", new[] { rooftop }, new[] { opening });
            Offer("NPC-003-Rekan-A", new[] { find, escort },
                new[] { rakaFirst, rakaLastIntro, rakaLast });
            Offer("NPC-004-Rekan-B", new[] { find, escort },
                new[] { sintaFirst, sintaLastIntro, sintaLast });
            Offer("NPC-006-Bos", new[] { bossFirst, bossFinal },
                new[] { firstBriefing, bossSeating, finalBriefing });
            Offer("NPC-002-Resepsionis", new[] { key }, new[] { nadia });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "Chapter one content created or updated: 8 main quests, 15 dialogue sequences, 6 NPC offers.";
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }

        private static QuestDefinition Quest(string filename, string id, string titleKey,
            params string[] objectives)
        {
            string path = Quests + "/" + filename + ".asset";
            QuestDefinition asset = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<QuestDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.Configure(id, QuestCategory.Main, titleKey, objectives);
            if (!asset.Validate(out string error)) throw new InvalidOperationException(error);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static DialogueSequence Dialogue(string filename, string id, params DialogueLine[] lines)
        {
            string path = Dialogues + "/" + filename + ".asset";
            DialogueSequence asset = AssetDatabase.LoadAssetAtPath<DialogueSequence>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<DialogueSequence>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.Configure(id, lines);
            if (!asset.Validate(out string error)) throw new InvalidOperationException(error);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static DialogueLine L(string speaker, string text) => new DialogueLine(speaker, text);

        private static void Offer(string profileFilename, QuestDefinition[] quests,
            DialogueSequence[] dialogues)
        {
            NpcProfile profile = AssetDatabase.LoadAssetAtPath<NpcProfile>(
                Profiles + profileFilename + ".asset");
            if (profile == null) throw new InvalidOperationException("Missing NPC profile: " + profileFilename);
            profile.SetStoryOffers(quests, dialogues);
            EditorUtility.SetDirty(profile);
        }
    }
}
