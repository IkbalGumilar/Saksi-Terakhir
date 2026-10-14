# Saksi Terakhir

**Bahasa Indonesia** · [English — The Last Witness](README_EN.md)

Game horor psikologis dan misteri 3D dengan sudut pandang orang pertama, berlatar Indonesia. Seorang pegawai arsip dan dua rekannya menelusuri sebuah desa yang ditemukan dalam catatan lama. Pemain mengikuti percakapan, membaca dokumen, dan mengambil keputusan yang dicatat sebagai kondisi cerita.

Repositori ini berisi proyek Unity, kode gameplay, aset, prefab, dan sumber Blender. Dokumentasi berikut menjelaskan implementasi yang tersedia saat ini tanpa mengungkap inti misterinya.

## Status pengembangan

Proyek masih dalam pengembangan. Sistem Bab 1 mencakup kegiatan di kantor, pencarian rekan, persiapan keberangkatan, perjalanan mobil otomatis ke SPBU, kegiatan di SPBU, dan penutup dengan layar hitam. Di Unity Editor, penutup menghentikan Play Mode setelah progres berhasil disimpan. Dalam build permainan, penutup membekukan gameplay pada layar hitam; transisi ke bab berikutnya belum tersedia.

Scene utama adalah [Regional Archive Office](Assets/_Project/Scenes/Regional%20Archive%20Office.unity), satu-satunya scene yang saat ini diaktifkan dalam [konfigurasi build](ProjectSettings/EditorBuildSettings.asset). Scene desa dan scene perakitan aset juga tersedia. Keberadaan aset dan kode tidak berarti seluruh permainan sudah selesai atau setiap alur sudah diuji pada semua perangkat.

Versi dan referensi dokumentasi ini diperiksa dari berkas proyek pada **11 Oktober 2026**. Pembaruan README tidak menjalankan Play Mode atau pengujian performa.

## Teknologi

| Komponen | Versi / penggunaan |
| --- | --- |
| Unity | **6000.3.24f1 — Unity 6.3 LTS** |
| Universal Render Pipeline | **17.3.0** |
| Input System | **1.19.0** |
| AI Navigation | **2.0.11** |
| uGUI | **2.0.0**, dengan TextMeshPro untuk teks |
| Unity Test Framework | **1.6.0** |
| Timeline | **1.8.11** |
| Bahasa pemrograman | C#; assembly runtime dan Editor terpisah |
| Lokalisasi | Tabel teks khusus Bahasa Indonesia dan Inggris |
| Penyimpanan | Snapshot JSON dengan enkripsi dan autentikasi |
| Pembuatan aset | Blender, model FBX, material, tekstur, dan prefab Unity |

Versi Editor mengikuti [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt). Dependensi tercatat dalam [manifest.json](Packages/manifest.json) dan [packages-lock.json](Packages/packages-lock.json). Paket yang terpasang tidak otomatis menandakan adanya fitur gameplay seperti multiplayer.

## Membuka proyek

1. Siapkan Unity Hub dan Unity **6000.3.24f1**. Tambahkan modul build sesuai platform tujuan jika diperlukan.
2. Tambahkan folder repositori melalui Unity Hub, lalu buka dengan versi tersebut.
3. Tunggu pemulihan paket, impor aset, dan kompilasi selesai. `Library` akan dibangun ulang bila belum tersedia.
4. Buka [Regional Archive Office.unity](Assets/_Project/Scenes/Regional%20Archive%20Office.unity).
5. Jalankan Play Mode. Gunakan **Permainan Baru** untuk memulai atau **Lanjutkan** jika ada autosave valid.

Scene dan aset sudah disimpan di repositori; menjalankan ulang semua alat pembuat scene bukan syarat untuk membuka proyek. Pengembangan dilakukan di Ubuntu, tetapi kompatibilitas build dan kebutuhan perangkat minimum tetap perlu diuji pada platform tujuan. Belum ada tolok ukur performa atau spesifikasi minimum resmi.

## Kontrol

