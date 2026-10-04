# Tipografi Saksi Terakhir

Paket font untuk arah menu horor psikologis: judul serif tinggi dan ramping,
menu serif yang tenang, serta varian miring untuk teks pendek.

## Font Asset yang dipilih di TextMeshPro

| Pemakaian | Font Asset |
| --- | --- |
| Judul besar | `TMP/Saksi Terakhir Display SDF.asset` |
| Tombol, kategori, nilai pengaturan | `TMP/Cormorant Garamond Medium SDF.asset` |
| Teks pendek miring | `TMP/Cormorant Garamond Medium Italic SDF.asset` |

Pilih objek teks di Unity, lalu seret Font Asset di atas ke kolom **Font Asset**
pada komponen **TextMeshPro - Text (UI)**. Font `.ttf` di folder `Source` adalah
berkas sumber; gunakan aset `.asset` dari folder `TMP` untuk teks TextMeshPro.

`Typography Preview.prefab` adalah contoh pemakaian font pada judul, daftar menu,
nilai pengaturan, dan navigasi. Buka prefab untuk melihat contoh; prefab ini
tidak mengubah scene game.

## Ukuran awal untuk kanvas 1920 × 1080

- Judul: 120–160, jarak karakter sekitar 4–8.
- Menu: 38–46, warna gading lembut `#E0DACC`.
- Nilai pengaturan: 36–42.
- Petunjuk kecil: 28–32.

Nilai ini titik awal desain dan perlu disesuaikan dengan posisi kamera, kontras
latar, serta ukuran teks yang kamu inginkan. Gunakan Display hanya untuk judul
besar; bentuknya terlalu ramping untuk label kecil.

## Pembuatan dan cakupan

Pemasang Editor membuat aset yang belum ada ketika Unity selesai mengimpor.
Jika perlu dijalankan kembali: **Saksi Terakhir → UI → Install Typography Fonts**.
Pemasang mempertahankan aset yang sudah ada. Untuk membangun ulang satu aset,
hapus aset itu melalui jendela Project lalu jalankan menu pemasang.

Font utama memiliki atlas statis untuk huruf Latin, angka, tanda baca, dan simbol
navigasi yang tersedia pada font sumber. Font menu mempunyai fallback dinamis
berukuran 1024 dengan **Clear Dynamic Data On Build** aktif. Tidak semua aksara
atau simbol ada di paket ini. Gunakan teks `Enter` untuk tombol konfirmasi;
simbol return `↵` tidak ada pada Cormorant Garamond.

Font ini mendekati arah tipografi mockup. Gambar generatif sebelumnya tidak
memiliki berkas font asli, sehingga hasil ini bukan salinan persis huruf gambar.

## Sumber dan lisensi

- **Saksi Terakhir Display Regular**: turunan Bodoni Moda, bobot 500, optical
  size 72, lebar huruf 58%, dengan nama keluarga baru. Judul khusus ini dibuat
  untuk proyek.
- **Cormorant Garamond Medium / Medium Italic**: bentuk statis dari bobot 500
  font Cormorant Garamond.

Keduanya memakai **SIL Open Font License 1.1**, dapat disertakan dalam game
komersial dengan tetap menyertakan lisensi dan atribusi. Salinan lisensi ada
di `Licenses`. Metadata sumber dan SHA-256 ada di `font-provenance.json`.

Sumber resmi:

- [Bodoni Moda](https://github.com/indestructible-type/Bodoni)
- [Cormorant](https://github.com/CatharsisFonts/Cormorant)

Font sumber, lisensi asli, dan skrip pembuatan berada di
`SourceArt/Typography` pada akar proyek. Skrip menggunakan fontTools 4.63.0;
pengguna Unity tidak perlu memasang fontTools untuk menggunakan font ini.
