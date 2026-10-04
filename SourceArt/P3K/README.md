# Kotak P3K — properti inventaris

Model mengikuti referensi kotak P3K vinil putih yang terbaring: tutup sedikit menggembung, jahitan/lis di keliling tepi, dua kancing logam kecil di sisi depan, dan cetakan palang merah serta `P.P.P.K.` yang menempel pada permukaan tutup. Isi tidak dimodelkan dan kotak masih dalam keadaan tertutup.

- `Kotak_P3K.blend`: sumber Blender dengan badan, tutup, lis, dan kancing terpisah.
- FBX meter untuk Unity: `Assets/_Project/Art/Models/Props/Inventory/Kotak_P3K.fbx`, dengan tekstur gambar disematkan.
- `Textures/`: albedo tutup dan vinil samping yang juga bisa dipakai untuk material Unity.
- Render tampak sudut dan atas disimpan sementara di `/tmp/saksi-terakhir-generated/previews/P3K/`.
- `build_p3k.py`, `make_p3k_textures.py`: generator yang dapat dijalankan ulang.

Ukuran sekitar 26 × 17 × 5,5 cm. Titik asal di tengah alas. Koleksi studio dalam `.blend` tidak diekspor. FBX diekspor langsung ke folder Inventory Unity. Model statis; mekanisme membuka kotak dan isi P3K belum dibuat.

Untuk membuat ulang dari akar repositori:

```bash
blender -b --python SourceArt/P3K/build_p3k.py
```