Binding berada di [InputSystem_Actions.inputactions](Assets/InputSystem_Actions.inputactions) dan dibaca oleh [PlayerInputReader](Assets/_Project/Scripts/Runtime/Player/PlayerInputReader.cs).

| Tindakan | Keyboard / mouse |
| --- | --- |
| Bergerak | WASD atau tombol panah |
| Melihat | Gerakan mouse |
| Berlari | Left Shift |
| Merunduk | C; tersedia konfigurasi tahan atau toggle |
| Melompat | Space |
| Berinteraksi | E |
| Buka / tutup inventaris dan jurnal | Tab |
| Tutup inventaris / permintaan pause | Escape |
| Pilih barang atau misi | Klik kiri |
| Menu barang | Klik kanan |
| Pindahkan barang | Drag and drop |

Binding gamepad juga tersedia: stick kiri/kanan untuk bergerak/melihat, tekan stick kiri untuk berlari, B untuk merunduk, A untuk melompat, Y untuk berinteraksi, Select untuk jurnal, dan Start untuk pause. Dukungan ini masih memerlukan pengujian pada perangkat fisik.

Header inventaris memiliki halaman **Inventory**, **Dokumen**, dan **Misi**, dengan pilihan klik atau navigasi panah. Dialog cerita maju otomatis sesuai durasi teks. Saat telepon, pemain tetap dapat bergerak dan melihat, sementara inventaris dan pause diblokir. Dalam mobil, kendaraan berjalan otomatis dan pemain dapat menggerakkan kamera. Dialog perjalanan selain telepon mengizinkan inventaris dan pause.

`Escape` menutup inventaris terlebih dahulu jika sedang terbuka. [PlayerPauseController](Assets/_Project/Scripts/Runtime/Player/PlayerPauseController.cs) menyediakan mekanisme pause dan penghubung UI; permintaan pause memerlukan panel atau penanganan yang terpasang. Membuka inventaris mengunci kontrol pemain, tetapi tidak menghentikan waktu dunia.

## Sistem gameplay

### Pemain dan interaksi

[PlayerController](Assets/_Project/Scripts/Runtime/Player/PlayerController.cs) menangani berjalan, berlari, merunduk, gravitasi, lompatan, dan gerak sinematik. [PlayerCameraController](Assets/_Project/Scripts/Runtime/Player/PlayerCameraController.cs) mengatur pandangan dan preview menu, sedangkan [PlayerInteractor](Assets/_Project/Scripts/Runtime/Interaction/PlayerInteractor.cs) mencari target interaksi dan menampilkan prompt.

Gerak otomatis menggunakan pemeriksaan medan dan navigasi untuk mencari pijakan serta menghindari halangan. Persiapan percakapan NPC mencari posisi dengan jarak sekitar **2 meter**, sesuai ruang yang dapat dicapai. Idle napas dan gerakan melihat kanan-kiri pada menu memiliki penanganan terpisah.

### Inventaris, dokumen, dan barang yang dipegang

| Bagian | Perilaku |
| --- | --- |
| Grid barang | Default **3 × 3**; dapat diatur **1–8 kolom** dan **1–8 baris**, hingga **64 slot** |
| Dokumen | Daftar terpisah; tidak memakai kapasitas grid barang dan tidak dapat dibuang seperti barang fisik |
| Pemindahan | Barang dapat dipindah atau ditukar antar-slot melalui drag and drop |
| Buang | Menu Buang atau drag ke luar batas panel barang membuat objek 3D dengan collider dan Rigidbody |
| Ambil kembali | Objek pickup dapat diambil kembali dengan identitas instance yang dipertahankan |
| Penuh | Penambahan barang ditolak dan notifikasi inventaris penuh ditampilkan |
| Pegang | Maksimal dua barang; barang ketiga menggantikan yang paling lama dipegang |
| Gunakan | Barang yang cocok di tangan dipakai langsung pada target; barang yang tersimpan dipilih lewat inventaris |

