using System.Collections.Generic;
using SaksiTerakhir.Localization;
using SaksiTerakhir.Story;
using UnityEngine;

namespace SaksiTerakhir.Npc
{
    /// <summary>Context supplies ordinary conversations; authored story dialogue keeps its own quest conditions.</summary>
    public static class NpcContextDialogueLibrary
    {
        private readonly struct Exchange
        {
            public readonly string First;
            public readonly string Second;
            public Exchange(string first, string second) { First = first; Second = second; }
        }

        public static DialogueLine[] BuildPlayerConversation(NpcProfile profile)
        {
            if (profile == null) return System.Array.Empty<DialogueLine>();
            string speaker = string.IsNullOrWhiteSpace(profile.Id) ? profile.DisplayName : profile.Id;
            var lines = new List<DialogueLine>();
            string[] authored = profile.DialogueLines;
            if (authored != null)
            {
                foreach (string text in authored)
                    if (!string.IsNullOrWhiteSpace(text)) lines.Add(new DialogueLine(speaker, text));
            }
            if (lines.Count > 0)
            {
                lines.Insert(1, new DialogueLine("PLAYER", Text("npc.world.player.tell_more",
                    "Ada yang perlu saya perhatikan di sini?", "Is there anything I should keep in mind here?")));
                lines.Add(new DialogueLine("PLAYER", Text("npc.world.player.thanks", "Baik, terima kasih. Saya lanjut dulu.",
                    "All right, thank you. I'll be on my way.")));
                return lines.ToArray();
            }

            Exchange exchange = PickPlayerExchange(profile);
            return new[]
            {
                new DialogueLine(speaker, Text("npc.world.open." + profile.BuildingContext,
                    OpeningIndonesian(profile), OpeningEnglish(profile))),
                new DialogueLine("PLAYER", exchange.First),
                new DialogueLine(speaker, exchange.Second),
                new DialogueLine("PLAYER", Text("npc.world.player.thanks", "Baik, terima kasih. Saya lanjut dulu.",
                    "All right, thank you. I'll be on my way."))
            };
        }

        public static string BuildGreeting(NpcProfile profile)
        {
            if (profile == null) return string.Empty;
            if (profile.Occupation == NpcOccupation.Security || profile.Department == NpcDepartment.Security)
                return Text("npc.world.greeting.security", "Selamat datang. Silakan lewat jalur pejalan kaki; kendaraan pelayanan kadang melintas di halaman.",
                    "Welcome. Please use the pedestrian path; service vehicles sometimes pass through the courtyard.");
            switch (profile.BuildingContext)
            {
                case NpcBuildingContext.Canteen:
                    return Text("npc.world.greeting.canteen", "Silakan mampir! Kalau mau makan di sini, pilih meja yang kosong dulu, ya.",
                        "Come on in! If you're eating here, please find an empty table first.");
                case NpcBuildingContext.GeneralStore:
                    return Text("npc.world.greeting.general_store", "Selamat datang. Kalau mencari perlengkapan perjalanan, raknya di sebelah sana.",
                        "Welcome. If you need travel supplies, the shelves are over there.");
                case NpcBuildingContext.ElectronicsStore:
                    return Text("npc.world.greeting.electronics", "Selamat datang! Kabel dan baterai ada di depan; kalau ingin cocokkan konektor, panggil saya saja.",
                        "Welcome! Cables and batteries are up front. Call me if you need help matching a connector.");
                case NpcBuildingContext.HardwareStore:
                    return Text("npc.world.greeting.hardware", "Silakan masuk. Hati-hati dekat tumpukan bahan; bagian berat biar kami yang ambilkan.",
                        "Come in. Watch your step near the stacked supplies; we'll handle the heavy items.");
                case NpcBuildingContext.Warehouse:
                    return Text("npc.world.greeting.warehouse", "Pagi. Jalur tengah harus tetap kosong, ya; nanti ada arsip masuk lagi.",
                        "Morning. Please keep the central aisle clear; another archive delivery is expected.");
                default:
                    return Text("npc.world.greeting.office", "Selamat datang di kantor arsip. Kalau perlu bantuan, resepsionis ada di bagian depan.",
                        "Welcome to the archive office. Reception is at the front if you need assistance.");
            }
        }

        public static void GetAmbientExchange(OfficeNpcAgent first, OfficeNpcAgent second,
            out string firstLine, out string secondLine)
            => GetAmbientExchange(first != null ? first.Profile : null, second != null ? second.Profile : null,
                out firstLine, out secondLine);

