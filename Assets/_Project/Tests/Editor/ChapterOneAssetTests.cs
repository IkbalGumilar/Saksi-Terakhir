using NUnit.Framework;
using SaksiTerakhir.Npc;
using SaksiTerakhir.Story;
using UnityEditor;

namespace SaksiTerakhir.Tests
{
    public sealed class ChapterOneAssetTests
    {
        private const string Root = "Assets/_Project/Story";

        [Test]
        public void AllEightMainQuestsHaveStableOrderedObjectives()
        {
            string[] names = { "rooftop", "call", "boss-first", "find-colleagues",
                "escort", "boss-final", "car-key", "vehicle" };
            foreach (string name in names)
            {
                QuestDefinition quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(
                    $"{Root}/Quests/{name}.asset");
                Assert.That(quest, Is.Not.Null, name);
                Assert.That(quest.Category, Is.EqualTo(QuestCategory.Main), name);
                Assert.That(quest.Validate(out _), Is.True, name);
            }
            QuestDefinition find = AssetDatabase.LoadAssetAtPath<QuestDefinition>(
                $"{Root}/Quests/find-colleagues.asset");
            Assert.That(find.ObjectiveKeys, Is.EqualTo(new[]
            {
                "quest.main.find.raka", "quest.main.find.sinta"
            }));
        }

        [TestCase("rooftop", 16, 20)]
        [TestCase("boss-call", 4, 6)]
        [TestCase("boss-first", 14, 18)]
        [TestCase("raka-first", 6, 8)]
        [TestCase("sinta-first", 6, 8)]
        [TestCase("raka-last", 12, 16)]
        [TestCase("sinta-last", 12, 16)]
        [TestCase("boss-final", 18, 22)]
        [TestCase("nadia-key", 5, 7)]
        public void StoryDialogueHasAuthoredLengthAndValidSpeakers(string name, int minimum,
            int maximum)
        {
            DialogueSequence sequence = AssetDatabase.LoadAssetAtPath<DialogueSequence>(
                $"{Root}/Dialogues/{name}.asset");
            Assert.That(sequence, Is.Not.Null, name);
            Assert.That(sequence.Validate(out _), Is.True, name);
            Assert.That(sequence.Lines.Count, Is.InRange(minimum, maximum), name);
            foreach (DialogueLine line in sequence.Lines)
                Assert.That(line.SpeakerId == "PLAYER" || line.SpeakerId == "NPC-021"
                    || line.SpeakerId == "NPC-022" || line.SpeakerId == "NPC-003"
                    || line.SpeakerId == "NPC-004" || line.SpeakerId == "NPC-006"
                    || line.SpeakerId == "NPC-002", Is.True, line.SpeakerId);
        }

        [Test]
        public void FinalBriefingContainsFictionalArchiveClue()
        {
            DialogueSequence sequence = AssetDatabase.LoadAssetAtPath<DialogueSequence>(
                $"{Root}/Dialogues/boss-final.asset");
            string text = string.Join(" ", System.Array.ConvertAll(
                new System.Collections.Generic.List<DialogueLine>(sequence.Lines).ToArray(),
                line => line.Text));
            Assert.That(text, Does.Contain("Provinsi Arunika"));
            Assert.That(text, Does.Contain("Kecamatan Tanjung Sagara"));
        }

        [TestCase("NPC-021-Pekerja-03", "chapter.rooftop")]
        [TestCase("NPC-022-Pekerja-04", "chapter.rooftop")]
        [TestCase("NPC-003-Rekan-A", "chapter.raka_first")]
        [TestCase("NPC-004-Rekan-B", "chapter.sinta_last")]
        [TestCase("NPC-006-Bos", "chapter.boss_final")]
        [TestCase("NPC-002-Resepsionis", "chapter.nadia_key")]
        public void StoryNpcsReferenceTheirDialogue(string profileName, string sequenceId)
        {
            NpcProfile profile = AssetDatabase.LoadAssetAtPath<NpcProfile>(
                $"Assets/_Project/Npc/Profiles/{profileName}.asset");
            Assert.That(profile, Is.Not.Null);
            Assert.That(System.Array.Exists(profile.StoryDialogues,
                sequence => sequence != null && sequence.Id == sequenceId), Is.True,
                profileName);
        }
    }
}
