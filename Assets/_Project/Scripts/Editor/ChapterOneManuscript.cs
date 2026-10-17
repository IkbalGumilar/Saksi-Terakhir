using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SaksiTerakhir.Story;
using UnityEngine;

namespace SaksiTerakhir.EditorTools
{
    // The manuscript remains the source of dialogue. Regeneration copies its words
    // instead of maintaining a second, gradually diverging story in editor code.
    internal sealed class ChapterOneManuscript
    {
        public string Main { get; }
        public string Branches { get; }

        public ChapterOneManuscript()
        {
            string project = Path.GetDirectoryName(Application.dataPath);
            Main = File.ReadAllText(Path.Combine(project, "docs/bab-01.md")).Replace("\r", string.Empty);
            Branches = File.ReadAllText(Path.Combine(project, "docs/bab-01-branch.md")).Replace("\r", string.Empty);
        }

        public static string Between(string source, string start, string end)
        {
            int beginning = source.IndexOf(start, StringComparison.Ordinal);
            if (beginning < 0) throw new InvalidDataException("Missing chapter-one section: " + start);
            beginning += start.Length;
            int ending = source.IndexOf(end, beginning, StringComparison.Ordinal);
            if (ending < 0) throw new InvalidDataException("Missing chapter-one section ending: " + end);
            return source.Substring(beginning, ending - beginning);
        }

        public static DialogueLine[] Speeches(string section)
        {
            var lines = new List<DialogueLine>();
            var quote = new StringBuilder();
            string speaker = string.Empty;
            foreach (string raw in section.Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith(">", StringComparison.Ordinal))
                {
                    if (string.IsNullOrEmpty(speaker))
                        throw new InvalidDataException("Chapter-one quotation has no speaker: " + line);
                    string words = PlainText(line.Substring(1).Trim());
                    if (words.Length != 0)
                    {
                        if (quote.Length != 0) quote.Append(' ');
                        quote.Append(words);
                    }
                    continue;
                }
                FlushQuote();
                if (line.StartsWith("**", StringComparison.Ordinal) && line.EndsWith(":**", StringComparison.Ordinal))
                    speaker = SpeakerId(line.Substring(2, line.Length - 5));
                // A few replies use narrative attribution instead of another bold
                // label (Bu Kantin's debt reply and the late vehicle branches).
                else
                {
                    string narratedSpeaker = NarratedSpeaker(line);
                    if (narratedSpeaker != null) speaker = narratedSpeaker;
                }
            }
            FlushQuote();
            if (lines.Count == 0) throw new InvalidDataException("Chapter-one dialogue section contains no speeches.");
            return lines.ToArray();

            void FlushQuote()
            {
                if (quote.Length == 0) return;
                lines.Add(new DialogueLine(speaker, quote.ToString()));
                quote.Clear();
            }
        }

        public static string QuotedDocument(string section)
        {
            var paragraphs = new List<string>();
            foreach (string raw in section.Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith(">", StringComparison.Ordinal))
                    paragraphs.Add(PlainText(line.Substring(1).Trim()));
            }
            if (paragraphs.Count == 0) throw new InvalidDataException("Chapter-one document section has no quoted text.");
            return string.Join("\n", paragraphs).Trim();
        }

        private static string PlainText(string value) => value.Replace("**", string.Empty);

        private static string NarratedSpeaker(string line)
        {
            foreach (string name in new[] { "Bu Kantin", "Petugas Digitalisasi", "Rekan 1", "Rekan 2", "Resepsionis", "Ucup", "MC", "Bos", "Dadan", "Rafi", "A", "B", "Pegawai" })
                if (line.StartsWith(name + " ", StringComparison.Ordinal)) return SpeakerId(name);
            return null;
        }

        private static string SpeakerId(string speaker)
        {
            switch (speaker)
            {
                case "Ucup":
                case "MC": return "PLAYER";
                case "Dadan":
                case "A": return "NPC-003";
                case "Rafi":
                case "B": return "NPC-004";
                case "Bos": return "NPC-006";
                case "Resepsionis": return "NPC-002";
                case "Rekan 1": return "NPC-021";
                case "Rekan 2": return "NPC-022";
                default: return speaker;
            }
        }
    }
}
