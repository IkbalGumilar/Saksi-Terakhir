# Buku agenda A5 — sampul kulit biru, 80 lembar

Model mengikuti referensi buku biru dengan sudut membulat, pengikat kulit dari sisi kanan, ornamen kuningan bermotif geometris, lingkar tempat pena, dan pita pembatas cokelat. Halaman berwarna krem, bergaris, dengan kolom memo/tanggal. Belum ada tulisan cerita pada kertas.

## Isi berkas

- `Buku_Agenda_A5_80Lembar.blend`: pose tertutup, 80 objek kertas bernama `Lembar_001` sampai `Lembar_080`.
- `Buku_Agenda_A5_Terbuka.blend`: pose terbuka di tengah buku, 40 lembar di kiri dan 40 di kanan.
- FBX Unity berada di `Assets/_Project/Art/Models/Props/Inventory/`. Semua 80 lembar menjadi objek terpisah (`Lembar_001`–`Lembar_080`) dengan origin di tepi jilid, sehingga bisa digerakkan prosedural per lembar. Sampul, penjepit, pengikat, dan aksesori juga menjadi objek terpisah.
- Render pratinjau disimpan sementara di `/tmp/saksi-terakhir-generated/previews/AgendaBook/`.
- `Textures/`: warna/butiran/roughness kulit biru, kertas krem bergaris, dan kertas krem polos.

## Ukuran dan penyuntingan

- Kertas A5: 148 × 210 mm.
- Jumlah: **80 lembar, 160 sisi halaman**.
- Tebal geometri tiap lembar: 0,075 mm, dengan jarak antarlapis 0,020 mm agar tepi lembar terbaca.
- Blok kertas: 7,58 mm. Badan buku bersampul sekitar 12,4 mm; pengikat dan ornamen menambah sedikit ketebalan di satu area.
- Skala Blender meter (1 unit = 1 meter); asal model berada di tengah alas.
- Origin setiap lembar berada di tepi jilid. Sampul depan dan belakang, punggung, pengikat, serta ornamen terpisah dalam sumber Blender.
- Urutan tertutup dimulai dari lembar 1 di atas; pose terbuka menampilkan sisi halaman 80 dan 81. Cetakan sisi balik menggunakan UV tersendiri agar tidak terbalik saat kertas dibalik.

Lima tekstur tersedia di `Assets/_Project/Art/Models/Props/Inventory/Textures/` dan juga tersimpan packed di file Blender. Jumlah lembar, ketebalan, orientasi UV, dan pembacaan ulang FBX sudah diperiksa di Blender. Tampilan material di Unity belum diperiksa.

Dua pose adalah pilihan model, bukan animasi membuka buku. Gerak prosedural belum dibuat. Koleksi studio tidak termasuk ekspor FBX.

Untuk membuat ulang dari akar repositori:

```bash
blender -b --python SourceArt/AgendaBook/build_agenda_book.py
```

Tambahkan `--skip-render` setelah nama skrip untuk memperbarui file Blender dan FBX tanpa merender ulang gambar pratinjau.
