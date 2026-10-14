using SaksiTerakhir.Inventory;

namespace SaksiTerakhir.Story
{
    public static class ChapterOneDocumentRewards
    {
        public const string AgendaId = "document.agenda_2026_08_24";
        public const string ReceiptId = "document.travel_receipt";

        public static bool GrantBriefingDocuments(InventoryPanelController inventory)
        {
            return Grant(inventory, ItemDefinitionCatalog.WanasetaArchiveId)
                && Grant(inventory, "document.field_assignment");
        }

        public static bool Grant(InventoryPanelController inventory, string id)
        {
            if (inventory == null || inventory.State == null) return false;
            id = ItemDefinitionCatalog.CanonicalId(id);
            foreach (InventoryEntry document in inventory.State.Documents)
                if (document.Id == id) return true;
            ItemDefinition definition = inventory.FindDefinition(id);
            return definition != null && definition.IsDocument
                && inventory.TryAdd(definition, "chapter1:" + id);
        }

        public static bool TrySubmitReceipts(ChapterOneDirector chapter, InventoryPanelController inventory)
        {
            if (chapter == null || inventory == null || inventory.State == null) return false;
            foreach (InventoryEntry document in inventory.State.Documents)
                if (document.Id == ReceiptId)
                    return chapter.TryApplyStoryEvent(ChapterOneStoryEvent.TravelReceiptSubmitted);
            return false;
        }
    }
}
