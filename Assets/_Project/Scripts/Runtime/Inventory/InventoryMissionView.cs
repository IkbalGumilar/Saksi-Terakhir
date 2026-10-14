using System;
using System.Collections.Generic;
using System.Text;
using SaksiTerakhir.Localization;
using SaksiTerakhir.Story;
using TMPro;
using UnityEngine;

namespace SaksiTerakhir.Inventory
{
    // Reuses the three authored category row templates, without changing assets
    // or requiring a second copy of the user's inventory layout.
    public sealed class InventoryMissionView : IDisposable
    {
        private readonly GameObject page;
        private readonly RectTransform content;
        private readonly UnityEngine.UI.ScrollRect scroll;
        private readonly RectTransform[] templates = new RectTransform[3];
        private readonly RectTransform[] headings = new RectTransform[3];
        private readonly List<GameObject> rows = new List<GameObject>();
        private readonly Action<QuestJournalEntry> selected;
        private QuestJournalController journal;
        private string displayedQuestId = string.Empty;
        private bool disposed;

        public GameObject FirstSelectable { get; private set; }

        public InventoryMissionView(GameObject root, Action<QuestJournalEntry> onSelected)
        {
            page = root;
            selected = onSelected;
            content = root != null ? root.transform.Find("Scroll View/Viewport/Content") as RectTransform : null;
            scroll = root != null ? root.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true) : null;
            if (content == null) return;
            string[] sectionNames = { "Main Quest", "Side Quest", "Optional Quest" };
            for (int i = 0; i < templates.Length; i++)
            {
                templates[i] = content.Find("Slot " + (i + 1)) as RectTransform;
                headings[i] = content.Find(sectionNames[i]) as RectTransform;
                if (templates[i] != null) templates[i].gameObject.SetActive(false);
                if (headings[i] != null) headings[i].gameObject.SetActive(false);
            }
            if (scroll != null)
            {
                scroll.content = content;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            }
            LocalizationManager.LanguageChanged += Refresh;
        }

        public void Bind(QuestJournalController source)
        {
            if (disposed || journal == source) return;
            if (journal != null) journal.Changed -= Refresh;
            journal = source;
            if (journal != null) journal.Changed += Refresh;
            Refresh();
        }

        public void Refresh()
        {
            if (disposed || content == null) return;
            UnityEngine.EventSystems.EventSystem events = UnityEngine.EventSystems.EventSystem.current;
            string focusedName = null;
            if (events != null && events.currentSelectedGameObject != null)
                foreach (GameObject row in rows)
                    if (row != null && events.currentSelectedGameObject.transform.IsChildOf(row.transform))
                        focusedName = row.name;
            foreach (GameObject row in rows) DestroyOwned(row);
            rows.Clear();
            FirstSelectable = null;
            QuestJournalEntry firstEntry = null;
            QuestJournalEntry displayedEntry = null;
            float height = 0f;
            for (int categoryIndex = 0; categoryIndex < templates.Length; categoryIndex++)
            {
                QuestCategory category = (QuestCategory)categoryIndex;
                RectTransform template = templates[categoryIndex];
                bool hasEntries = false;
                if (journal != null)
                    foreach (QuestJournalEntry entry in journal.Entries)
                        if (entry.Definition != null && entry.Definition.Category == category) { hasEntries = true; break; }
                RectTransform heading = headings[categoryIndex];
                if (heading != null)
                {
                    heading.gameObject.SetActive(hasEntries);
                    if (hasEntries)
                    {
                        float headingHeight = Mathf.Max(1f, heading.rect.height);
                        Vector2 position = heading.anchoredPosition;
                        position.y = -height - headingHeight * (1f - heading.pivot.y);
                        heading.anchoredPosition = position;
                        height += headingHeight;
                    }
                }
                if (!hasEntries || template == null) continue;
                foreach (QuestJournalEntry entry in journal.Entries)
                {
                    if (entry.Definition == null || entry.Definition.Category != category) continue;
                    RectTransform row = UnityEngine.Object.Instantiate(template, content, false);
                    row.name = "Mission " + entry.Definition.Id;
                    Vector2 position = row.anchoredPosition;
                    float rowHeight = Mathf.Max(InventoryView.SlotStep, row.rect.height);
                    position.y = -height - rowHeight * (1f - row.pivot.y);
                    row.anchoredPosition = position;
                    height += rowHeight;
                    BindRow(row, entry);
                    row.gameObject.SetActive(true);
                    rows.Add(row.gameObject);
                    if (firstEntry == null) firstEntry = entry;
                    if (entry.Definition.Id == displayedQuestId) displayedEntry = entry;
                }
            }
            float viewportHeight = scroll != null && scroll.viewport != null ? scroll.viewport.rect.height : 0f;
            Vector2 size = content.sizeDelta;
            size.y = Mathf.Max(viewportHeight, height);
            content.sizeDelta = size;
            displayedEntry = displayedEntry ?? firstEntry;
            displayedQuestId = displayedEntry?.Definition.Id ?? string.Empty;
            if (page.activeSelf) selected?.Invoke(displayedEntry);
            if (focusedName != null && events != null && page.activeInHierarchy)
            {
                GameObject target = rows.Find(row => row != null && row.name == focusedName
                    && row.GetComponent<UnityEngine.UI.Button>().interactable);
                events.SetSelectedGameObject(target != null ? target : FirstSelectable);
            }
        }