        public static void GetAmbientExchange(NpcProfile first, NpcProfile second,
            out string firstLine, out string secondLine)
        {
            Exchange selected = PickAmbientExchange(first, second);
            firstLine = selected.First;
            secondLine = selected.Second;
        }

        private static Exchange PickPlayerExchange(NpcProfile profile)
        {
            if (profile.Occupation == NpcOccupation.Cleaner)
                return Pair("player.cleaner", "Lantainya sedang dibersihkan?", "Iya. Ambil sisi yang kering saja, saya sebentar lagi selesai.",
                    "Are you cleaning this floor?", "Yes. Please use the dry side; I'll be finished soon.");
            if (profile.Occupation == NpcOccupation.Security)
                return Pair("player.security", "Kalau menuju kantor, lewat mana?", "Ikuti trotoar menuju pintu utama. Jalur dekat gudang untuk kegiatan pelayanan, jadi tetap perhatikan sekeliling.",
                    "Which way leads to the office?", "Follow the pavement to the main entrance. The warehouse route is used for service work, so stay alert there.");
            if (profile.Occupation == NpcOccupation.Courier)
                return Pair("player.courier", "Banyak berkas yang harus dibawa?", "Lumayan. Saya antar per bagian supaya tidak tertukar; yang paling repot justru memastikan tanda terimanya lengkap.",
                    "Do you have many files to deliver?", "Quite a few. I deliver them department by department to avoid mix-ups; getting every receipt signed is the hard part.");
            switch (profile.BuildingContext)
            {
                case NpcBuildingContext.Canteen:
                    return Pair("player.canteen", "Biasanya ramai jam berapa?", "Menjelang makan siang. Kalau sekarang masih tenang, nanti orang kantor datang hampir bersamaan.",
                        "When does it usually get busy?", "Just before lunch. It's quiet now, but the office staff tend to arrive all at once.");
                case NpcBuildingContext.GeneralStore:
                    return Pair("player.general_store", "Perlengkapan untuk perjalanan ada di sini?", "Ada beberapa kebutuhan harian. Periksa isi tas sebelum berangkat; barang kecil justru paling sering terlupa.",
                        "Can I find travel supplies here?", "We have some everyday essentials. Check your bag before leaving; it's usually the small things people forget.");
                case NpcBuildingContext.ElectronicsStore:
                    return Pair("player.electronics", "Kalau membawa power bank, ada yang perlu dicek?", "Cocokkan konektornya dan pastikan kabelnya sehat. Kapasitas besar tidak banyak membantu kalau kabelnya putus di dalam.",
                        "What should I check when taking a power bank?", "Match the connector and check the cable. Extra capacity won't help much if the cable is broken inside.");
                case NpcBuildingContext.HardwareStore:
                    return Pair("player.hardware", "Tali di sini beda-beda untuk apa?", "Bahan dan diameternya menentukan kegunaan. Jelaskan kebutuhanmu dulu; jangan memilih hanya karena terlihat tebal.",
                        "Why are there different ropes here?", "Their material and diameter suit different uses. Tell us what you need first; don't choose one just because it looks thick.");
                case NpcBuildingContext.Warehouse:
                    return Pair("player.warehouse", "Arsip sebanyak ini diatur bagaimana?", "Ada lokasi rak dan nomor pencatatannya. Kalau selesai mengambil berkas, kembalikan sesuai daftar, bukan sekadar ke rak yang terlihat kosong.",
                        "How do you keep this many archives organised?", "Every file has a recorded shelf location. Return it according to the register, rather than to whichever shelf looks empty.");
                case NpcBuildingContext.Outdoors:
                    return Pair("player.outdoors", "Kawasan ini biasanya sibuk?", "Pagi dan menjelang sore paling ramai. Di tengah hari lebih tenang, kecuali sedang ada pengiriman ke gudang.",
                        "Is this area usually busy?", "Mornings and late afternoons are busiest. Midday is quieter unless the warehouse is receiving a delivery.");
                default:
                    if (profile.Department == NpcDepartment.Digitization || profile.Occupation == NpcOccupation.Technician)
                        return Pair("player.digitization", "Semua arsip langsung dipindai?", "Harus diperiksa dahulu. Hasil pindai yang tajam tetap percuma kalau nomor berkas dan halaman tertukar.",
                            "Do you scan every archive straight away?", "We check them first. Even a sharp scan is useless if the file numbers or pages are mixed up.");
                    if (profile.Department == NpcDepartment.Archives || profile.Occupation == NpcOccupation.Archivist)
                        return Pair("player.archives", "Sedang mencari berkas tertentu?", "Saya cocokkan daftar dan isi map. Kadang satu lembar terselip membuat seluruh catatan terlihat tidak lengkap.",
                            "Are you looking for a particular file?", "I'm checking the register against the folders. A single misplaced sheet can make the whole record look incomplete.");
                    return Pair("player.office", "Pekerjaan hari ini banyak?", "Ada laporan dan beberapa berkas masuk. Saya selesaikan satu-satu; kalau semuanya dibuka sekaligus, justru tidak ada yang selesai.",
                        "Is there much work today?", "Some reports and a few incoming files. I handle them one at a time; opening everything at once means finishing nothing.");
            }
        }

