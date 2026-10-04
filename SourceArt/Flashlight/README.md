# Senter

`Flashlight.blend` adalah salinan tanpa perubahan dari `/home/ikbal/Dokumen/Flashlight.blend` (SHA-256 `1711fa260bef08b56c1069b5c2772609367576afc0585a8fe24c540f459de2f4`). Berkas asli di Dokumen tidak diubah.

- FBX Unity berada di `Assets/_Project/Art/Models/Props/Inventory/Flashlight.fbx`, dengan 12 bagian mesh dan 5 material bernama seperti sumber. Arah atas disesuaikan menjadi sumbu Y.
- Render pratinjau disimpan sementara di `/tmp/saksi-terakhir-generated/previews/Flashlight/`.
- `export_flashlight.py`: membuat ulang FBX dan pratinjau tanpa menyimpan perubahan ke `.blend`.

Model sumber memakai satuan meter dan tegak pada sumbu Z Blender. Dimensi keseluruhan, termasuk tali gantung, sekitar **4,8 × 4,8 × 20,76 cm**. Badan senter saja dari tutup belakang hingga bezel sekitar 15,2 cm. Materialnya menggunakan warna/nilai bahan, tanpa berkas tekstur gambar eksternal.

FBX dibuka kembali di Blender 5.2.2: 12 mesh, 5 material, serta ukuran dan nama bagian tetap terbaca. Penampilan material di Unity dapat memerlukan penyesuaian shader setelah impor.