Ukuran grid dapat diperbesar melalui `TrySetGridSize`; penemuan tas dan seluruh aturan upgrade masih merupakan konten yang perlu disambungkan. Slot diduplikasi dari template UI dengan jarak **100 unit**. Penanda genggam dan model tangan diperbarui mengikuti status barang.

Definisi menggunakan [ItemDefinition](Assets/_Project/Scripts/Runtime/Inventory/ItemDefinition.cs), sebuah ScriptableObject:

| Atribut | Isi |
| --- | --- |
| ID | Identitas tetap, misalnya `item.car_key` |
| Nama | Key teks lokalisasi |
| Deskripsi | Key teks lokalisasi |
| Jenis | `Item` atau `Document` |
| Icon | Sprite slot |
| Prefab Model | Model 3D; boleh kosong untuk dokumen |
| Isi Dokumen | Teks bacaan untuk dokumen |

[Catalog barang](Assets/_Project/Resources/ItemDefinitionCatalog.asset) menghubungkan definisi dengan sistem. Definisi berada di [Items](Assets/_Project/Items), icon di [Items/Image](Assets/_Project/Items/Image), dan objek pickup di [Prefabs/Inventory/Pickups](Assets/_Project/Prefabs/Inventory/Pickups). Barang bermodel yang tersedia meliputi kunci/remote mobil, senter, baterai Li-ion 18650, power bank, tali tambang 10 meter, tali nilon 10 meter, nasi bungkus, kotak P3K, dan buku agenda.

Efek penggunaan bergantung pada handler atau target yang dipasang; mempunyai model dan definisi belum berarti setiap barang memiliki efek gameplay lengkap. Alur utama terdapat dalam [InventoryPanelController](Assets/_Project/Scripts/Runtime/Inventory/InventoryPanelController.cs), dengan logika data di [InventoryState](Assets/_Project/Scripts/Runtime/Inventory/InventoryState.cs).

### Quest, percabangan, dan dialog

[ChapterOneProgress](Assets/_Project/Scripts/Runtime/Story/ChapterOneProgress.cs) mengatur tahapan Bab 1. [ChapterOneStoryState](Assets/_Project/Scripts/Runtime/Story/ChapterOneStoryState.cs) menyimpan pilihan dan fakta cerita, sedangkan [ChapterOneConditionEvaluator](Assets/_Project/Scripts/Runtime/Story/ChapterOneConditionEvaluator.cs) memeriksa syarat quest, dialog, barang, dan dokumen.

Definisi [quest](Assets/_Project/Story/Quests) dan [dialog](Assets/_Project/Story/Dialogues) dipisahkan dari pengendali adegan. Aktivitas mencakup persiapan kantor, sarapan, pencarian rekan, makan bersama, ATM, pembelian perlengkapan, dan kegiatan SPBU. Penawaran toko yang tersedia mengikuti aktivitas Bab 1; ekonomi perdagangan umum belum menjadi sistem lengkap.

Jurnal mendukung **Main**, **Side**, dan **Optional Quest**, termasuk status aktif, selesai, dan dilewati. Quest utama mengikuti progres cerita. Pemain dapat memilih satu quest pendukung untuk ditampilkan di bawahnya: **kuning** untuk utama, **hijau** untuk side, dan **biru** untuk optional. [QuestJournalCatalog](Assets/_Project/Resources/QuestJournalCatalog.asset) berisi definisi jurnal.

Notifikasi memakai antrean agar tidak menumpuk. Saat checkpoint quest utama berubah, notifikasi quest selesai lebih dahulu, kemudian penyimpanan dilakukan dengan logo autosave yang berkedip perlahan.

### NPC dan populasi dunia

[NpcPopulationManager](Assets/_Project/Scripts/Runtime/Npc/NpcPopulationManager.cs) mengelola titik spawn, pemilihan profil, pool reuse, serta spawn/despawn berbasis jarak. Titik aktivitas mendukung berjalan, bekerja sambil duduk, mengetik, membersihkan, membawa dokumen, dan berjaga. NPC yang diperlukan misi, mempunyai perintah cerita, atau sedang bercakap mendapat perlindungan sesuai statusnya.

