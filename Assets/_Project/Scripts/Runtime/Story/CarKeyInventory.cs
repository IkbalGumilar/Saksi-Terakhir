using System;

namespace SaksiTerakhir.Story
{
    public sealed class CarKeyInventory
    {
        public const string CarKeyId = "car-key";
        private readonly ChapterOneProgress progress;

        public CarKeyInventory(ChapterOneProgress chapterProgress)
        {
            progress = chapterProgress ?? throw new ArgumentNullException(nameof(chapterProgress));
        }

        public bool Has(string itemId) => itemId == CarKeyId && progress.HasCarKey;

        public bool TryAdd(string itemId) => itemId == CarKeyId
            && progress.TryApply(ChapterOneEvent.CarKeyReceived);
    }
}
