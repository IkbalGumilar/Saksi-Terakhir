# Rancangan main quest bab pembuka: dari rooftop sampai mobil

Tanggal: 26 September 2026

Status: rancangan untuk ditinjau sebelum implementasi

## Tujuan dan batas

Bab pembuka dimulai otomatis saat permainan baru dimulai. Pemain bertemu dua pekerja di rooftop, menerima panggilan bos, mencari dua rekan kerja dalam urutan bebas, mendengar pengarahan akhir, mengambil kunci mobil dari resepsionis, lalu mendekati mobil. Bab ini berhenti sebelum pemain masuk atau mengemudikan mobil.

“Level pertama” dalam percakapan ini berarti tahap cerita pertama. Game tidak memakai level pemain atau XP. Semua quest pada bab ini berkategori `Main`, ditandai titik kuning dan tampil paling atas. Definisi quest adalah asset; status berjalan milik pemain disimpan terpisah.

Pemeran yang sudah ada dipertahankan:

| Peran | NPC | Posisi awal cerita | Perilaku sampai dipanggil |
| --- | --- | --- | --- |
| Pekerja rooftop | Bima (`NPC-021`) dan Yuni (`NPC-022`) | Dua titik kerja rooftop | Menunggu di rooftop sampai percakapan pembuka selesai, lalu kembali ke rute kerja lama |
| Rekan A | Raka (`NPC-003`) | Titik kerja lobi lantai 1 | Tetap di titik kerja sampai pemain berbicara dengannya pada tahap pencarian |
| Rekan B | Sinta (`NPC-004`) | Titik kerja lantai 2 | Tetap di titik kerja sampai pemain berbicara dengannya pada tahap pencarian |
| Bos | Arya (`NPC-006`) | Ruang bos lantai 4 | Menunggu dalam posisi duduk di sofa sepanjang bab |
| Resepsionis | Nadia (`NPC-002`) | Meja resepsionis | Menyerahkan kunci mobil hanya pada tahap yang benar |

Jumlah 25 NPC tidak berubah. Dua pekerja rooftop tidak diganti menjadi Raka dan Sinta.

## Keadaan proyek saat rancangan dibuat

Scene `Regional Archive Office` memiliki 25 `NavMeshAgent`, satu `NavMeshSurface` dengan data bake, dan empat `NavMeshLink`, termasuk hubungan pintu ruang eksekutif. Paket AI Navigation `2.0.11` sudah terpasang. `OfficeNpcAgent` menangani patroli, aktivitas, dan percakapan singkat; `NpcInteractable` saat ini membaca tiga baris dari `NpcProfile` dan berhenti setelah baris terakhir. Dialog tiga orang, pengarahan berjalan, main quest, inventaris, dan penyimpanan progres belum tersedia.

UI `Save Area` sudah memiliki prompt interaksi, panel dialog NPC biasa, serta object `Notificatuion` yang belum dipakai sebagai sistem cerita. Tidak ditemukan object sofa bernama tersendiri dalam YAML scene: furnitur ruang eksekutif menyatu dalam mesh kantor. Letak duduk yang tepat harus dipastikan secara visual di Unity sebelum anchor kursi dipasang.

## Pendekatan

Gunakan pengatur bab pembuka yang menerima event interaksi dan kedatangan NPC, sementara isi quest dan urutan baris dialog berada dalam ScriptableObject. `NpcProfile` menyimpan referensi quest/percakapan yang dapat ditawarkan NPC tersebut; pengatur bab menentukan kapan referensi itu boleh aktif. Quest pertama otomatis aktif pada permainan baru. Item kunci mobil hanya memenuhi objective yang sudah aktif, tidak memulai quest.

Pendekatan satu skrip yang menyimpan semua dialog dan kondisi ditolak karena dua kemungkinan urutan Raka/Sinta akan mudah saling bertabrakan. Sistem graf quest umum untuk seluruh game ditunda karena bab ini baru membutuhkan satu alur utama dengan dua cabang pendek. Data dan event dibuat cukup terbuka untuk menambah side quest dan optional quest nanti tanpa mencampurnya ke bab pembuka.

Komponen yang direncanakan:

- `QuestDefinition` dan `DialogueSequence` sebagai asset data, dengan ID stabil, judul, objective, baris dialog, pembicara, serta pemicu lanjutan.
- `ChapterOneDirector` sebagai pemilik status cerita dan satu-satunya tempat yang mengubah tahap main quest.
- `StoryDialogueController` untuk dialog berdiri, telepon, dan dialog berjalan; setiap baris menyebut ID pembicara.
- Pengendali story pada `OfficeNpcAgent` untuk menghentikan patroli sementara, menghadap pembicara, berjalan ke tujuan yang ditentukan cerita, dan memulihkan perilaku biasa setelah selesai.
- `BossOfficeGate` pada pintu ruang bos untuk aturan akses pemain, prompt dinamis, penutupan otomatis, dan izin khusus NPC yang dipanggil.
- `QuestTrackerView`, `StoryDialogueView`, dan notifikasi singkat pada Canvas uGUI yang sudah ada.
- Inventaris minimum dengan item `car-key` dan save JSON untuk progres bab, tanpa membuat sistem barang umum di luar kebutuhan ini.

## Urutan cerita dan quest

| Tahap | Main quest yang terlihat | Pemicu lanjut | Akibat |
| --- | --- | --- | --- |
| `MeetRooftopWorkers` | **Temui rekan kerja di rooftop** — Bicara dengan Bima dan Yuni | Pemain menekan E pada salah satu dari mereka; percakapan tiga orang selesai | Panggilan Arya masuk |
| `AnswerBossCall` | **Jawab telepon dari Bos Arya** | Pemain menjawab dan panggilan selesai | Quest berganti menjadi menemui bos |
| `MeetBoss` | **Temui Bos Arya di ruangannya** | Pemain masuk ke ruang bos dan menyelesaikan pengarahan pertama | Quest pencarian rekan dimulai |
| `FindColleagues` | **Cari rekan-rekan** dengan dua objective Raka dan Sinta | Pemain berbicara dengan Raka dan Sinta, urutannya bebas | Tiap objective dicoret segera setelah percakapannya selesai |
| `EscortLastColleague` | **Ikuti rekanmu ke ruang bos** | Rekan terakhir tiba di pintu dan dialog berjalan selesai; rekan pertama sudah tiba | Pintu dibuka untuk pertemuan bertiga dengan bos |
| `FinalBossBriefing` | **Dengarkan pengarahan Bos Arya** | Pengarahan akhir selesai | Quest berganti menjadi mengambil kunci mobil |
| `CollectCarKey` | **Ambil kunci mobil dari Nadia** | Pemain berinteraksi dengan Nadia dan item `car-key` tercatat | Quest berganti menjadi menuju mobil |
| `ReachVehicle` | **Pergi ke mobil kantor** | Pemain mendekati SUV pada radius interaksi; kunci sudah dimiliki | Bagian main quest ini selesai; pemain tetap di luar mobil |

Tracker pada tahap `FindColleagues` menampilkan:

```text
● Cari rekan-rekan
  - Cari Rekan A (Raka)
  - Cari Rekan B (Sinta)
```

Setelah salah satu ditemukan, barisnya tetap terlihat dengan coretan TMP dan warna lebih redup. Urutan tampilan tetap A lalu B, tidak bergantung pada urutan pemain menemukannya. Tidak ada objective yang tercoret sebelum dialog NPC tersebut selesai.

## Percakapan dan pengarahan

Semua percakapan memakai teks Bahasa Indonesia, nama pembicara yang jelas, dan tombol E untuk melanjutkan dialog berdiri. Target panjang adalah 16–20 baris untuk rooftop, 4–6 baris untuk telepon, 14–18 baris untuk pengarahan bos pertama, 6–8 baris untuk rekan pertama, 12–16 baris untuk rekan terakhir saat berjalan, 18–22 baris untuk pengarahan bos akhir, dan 5–7 baris untuk Nadia. Setiap baris dibuat pendek agar terbaca di HUD; panjang adegan datang dari giliran bicara dan jeda, bukan satu paragraf panjang.

Percakapan rooftop melibatkan pemain, Bima, dan Yuni. Bima mengusulkan perjalanan ke pantai akhir pekan, Yuni membicarakan siapa saja yang ikut dan persiapan kantor, pemain menanggapi, lalu tersirat bahwa Arya pun diundang. Bima dan Yuni berhenti bergerak selama dialog. NPC yang mendengarkan menghadap pembicara baris saat itu; pembicara menghadap lawan bicaranya. Gerak pemain terkunci selama dialog berdiri, sementara kamera tetap dapat melihat sekeliling. Setelah dialog dan telepon selesai, kedua pekerja kembali ke rute lama.

