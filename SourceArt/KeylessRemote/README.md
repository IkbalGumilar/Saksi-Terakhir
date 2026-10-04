# Remote keyless, kunci darurat, dan dompet kulit

Model Blender untuk meninjau barang inventory. Referensi bentuk berasal dari tiga foto yang diberikan pengguna. Bagian belakang remote dibiarkan polos tanpa logo atau merek.

- `Remote_Keyless_Tanpa_Logo.blend`: sumber Blender yang dapat diedit; keadaan awal sudah terpasang.
- FBX Unity berada di `Assets/_Project/Art/Models/Props/Inventory/Remote_Keyless_Separated_Parts.fbx`; objek remote, kunci darurat, dan dompet tetap terpisah dalam hierarki.
- `build_keyless.py`: skrip untuk membuat ulang model dengan Blender 5.2.
- Render pratinjau disimpan sementara di `/tmp/saksi-terakhir-generated/previews/KeylessRemote/`.

## Memisahkan bagian di Blender

1. Di Outliner, pilih `KUNCI_DARURAT_ROOT__GESER_Y_NEGATIF_UNTUK_MELEPAS`.
2. Geser pada sumbu Y negatif sekitar 0,10 meter. Bilah akan keluar melalui rongga di ujung remote; gagang logam memang terlihat di luar saat keadaan awal.
3. Dompet mengikuti kunci karena `DOMPET_KULIT_ROOT__BISA_DILEPAS` adalah anak dari objek kunci. Untuk menjadikannya benar-benar bebas, pilih root dompet lalu gunakan **Alt+P → Clear Parent (Keep Transform)**.

Sakelar pelepas dan LED dibuat sebagai detail visual. Belum ada animasi atau logika input yang menggerakkan sakelar maupun kunci secara otomatis.
