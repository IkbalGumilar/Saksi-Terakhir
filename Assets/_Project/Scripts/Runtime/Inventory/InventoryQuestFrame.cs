using SaksiTerakhir.Story;
using UnityEngine;
using UnityEngine.UI;

namespace SaksiTerakhir.Inventory
{
    // Only runtime slot copies receive this frame; the authored slot remains editable.
    public sealed class InventoryQuestFrame : MonoBehaviour
    {
        private readonly Image[] edges = new Image[4];
        private Color color;
        public QuestCategory? Category { get; private set; }

        public static InventoryQuestFrame Create(RectTransform slot)
        {
            var rect = (RectTransform)new GameObject("Quest Item Frame", typeof(RectTransform)).transform;
            rect.SetParent(slot, false);
            rect.gameObject.layer = slot.gameObject.layer;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var frame = rect.gameObject.AddComponent<InventoryQuestFrame>();
            for (int index = 0; index < frame.edges.Length; index++)
            {
                var edge = (RectTransform)new GameObject("Border " + index, typeof(RectTransform)).transform;
                edge.SetParent(rect, false);
                edge.gameObject.layer = rect.gameObject.layer;
                bool vertical = index < 2;
                float side = index % 2;
                edge.anchorMin = vertical ? new Vector2(side, 0f) : new Vector2(0f, side);
                edge.anchorMax = vertical ? new Vector2(side, 1f) : new Vector2(1f, side);
                edge.pivot = Vector2.one * 0.5f;
                edge.sizeDelta = vertical ? new Vector2(3f, 0f) : new Vector2(0f, 3f);
                edge.anchoredPosition = Vector2.zero;
                frame.edges[index] = edge.gameObject.AddComponent<Image>();
                frame.edges[index].raycastTarget = false;
            }
            frame.SetCategory(null);
            return frame;
        }

        public void SetCategory(QuestCategory? category)
        {
            Category = category;
            gameObject.SetActive(category.HasValue);
            if (!category.HasValue) return;
            color = QuestTrackerView.DotColor(category.Value);
            Render(1f);
        }

        private void Update() => Render(0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 2f));

        private void Render(float brightness)
        {
            Color visible = new Color(color.r * brightness, color.g * brightness, color.b * brightness, 1f);
            foreach (Image edge in edges) if (edge != null) edge.color = visible;
        }
    }
}
