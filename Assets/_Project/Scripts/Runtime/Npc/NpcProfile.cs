using System;
using SaksiTerakhir.Story;
using UnityEngine;

namespace SaksiTerakhir.Npc
{
    [Serializable]
    public sealed class NpcWaypointDefinition
    {
        [SerializeField] private string key = string.Empty;
        [SerializeField] private string label = string.Empty;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private NpcActivityKind activity = NpcActivityKind.Work;
        [SerializeField] private NpcZone zone = NpcZone.Interior;
        [SerializeField] private int floor;
        [SerializeField] private Vector2 dwellSeconds = new Vector2(5f, 12f);

        public string Key => key;
        public string Label => label;
        public Vector3 LocalPosition => localPosition;
        public NpcActivityKind Activity => activity;
        public NpcZone Zone => zone;
        public int Floor => floor;
        public Vector2 DwellSeconds => dwellSeconds;

#if UNITY_EDITOR
        public void Configure(string waypointKey, string waypointLabel, Vector3 buildingLocalPosition,
            NpcActivityKind activityKind, NpcZone waypointZone, int waypointFloor, Vector2 dwell)
        {
            key = waypointKey;
            label = waypointLabel;
            localPosition = buildingLocalPosition;
            activity = activityKind;
            zone = waypointZone;
            floor = waypointFloor;
            float minimum = Mathf.Max(1f, dwell.x);
            dwellSeconds = new Vector2(minimum, Mathf.Max(minimum, dwell.y));
        }
#endif
    }

    [CreateAssetMenu(fileName = "NpcProfile", menuName = "Saksi Terakhir/NPC Profile")]
    public sealed class NpcProfile : ScriptableObject
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayName = "NPC";
        [SerializeField, Min(1)] private int age = 25;
        [SerializeField, Range(2f, 5f)] private float movementSpeed = 2f;
        [SerializeField] private NpcRole role = NpcRole.Worker;
        [SerializeField] private bool stationary;
        [SerializeField] private NpcWaypointDefinition home;
        [SerializeField] private NpcWaypointDefinition[] route = Array.Empty<NpcWaypointDefinition>();
        [SerializeField] private string[] dialogueLines = Array.Empty<string>();
        [SerializeField] private string[] ambientLines = Array.Empty<string>();
        [SerializeField] private QuestDefinition[] storyQuests = Array.Empty<QuestDefinition>();
        [SerializeField] private DialogueSequence[] storyDialogues = Array.Empty<DialogueSequence>();

        public string Id => id;
        public string DisplayName => displayName;
        public int Age => age;
        public float MovementSpeed => Mathf.Clamp(movementSpeed, 2f, 5f);
        public NpcRole Role => role;
        public bool Stationary => stationary;
        public NpcWaypointDefinition Home => home;
        public NpcWaypointDefinition[] Route => route;
        public string[] DialogueLines => dialogueLines;
        public string[] AmbientLines => ambientLines;
        public QuestDefinition[] StoryQuests => storyQuests;
        public DialogueSequence[] StoryDialogues => storyDialogues;

        public void SetStoryOffers(QuestDefinition[] quests, DialogueSequence[] dialogues)
        {
            storyQuests = quests ?? Array.Empty<QuestDefinition>();
            storyDialogues = dialogues ?? Array.Empty<DialogueSequence>();
        }

#if UNITY_EDITOR
        public void Configure(string id, string displayName, int age, float movementSpeed, NpcRole role,
            bool stationary, NpcWaypointDefinition home, NpcWaypointDefinition[] route,
            string[] dialogueLines, string[] ambientLines)
        {
            this.id = id;
            this.displayName = displayName;
            this.age = Mathf.Max(1, age);
            this.movementSpeed = Mathf.Clamp(movementSpeed, 2f, 5f);
            this.role = role;
            this.stationary = stationary;
            this.home = home;
            this.route = route ?? Array.Empty<NpcWaypointDefinition>();
            this.dialogueLines = dialogueLines ?? Array.Empty<string>();
            this.ambientLines = ambientLines ?? Array.Empty<string>();
        }

        private void OnValidate()
        {
            age = Mathf.Max(1, age);
            movementSpeed = Mathf.Clamp(movementSpeed, 2f, 5f);
        }
#endif
    }
}