Batas **15 NPC** adalah batas NPC yang ditampilkan oleh pengelola renderer dalam scene, termasuk NPC misi. Jumlah aktor aktif dapat lebih besar. NPC misi yang disembunyikan tetap dapat dipertahankan; berkurangnya jarak pandang tidak otomatis menghancurkan peserta misi.

[NpcProfile](Assets/_Project/Scripts/Runtime/Npc/NpcProfile.cs) memuat identitas, peran, pekerjaan, departemen, konteks bangunan, rute, serta `interactable`. Nilai `false` menonaktifkan percakapan langsung dengan pemain, sementara percakapan ambient dan sapaan masih dapat berjalan. Konteks percakapan berbeda menurut bangunan dan pekerjaan; sapaan satu arah menggunakan memori pemicu agar tidak terus berulang.

NPC berpapasan memakai prioritas **0–10**, default **1**. Nilai lebih rendah mengalah; prioritas sama memilih pihak yang mengalah secara acak untuk satu pertemuan. Pemain tidak memakai prioritas NPC ini. Navigasi memeriksa medan, passage, pintu, dan NavMesh/link; kelancaran tetap bergantung pada collider, lebar jalur, dan data navigasi scene.

### Pintu dan kendaraan

[DoorInteractable](Assets/_Project/Scripts/Runtime/Interaction/DoorInteractable.cs) mendukung pintu ayun, geser, dan roll-up, kunci barang, serta pemeriksaan jalur. Pintu yang dibuka pemain menjadwalkan tutup otomatis setelah **4–7 detik** secara acak; pembukaan NPC atau gerak otomatis menggunakan **1 detik**. Jalur yang masih ditempati menunda penutupan. Setiap pintu tetap memerlukan komponen dan konfigurasi yang sesuai.

[VehicleKeyUse](Assets/_Project/Scripts/Runtime/VehicleKeyUse.cs) menangani remote mobil dengan jangkauan default **5 meter**, pembukaan pintu pengemudi, dan posisi duduk berdasarkan kondisi cerita. [VehicleSignalFeedback](Assets/_Project/Scripts/Runtime/VehicleSignalFeedback.cs) menyediakan lampu sein dan referensi audio yang dapat diisi.

[VehicleJourneyDirector](Assets/_Project/Scripts/Runtime/VehicleJourneyDirector.cs), [AutomaticVehicleDriver](Assets/_Project/Scripts/Runtime/AutomaticVehicleDriver.cs), dan [VehicleRoutePath](Assets/_Project/Scripts/Runtime/VehicleRoutePath.cs) menjalankan rute otomatis kantor–SPBU dan keberangkatan dari SPBU. Kamera dapat digerakkan saat duduk; perjalanan dan posisi kendaraan ikut dalam snapshot simpanan.

[FuelStationChapterDirector](Assets/_Project/Scripts/Runtime/Story/FuelStationChapterDirector.cs) mengelola kegiatan SPBU, pilihan dialog/dokumen, kembali ke mobil, dan penutup Bab 1. Lanjutan cerita desa belum disambungkan menjadi transisi bab yang lengkap.

## Simpan, muat, dan menu utama

[GameSessionController](Assets/_Project/Scripts/Runtime/Persistence/GameSessionController.cs) mengatur sesi dan menu:

| Pilihan | Perilaku saat ini |
| --- | --- |
| Permainan Baru | Memuat ulang scene dengan kondisi permainan baru |
| Lanjutkan | Melanjutkan snapshot yang sudah dimuat untuk preview menu, setelah animasi fade |
| Muat Permainan | Membaca ulang autosave yang sama dan memuat ulang scene aktif |

Tanpa simpanan valid, **Lanjutkan** dan **Muat Permainan** disembunyikan. Kontrol gameplay dihentikan selama menu utama terbuka. Preview memeriksa pandangan dan ruang di sekitar pemain; **Lanjutkan mempertahankan posisi/pandangan yang telah disesuaikan**, sesuai kondisi preview. Pemilihan banyak slot dan penyimpanan manual melalui UI belum tersedia.

