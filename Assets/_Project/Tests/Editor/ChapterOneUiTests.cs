using System.Collections.Generic;
using NUnit.Framework;
using SaksiTerakhir.Interaction;
using SaksiTerakhir.Localization;
using SaksiTerakhir.Story;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SaksiTerakhir.Tests
{
    public sealed class ChapterOneUiTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in created)
                if (item != null) Object.DestroyImmediate(item);
            created.Clear();
        }

        [Test]
        public void TrackerKeepsRakaThenSintaAndOnlyStrikesCompletedObjective()
        {
            GameObject root = NewUi("Tracker");
            TMP_Text title = NewText(root.transform, "Title");
            TMP_Text objectives = NewText(root.transform, "Objectives");
            Image dot = NewUi("Dot").AddComponent<Image>();
            dot.transform.SetParent(root.transform, false);
            QuestTrackerView view = root.AddComponent<QuestTrackerView>();
            view.Configure(null, root, title, objectives, dot);
            QuestDefinition quest = ScriptableObject.CreateInstance<QuestDefinition>();
            created.Add(quest);
            quest.Configure("main.chapter1.find", QuestCategory.Main, "quest.main.find",
                new[] { "quest.main.find.raka", "quest.main.find.sinta" });
            ChapterOneProgress progress = new ChapterOneProgress();
            progress.TryApply(ChapterOneEvent.RooftopDialogueFinished);
            progress.TryApply(ChapterOneEvent.BossCallFinished);
            progress.TryApply(ChapterOneEvent.FirstBriefingFinished);

            view.Render(quest, progress);
            Assert.That(dot.color, Is.EqualTo(QuestTrackerView.MainDotColor));
            Assert.That(objectives.text.IndexOf("quest.main.find.raka"),
                Is.LessThan(objectives.text.IndexOf("quest.main.find.sinta")));
            Assert.That(objectives.text, Does.Not.Contain("<s>"));

            progress.TryApply(ChapterOneEvent.ColleagueDialogueFinished, "NPC-003");
            view.Render(quest, progress);
            Assert.That(objectives.text, Does.Contain("<s>"));
            Assert.That(objectives.text, Does.Contain("quest.main.find.sinta"));
            Assert.That(objectives.text.IndexOf("</s>"),
                Is.LessThan(objectives.text.IndexOf("quest.main.find.sinta")));
        }

        [Test]
        public void StoryDialogueHidesGenericPromptAndRestoresItsState()
        {
            GameObject promptObject = NewUi("Prompt View");
            InteractionPromptView prompt = promptObject.AddComponent<InteractionPromptView>();
            GameObject promptRoot = NewUi("Prompt");
            GameObject genericDialogue = NewUi("Generic Dialogue");
            prompt.ConfigureStoryVisibility(promptRoot, genericDialogue);
            GameObject storyRoot = NewUi("Story Panel");
            StoryDialogueView story = storyRoot.AddComponent<StoryDialogueView>();
            story.Configure(null, storyRoot, NewText(storyRoot.transform, "Speaker"),
                NewText(storyRoot.transform, "Line"), prompt, null);
            DialogueSequence sequence = ScriptableObject.CreateInstance<DialogueSequence>();
            created.Add(sequence);
            sequence.Configure("test.story", new[] { new DialogueLine("NPC-021", "Halo.") });

            story.Render(sequence, 0);
            Assert.That(prompt.StoryDialogueActive, Is.True);
            Assert.That(promptRoot.activeSelf, Is.False);
            Assert.That(genericDialogue.activeSelf, Is.False);
            story.Hide();
            Assert.That(prompt.StoryDialogueActive, Is.False);
        }

        [Test]
        public void LocalizationContainsDoorAndQuestPrompts()
        {
            LocalizationTable table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(
                "Assets/_Project/Localization/LocalizationTable.asset");
            Assert.That(table.GetText("interact.story.find_raka", GameLanguage.Indonesian),
                Is.EqualTo("Temukan Raka"));
            Assert.That(table.GetText("interact.story.find_sinta", GameLanguage.Indonesian),
                Is.EqualTo("Temukan Sinta"));
            Assert.That(table.GetText("quest.main.find", GameLanguage.Indonesian),
                Is.EqualTo("Cari rekan-rekan"));
        }

        private GameObject NewUi(string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            created.Add(go);
            return go;
        }

        private TMP_Text NewText(Transform parent, string name)
        {
            GameObject go = NewUi(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<TextMeshProUGUI>();
        }
    }
}