        private void BindRow(RectTransform row, QuestJournalEntry entry)
        {
            TMP_Text title = FindText(row, "Title");
            TMP_Text description = FindText(row, "Description");
            bool finished = entry.Status == StoryQuestStatus.Completed || entry.Status == StoryQuestStatus.Skipped;
            string name = LocalizationManager.Get(entry.Definition.TitleKey);
            if (title != null) title.text = finished ? "<s>" + name + "</s>" : name;
            if (description != null)
            {
                string location = LocationText(entry.Definition);
                description.text = StatusText(entry.Status) + " · " + (string.IsNullOrWhiteSpace(location)
                    ? ObjectivesText(entry.Definition) : location);
            }
            Transform icon = row.Find("Icon");
            if (icon != null && icon.TryGetComponent(out UnityEngine.UI.Image image))
                image.color = CategoryColor(entry.Definition.Category);
            UnityEngine.UI.Button button = row.GetComponent<UnityEngine.UI.Button>();
            if (button == null) button = row.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = row.GetComponent<UnityEngine.UI.Graphic>();
            button.interactable = true;
            if (button.targetGraphic != null) button.targetGraphic.raycastTarget = true;
            UnityEngine.UI.ColorBlock colors = button.colors;
            colors.normalColor = entry.IsSelected ? new Color(1f, 0.9f, 0.65f, 1f) : Color.white;
            button.colors = colors;
            foreach (UnityEngine.UI.Graphic graphic in row.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                if (graphic.transform != row) graphic.raycastTarget = false;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                displayedQuestId = entry.Definition.Id;
                // Reading a main or finished entry does not change HUD tracking.
                if (journal != null && entry.Definition.Category != QuestCategory.Main
                    && entry.Status == StoryQuestStatus.Active && journal.TrySelect(entry.Definition.Id)) return;
                selected?.Invoke(entry);
            });
            if (FirstSelectable == null && button.interactable) FirstSelectable = row.gameObject;
        }

        public void StopMovement() { if (scroll != null) scroll.StopMovement(); }

        public static Color CategoryColor(QuestCategory category) => category == QuestCategory.Main
            ? QuestTrackerView.MainDotColor : category == QuestCategory.Optional
                ? QuestTrackerView.OptionalDotColor : QuestTrackerView.SideDotColor;

        public static string StatusText(StoryQuestStatus status)
        {
            bool indonesian = LocalizationManager.CurrentLanguage == GameLanguage.Indonesian;
            if (status == StoryQuestStatus.Completed) return indonesian ? "Selesai" : "Completed";
            if (status == StoryQuestStatus.Skipped) return indonesian ? "Dibatalkan" : "Cancelled";
            return indonesian ? "Aktif" : "Active";
        }

        public static string ObjectivesText(QuestDefinition quest)
        {
            if (quest == null) return string.Empty;
            var text = new StringBuilder();
            foreach (string key in quest.ObjectiveKeys)
            {
                if (text.Length > 0) text.Append('\n');
                text.Append(LocalizationManager.Get(key));
            }
            return text.ToString();
        }

        public static string DescriptionText(QuestDefinition quest)
            => quest == null || string.IsNullOrWhiteSpace(quest.DescriptionKey)
                ? string.Empty : LocalizationManager.Get(quest.DescriptionKey);

        public static string LocationText(QuestDefinition quest)
            => quest == null || string.IsNullOrWhiteSpace(quest.LocationKey)
                ? string.Empty : LocalizationManager.Get(quest.LocationKey);

        public static string DetailsText(QuestDefinition quest)
        {
            if (quest == null) return string.Empty;
            var text = new StringBuilder(DescriptionText(quest));
            string location = LocationText(quest);
            if (!string.IsNullOrWhiteSpace(location))
            {
                if (text.Length > 0) text.Append("\n\n");
                text.Append(LocalizationManager.Get("quest.journal.location_label")).Append(": ").Append(location);
            }
            string objectives = ObjectivesText(quest);
            if (!string.IsNullOrWhiteSpace(objectives))
            {
                if (text.Length > 0) text.Append("\n\n");
                text.Append(LocalizationManager.Get("quest.journal.objectives_label")).Append(":\n").Append(objectives);
            }
            return text.ToString();
        }

        private static TMP_Text FindText(Transform root, string name)
        {
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
                if (string.Equals(label.name.Trim(' ', '\'', '"'), name, StringComparison.OrdinalIgnoreCase)) return label;
            return null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (journal != null) journal.Changed -= Refresh;
            LocalizationManager.LanguageChanged -= Refresh;
            foreach (GameObject row in rows) DestroyOwned(row);
            rows.Clear();
        }

        private static void DestroyOwned(GameObject owned)
        {
            if (owned == null) return;
            owned.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(owned);
            else UnityEngine.Object.DestroyImmediate(owned);
        }
    }
}