Autosave berada di `Application.persistentDataPath/autosave.json`. [GameSaveSnapshot](Assets/_Project/Scripts/Runtime/Persistence/GameSaveSnapshot.cs) dan [GameWorldSnapshot](Assets/_Project/Scripts/Runtime/Persistence/GameWorldSnapshot.cs) memuat progres/cabang, jurnal, pose pemain dan kamera, barang/dokumen, posisi slot dan genggaman, pintu, objek pickup/drop, sapaan, aktor cerita yang perlu dipertahankan, serta perjalanan kendaraan. Populasi ambient biasa dapat dibangun ulang dan tidak seluruhnya dimasukkan ke save.

[ProtectedJsonSaveStore](Assets/_Project/Scripts/Runtime/Persistence/ProtectedJsonSaveStore.cs) menyimpan envelope JSON berversi dengan **AES-256-CBC**, IV acak, dan **HMAC-SHA256**. Penulisan memakai file sementara dan penggantian atomik; [GameSaveRepository](Assets/_Project/Scripts/Runtime/Persistence/GameSaveRepository.cs) dapat memulihkan cadangan `.bak` yang valid. Format ini menghambat modifikasi biasa dan mendeteksi kerusakan. Kunci berada dalam aplikasi, sehingga perlindungannya tidak kebal terhadap rekayasa balik. Steam Cloud belum diintegrasikan.

## Waktu dan lokalisasi

[TimeOfDayPreset](Assets/_Project/Scripts/Runtime/Environment/TimeOfDayPreset.cs) dan [TimeOfDayApplier](Assets/_Project/Scripts/Runtime/Environment/TimeOfDayApplier.cs) mengatur skybox, exposure/tint, cahaya matahari atau bulan, ambient, dan kabut. Tujuh preset tersedia di [Environment](Assets/_Project/Environment):

| Preset | Waktu |
| --- | --- |
| Dini hari | 05.00 |
| Pagi | 07.00 |
| Siang pertama | 11.00 |
| Siang kedua | 13.00 |
| Sore | 17.00 |
| Malam | 20.00 |
| Tengah malam | 00.00 |

Preset digunakan oleh menu dan cerita; simulasi cuaca serta siklus waktu bebas belum tersedia.

[LocalizationTable.asset](Assets/_Project/Localization/LocalizationTable.asset) menyimpan teks Indonesia dan Inggris. [LocalizationManager](Assets/_Project/Scripts/Runtime/Localization/LocalizationManager.cs) mengelola key, fallback, pembaruan tampilan, dan preferensi bahasa melalui `PlayerPrefs`. Bahasa default aset saat ini adalah Indonesia. Judul Inggris dalam README merupakan terjemahan judul; nama proyek dan namespace kode tetap `SaksiTerakhir`.

## Struktur proyek dan scene