Saat panel dialog cerita aktif, tombol E melanjutkan satu baris saja dan tidak sekaligus memicu interaksi pintu/NPC biasa. Penahanan Bima, Yuni, Raka, dan Sinta dipasang sebelum patroli pertama mereka dimulai agar posisi pembuka tidak berubah selama satu frame pun.

Panggilan ditampilkan sebagai panel sederhana “Panggilan masuk: Arya”, dengan E untuk menjawab. Arya meminta pemain datang ke ruangannya tanpa menjelaskan seluruh persoalan melalui telepon.

Pengarahan pertama berlangsung saat Arya sudah berada di sofa dan ruangannya kosong dari NPC biasa. Ia menjelaskan ada satu permukiman dalam arsip lama yang belum dapat dikonfirmasi, meminta pemain memastikan tempat itu benar-benar ada, dan mengarahkan pemain mengajak Raka serta Sinta. Detail alamat disimpan untuk pengarahan akhir agar pencarian dua rekan tetap memiliki tujuan cerita.

Jika Raka ditemukan pertama, ia membahas panggilan Arya dan berangkat sendiri ke ruang bos. Jika Sinta ditemukan pertama, ia melakukan hal yang sama dengan kalimat yang berbeda. NPC pertama berjalan dengan kecepatan `4 m/s` dan menunggu di tempat duduk/berkumpul yang disiapkan di ruang bos. Kecepatan lama dipulihkan setelah ia tiba.

Rekan yang ditemukan terakhir memulai percakapan berbeda:

- Raka mengungkapkan ia berencana meninggalkan pekerjaan dan mendirikan usaha sendiri setelah tugas ini. Nada pembicaraannya tenang, dengan sedikit keraguan tentang waktunya.
- Sinta membicarakan liburan kantor ke pantai dan rencananya menyiapkan perjalanan setelah tugas ini selesai.

Rekan terakhir berjalan lebih dulu menuju ruang bos dengan kecepatan yang nyaman untuk langkah biasa pemain (`2.2 m/s`). Pemain mengikutinya dari jarak paling jauh `5 m`. Jika pemain tertinggal lebih jauh, NPC berhenti dan percakapan ikut jeda sampai pemain mendekat. Baris dialog berjalan muncul otomatis dalam jeda yang dapat dibaca, tanpa menuntut pemain menekan E sambil bergerak. Bila jalur selesai lebih dulu daripada percakapan, NPC berhenti di depan pintu dan menghadap pemain sampai baris terakhir berakhir. Setelah itu pintu terbuka, NPC masuk, lalu berada di tempat duduk bersama rekan pertama.

Pengarahan akhir hanya dimulai setelah kedua rekan berada di ruang bos. Arya menjelaskan bahwa desa tersebut tidak muncul dalam peta dan catatan wilayah modern, namun sebuah berkas tua mencatat petunjuk lokasi di **Provinsi Arunika, ujung selatan Kecamatan Tanjung Sagara**. Kedua nama ini fiktif sementara dan disimpan di data cerita agar mudah diganti. Ia membahas keraguan arsip, alasan tim perlu memeriksa langsung, dan meminta pemain mengambil kunci mobil dari Nadia. Nadia menyerahkan kunci satu kali, kemudian pemain berjalan ke SUV. Tidak ada animasi masuk kendaraan pada bagian ini.

## Ruang bos, pintu, dan NPC latar

Satu zona ruang bos membatasi NPC latar. Sebelum adegan bos dimulai, NPC latar yang berada di dalam diarahkan keluar melalui NavMesh ke titik aman di koridor lantai 4. Rute `executive_talk` untuk atasan dan penjaga arsip lantai 4 tidak boleh membawa mereka kembali selama bab ini. Arya tetap di titik duduk; Raka dan Sinta mendapat izin masuk setelah dipanggil. Tidak ada NPC yang dihapus atau dipindahkan secara teleport hanya untuk membersihkan ruangan.

