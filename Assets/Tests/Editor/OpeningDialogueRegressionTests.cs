using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using MemorialArchive.Gameplay.Dialogue.Config;

namespace MemorialArchive.Tests.Editor
{
    public sealed class OpeningDialogueRegressionTests
    {
        private const string ConfigPath = "Assets/GameConfigs/Dialogue/OpeningDialogue.asset";

        [Test]
        public void OpeningDialogue_UsesRequiredCgOrderAndStopsAtGameplayHandoff()
        {
            var config = AssetDatabase.LoadAssetAtPath<DialogueSequenceConfig>(ConfigPath);
            Assert.That(config, Is.Not.Null);

            var cgNames = new List<string>();
            var lastIndex = -1;
            foreach (var node in config.Nodes)
            {
                if (node.NodeId == "opening_051") lastIndex = cgNames.Count;
                if (node.CgCommand == DialogueCgCommand.Set)
                {
                    var path = AssetDatabase.GetAssetPath(node.CgSprite);
                    cgNames.Add(System.IO.Path.GetFileNameWithoutExtension(path));
                }
            }

            Assert.That(cgNames, Is.EqualTo(new[]
            {
                "CG01", "CG02", "CG03", "CG04", "CG03", "CG02", "CG05", "CG02"
            }));
            Assert.That(Array.Find(config.Nodes, n => n.NodeId == "opening_052"), Is.Null);
            Assert.That(lastIndex, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void OpeningDialogue_OnlyOpeningFourQuotationLinesAreCentered()
        {
            var config = AssetDatabase.LoadAssetAtPath<DialogueSequenceConfig>(ConfigPath);
            for (var i = 0; i < config.Nodes.Length; i++)
            {
                var node = config.Nodes[i];
                var shouldCenter = node.NodeId == "opening_012" || node.NodeId == "opening_013"
                    || node.NodeId == "opening_014" || node.NodeId == "opening_015";
                Assert.That(node.CenterText, Is.EqualTo(shouldCenter), node.NodeId);
            }

            var footsteps = Array.Find(config.Nodes, n => n.NodeId == "opening_047");
            StringAssert.Contains("<size=32><i><color=#FFFFFF>一步步朝大门走来的……脚步声。</color></i></size>", footsteps.Text);
        }
    }
}
