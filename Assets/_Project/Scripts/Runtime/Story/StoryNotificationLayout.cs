using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SaksiTerakhir.Story
{
    /// <summary>
    /// Keeps the transient story notice separate from the persistent quest tracker.
    /// </summary>
    public static class StoryNotificationLayout
    {
        public static readonly Vector2 ScreenAnchor = new Vector2(1f, 1f);
        public static readonly Vector2 ScreenOffset = new Vector2(-18f, -20f);
        public static readonly Vector2 Size = new Vector2(330f, 72f);
        public static readonly Vector2 TextPadding = new Vector2(12f, 8f);

        public static void Apply(GameObject notificationRoot, TMP_Text notificationLabel)
        {
            if (notificationRoot == null) return;
            Apply(notificationRoot.GetComponent<RectTransform>(), notificationLabel);
        }

        public static void Apply(RectTransform notificationRoot, TMP_Text notificationLabel)
        {
            if (notificationRoot != null)
            {
                notificationRoot.anchorMin = ScreenAnchor;
                notificationRoot.anchorMax = ScreenAnchor;
                notificationRoot.pivot = ScreenAnchor;
                notificationRoot.anchoredPosition = ScreenOffset;
                notificationRoot.sizeDelta = Size;

                Image background = notificationRoot.GetComponent<Image>();
                if (background != null)
                {
                    background.color = new Color(0.04f, 0.05f, 0.07f, 0.86f);
                    background.raycastTarget = false;
                }
            }

            if (notificationLabel == null) return;
            RectTransform labelRect = notificationLabel.GetComponent<RectTransform>();
            if (labelRect != null)
            {
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.offsetMin = TextPadding;
                labelRect.offsetMax = -TextPadding;
                labelRect.localScale = Vector3.one;
            }

            notificationLabel.enableAutoSizing = true;
            notificationLabel.fontSizeMin = 18f;
            notificationLabel.fontSizeMax = 24f;
            notificationLabel.enableWordWrapping = true;
            notificationLabel.overflowMode = TextOverflowModes.Ellipsis;
            notificationLabel.alignment = TextAlignmentOptions.MidlineLeft;
            notificationLabel.raycastTarget = false;
        }
    }
}
