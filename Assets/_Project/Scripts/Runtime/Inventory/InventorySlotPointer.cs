using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SaksiTerakhir.Inventory
{
    // Added only to runtime item slots. Decorative graphics route to this root.
    [DisallowMultipleComponent]
    public sealed class InventorySlotPointer : MonoBehaviour, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private InventoryEntry entry;
        private InventoryEntry draggingEntry;
        private Action<InventoryEntry, PointerEventData> context;
        private Func<InventoryEntry, PointerEventData, bool> beginDrag;
        private Action<PointerEventData> drag;
        private Action<InventoryEntry, PointerEventData> endDrag;
        private Action cancelDrag;
        private UnityEngine.UI.ScrollRect scroll;
        private bool scrolling;

        public void Configure(InventoryEntry value,
            Action<InventoryEntry, PointerEventData> showContext,
            Func<InventoryEntry, PointerEventData, bool> begin,
            Action<PointerEventData> update,
            Action<InventoryEntry, PointerEventData> end,
            Action cancel, UnityEngine.UI.ScrollRect fallbackScroll)
        {
            if (draggingEntry != null && !ReferenceEquals(draggingEntry, value))
            {
                CancelDrag();
                cancelDrag?.Invoke();
            }
            entry = value;
            context = showContext;
            beginDrag = begin;
            drag = update;
            endDrag = end;
            cancelDrag = cancel;
            scroll = fallbackScroll;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right
                && entry != null && entry.Type == InventoryEntryType.Item)
                context?.Invoke(entry, eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (entry == null)
            {
                scrolling = scroll != null;
                if (scrolling) scroll.OnBeginDrag(eventData);
                return;
            }
            if (entry.Type != InventoryEntryType.Item) return;
            if (beginDrag != null && beginDrag(entry, eventData)) draggingEntry = entry;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (scrolling && scroll != null) scroll.OnDrag(eventData);
            else if (draggingEntry != null) drag?.Invoke(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (scrolling)
            {
                scrolling = false;
                if (scroll != null) scroll.OnEndDrag(eventData);
                return;
            }
            InventoryEntry dragged = draggingEntry;
            draggingEntry = null;
            if (dragged != null) endDrag?.Invoke(dragged, eventData);
        }

        public void CancelDrag()
        {
            draggingEntry = null;
            scrolling = false;
        }

        private void OnDisable()
        {
            bool wasDragging = draggingEntry != null;
            CancelDrag();
            if (wasDragging) cancelDrag?.Invoke();
        }
    }
}
