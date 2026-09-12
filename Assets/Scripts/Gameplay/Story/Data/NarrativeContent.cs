using System;
using System.Collections.Generic;
using UnityEngine;

namespace MemorialArchive.Gameplay.Story.Data
{
    [Serializable]
    public sealed class ReadingEntry
    {
        public string id;
        public string title;
        public bool isDiary;
        public string sceneId;
        public string content;
        public string reflectionId;
        public string[] aliases = Array.Empty<string>();
    }

    [Serializable]
    public sealed class NarrativeChoice
    {
        public string text;
        public int next;
    }

    [Serializable]
    public sealed class NarrativeLine
    {
        public string text;
        public string speaker;
        public int next = -1;
        public bool fadeCecil;
        public NarrativeChoice[] choices = Array.Empty<NarrativeChoice>();
    }

    [Serializable]
    public sealed class NarrativeSequence
    {
        public string id;
        public string sceneId;
        public bool onFirstEnter;
        public string interactionId;
        public string[] requiredNotes = Array.Empty<string>();
        public NarrativeLine[] lines = Array.Empty<NarrativeLine>();
    }

    [Serializable]
    public sealed class NarrativeContent
    {
        public ReadingEntry[] notes = Array.Empty<ReadingEntry>();
        public NarrativeSequence[] sequences = Array.Empty<NarrativeSequence>();

        public static NarrativeContent Load()
        {
            var source = Resources.Load<TextAsset>("Narrative/story_content");
            return source != null ? JsonUtility.FromJson<NarrativeContent>(source.text) : new NarrativeContent();
        }
        public ReadingEntry FindNote(string id) => Array.Find(notes, n => n.id == id || Array.IndexOf(n.aliases, id) >= 0);
        public NarrativeSequence FindSequence(string id) => Array.Find(sequences, s => s.id == id);
    }

    [Serializable]
    public sealed class NarrativeProgress
    {
        public List<string> collectedNotes = new List<string>();
        public List<string> readNotes = new List<string>();
        public List<string> playedSequences = new List<string>();
        public List<string> pendingSequences = new List<string>();
    }
}