| Lokasi | Isi |
| --- | --- |
| [Assets/_Project/Scripts/Runtime](Assets/_Project/Scripts/Runtime) | Pemain, interaksi, inventaris, cerita, NPC, navigasi, penyimpanan, lingkungan, dan kendaraan |
| [Assets/_Project/Scripts/Editor](Assets/_Project/Scripts/Editor) | Alat konfigurasi, pembuat aset, dan pemeriksaan Editor |
| [Assets/_Project/Tests](Assets/_Project/Tests) | Pengujian EditMode dan PlayMode |
| [Assets/_Project/Scenes](Assets/_Project/Scenes) | Scene gameplay, perakitan, dan studio potret |
| [Assets/_Project/Prefabs](Assets/_Project/Prefabs) | Pemain, NPC, kendaraan, pickup, dan lingkungan gabungan |
| [Assets/_Project/Art](Assets/_Project/Art) | Model, material, dan tekstur untuk konten utama |
| [Assets/_Project/Items](Assets/_Project/Items) | Definisi barang/dokumen dan icon |
| [Assets/_Project/Story](Assets/_Project/Story) | Definisi quest dan dialog |
| [Assets/_Project/Npc](Assets/_Project/Npc) dan [Data/Npc](Assets/_Project/Data/Npc) | Profil NPC kantor dan populasi |
| [Assets/_Project/Resources](Assets/_Project/Resources) | Catalog barang dan jurnal yang dimuat runtime |
| [Assets/_Project/Navigation](Assets/_Project/Navigation) | Data navigasi lingkungan |
| [Assets/_Project/UI](Assets/_Project/UI) | Font dan aset UI |
| [Assets/ModularAsset](Assets/ModularAsset) | Kit bangunan, jalan, vegetasi, dan perlengkapan |
| [Assets/Settings](Assets/Settings) | Konfigurasi render pipeline dan volume |
| [Packages](Packages) / [ProjectSettings](ProjectSettings) | Dependensi dan konfigurasi Unity |
| [SourceArt](SourceArt) | Sumber Blender `.blend` |

| Scene | Kegunaan |
| --- | --- |
| [Regional Archive Office](Assets/_Project/Scenes/Regional%20Archive%20Office.unity) | Scene utama kawasan kantor dan gameplay Bab 1 |
| [Regional Vilage](Assets/_Project/Scenes/Regional%20Vilage.unity) | Scene desa untuk pengembangan lanjutan; ejaan mengikuti nama berkas |
| [OfficeArchive_Assembly](Assets/_Project/Scenes/OfficeArchive_Assembly.unity) | Perakitan kantor modular |
| [WarehouseArchive_Assembly](Assets/_Project/Scenes/WarehouseArchive_Assembly.unity) | Perakitan gudang arsip |
| [RoadLoop_Assembly](Assets/_Project/Scenes/RoadLoop_Assembly.unity) | Perakitan jaringan jalan dan kawasan |
| [ItemShow](Assets/_Project/Scenes/ItemShow.unity) | Studio potret model barang untuk icon |

Lingkungan mencakup kantor arsip empat lantai dan rooftop, parkiran, enam gudang, jalan loop dan trotoar, kawasan rumah/toko, kantin, gedung perkantoran, SPBU, taman, dan vegetasi batas dunia. Sebagian bangunan sekitar berfungsi sebagai dekorasi dengan interior kosong. Model pintu, lift, ATM, atau toko tidak selalu berarti seluruh fungsinya sudah disambungkan.

Kit modular dikelompokkan dalam `Bangunan` (struktur, rumah, rumah panggung, pom bensin), `Lingkungan` (jalan dan vegetasi), serta `Perlengkapan`. Prefab lingkungan gabungan berada di [Prefabs/Environment](Assets/_Project/Prefabs/Environment). Sumber Blender dipisahkan dari aset hasil impor agar pencarian aset Unity lebih mudah.

## Alat Editor

Menu berikut berasal dari kode yang tersedia. Alat konfigurasi dapat mengubah scene/aset; simpan pekerjaan sebelum menjalankannya dan pilih alat sesuai kebutuhan.