        private static Exchange PickAmbientExchange(NpcProfile first, NpcProfile second)
        {
            NpcBuildingContext context = first != null ? first.BuildingContext : NpcBuildingContext.OfficeArchive;
            if (context == NpcBuildingContext.OfficeArchive || context == NpcBuildingContext.Warehouse)
            {
                if (Is(first, NpcDepartment.Archives) && Is(second, NpcDepartment.Digitization))
                    return ArchiveScanExchange(false);
                if (Is(first, NpcDepartment.Digitization) && Is(second, NpcDepartment.Archives))
                    return ArchiveScanExchange(true);
                if (Is(first, NpcDepartment.Administration) && Is(second, NpcDepartment.Logistics))
                    return Pair("ambient.admin_logistics", "Nomor tanda terima berkas yang tadi sudah masuk?", "Sudah. Tinggal satu bagian yang belum tanda tangan; saya susul setelah pengantaran ini.",
                        "Has the receipt number for those files been entered?", "Yes. One department still needs to sign; I'll follow up after this delivery.");
                if (Is(first, NpcDepartment.Logistics) && Is(second, NpcDepartment.Administration))
                    return Pair("ambient.logistics_admin", "Berkas pengantaran sudah saya taruh di meja administrasi.", "Terima kasih. Saya cocokkan dengan daftarnya sebelum jam makan siang.",
                        "I've left the delivery files at the administration desk.", "Thanks. I'll check them against the register before lunch.");
                if (Is(first, NpcDepartment.Facilities) || first != null && first.Occupation == NpcOccupation.Cleaner)
                    return Pair("ambient.facilities", "Sebelah sini baru saya pel. Jangan lewat yang masih basah, ya.", "Baik, saya ambil sisi sana. Nanti kalau butuh jalurnya dikosongkan, bilang saja.",
                        "I've just mopped this side. Please avoid the wet patch.", "All right, I'll use the other side. Let me know if you need the route cleared.");
                if (Is(first, NpcDepartment.Security) || Is(second, NpcDepartment.Security))
                    return Pair("ambient.security", "Kendaraan pengiriman berikutnya sudah mendapat izin masuk?", "Sudah dikonfirmasi. Jalur dekat gudang tetap kosong supaya mudah berputar.",
                        "Has the next delivery vehicle been cleared to enter?", "It's confirmed. We're keeping the warehouse route clear so it can turn.");
            }
            int variant = Random.Range(0, 3);
            switch (context)
            {
                case NpcBuildingContext.Canteen:
                    if (variant == 0) return Pair("ambient.canteen.0", "Kalau anak kantor datang barengan, meja depan langsung penuh.", "Makanya gelas saya siapkan dulu. Pesanan makan bisa menyusul, minumnya jangan sampai terlambat.", "The front tables fill up when the office staff arrive together.", "That's why I get the glasses ready first. Food can follow, but drinks shouldn't take too long.");
                    if (variant == 1) return Pair("ambient.canteen.1", "Aromanya sudah bikin lapar sebelum jam istirahat.", "Jangan habiskan lauknya dulu. Nanti pelanggan datang, kita malah menyajikan cerita.", "The smell is making me hungry before break time.", "Don't eat all the side dishes. Otherwise we'll only have stories left to serve customers.");
                    return Pair("ambient.canteen.2", "Meja sebelah sudah dibersihkan?", "Sudah. Saya sisakan jalur tengah supaya yang membawa baki tidak saling senggol.", "Has that table been cleaned?", "Yes. I've kept the middle aisle clear so people with trays don't bump into each other.");
                case NpcBuildingContext.GeneralStore:
                    if (variant == 0) return Pair("ambient.store.0", "Barang kecil cepat habis, padahal raknya kelihatan penuh.", "Yang sering dicari taruh depan saja. Pelanggan tidak perlu keliling untuk satu baterai.", "The small items sell out quickly even when the shelves look full.", "Keep the common ones up front. Customers shouldn't have to circle the shop for one battery.");
                    if (variant == 1) return Pair("ambient.store.1", "Pengiriman pagi ini sudah dihitung?", "Sudah, jumlahnya cocok. Tinggal rapikan dus supaya tidak menutup jalan.", "Have you counted this morning's delivery?", "Yes, the numbers match. We just need to move the boxes out of the aisle.");
                    return Pair("ambient.store.2", "Ada pelanggan mencari bekal perjalanan tadi.", "Biasanya baru ingat perlengkapan ketika sudah hampir berangkat. Untung masih sempat mampir.", "A customer was asking for travel supplies earlier.", "People often remember them just before leaving. At least they still had time to stop here.");
                case NpcBuildingContext.ElectronicsStore:
                    if (variant == 0) return Pair("ambient.electronics.0", "Dua kabel ini mirip, tapi konektornya berbeda.", "Pisahkan labelnya. Nanti yang disalahkan perangkatnya, padahal kabelnya tidak cocok.", "These two cables look alike, but their connectors differ.", "Give them separate labels. Otherwise people blame the device when the cable doesn't fit.");
                    if (variant == 1) return Pair("ambient.electronics.1", "Unit servis yang tadi ternyata hanya konektornya kotor.", "Kadang perbaikannya sederhana. Yang lama itu menjelaskan kenapa perangkat tidak perlu langsung diganti.", "That service unit only had a dirty connector.", "Sometimes the repair is simple. Explaining why the device doesn't need replacing takes longer.");
                    return Pair("ambient.electronics.2", "Display yang di depan sudah dicoba?", "Sudah. Kabel cadangannya saya simpan di laci supaya tidak tertukar dengan barang jualan.", "Have you checked the front display unit?", "Yes. Its spare cable is in the drawer so it won't get mixed up with stock.");
                case NpcBuildingContext.HardwareStore:
                    if (variant == 0) return Pair("ambient.hardware.0", "Tali nilon yang ini jangan dicampur dengan tali serat alam.", "Siap. Kegunaannya berbeda, walau sama-sama bisa digulung rapi.", "Don't mix this nylon rope with the natural-fibre rope.", "Understood. They serve different purposes even if both coil neatly.");
                    if (variant == 1) return Pair("ambient.hardware.1", "Bahan berat pindahkan ke sisi bawah rak saja.", "Iya, supaya tidak perlu mengangkat tinggi dan raknya lebih stabil.", "Keep the heavy supplies on the lower shelves.", "Yes, that avoids lifting them too high and keeps the rack steadier.");
                    return Pair("ambient.hardware.2", "Pesanan tadi minta ukuran yang sama persis.", "Ukur lagi sebelum dipotong. Salah sedikit masih tetap salah kalau sudah sampai lokasi.", "That order needs the exact same dimensions.", "Measure again before cutting. A small error is still an error once it reaches the site.");
                case NpcBuildingContext.Warehouse:
                    return Pair("ambient.warehouse." + variant, "Rak ini sudah cocok dengan daftar lokasi?", "Sudah saya cek. Berkas yang belum dicatat saya pisahkan dulu di meja penerimaan.", "Does this rack match the location register?", "I've checked it. Unregistered files are separate at the receiving desk.");
                case NpcBuildingContext.Outdoors:
                    if (variant == 0) return Pair("ambient.outdoors.0", "Trotoar sini lumayan nyaman kalau belum ramai.", "Iya. Kalau ada pengiriman, saya biasanya menunggu di sisi yang tidak menutup jalan.", "This pavement is pleasant before it gets busy.", "Yes. When a delivery arrives, I wait on the side that doesn't block the route.");
                    if (variant == 1) return Pair("ambient.outdoors.1", "Kamu hendak ke kantin juga?", "Sebentar lagi. Saya selesaikan urusan di toko dulu supaya tidak bolak-balik.", "Are you going to the canteen too?", "Soon. I'll finish at the shop first so I don't have to walk back and forth.");
                    return Pair("ambient.outdoors.2", "Hari ini panasnya cepat sekali terasa.", "Untung ada tempat berteduh. Jalan pelan saja, yang penting tidak berhenti di tengah trotoar.", "It's getting hot quickly today.", "At least there's some shade. We can walk slowly; we just shouldn't stop in the middle of the pavement.");
                default:
                    if (variant == 0) return Pair("ambient.office.0", "Map yang tadi ada di meja mana?", "Di sebelah daftar masuk. Jangan tertukar dengan berkas yang sudah selesai diperiksa.", "Which desk has that folder?", "It's beside the incoming register. Don't mix it with the files already checked.");
                    if (variant == 1) return Pair("ambient.office.1", "Printer tiba-tiba berhenti waktu saya sudah hampir selesai.", "Cek baki kertas dulu. Kemarin saya menyalahkan komputer, ternyata kertasnya habis.", "The printer stopped just as I was almost finished.", "Check the paper tray first. Yesterday I blamed the computer, but the paper had run out.");
                    return Pair("ambient.office.2", "Istirahat nanti mampir kantin?", "Iya, setelah laporan ini selesai. Kalau saya tinggal sekarang, nanti lupa sudah sampai bagian mana.", "Shall we stop at the canteen during break?", "Yes, after this report. If I leave now, I'll forget where I got to.");
            }
        }

