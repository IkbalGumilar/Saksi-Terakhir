# Nasi uduk bungkus — properti Bab 1

Satu bungkus nasi uduk tertutup mengikuti siluet referensi: kertas kraft agak menggembung, lipatan muka seperti amplop, pita daun hijau berujung V, dan stiker generik. Tidak ada label harga, logo penjual, atau nasi yang terlihat. Item ini dipakai sebagai bekal yang diberikan A kepada MC pada cabang cerita Bab 1.

## Berkas

- `Nasi_Uduk_Bungkus.blend` — sumber Blender yang bisa diedit. Badan, lipatan, pita, dan stiker diberi nama dan dibuat terpisah.
- FBX Unity: `Assets/_Project/Art/Models/Props/Inventory/Nasi_Uduk_Bungkus.fbx`. Tekstur disematkan pada FBX; salinan sumbernya ada di `Textures/`.
- Render pratinjau disimpan sementara di `/tmp/saksi-terakhir-generated/previews/NasiBungkus/`.
- `build_nasi_bungkus.py` dan `make_textures.py` — pembuat model dan tekstur yang dapat dijalankan ulang.

Satuan model adalah meter, dengan lebar sekitar 23 cm dan tinggi 18 cm. Titik asal ada di tengah alas. Koleksi `STUDIO - pratinjau saja` di Blender berisi kamera, lampu, dan lantai; koleksi itu tidak diekspor ke FBX. FBX diekspor langsung ke folder Inventory Unity. Model statis; lipatan dan pita dapat dimodifikasi, tetapi tidak memiliki simulasi kertas atau isi yang bisa dibuka.

Untuk membuat ulang dari akar repositori:

```bash
blender -b --python SourceArt/NasiBungkus/build_nasi_bungkus.py
```