Pintu ruang bos dapat dipakai untuk pertemuan pertama. Setelah Arya meminta dua rekan, pemain boleh keluar; pintu baru menutup dan terkunci setelah pemain berada di luar agar ia tidak terjebak. Pemain belum boleh membukanya lagi sampai keduanya ditemukan. Jika pemain kembali dengan hanya satu rekan, Arya menegur dari dekat pintu; pintu menutup otomatis dan prompt E berubah menjadi **Temukan Raka** atau **Temukan Sinta** sesuai objective yang belum selesai. Jika belum ada rekan ditemukan, prompt meminta pemain menemukan keduanya. NPC yang dipanggil boleh melintasi pintu melalui jalur cerita, tetapi akses pemain tetap mengikuti status quest. Saat dialog rekan terakhir selesai dan kedua rekan siap, pintu dibuka kembali untuk pemain dan pengarahan akhir.

Selama pintu terkunci oleh cerita, sistem interaksi tetap mendeteksinya agar prompt E dan teguran Arya dapat muncul. Menekan E tidak mengubah status quest atau membuka pintu sebelum syarat dipenuhi.

Titik duduk Arya dan kedua rekan dibuat sebagai anchor di ruang eksekutif. Karena mesh sofa belum bernama terpisah di scene, posisi anchor dan tampilan duduk kapsul harus dicek secara visual. Jika furnitur yang tampak bukan sofa, tambahkan sofa sederhana ke ruang itu sebelum mengklaim ketiga NPC sudah duduk sesuai cerita.

## Progres, save, dan tampilan

`NpcProfile` hanya memuat identitas dan referensi quest/percakapan yang bisa ditawarkan NPC. `QuestDefinition` menyimpan data tetap: ID, kategori `Main`, judul, objective, dan syarat. `ChapterOneDirector` menyimpan status berjalan: tahap, rekan yang sudah diajak bicara, urutan pertama/terakhir, status kedatangan mereka, dialog yang sudah selesai, status pintu, dan apakah kunci mobil telah diambil. Item tidak pernah memulai quest.

Status berjalan disimpan sebagai JSON berversi di `Application.persistentDataPath` setiap kali tahap berubah. Saat memuat save, posisi dan perintah sementara NPC, pintu, serta tracker dibangun ulang dari status itu; ScriptableObject tidak ditulis ulang. Panggilan bos, kunci mobil, dan dialog penting tidak terpicu dua kali setelah load.

Object `Notificatuion` yang sudah ada dipakai untuk pesan singkat pergantian quest dan panggilan masuk. Tracker main quest yang menetap dibuat terpisah agar notifikasi sementara tidak menimpa daftar objective. Dialog cerita mendapat panel TMP tersendiri sehingga dialog NPC biasa dan prompt interaksi E tidak saling mengendalikan panel yang sama. Teks UI mengikuti sistem lokalisasi proyek; naskah Indonesia menjadi isi utama bab ini.

## Kondisi gagal dan verifikasi

- Tujuan NavMesh dicek sebelum NPC bergerak. Jika jalur tidak lengkap, NPC menunggu dan mencoba lagi tanpa memajukan quest diam-diam; masalah jalur dicatat di Console.
- Pemain tidak dapat membuka pintu bos saat syarat rekan belum terpenuhi, termasuk ketika salah satu NPC masuk melalui pintu yang sama.
- Bila rekan pertama masih berjalan saat rekan terakhir mencapai pintu, rekan terakhir menunggu sampai keduanya siap; pengarahan akhir tidak terpicu lebih dini.
- Menemukan Raka lalu Sinta maupun Sinta lalu Raka harus menghasilkan dua coretan objective yang benar dan percakapan terakhir yang sesuai tokohnya.
- Percakapan berdiri menghentikan gerak NPC terkait, orientasi pembicara mengikuti baris, dan kendali pemain pulih setelah selesai.
- Dialog berjalan berhenti saat pemain berjarak lebih dari `5 m`, berlanjut saat dekat, dan tidak terpotong ketika NPC mencapai pintu lebih dahulu.
- Ruang bos hanya ditempati Arya dan rekan yang diundang pada adegan cerita; NPC latar tetap beraktivitas di luar zona.
- Setelah Nadia menyerahkan kunci, save/load tidak menggandakan item. Bagian ini selesai saat pemain mencapai SUV tanpa memicu naik kendaraan.

Verifikasi dilakukan melalui tes status quest/cabang dan save yang bermakna, lalu Play Mode di scene kantor untuk jalur rooftop–lantai 4, pintu bos, posisi duduk, dialog, tracker, resepsionis, dan SUV. Keberhasilan tes kode saja tidak menggantikan pemeriksaan visual posisi sofa serta rute NPC di Editor.
