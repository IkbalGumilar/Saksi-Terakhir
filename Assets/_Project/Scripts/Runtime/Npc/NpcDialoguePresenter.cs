using SaksiTerakhir.Interaction;
using SaksiTerakhir.Localization;
using SaksiTerakhir.Player;
using SaksiTerakhir.Story;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SaksiTerakhir.Npc
{
    /// <summary>Automatic ordinary dialogue. One-way greetings use an NPC caption without taking player control.</summary>
    [DisallowMultipleComponent]
    public sealed class NpcDialoguePresenter : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float charactersPerSecond = 34f;
        [SerializeField, Min(0.1f)] private float afterLineSeconds = 0.65f;
        private PlayerController playerMovement;
        private PlayerCameraController playerCamera;
        private GameObject panelRoot;
        private TMP_Text speakerLabel;
        private TMP_Text lineLabel;
        private InteractionPromptView prompt;
        private StoryDialogueController[] storyControllers = System.Array.Empty<StoryDialogueController>();
        private NpcInteractable speaker;
        private OfficeNpcAgent agent;
        private NpcLabel caption;
        private DialogueLine[] lines;
        private int index;
        private float lineEndsAt;
        private float revealedCharacters;
        private bool direct;
        private bool previousMovementLock;
        private bool previousLookLock;
        private Transform previousFocus;
        private string greetingId;
        private float greetingEndsAt;
        private bool playbackActive;

        public static NpcDialoguePresenter Instance { get; private set; }
        public bool IsPlaying => playbackActive;
        public bool IsDirectConversation => IsPlaying && direct;
        public NpcInteractable ActiveSpeaker => speaker;
        public string CurrentLine => lines != null && index >= 0 && index < lines.Length ? lines[index].Text : string.Empty;
        public bool IsSpeakingWith(NpcInteractable npc) => npc != null && speaker == npc;

        public static NpcDialoguePresenter Ensure(Transform player)
        {
            if (player == null) return null;
            if (Instance != null && Instance.gameObject.scene == player.gameObject.scene) return Instance;
            NpcDialoguePresenter presenter = player.GetComponent<NpcDialoguePresenter>();
            if (presenter == null) presenter = player.gameObject.AddComponent<NpcDialoguePresenter>();
            presenter.BindPlayer(player);
            return presenter;
        }

        private void Awake()
        {
            Instance = this;
            BindPlayer(transform);
        }

        private void BindPlayer(Transform player)
        {
            playerMovement = player.GetComponent<PlayerController>();
            playerCamera = player.GetComponent<PlayerCameraController>();
            StoryDialogueController[] found = FindObjectsByType<StoryDialogueController>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var sceneControllers = new System.Collections.Generic.List<StoryDialogueController>();
            foreach (StoryDialogueController controller in found)
                if (controller.gameObject.scene == gameObject.scene) sceneControllers.Add(controller);
            storyControllers = sceneControllers.ToArray();
            if (panelRoot == null) BindAuthoredPresentation();
        }

        public bool TryPlayConversation(NpcInteractable npc, Transform player)
        {
            if (npc == null || player == null || playerMovement == null || npc.Profile == null
                || !npc.Profile.Interactable || IsForeignDialoguePlaying() || playerMovement.UiMovementLocked
                || playerMovement.StoryMovementLocked || Time.timeScale <= 0f) return false;
            if (IsPlaying)
            {
                if (direct) return false;
                Stop();
            }
            if (panelRoot == null || lineLabel == null) BindAuthoredPresentation();
            if (panelRoot == null || lineLabel == null) return false;
            DialogueLine[] content = NpcContextDialogueLibrary.BuildPlayerConversation(npc.Profile);
            if (content.Length == 0) return false;
            speaker = npc;
            playbackActive = true;
            agent = npc.GetComponent<OfficeNpcAgent>();
            lines = content;
            index = 0;
            direct = true;
            previousMovementLock = playerMovement.StoryDialogueMovementLocked;
            previousLookLock = playerCamera != null && playerCamera.StoryDialogueLookLocked;
            previousFocus = playerCamera != null ? playerCamera.StoryFocusTarget : null;
            agent?.BeginPlayerInteraction(player);
            playerMovement.SetStoryMovementLocked(true);
            if (playerCamera != null)
            {
                playerCamera.SetStoryLookLocked(true);
                playerCamera.SetStoryFocus(npc.transform);
            }
            if (prompt != null) prompt.SetStoryDialogueActive(true);
            panelRoot.SetActive(true);
            RenderLine();
            return true;
        }

        public bool TryPlayGreeting(NpcInteractable npc, Transform player, string stableGreetingId)
        {
            if (npc == null || player == null || npc.Profile == null || string.IsNullOrWhiteSpace(stableGreetingId)
                || NpcGreetingMemory.Contains(stableGreetingId) || IsPlaying || IsForeignDialoguePlaying()
                || playerMovement == null || playerMovement.UiMovementLocked || playerMovement.StoryMovementLocked
                || Time.timeScale <= 0f) return false;
            OfficeNpcAgent npcAgent = npc.GetComponent<OfficeNpcAgent>();
            if (npcAgent == null || npcAgent.StoryMoveRequested || npcAgent.StorySeated
                || npcAgent.CurrentState == NpcState.StoryHeld || npcAgent.CurrentState == NpcState.StoryMoving
                || npcAgent.CurrentState == NpcState.PlayerConversation
                || npcAgent.CurrentState == NpcState.Conversation) return false;
            NpcLabel npcCaption = npc.GetComponentInChildren<NpcLabel>(true);
            if (npcCaption == null) return false;
            string text = NpcContextDialogueLibrary.BuildGreeting(npc.Profile);
            if (string.IsNullOrWhiteSpace(text)) return false;
            speaker = npc;
            playbackActive = true;
            agent = npcAgent;
            caption = npcCaption;
            direct = false;
            greetingId = stableGreetingId;
            float seconds = Mathf.Max(3f, text.Length / Mathf.Max(1f, charactersPerSecond) + afterLineSeconds);
            greetingEndsAt = Time.time + seconds;
            agent.BeginPlayerInteraction(player);
            caption.Say(text, seconds);
            return true;
        }

        public void StopFor(NpcInteractable npc)
        {
            if (IsSpeakingWith(npc)) Stop();
        }

        public void Stop()
        {
            bool wasDirect = direct;
            bool storyOwnsControl = IsForeignDialoguePlaying();
            if (agent != null) agent.EndPlayerInteraction();
            if (caption != null) caption.ClearSpeech();
            speaker = null;
            agent = null;
            caption = null;
            lines = null;
            greetingId = null;
            index = -1;
            direct = false;
            playbackActive = false;
            if (!wasDirect) return;
            if (!storyOwnsControl && playerMovement != null) playerMovement.SetStoryMovementLocked(previousMovementLock);
            if (!storyOwnsControl && playerCamera != null)
            {
                playerCamera.SetStoryLookLocked(previousLookLock);
                if (previousFocus != null) playerCamera.SetStoryFocus(previousFocus);
                else playerCamera.ClearStoryFocus();
            }
            // A story sequence can take this same authored panel over during an interruption.
            if (storyOwnsControl) return;
            if (panelRoot != null) panelRoot.SetActive(false);
            if (prompt != null) prompt.SetStoryDialogueActive(false);
        }

        private void Update()
        {
            if (!IsPlaying) return;
            if (speaker == null || !speaker.isActiveAndEnabled || agent == null || !agent.isActiveAndEnabled)
            { Stop(); return; }
            if (IsForeignDialoguePlaying() || playerMovement != null && playerMovement.UiMovementLocked)
            { Stop(); return; }
            if (!direct)
            {
                if (playerMovement != null && playerMovement.StoryMovementLocked) { Stop(); return; }
                if (Time.time >= greetingEndsAt)
                {
                    NpcGreetingMemory.Mark(greetingId);
                    Stop();
                }
                return;
            }
            if (lineLabel != null)
            {
                revealedCharacters += Time.deltaTime * Mathf.Max(1f, charactersPerSecond);
                lineLabel.maxVisibleCharacters = Mathf.FloorToInt(revealedCharacters);
            }
            if (Time.time < lineEndsAt) return;
            index++;
            if (lines == null || index >= lines.Length) { Stop(); return; }
            RenderLine();
        }

        private void RenderLine()
        {
            DialogueLine line = lines[index];
            string name = line.SpeakerId == "PLAYER"
                ? (LocalizationManager.CurrentLanguage == GameLanguage.Indonesian ? "Kamu" : "You")
                : speaker.DisplayName;
            if (speakerLabel != null) speakerLabel.text = name;
            lineLabel.text = line.Text;
            lineLabel.maxVisibleCharacters = 0;
            revealedCharacters = 0f;
            lineEndsAt = Time.time + Mathf.Max(1f, line.Text.Length / Mathf.Max(1f, charactersPerSecond) + afterLineSeconds);
        }

        private bool IsForeignDialoguePlaying()
        {
            foreach (StoryDialogueController controller in storyControllers)
                if (controller != null && controller.isActiveAndEnabled && controller.IsPlaying) return true;
            return false;
        }

        private void BindAuthoredPresentation()
        {
            Canvas sceneCanvas = null;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                Canvas candidate = root.GetComponent<Canvas>();
                if (candidate == null) candidate = root.GetComponentInChildren<Canvas>(true);
                if (candidate == null || candidate.renderMode == RenderMode.WorldSpace) continue;
                sceneCanvas = candidate;
                break;
            }
            if (sceneCanvas == null) return;
            foreach (RectTransform element in sceneCanvas.GetComponentsInChildren<RectTransform>(true))
                if (element.name == "Chapter One Dialogue Panel") { panelRoot = element.gameObject; break; }
            if (panelRoot != null)
            {
                foreach (TMP_Text label in panelRoot.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (label.name == "Speaker Name" || label.name == "Speaker") speakerLabel = label;
                    if (label.name == "Dialogue Line") lineLabel = label;
                }
            }
            prompt = sceneCanvas.GetComponentInChildren<InteractionPromptView>(true);
            if (panelRoot == null || lineLabel == null) CreateFallbackPresentation(sceneCanvas);
        }

        private void CreateFallbackPresentation(Canvas canvas)
        {
            Transform parent = canvas.transform;
            foreach (RectTransform child in canvas.GetComponentsInChildren<RectTransform>(true))
                if (child.name == "Save Area" || child.name == "Safe Area") { parent = child; break; }
            var root = new GameObject("NPC Automatic Dialogue", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.25f, 0.08f);
            rect.anchorMax = new Vector2(0.75f, 0.22f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image background = root.GetComponent<Image>();
            background.color = new Color(0.035f, 0.055f, 0.09f, 0.92f);
            background.raycastTarget = false;
            panelRoot = root;
            speakerLabel = CreateLabel(root.transform, "Speaker Name", new Vector2(0.04f, 0.7f), new Vector2(0.96f, 0.94f), 20f);
            speakerLabel.color = new Color(1f, 0.8f, 0.32f);
            lineLabel = CreateLabel(root.transform, "Dialogue Line", new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.69f), 20f);
            root.SetActive(false);
        }

        private static TMP_Text CreateLabel(Transform parent, string name, Vector2 minimum, Vector2 maximum, float size)
        {
            var holder = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            holder.transform.SetParent(parent, false);
            RectTransform rect = holder.GetComponent<RectTransform>();
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            TMP_Text text = holder.GetComponent<TMP_Text>();
            text.fontSize = size;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private void OnDisable() => Stop();
        private void OnDestroy()
        {
            Stop();
            if (Instance == this) Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;
    }
}
