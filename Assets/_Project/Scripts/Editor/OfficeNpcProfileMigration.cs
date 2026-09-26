using System;
using System.Collections.Generic;
using SaksiTerakhir.Npc;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace SaksiTerakhir.EditorTools
{
    /// <summary>Moves the existing office cast and its shared scene waypoints into individual NPC assets.</summary>
    public static class OfficeNpcProfileMigration
    {
        private const string ScenePath = "Assets/_Project/Scenes/Regional Archive Office.unity";
        private const string ProfileFolder = "Assets/_Project/Npc/Profiles";
        private const int ExpectedActors = 25;
        private const int InteractableLayer = 6;

        private sealed class CharacterSpec
        {
            public readonly string LegacyName;
            public readonly string DisplayName;
            public readonly NpcRole Role;
            public readonly int Age;
            public readonly float Speed;
            public readonly string[] Dialogue;
            public readonly string[] Ambient;

            public CharacterSpec(string legacyName, string displayName, NpcRole role, int age, float speed,
                string line1, string line2, string line3, string ambient1, string ambient2)
            {
                LegacyName = legacyName;
                DisplayName = displayName;
                Role = role;
                Age = age;
                Speed = speed;
                Dialogue = new[] { line1, line2, line3 };
                Ambient = new[] { ambient1, ambient2 };
            }
        }

        private sealed class ActorSnapshot
        {
            public OfficeNpcAgent Actor;
            public CharacterSpec Character;
            public string Id;
            public string AssetPath;
            public NpcWaypointDefinition Home;
            public NpcWaypointDefinition[] Route;
            public NpcProfile Profile;
        }

        // IDs follow this explicit cast order, independent of Unity hierarchy ordering.
        private static readonly CharacterSpec[] Cast =
        {
            new CharacterSpec("Satpam", "Damar", NpcRole.Security, 38, 2.8f,
                "Selamat datang di kantor arsip. Ada yang bisa saya bantu?", "Saya berpatroli dari halaman sampai pintu depan.", "Kalau mencari pegawai, tanyakan ke Nadia di resepsionis.",
                "Pintu masuk sudah saya periksa.", "Halaman depan masih aman."),
            new CharacterSpec("Resepsionis", "Nadia", NpcRole.Receptionist, 27, 2f,
                "Selamat datang. Siapa yang ingin Anda temui?", "Daftar kunjungan ada di meja depan.", "Ruang kerja dan arsip berada di lantai atas.",
                "Saya mencatat tamu yang masuk.", "Jadwal kunjungan hari ini cukup padat."),
            new CharacterSpec("Rekan A", "Raka", NpcRole.ColleagueA, 29, 2.6f,
                "Saya sedang mencari berkas yang belum dikembalikan.", "Saya biasa memeriksa arsip di beberapa lantai.", "Beri tahu saya kalau Anda menemukan map berlabel lama.",
                "Berkas tadi sepertinya ada di lantai tiga.", "Saya akan cek rak berikutnya."),
            new CharacterSpec("Rekan B", "Sinta", NpcRole.ColleagueB, 32, 2.7f,
                "Halo, saya sedang menyusun daftar arsip.", "Ada beberapa dokumen yang harus saya cocokkan.", "Kalau perlu bantuan, temui saya di ruang kerja lantai dua.",
                "Nomor berkas ini perlu dicocokkan.", "Saya akan turun mengambil dokumen."),
            new CharacterSpec("Atasan", "Ratih", NpcRole.Supervisor, 44, 2.3f,
                "Pastikan setiap arsip kembali ke tempatnya.", "Saya sedang meninjau laporan dari beberapa lantai.", "Bila ada berkas bermasalah, laporkan kepada saya.",
                "Laporan harian harus selesai tepat waktu.", "Saya akan periksa ruang arsip lagi."),
            new CharacterSpec("Bos", "Arya", NpcRole.Boss, 56, 2f,
                "Selamat datang di kantor kami.", "Saya menunggu ringkasan pekerjaan hari ini.", "Silakan sampaikan temuan penting kepada Ratih.",
                "Saya ingin melihat ringkasan terbaru.", "Pastikan semua bagian bekerja rapi."),

            new CharacterSpec("Penjaga Arsip L1-1", "Fajar", NpcRole.ArchiveGuard, 35, 2f,
                "Saya menjaga pintu arsip lantai satu.", "Buku peminjaman ada di dekat meja ini.", "Setiap berkas keluar harus dicatat dahulu.",
                "Pintu arsip harus tetap terpantau.", "Saya cek daftar peminjaman lagi."),
            new CharacterSpec("Penjaga Arsip L1-2", "Bayu", NpcRole.ArchiveGuard, 24, 2.4f,
                "Saya memeriksa susunan dokumen di lantai satu.", "Beberapa map baru datang pagi ini.", "Nomor rak bisa dilihat pada label di depan.",
                "Map ini sudah saya urutkan.", "Masih ada satu rak yang perlu dicek."),
            new CharacterSpec("Penjaga Arsip L1-3", "Hendra", NpcRole.ArchiveGuard, 31, 2.3f,
                "Arsip lama disimpan di sisi belakang.", "Saya sedang menghitung jumlah map di rak.", "Tolong kembalikan berkas ke urutan semula.",
                "Jumlah map di rak ini cocok.", "Saya lanjut ke sisi belakang."),
            new CharacterSpec("Penjaga Arsip L2-1", "Lilis", NpcRole.ArchiveGuard, 41, 2f,
                "Saya bertugas di pos arsip lantai dua.", "Daftar kunjungan ruang arsip ada pada saya.", "Jangan membawa dokumen tanpa izin petugas.",
                "Saya catat siapa yang masuk.", "Pos lantai dua tetap saya jaga."),
            new CharacterSpec("Penjaga Arsip L2-2", "Naufal", NpcRole.ArchiveGuard, 28, 2.5f,
                "Saya menata berkas sesuai tahun penerbitan.", "Rak sebelah sini sedang saya periksa.", "Beri tahu saya jika ada label yang tertukar.",
                "Urutan tahunnya sudah benar.", "Rak berikutnya perlu diperiksa."),
            new CharacterSpec("Penjaga Arsip L2-3", "Tio", NpcRole.ArchiveGuard, 33, 2.4f,
                "Ada beberapa dokumen yang baru dipindahkan.", "Saya berkeliling di ruang arsip lantai dua.", "Semua perpindahan berkas harus masuk catatan.",
                "Dokumen pindahan sudah saya cek.", "Saya kembali ke rak depan."),
            new CharacterSpec("Penjaga Arsip L3-1", "Intan", NpcRole.ArchiveGuard, 39, 2f,
                "Selamat datang di arsip lantai tiga.", "Saya menjaga daftar peminjaman lantai ini.", "Tanyakan lokasi map sebelum membuka rak.",
                "Daftar arsip lantai tiga sudah diperbarui.", "Saya tetap di pos depan."),
            new CharacterSpec("Penjaga Arsip L3-2", "Maya", NpcRole.ArchiveGuard, 26, 2.5f,
                "Saya memeriksa rak bagian tengah.", "Beberapa map perlu diberi label baru.", "Tolong jangan ubah susunan berkas saat saya mencatatnya.",
                "Label rak tengah mulai pudar.", "Saya akan ambil daftar inventaris."),
            new CharacterSpec("Penjaga Arsip L3-3", "Rizki", NpcRole.ArchiveGuard, 34, 2.4f,
                "Rak belakang memuat berkas yang jarang dipakai.", "Saya sedang mencocokkan nomor inventaris.", "Jika menemukan map tanpa label, serahkan kepada petugas.",
                "Nomor inventaris ini belum cocok.", "Saya periksa map berikutnya."),
            new CharacterSpec("Penjaga Arsip L4-1", "Surya", NpcRole.ArchiveGuard, 47, 2f,
                "Saya menjaga akses arsip lantai empat.", "Ruang pimpinan berada tidak jauh dari sini.", "Catat dahulu bila Anda perlu membawa berkas keluar.",
                "Akses lantai empat harus tercatat.", "Saya tetap berjaga di depan."),
            new CharacterSpec("Penjaga Arsip L4-2", "Dini", NpcRole.ArchiveGuard, 36, 2.4f,
                "Saya memeriksa rak dekat ruang pimpinan.", "Berkas penting perlu ditata lebih teliti.", "Saya akan mengembalikan map ini ke tempatnya.",
                "Rak dekat pimpinan sudah saya periksa.", "Map ini harus kembali ke urutan awal."),
            new CharacterSpec("Penjaga Arsip L4-3", "Bagas", NpcRole.ArchiveGuard, 42, 2.3f,
                "Saya berkeliling di arsip lantai empat.", "Ada beberapa dokumen yang perlu dicocokkan.", "Laporkan jika ada pintu atau rak yang terbuka.",
                "Saya akan cek koridor berikutnya.", "Dokumen lantai empat masih lengkap."),

            new CharacterSpec("Pekerja 01", "Wulan", NpcRole.Worker, 22, 2.7f,
                "Saya sedang mengantar dokumen dari halaman.", "Kadang saya membantu resepsionis di lobi.", "Beri saya waktu untuk menyelesaikan pemeriksaan luar.",
                "Dokumen dari luar sudah datang.", "Saya masuk sebentar ke lobi."),
            new CharacterSpec("Pekerja 02", "Putri", NpcRole.Worker, 23, 2.8f,
                "Saya bertugas memeriksa area halaman.", "Setelah ini saya akan ke pintu depan.", "Kalau ada paket, letakkan di meja penerimaan.",
                "Halaman timur sudah saya cek.", "Saya lanjut ke pos depan."),
            new CharacterSpec("Pekerja 03", "Bima", NpcRole.Worker, 30, 2.6f,
                "Saya sedang memeriksa bagian rooftop.", "Dari atas, halaman terlihat cukup jelas.", "Saya juga turun sesekali untuk mengambil laporan.",
                "Rooftop bagian barat aman.", "Saya akan lihat sisi lainnya."),
            new CharacterSpec("Pekerja 04", "Yuni", NpcRole.Worker, 25, 2.7f,
                "Saya berpatroli di rooftop hari ini.", "Saya memeriksa jalur dan sudut atap.", "Kalau ada yang perlu dilaporkan, saya akan turun.",
                "Sisi timur rooftop sudah bersih.", "Saya cek sudut berikutnya."),
            new CharacterSpec("Pekerja 05", "Arif", NpcRole.Worker, 37, 2.5f,
                "Saya mengurus dokumen di lantai satu.", "Beberapa berkas harus saya bawa ke lantai dua.", "Saya akan kembali setelah daftar ini selesai.",
                "Daftar lantai satu hampir selesai.", "Saya bawa berkas ini ke atas."),
            new CharacterSpec("Pekerja 06", "Dewi", NpcRole.Worker, 40, 2.5f,
                "Saya bekerja di lantai dua.", "Saya sedang mencocokkan dokumen dengan daftar arsip.", "Sesudah ini saya akan memeriksa lantai tiga.",
                "Dokumen lantai dua perlu disusun.", "Saya naik untuk memeriksa arsip."),
            new CharacterSpec("Pekerja 07", "Nanda", NpcRole.Worker, 45, 2.4f,
                "Saya menyelesaikan pekerjaan di lantai tiga.", "Saya sering turun untuk memeriksa berkas lain.", "Tolong beri tahu jika menemukan dokumen tanpa nomor.",
                "Pekerjaan lantai tiga masih berjalan.", "Saya turun mengambil daftar arsip."),
        };

        [MenuItem("Saksi Terakhir/NPC/Migrate 25 NPC Profiles")]
        public static void MigrateFromMenu() => Debug.Log(Migrate());

        public static string Migrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("NPC profile migration must run in Edit Mode.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject system = GameObject.Find("Office NPC System");
            GameObject building = GameObject.Find("Office_ArchiveHQ");
            if (system == null || building == null)
                throw new InvalidOperationException("Office NPC System or Office_ArchiveHQ is missing.");
            OfficeNpcDirector director = system.GetComponent<OfficeNpcDirector>();
            if (director == null) throw new InvalidOperationException("OfficeNpcDirector is missing.");
            OfficeNpcAgent[] actors = system.GetComponentsInChildren<OfficeNpcAgent>(true);
            if (Cast.Length != ExpectedActors || actors.Length != ExpectedActors || director.Actors.Length != ExpectedActors)
                throw new InvalidOperationException("Migration requires the original 25 NPCs and a 25-member director cast.");
            var sceneActors = new HashSet<OfficeNpcAgent>(actors);
            foreach (OfficeNpcAgent actor in director.Actors)
                if (actor == null || !sceneActors.Remove(actor))
                    throw new InvalidOperationException("Director cast does not match the 25 scene NPCs.");
            if (sceneActors.Count != 0) throw new InvalidOperationException("Some scene NPCs are absent from the director cast.");

            Transform oldPoints = system.transform.Find("NPC Activity Points");
            int assigned = 0;
            foreach (OfficeNpcAgent actor in actors)
                if (actor.Profile != null) assigned++;
            if (oldPoints == null)
            {
                if (assigned != ExpectedActors)
                    throw new InvalidOperationException("Legacy waypoints are absent, but some NPC profiles are unassigned.");
                ValidateCompletedMigration(actors, director, building.transform);
                return "The 25 NPC profiles are already assigned; no duplicate assets or NPCs were created.";
            }
            if (assigned != 0)
                throw new InvalidOperationException("Partial profile assignment detected; inspect the scene before migrating.");

            NavMeshSurface surface = system.GetComponentInChildren<NavMeshSurface>(true);
            if (surface == null || surface.navMeshData == null)
                throw new InvalidOperationException("Saved office navigation is required for waypoint validation.");
            var filter = new NavMeshQueryFilter { agentTypeID = surface.agentTypeID, areaMask = NavMesh.AllAreas };
            var byName = new Dictionary<string, OfficeNpcAgent>(StringComparer.Ordinal);
            foreach (OfficeNpcAgent actor in actors)
            {
                if (!byName.TryAdd(actor.gameObject.name, actor))
                    throw new InvalidOperationException($"Duplicate legacy NPC name: {actor.gameObject.name}.");
                if (actor.gameObject.layer != 0 || actor.GetComponent<CapsuleCollider>() == null
                    || actor.GetComponent<NavMeshAgent>() == null)
                    throw new InvalidOperationException($"Unexpected physical setup on {actor.gameObject.name}.");
                if (actor.GetComponent<NpcInteractable>() != null || actor.transform.Find("NPC Interaction Trigger") != null)
                    throw new InvalidOperationException($"Interaction setup already exists on {actor.gameObject.name}.");
            }

            var snapshots = new List<ActorSnapshot>(ExpectedActors);
            for (int i = 0; i < Cast.Length; i++)
            {
                CharacterSpec character = Cast[i];
                if (!byName.TryGetValue(character.LegacyName, out OfficeNpcAgent actor))
                    throw new InvalidOperationException($"Original NPC {character.LegacyName} is missing.");
                if (actor.Role != character.Role || actor.Home == null || !actor.Home.transform.IsChildOf(oldPoints))
                    throw new InvalidOperationException($"Legacy role or home is invalid for {character.LegacyName}.");
                var route = new NpcWaypointDefinition[actor.Route.Length];
                for (int j = 0; j < route.Length; j++)
                {
                    if (actor.Route[j] == null || !actor.Route[j].transform.IsChildOf(oldPoints))
                        throw new InvalidOperationException($"Legacy route point {j} is missing for {character.LegacyName}.");
                    route[j] = CopyPoint(actor.Route[j], building.transform);
                }
                string id = $"NPC-{i + 1:000}";
                snapshots.Add(new ActorSnapshot
                {
                    Actor = actor,
                    Character = character,
                    Id = id,
                    AssetPath = $"{ProfileFolder}/{id}-{character.LegacyName.Replace(' ', '-')}.asset",
                    Home = CopyPoint(actor.Home, building.transform),
                    Route = route
                });
            }

            EnsureProfileFolder();
            foreach (ActorSnapshot snapshot in snapshots)
            {
                UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(snapshot.AssetPath);
                if (existing != null && !(existing is NpcProfile))
                    throw new InvalidOperationException($"A non-profile asset already occupies {snapshot.AssetPath}.");
                snapshot.Profile = existing as NpcProfile;
                if (snapshot.Profile == null)
                {
                    snapshot.Profile = ScriptableObject.CreateInstance<NpcProfile>();
                    snapshot.Profile.Configure(snapshot.Id, snapshot.Character.DisplayName,
                        snapshot.Character.Age, snapshot.Character.Speed, snapshot.Character.Role,
                        snapshot.Actor.Stationary, snapshot.Home, snapshot.Route,
                        snapshot.Character.Dialogue, snapshot.Character.Ambient);
                    AssetDatabase.CreateAsset(snapshot.Profile, snapshot.AssetPath);
                }
            }
            AssetDatabase.SaveAssets();
            ValidateAllAssets(snapshots, building.transform, filter);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Migrate office NPC profiles");
            try
            {
                foreach (ActorSnapshot snapshot in snapshots)
                {
                    OfficeNpcAgent actor = snapshot.Actor;
                    Undo.RecordObject(actor, "Assign NPC profile");
                    actor.SetProfile(snapshot.Profile);
                    EditorUtility.SetDirty(actor);
                    NavMeshAgent navigation = actor.GetComponent<NavMeshAgent>();
                    Undo.RecordObject(navigation, "Set NPC movement speed");
                    navigation.speed = snapshot.Profile.MovementSpeed;
                    EditorUtility.SetDirty(navigation);

                    Undo.AddComponent<NpcInteractable>(actor.gameObject);
                    GameObject trigger = new GameObject("NPC Interaction Trigger");
                    Undo.RegisterCreatedObjectUndo(trigger, "Add NPC interaction trigger");
                    trigger.transform.SetParent(actor.transform, false);
                    trigger.layer = InteractableLayer;
                    CapsuleCollider collider = Undo.AddComponent<CapsuleCollider>(trigger);
                    collider.center = new Vector3(0f, 0.95f, 0f);
                    collider.height = 1.9f;
                    collider.radius = 0.35f;
                    collider.isTrigger = true;

                    NavMeshModifier modifier = actor.GetComponent<NavMeshModifier>();
                    if (modifier == null) modifier = Undo.AddComponent<NavMeshModifier>(actor.gameObject);
                    Undo.RecordObject(modifier, "Exclude moving NPC from navigation bake");
                    modifier.ignoreFromBuild = true;
                    EditorUtility.SetDirty(modifier);
                }
                Undo.RecordObject(director, "Bind NPC building root");
                director.SetBuildingRoot(building.transform);
                EditorUtility.SetDirty(director);
                Undo.DestroyObjectImmediate(oldPoints.gameObject);

                ValidateCompletedMigration(actors, director, building.transform);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Could not save the migrated office scene.");
                Undo.CollapseUndoOperations(undoGroup);
                return $"Migrated {ExpectedActors} NPCs to individual profiles in {ProfileFolder}; removed the 37 legacy scene waypoints.";
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
        }

        private static NpcWaypointDefinition CopyPoint(NpcActivityPoint source, Transform building)
        {
            var definition = new NpcWaypointDefinition();
            definition.Configure(source.gameObject.name, source.Label,
                building.InverseTransformPoint(source.transform.position), source.Activity,
                source.Zone, source.Floor, source.DwellSeconds);
            return definition;
        }

        private static void EnsureProfileFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Npc"))
                AssetDatabase.CreateFolder("Assets/_Project", "Npc");
            if (!AssetDatabase.IsValidFolder(ProfileFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Npc", "Profiles");
        }

        private static void ValidateAllAssets(List<ActorSnapshot> snapshots, Transform building,
            NavMeshQueryFilter filter)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var definitions = new Dictionary<string, NpcWaypointDefinition>(StringComparer.Ordinal);
            foreach (ActorSnapshot snapshot in snapshots)
            {
                NpcProfile profile = AssetDatabase.LoadAssetAtPath<NpcProfile>(snapshot.AssetPath);
                if (profile == null || profile.Id != snapshot.Id || !ids.Add(profile.Id))
                    throw new InvalidOperationException($"Missing, mismatched or duplicate NPC ID at {snapshot.AssetPath}.");
                if (string.IsNullOrWhiteSpace(profile.DisplayName) || profile.Age < 1
                    || profile.MovementSpeed < 2f || profile.MovementSpeed > 5f
                    || profile.Role != snapshot.Character.Role || profile.DialogueLines == null
                    || profile.DialogueLines.Length < 3 || profile.AmbientLines == null
                    || profile.AmbientLines.Length < 2 || profile.Route == null)
                    throw new InvalidOperationException($"Incomplete NPC profile {snapshot.AssetPath}.");
                foreach (string line in profile.DialogueLines)
                    if (string.IsNullOrWhiteSpace(line)) throw new InvalidOperationException($"Empty dialogue in {snapshot.AssetPath}.");
                foreach (string line in profile.AmbientLines)
                    if (string.IsNullOrWhiteSpace(line)) throw new InvalidOperationException($"Empty ambient line in {snapshot.AssetPath}.");
                Vector3 home = ValidateWaypoint(profile.Home, building, filter, definitions);
                foreach (NpcWaypointDefinition waypoint in profile.Route)
                {
                    Vector3 destination = ValidateWaypoint(waypoint, building, filter, definitions);
                    var path = new NavMeshPath();
                    if (!NavMesh.CalculatePath(home, destination, filter, path)
                        || path.status != NavMeshPathStatus.PathComplete)
                        throw new InvalidOperationException($"Incomplete route for {profile.Id}/{waypoint.Key}.");
                }
                snapshot.Profile = profile;
            }
            string[] assetGuids = AssetDatabase.FindAssets("t:NpcProfile", new[] { ProfileFolder });
            if (assetGuids.Length != ExpectedActors)
                throw new InvalidOperationException($"Expected exactly {ExpectedActors} profiles in {ProfileFolder}, found {assetGuids.Length}.");
        }

        private static Vector3 ValidateWaypoint(NpcWaypointDefinition point, Transform building,
            NavMeshQueryFilter filter, Dictionary<string, NpcWaypointDefinition> definitions)
        {
            if (point == null || string.IsNullOrWhiteSpace(point.Key) || string.IsNullOrWhiteSpace(point.Label)
                || point.Floor < 0 || point.Floor > 5 || point.DwellSeconds.x < 1f
                || point.DwellSeconds.y < point.DwellSeconds.x)
                throw new InvalidOperationException("An NPC profile contains an invalid waypoint.");
            if (definitions.TryGetValue(point.Key, out NpcWaypointDefinition earlier))
            {
                if ((earlier.LocalPosition - point.LocalPosition).sqrMagnitude > 0.0001f
                    || earlier.Label != point.Label || earlier.Activity != point.Activity
                    || earlier.Zone != point.Zone || earlier.Floor != point.Floor
                    || (earlier.DwellSeconds - point.DwellSeconds).sqrMagnitude > 0.0001f)
                    throw new InvalidOperationException($"Conflicting shared waypoint key {point.Key}.");
            }
            else definitions.Add(point.Key, point);
            Vector3 requested = building.TransformPoint(point.LocalPosition);
            if (!NavMesh.SamplePosition(requested, out NavMeshHit hit, 0.6f, filter)
                || Mathf.Abs(hit.position.y - requested.y) > 0.4f)
                throw new InvalidOperationException($"Waypoint {point.Key} is not on the saved navigation mesh.");
            return hit.position;
        }

        private static void ValidateCompletedMigration(OfficeNpcAgent[] actors,
            OfficeNpcDirector director, Transform building)
        {
            if (director.Actors.Length != ExpectedActors || systemHasLegacyPoints(director.transform))
                throw new InvalidOperationException("NPC cast or legacy waypoint cleanup is incomplete.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfficeNpcAgent actor in actors)
            {
                if (actor == null || actor.Profile == null || !ids.Add(actor.Profile.Id)
                    || actor.Home != null || actor.Route.Length != 0
                    || actor.GetComponent<NpcInteractable>() == null
                    || actor.GetComponent<NavMeshAgent>() == null
                    || Mathf.Abs(actor.GetComponent<NavMeshAgent>().speed - actor.Profile.MovementSpeed) > 0.001f
                    || actor.GetComponent<NavMeshModifier>() == null
                    || !actor.GetComponent<NavMeshModifier>().ignoreFromBuild
                    || actor.gameObject.layer != 0)
                    throw new InvalidOperationException($"NPC migration is incomplete on {actor?.gameObject.name}.");
                Transform trigger = actor.transform.Find("NPC Interaction Trigger");
                if (trigger == null || trigger.gameObject.layer != InteractableLayer
                    || !trigger.TryGetComponent(out CapsuleCollider collider) || !collider.isTrigger)
                    throw new InvalidOperationException($"NPC interaction trigger is missing on {actor.gameObject.name}.");
            }
            if (ids.Count != ExpectedActors || building == null)
                throw new InvalidOperationException("NPC identity or building reference validation failed.");
        }

        private static bool systemHasLegacyPoints(Transform system) => system.Find("NPC Activity Points") != null;
    }
}
