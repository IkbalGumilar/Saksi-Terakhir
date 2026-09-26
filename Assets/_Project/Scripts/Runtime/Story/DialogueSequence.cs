using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaksiTerakhir.Story
{
    [Serializable]
    public sealed class DialogueLine
    {
        [SerializeField] private string speakerId = string.Empty;
        [SerializeField, TextArea(2, 4)] private string text = string.Empty;

        public string SpeakerId => speakerId;
        public string Text => text;

        public DialogueLine(string id, string content)
        {
            speakerId = id;
            text = content;
        }
    }

    [CreateAssetMenu(fileName = "DialogueSequence", menuName = "Saksi Terakhir/Dialogue Sequence")]
    public sealed class DialogueSequence : ScriptableObject
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private DialogueLine[] lines = Array.Empty<DialogueLine>();

        public string Id => id;
        public IReadOnlyList<DialogueLine> Lines => lines;

        public void Configure(string sequenceId, DialogueLine[] dialogueLines)
        {
            id = sequenceId;
            lines = dialogueLines ?? Array.Empty<DialogueLine>();
        }

        public bool Validate(out string error)
        {
            if (string.IsNullOrWhiteSpace(id) || lines.Length == 0)
            {
                error = "Dialogue ID and at least one line are required.";
                return false;
            }
            foreach (DialogueLine line in lines)
            {
                if (line == null || string.IsNullOrWhiteSpace(line.SpeakerId)
                    || string.IsNullOrWhiteSpace(line.Text))
                {
                    error = $"Dialogue {id} has an incomplete line.";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }
    }
}
