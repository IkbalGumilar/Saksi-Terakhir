# Dua tali 10 meter untuk inventaris Bab 1

Model dibuat mengikuti dua foto referensi pengguna. Masing-masing **tali utama** mempunyai panjang jalur pusat 10 meter yang dihitung dari mesh, walaupun ditampilkan dalam keadaan digulung untuk inventaris. Ikatan tengah tambang dan pita kertas nilon adalah bagian kemasan terpisah; panjangnya tidak dihitung sebagai tali utama.

| Item | Ukuran tali | Bentuk saat disimpan | Sumber Blender | FBX Unity |
|---|---:|---|---|---|
| Tali tambang | 12 mm × 10 m | Hank krem dari serat alami, beberapa loop U, diikat dengan lilitan tengah | `Tali_Tambang_10m.blend` | `Assets/_Project/Art/Models/Props/Inventory/Tali_Tambang_10m.fbx` |
| Tali nilon | 8 mm × 10 m | Hank hijau gelap berbintik merah, lebih longgar, pita kertas “8 mm / 10 m” | `Tali_Nilon_10m.blend` | `Assets/_Project/Art/Models/Props/Inventory/Tali_Nilon_10m.fbx` |

Render pratinjau sementara disimpan di `/tmp/saksi-terakhir-generated/previews/Ropes/`. Tekstur pilinan dan label kertas berada di `Textures/` serta dikemas dalam file `.blend`; FBX juga diekspor dengan tekstur tertanam. Setiap sumber Blender menyimpan mesh `TALI_UTAMA_10M`, penanda `UJUNG_A` dan `UJUNG_B`, serta kurva panduan tersembunyi `PANDUAN_JALUR_10M__SEMBUNYI` untuk memeriksa bentuknya. Properti pada root menyimpan panjang 10 m dan diameter.

Jalurnya dibentuk melengkung dan ujungnya sedikit terurai sehingga gulungan tidak tampak kaku. Ini adalah **bentuk aset saat disimpan**. Jika nanti pemain harus menarik, mengikat, atau mengulur tali secara dinamis di Unity, model perlu dipasangkan dengan sistem deformasi/fisika gameplay tersendiri.

Untuk membuat ulang aset, jalankan `blender -b --python SourceArt/Ropes/build_rope_models.py`. Skrip membuat tekstur dengan `make_rope_textures.py` memakai Pillow pada Python sistem dan mengekspor FBX langsung ke folder Inventory Unity.
