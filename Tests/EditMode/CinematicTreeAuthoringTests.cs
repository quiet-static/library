using System;
using NUnit.Framework;
using QuietStatic.Toolkit.Cinematics;
using QuietStatic.Toolkit.Editor.Cinematics;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Tests.EditMode
{
    public sealed class CinematicTreeAuthoringTests
    {
        private CinematicTree tree;
        [SetUp] public void Setup() => tree = ScriptableObject.CreateInstance<CinematicTree>();
        [TearDown] public void Cleanup() => UnityEngine.Object.DestroyImmediate(tree);

        [Test] public void ReorderAndDuplicate_PreserveExistingShotIdentities()
        {
            CinematicTreeCommands.Add(tree);
            CinematicTreeCommands.Add(tree);
            string first = tree.Beats[0].id, second = tree.Beats[1].id;
            CinematicTreeCommands.Move(tree, 0, 1);
            Assert.That(tree.Beats[0].id, Is.EqualTo(second));
            Assert.That(tree.Beats[1].id, Is.EqualTo(first));
            CinematicTreeCommands.Duplicate(tree, 0);
            Assert.That(tree.Beats.Count, Is.EqualTo(3));
            Assert.That(tree.Beats[0].id, Is.Not.EqualTo(second));
            Assert.That(tree.Beats[1].id, Is.EqualTo(second));
            Assert.That(tree.TryValidate(out _), Is.True);
            CinematicTreeCommands.Delete(tree, 0);
            Assert.That(tree.Beats[0].id, Is.EqualTo(second));
        }

        [Test] public void JsonRoundTrip_PreservesRichTextIdsTimingAndOrder()
        {
            string json = "{\"schemaVersion\":1,\"description\":\"Opening\",\"beats\":[{\"id\":\"wide\",\"text\":\"<i>Rain.</i>\",\"charactersPerSecond\":12,\"holdSeconds\":4},{\"id\":\"close\",\"text\":\"Office\",\"charactersPerSecond\":0,\"holdSeconds\":0}]}";
            CinematicTreeCommands.ImportJson(tree, json);
            string exported = CinematicTreeCommands.ExportJson(tree);
            CinematicTreeCommands.ImportJson(tree, exported);
            Assert.That(tree.Description, Is.EqualTo("Opening"));
            Assert.That(tree.Beats[0].text, Is.EqualTo("<i>Rain.</i>"));
            Assert.That(tree.Beats[0].holdSeconds, Is.EqualTo(4));
            Assert.That(tree.Beats[1].id, Is.EqualTo("close"));
            Assert.That(tree.TryValidate(out _), Is.True);
        }

        [Test] public void InvalidImport_DoesNotPartiallyReplaceExistingTree()
        {
            CinematicTreeCommands.Add(tree);
            string original = CinematicTreeCommands.ExportJson(tree);
            Assert.Throws<ArgumentException>(() => CinematicTreeCommands.ImportJson(tree,
                "{\"schemaVersion\":1,\"beats\":[{\"id\":\"same\",\"text\":\"One\"},{\"id\":\"same\",\"text\":\"Two\"}]}"));
            Assert.That(CinematicTreeCommands.ExportJson(tree), Is.EqualTo(original));
            Assert.Throws<ArgumentException>(() => CinematicTreeCommands.ImportJson(tree, "{\"beats\":[]}"));
        }

        [Test] public void BeatEdits_CanBeUndone()
        {
            CinematicTreeCommands.Add(tree);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            string original = tree.Beats[0].id;
            CinematicTreeCommands.Add(tree);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(tree.Beats.Count, Is.EqualTo(1));
            Assert.That(tree.Beats[0].id, Is.EqualTo(original));
        }
    }
}
