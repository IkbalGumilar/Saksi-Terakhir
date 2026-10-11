using System;
using System.Globalization;
using SaksiTerakhir.Environment;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Inventory;
using SaksiTerakhir.Localization;
using SaksiTerakhir.Player;
using UnityEngine;

namespace SaksiTerakhir.Story
{
    public enum ChapterOneActivityType
    {
        MorningDesk, Breakfast, WrongRouteOffer, WrongRouteDelivery,
        DeskPreparation, SharedMeal, Shop
    }

    [Serializable]
    public sealed class ChapterOneShopOffer
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField, Min(0)] private int priceRupiah;
        public ItemDefinition Item => item;
        public int PriceRupiah => priceRupiah;
        public ChapterOneShopOffer(ItemDefinition definition, int price)
        { item = definition; priceRupiah = Mathf.Max(0, price); }
    }

    // Activity outcomes are recorded by the chapter only after their dialogue or
    // inventory transaction succeeds. Shared ScriptableObjects remain immutable.
    [DisallowMultipleComponent]
    public sealed class ChapterOneActivityInteractable : Interactable
    {
        [SerializeField] private ChapterOneDirector chapter;
        [SerializeField] private StoryDialogueController dialogue;
        [SerializeField] private ChapterOneActivityType activity;
        [SerializeField] private Transform mealSeat;
        [SerializeField] private Transform mealStand;
        [SerializeField] private string shopName = "Toserba";
        [SerializeField] private ChapterOneShopOffer[] offers = Array.Empty<ChapterOneShopOffer>();

        private StoryPlayerSeating ownedSeating;
        private Vector3 standingPosition;
        private Quaternion standingRotation;
        private int offerIndex;

        public ChapterOneActivityType Activity => activity;
        public ChapterOneShopOffer[] Offers => offers;

        public void Configure(ChapterOneDirector director, ChapterOneActivityType type,
            StoryDialogueController controller = null, Transform seat = null, Transform stand = null)
        {
            chapter = director;
            activity = type;
            dialogue = controller;
            mealSeat = seat;
            mealStand = stand;
        }

        public void ConfigureShop(ChapterOneDirector director, string storeName,
            ChapterOneShopOffer[] storeOffers, StoryDialogueController controller = null)
        {
            Configure(director, ChapterOneActivityType.Shop, controller);
            shopName = storeName;
            offers = storeOffers ?? Array.Empty<ChapterOneShopOffer>();
        }

        public override bool CanInteract(Transform actor)
        {
            if (!base.CanInteract(actor) || actor == null || chapter == null || !chapter.isActiveAndEnabled
                || chapter.GameplaySuspended || chapter.Progress == null || ChapterOneChoiceView.IsOpen
                || (dialogue != null && dialogue.IsPlaying)) return false;
            InventoryPanelController inventory = actor.GetComponentInParent<InventoryPanelController>();
            if (inventory == null || inventory.State == null || inventory.IsOpen) return false;
            ChapterOneProgress progress = chapter.Progress;
            ChapterOneStoryState story = progress.Story;
            bool atStation = progress.Stage == ChapterOneStage.AtFuelStation
                || progress.Stage == ChapterOneStage.ReturnToVehicle;
            if (story.DepartureDriver != ChapterOneDepartureDriver.Unassigned && !(atStation && activity == ChapterOneActivityType.Shop))
                return false;
            bool preparing = progress.Stage == ChapterOneStage.CollectCarKey
                || progress.Stage == ChapterOneStage.ReachVehicle;
            switch (activity)
            {
                case ChapterOneActivityType.MorningDesk:
                    return !progress.MorningRoutineComplete;
                case ChapterOneActivityType.Breakfast:
                    return !progress.MorningRoutineComplete && story.Breakfast == BreakfastChoice.Unresolved;
                case ChapterOneActivityType.WrongRouteOffer:
                    return !progress.MorningRoutineComplete && story.WrongRouteQuest == StoryQuestStatus.NotStarted;
                case ChapterOneActivityType.WrongRouteDelivery:
                    return story.WrongRouteQuest == StoryQuestStatus.Active;
                case ChapterOneActivityType.DeskPreparation:
                    return preparing && (story.DeskPreparationQuest == StoryQuestStatus.NotStarted
                        || story.DeskPreparationQuest == StoryQuestStatus.Active
                        || (story.DeskPrepared && story.Agenda == AgendaChoice.Unresolved));
                case ChapterOneActivityType.SharedMeal:
                    return progress.Stage == ChapterOneStage.CollectCarKey && !progress.HasCarKey
                        && chapter.MealColleaguesReady
                        && (story.SharedMealQuest == StoryQuestStatus.NotStarted
                            || story.SharedMealQuest == StoryQuestStatus.Active)
                        && dialogue != null && mealSeat != null && mealStand != null
                        && actor.GetComponentInParent<StoryPlayerSeating>() != null;
                case ChapterOneActivityType.Shop:
                    return (preparing || atStation) && offers.Length > 0;
                default:
                    return false;
            }
        }

        public override void Interact(Transform actor)
        {
            if (!CanInteract(actor)) return;
            InventoryPanelController inventory = actor.GetComponentInParent<InventoryPanelController>();
            switch (activity)
            {
                case ChapterOneActivityType.MorningDesk:
                    chapter.TryCompleteMorningRoutine();
                    break;
                case ChapterOneActivityType.Breakfast:
                    AcceptBreakfastQuest();
                    Play("chapter.breakfast", () => ShowBreakfastChoice(actor));
                    break;
                case ChapterOneActivityType.WrongRouteOffer:
                    Play("chapter.wrong_route_offer", () => ShowWrongRouteChoice(actor));
                    break;
                case ChapterOneActivityType.WrongRouteDelivery:
                    Play("chapter.wrong_route_delivered", () =>
                        chapter.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteCompleted));
                    break;
                case ChapterOneActivityType.DeskPreparation:
                    ShowDeskChoice(actor, inventory);
                    break;
                case ChapterOneActivityType.SharedMeal:
                    BeginSharedMeal(actor);
                    break;
                case ChapterOneActivityType.Shop:
                    Play("chapter.shop_supplies", () => ShowShop(actor, inventory));
                    break;
            }
        }

        private void Play(string id, Action completed)
        {
            chapter.TryPlayActivityDialogue(id, () =>
            {
                if (this != null && isActiveAndEnabled && chapter != null && !chapter.GameplaySuspended)
                    completed?.Invoke();
            });
        }

        private void ShowBreakfastChoice(Transform actor)
        {
            ChapterOneChoiceView.Show(this, actor, Text("Bayar sarapan?", "Pay for breakfast?"),
                new ChapterOneChoiceView.Option(Text("Bayar sekarang", "Pay now"), () =>
                    Play("chapter.breakfast_paid", () => FinishBreakfast(ChapterOneStoryEvent.BreakfastPaid))),
                new ChapterOneChoiceView.Option(Text("Ngutang dulu", "Pay later"), () =>
                    Play("chapter.breakfast_owed", () => FinishBreakfast(ChapterOneStoryEvent.BreakfastOwed))));
        }

        private void AcceptBreakfastQuest()
        {
            QuestJournalCatalog catalog = QuestJournalCatalog.LoadDefault();
            if (catalog == null) return;
            foreach (QuestDefinition quest in catalog.Definitions)
                if (quest != null && quest.Id == "optional.chapter1.breakfast")
                {
                    QuestJournalController.GetOrCreate(chapter).TryAccept(quest);
                    return;
                }
        }

        private void FinishBreakfast(ChapterOneStoryEvent outcome)
        {
            if (!chapter.TryApplyStoryEvent(outcome)) return;
            QuestJournalController.GetOrCreate(chapter).TryFinish("optional.chapter1.breakfast");
        }

        private void ShowWrongRouteChoice(Transform actor)
        {
            ChapterOneChoiceView.Show(this, actor,
                Text("Antar berkas ke ruang digitalisasi?", "Deliver the file to digitization?"),
                new ChapterOneChoiceView.Option(Text("Terima", "Accept"), () =>
                    Play("chapter.wrong_route_accepted", () => chapter.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteStarted))),
                new ChapterOneChoiceView.Option(Text("Tolak", "Decline"), () =>
                    Play("chapter.wrong_route_declined", () => chapter.TryApplyStoryEvent(ChapterOneStoryEvent.WrongRouteSkipped))));
        }

        private void ShowDeskChoice(Transform actor, InventoryPanelController inventory)
        {
            if (chapter.Progress.Story.DeskPrepared) { ShowAgendaChoice(actor, inventory); return; }
            ChapterOneChoiceView.Show(this, actor, Text("Rapikan meja sebelum pergi?", "Tidy the desk before leaving?"),
                new ChapterOneChoiceView.Option(Text("Rapikan", "Tidy up"), () =>
                {
                    if (chapter.Progress.Story.DeskPreparationQuest == StoryQuestStatus.NotStarted)
                        chapter.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationStarted);
                    Play("chapter.desk_prepared", () =>
                    {
                        if (!GiveAgendaBook(inventory)) return;
                        chapter.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationCompleted);
                        ShowAgendaChoice(actor, inventory);
                    });
                }),
                new ChapterOneChoiceView.Option(Text("Lewati", "Skip"), () =>
                    chapter.TryApplyStoryEvent(ChapterOneStoryEvent.DeskPreparationSkipped)));
        }

        private bool GiveAgendaBook(InventoryPanelController inventory)
        {
            const string id = "item.agenda_book";
            return inventory.HasItem(id) || inventory.TryAdd(inventory.FindDefinition(id), "chapter1:agenda-book");
        }

        private void ShowAgendaChoice(Transform actor, InventoryPanelController inventory)
        {
            if (chapter.Progress.Story.Agenda != AgendaChoice.Unresolved) return;
            ChapterOneChoiceView.Show(this, actor, Text("Tulis agenda hari ini?", "Write today's agenda?"),
                new ChapterOneChoiceView.Option(Text("Tulis agenda", "Write agenda"), () =>
                    Play("chapter.agenda_written", () =>
                    {
                        if (ChapterOneDocumentRewards.Grant(inventory, ChapterOneDocumentRewards.AgendaId))
                            chapter.TryApplyStoryEvent(ChapterOneStoryEvent.AgendaWritten);
                    })),
                new ChapterOneChoiceView.Option(Text("Lewati", "Skip"), () =>
                    chapter.TryApplyStoryEvent(ChapterOneStoryEvent.AgendaSkipped)));
        }

        private void BeginSharedMeal(Transform actor)
        {
            StoryPlayerSeating seating = actor.GetComponentInParent<StoryPlayerSeating>();
            standingPosition = seating.transform.position;
            standingRotation = seating.transform.rotation;
            if (!seating.SeatAt(mealSeat)) return;
            ownedSeating = seating;
            if (chapter.Progress.Story.SharedMealQuest == StoryQuestStatus.NotStarted
                && !chapter.TryApplyStoryEvent(ChapterOneStoryEvent.SharedMealStarted))
            { EndSharedMeal(); return; }
            if (dialogue != null) dialogue.DialogueEnded += EndSharedMeal;
            if (!chapter.TryPlayActivityDialogue("chapter.shared_meal", () =>
                {
                    if (this != null && isActiveAndEnabled && chapter != null)
                        chapter.TryApplyStoryEvent(ChapterOneStoryEvent.SharedMealCompleted);
                    EndSharedMeal();
                })) EndSharedMeal();
        }

        private void EndSharedMeal()
        {
            if (dialogue != null) dialogue.DialogueEnded -= EndSharedMeal;
            if (ownedSeating == null) return;
            StoryPlayerSeating seating = ownedSeating;
            ownedSeating = null;
            // If another cinematic took ownership of the pose, leave that owner alone.
            if (!seating.IsSeated || seating.CurrentSeat != mealSeat) return;
            seating.StandUp();
            CharacterController controller = seating.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (wasEnabled) controller.enabled = false;
            seating.transform.SetPositionAndRotation(mealStand != null ? mealStand.position : standingPosition,
                mealStand != null ? mealStand.rotation : standingRotation);
            if (wasEnabled) controller.enabled = true;
        }

        private void ShowShop(Transform actor, InventoryPanelController inventory)
        {
            if (this == null || !isActiveAndEnabled || chapter == null || offers.Length == 0) return;
            offerIndex = Mathf.Clamp(offerIndex, 0, offers.Length - 1);
            ChapterOneShopOffer offer = offers[offerIndex];
            if (offer == null || offer.Item == null || offer.Item.IsDocument || offer.Item.ModelPrefab == null)
            { chapter.RequestNotice("inventory.unavailable"); return; }
            string name = LocalizationManager.Get(offer.Item.NameKey);
            string price = "Rp" + offer.PriceRupiah.ToString("N0", CultureInfo.GetCultureInfo("id-ID"));
            ChapterOneChoiceView.Show(this, actor, shopName + "\n" + name + " — " + price,
                new ChapterOneChoiceView.Option(Text("Beli", "Buy"), () =>
                {
                    Buy(inventory, offer);
                    ShowShop(actor, inventory);
                }),
                new ChapterOneChoiceView.Option(Text("Barang berikutnya", "Next item"), () =>
                {
                    offerIndex = (offerIndex + 1) % offers.Length;
                    ShowShop(actor, inventory);
                }),
                new ChapterOneChoiceView.Option(Text("Selesai", "Done"), () => CompleteShopping(inventory)));
        }

        private void Buy(InventoryPanelController inventory, ChapterOneShopOffer offer)
        {
            if (this == null || !isActiveAndEnabled || chapter == null || chapter.GameplaySuspended
                || inventory == null || offer == null || offer.Item == null) return;
            ChapterOneStage stage = chapter.Progress.Stage;
            bool atStation = stage == ChapterOneStage.AtFuelStation || stage == ChapterOneStage.ReturnToVehicle;
            if (!atStation && stage != ChapterOneStage.CollectCarKey && stage != ChapterOneStage.ReachVehicle) return;
            InventoryEntry purchasedItem = offer.Item.CreateEntry();
            if (!inventory.TryAdd(purchasedItem)) return;
            // Prices belong to the receipt. No currency balance exists in Bab 1 yet.
            string name = LocalizationManager.Get(offer.Item.NameKey);
            string description = shopName + "\n24 Agustus 2026 · " + ReceiptTime()
                + "\n" + name + " × 1\nTotal: Rp"
                + offer.PriceRupiah.ToString("N0", CultureInfo.GetCultureInfo("id-ID"));
            InventoryEntry receipt = new InventoryEntry(ChapterOneDocumentRewards.ReceiptId,
                Text("Struk pembayaran — ", "Payment receipt — ") + shopName,
                InventoryEntryType.Document, description: description);
            if (!inventory.TryAdd(receipt))
            {
                inventory.Remove(purchasedItem);
                chapter.RequestNotice("inventory.unavailable");
                return;
            }
            if (!atStation)
            {
                if (chapter.Progress.Story.SuppliesQuest == StoryQuestStatus.NotStarted)
                    chapter.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesStarted);
                chapter.TryRecordTravelPurchase(offer.Item.Id);
            }
            chapter.RequestNotice("inventory.purchase_complete");
            if (atStation)
            {
                chapter.NotifyStationProgress();
                var session = chapter.GetComponent<SaksiTerakhir.Persistence.GameSessionController>();
                if (session != null) session.RequestAutosave();
            }
        }

        private void CompleteShopping(InventoryPanelController inventory)
        {
            if (this != null && isActiveAndEnabled && chapter != null && inventory != null
                && chapter.Progress.Story.SuppliesQuest == StoryQuestStatus.Active
                && chapter.Progress.Story.PurchasedSupplyIds.Count > 0)
                chapter.TryApplyStoryEvent(ChapterOneStoryEvent.SuppliesCompleted);
        }

        private string ReceiptTime()
        {
            float hour = 13f;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                TimeOfDayApplier time = root.GetComponentInChildren<TimeOfDayApplier>();
                if (time == null || time.ActivePreset == null) continue;
                hour = time.ActivePreset.hourOfDay;
                break;
            }
            int totalMinutes = Mathf.RoundToInt(Mathf.Repeat(hour, 24f) * 60f) % (24 * 60);
            return (totalMinutes / 60).ToString("00") + ":" + (totalMinutes % 60).ToString("00");
        }

        private static string Text(string indonesian, string english)
            => LocalizationManager.CurrentLanguage == GameLanguage.Indonesian ? indonesian : english;

        protected override string ResolvePromptKey()
            => activity == ChapterOneActivityType.Shop ? "interact.chapter.shop" : "interact.chapter.activity";

        private void OnDisable()
        {
            ChapterOneChoiceView.CancelForOwner(this);
            EndSharedMeal();
        }
    }
}