        private static Exchange ArchiveScanExchange(bool reversed)
        {
            Exchange exchange = Pair("ambient.archives_digitization", "Map yang ini sudah cocok dengan nomor di daftar. Bisa masuk antrean pindai?", "Bisa. Saya cek urutan halamannya dahulu supaya hasil digitalnya mengikuti berkas asli.",
                "This folder matches the register number. Can it join the scanning queue?", "Yes. I'll check the page order first so the digital copy follows the original.");
            return reversed ? new Exchange(exchange.Second, exchange.First) : exchange;
        }

        private static bool Is(NpcProfile profile, NpcDepartment department)
            => profile != null && profile.Department == department;

        private static Exchange Pair(string key, string firstId, string secondId, string firstEn, string secondEn)
            => new Exchange(Text("npc.world." + key + ".first", firstId, firstEn),
                Text("npc.world." + key + ".second", secondId, secondEn));

        private static string Text(string key, string indonesian, string english)
        {
            string translated = LocalizationManager.Get(key);
            if (!string.IsNullOrWhiteSpace(translated) && translated != key) return translated;
            return LocalizationManager.CurrentLanguage == GameLanguage.Indonesian ? indonesian : english;
        }

        private static string OpeningIndonesian(NpcProfile profile)
        {
            if (profile.Occupation == NpcOccupation.Security) return "Selamat datang. Ada yang bisa saya bantu?";
            if (profile.Occupation == NpcOccupation.Cleaner) return "Pagi. Hati-hati, bagian ini masih agak basah.";
            switch (profile.BuildingContext)
            {
                case NpcBuildingContext.Canteen: return "Silakan. Mau istirahat sebentar atau sedang mencari seseorang?";
                case NpcBuildingContext.GeneralStore: return "Silakan lihat-lihat. Kalau perlu bantuan mencari barang, bilang saja.";
                case NpcBuildingContext.ElectronicsStore: return "Halo. Sedang mencari perlengkapan atau mau bertanya dulu?";
                case NpcBuildingContext.HardwareStore: return "Silakan. Kalau butuh bahan, sebutkan kegunaannya dulu, ya.";
                case NpcBuildingContext.Warehouse: return "Pagi. Saya sedang mencocokkan arsip di bagian ini.";
                case NpcBuildingContext.Outdoors: return "Halo. Ada yang bisa saya bantu?";
                default: return "Pagi. Ada keperluan dengan bagian kami?";
            }
        }

        private static string OpeningEnglish(NpcProfile profile)
        {
            if (profile.Occupation == NpcOccupation.Security) return "Welcome. Can I help you?";
            if (profile.Occupation == NpcOccupation.Cleaner) return "Morning. Careful, this part is still a little wet.";
            switch (profile.BuildingContext)
            {
                case NpcBuildingContext.Canteen: return "Come in. Taking a break, or looking for someone?";
                case NpcBuildingContext.GeneralStore: return "Feel free to look around. Let me know if you need help finding something.";
                case NpcBuildingContext.ElectronicsStore: return "Hello. Are you looking for equipment, or would you like to ask something first?";
                case NpcBuildingContext.HardwareStore: return "Come in. If you need supplies, tell us what they're for first.";
                case NpcBuildingContext.Warehouse: return "Morning. I'm checking the archives in this section.";
                case NpcBuildingContext.Outdoors: return "Hello. Can I help you?";
                default: return "Morning. Do you need something from our department?";
            }
        }
    }
}