| Menu `Saksi Terakhir` | Sumber / fungsi |
| --- | --- |
| `Chapter One/Configure Active World` | [ChapterOneWorldSetup](Assets/_Project/Scripts/Editor/ChapterOneWorldSetup.cs): menghubungkan konten Bab 1 pada dunia aktif |
| `Inventory/Hubungkan UI Inventory` | [InventorySceneSetup](Assets/_Project/Scripts/Editor/InventorySceneSetup.cs): menghubungkan UI inventaris |
| `Inventory/Buat Catalog dan Prefab Pickup` | [InventoryItemAssetSetup](Assets/_Project/Scripts/Editor/InventoryItemAssetSetup.cs): menyiapkan catalog dan pickup |
| `Inventory/Periksa Catalog dan Prefab Pickup` | Memeriksa referensi melalui alat yang sama |
| `NPC/Set Up World Population` | [WorldNpcSceneSetup](Assets/_Project/Scripts/Editor/WorldNpcSceneSetup.cs): menyiapkan populasi NPC |
| `Doors/Configure Active Scene` | [WorldDoorSetup](Assets/_Project/Scripts/Editor/WorldDoorSetup.cs): mengonfigurasi pintu |
| `Vehicles/Connect Office To SPBU Journey` | [VehicleJourneySetup](Assets/_Project/Scripts/Editor/VehicleJourneySetup.cs): menyambungkan rute kendaraan |
| `Connect Fuel Station Chapter Ending` | [FuelStationChapterSetup](Assets/_Project/Scripts/Editor/FuelStationChapterSetup.cs): menyambungkan kegiatan dan penutup SPBU |
| `Create Time Of Day Presets` / `Time Of Day/Configure Active Scene` | [TimeOfDaySetup](Assets/_Project/Scripts/Editor/TimeOfDaySetup.cs): preset dan penerapan waktu |
| `ItemShow/Buat Ulang Studio Foto` | [ItemShowPhotoStudioSetup](Assets/_Project/Scripts/Editor/ItemShowPhotoStudioSetup.cs): studio potret barang |

## Pengujian dan pedoman perubahan

[Pengujian Editor](Assets/_Project/Tests/Editor) mencakup state inventaris, penggunaan/drop, quest/kondisi, save, menu, pintu, NPC/navigasi, duduk, dan perjalanan kendaraan. [Pengujian PlayMode](Assets/_Project/Tests/PlayMode) mencakup alur kantor Bab 1, bootstrap sesi, serta fisika inventaris.

Buka **Window > General > Test Runner** untuk menjalankan suite **EditMode** atau **PlayMode**. Pemeriksaan referensi berkas atau keberhasilan kompilasi tidak menggantikan pengujian alur permainan. Pengujian scene besar sebaiknya dilakukan pada satu Editor aktif untuk menghindari beban memori tambahan.

Saat mengubah kode, gunakan nama yang menjelaskan maksud, konfigurasi Inspector yang terkontrol, dan tanggung jawab komponen yang jelas. Pertahankan ID barang/quest, key lokalisasi, kompatibilitas save, GUID, serta referensi scene/prefab. Pisahkan aturan data dari tampilan bila itu mempermudah pemeliharaan; perubahan UI dan layout perlu mengikuti scene yang dibuat pengembang.

## Kebersihan repositori dan dokumentasi

[.gitignore](.gitignore) mengabaikan cache Unity, hasil build, berkas IDE yang dihasilkan, konfigurasi pribadi agen, dump crash, cache Python, dan cadangan Blender. Aset sumber beserta `.meta`, `Packages`, `ProjectSettings`, sumber `.blend`, dan kedua README tetap dapat masuk version control. Perubahan aturan ignore tidak otomatis mengeluarkan berkas yang sudah dilacak Git.

Catatan kode, dokumentasi pendukung, dan alat pembuatan dokumen disimpan **di luar checkout**, di `/home/ikbal/Dokumen/Dokumentasi Saksi Terakhir` pada workspace pengembang. Dua naskah sumber dialog Bab 1 berada secara lokal di `docs/bab-01.md` dan `docs/bab-01-branch.md`; keduanya diabaikan Git untuk menjaga spoiler. Naskah tersebut diperlukan saat membuat ulang konten dialog, sementara permainan memakai aset dialog yang sudah tersimpan. Berkas sementara dibuat di `/tmp`. `SourceArt` di proyek hanya menampung sumber Blender. README Indonesia dan Inggris tetap berada di root repositori.

## Lisensi

Belum ada berkas lisensi umum untuk kode dan aset proyek di root repositori. Lisensi font pihak ketiga tetap tersedia di [UI/Fonts/Licenses](Assets/_Project/UI/Fonts/Licenses). Periksa izin pemilik proyek sebelum menggunakan atau mendistribusikan kode dan aset di luar proyek ini.
