using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Core.History;

namespace Tests.Editor
{
    internal class TestCommand : RoadBuilder.Core.History.IReversibleCommand
    {
        public string Description { get; }
        public bool IsExecuted { get; private set; }
        public TestCommand(string description)
        {
            Description = description;
            IsExecuted = true;
        }
        public bool Execute()
        {
            IsExecuted = true;
            return true;
        }
        public bool Undo()
        {
            IsExecuted = false;
            return true;
        }
    }

    [TestFixture]
    public class BuildMenuPanelHistoryUITests
    {
        private GameObject panelObj;
        private BuildMenuPanel panel;
        private GameObject managerObj;
        private UndoRedoManager manager;

        [SetUp]
        public void SetUp()
        {
            managerObj = new GameObject("UndoRedoManager_Test");
            manager = managerObj.AddComponent<UndoRedoManager>();
            manager.Initialize(50);

            panelObj = new GameObject("BuildMenuPanel_Test", typeof(RectTransform));
            panel = panelObj.AddComponent<BuildMenuPanel>();

            // Mock UI button hierarchy
            GameObject undoObj = new GameObject("UndoBtn");
            undoObj.transform.SetParent(panelObj.transform);
            panel.undoButton = undoObj.AddComponent<Button>();
            panel.undoBgImage = undoObj.AddComponent<Image>();
            GameObject undoTextObj = new GameObject("UndoText");
            undoTextObj.transform.SetParent(undoObj.transform);
            panel.undoLabel = undoTextObj.AddComponent<Text>();

            GameObject undoBadgeObj = new GameObject("UndoBadge");
            undoBadgeObj.transform.SetParent(undoObj.transform);
            panel.undoBadgeObj = undoBadgeObj;
            panel.undoBadge = undoBadgeObj.AddComponent<Text>();

            GameObject redoObj = new GameObject("RedoBtn");
            redoObj.transform.SetParent(panelObj.transform);
            panel.redoButton = redoObj.AddComponent<Button>();
            panel.redoBgImage = redoObj.AddComponent<Image>();
            GameObject redoTextObj = new GameObject("RedoText");
            redoTextObj.transform.SetParent(redoObj.transform);
            panel.redoLabel = redoTextObj.AddComponent<Text>();

            GameObject redoBadgeObj = new GameObject("RedoBadge");
            redoBadgeObj.transform.SetParent(redoObj.transform);
            panel.redoBadgeObj = redoBadgeObj;
            panel.redoBadge = redoBadgeObj.AddComponent<Text>();
        }

        [TearDown]
        public void TearDown()
        {
            if (panelObj != null) UnityEngine.Object.DestroyImmediate(panelObj);
            if (managerObj != null) UnityEngine.Object.DestroyImmediate(managerObj);
        }

        [Test]
        public void Test_InitialState_ButtonsDisabled_BadgesHidden()
        {
            panel.UpdateHistoryButtons();

            Assert.IsFalse(panel.undoButton.interactable, "Undo button should be disabled when history is empty");
            Assert.IsFalse(panel.redoButton.interactable, "Redo button should be disabled when redo stack is empty");
            Assert.IsFalse(panel.undoBadgeObj.activeSelf, "Undo badge should be hidden when count is 0");
            Assert.IsFalse(panel.redoBadgeObj.activeSelf, "Redo badge should be hidden when count is 0");
        }

        [Test]
        public void Test_RecordAction_EnablesUndo_ShowsBadgeCount()
        {
            var cmd = new TestCommand("Spawn road");
            manager.RecordAction(cmd);
            panel.UpdateHistoryButtons();

            Assert.IsTrue(panel.undoButton.interactable, "Undo button must be active after recording action");
            Assert.IsFalse(panel.redoButton.interactable, "Redo button must still be disabled");
            Assert.IsTrue(panel.undoBadgeObj.activeSelf, "Undo badge must be visible");
            Assert.AreEqual("1", panel.undoBadge.text);
        }

        [Test]
        public void Test_UndoAction_EnablesRedo_UpdatesBadges()
        {
            manager.RecordAction(new TestCommand("Segment 1"));
            manager.RecordAction(new TestCommand("Segment 2"));
            panel.UpdateHistoryButtons();

            Assert.AreEqual("2", panel.undoBadge.text);

            manager.Undo();
            panel.UpdateHistoryButtons();

            Assert.IsTrue(panel.undoButton.interactable);
            Assert.IsTrue(panel.redoButton.interactable);
            Assert.AreEqual("1", panel.undoBadge.text);
            Assert.IsTrue(panel.redoBadgeObj.activeSelf);
            Assert.AreEqual("1", panel.redoBadge.text);
        }

        [Test]
        public void Test_EventSubscription_UpdatesButtonsAutomatically()
        {
            // Subscribing to manager event
            manager.OnHistoryChanged += panel.UpdateHistoryButtons;

            manager.RecordAction(new TestCommand("Auto Test"));
            Assert.IsTrue(panel.undoButton.interactable);
            Assert.AreEqual("1", panel.undoBadge.text);

            manager.Undo();
            Assert.IsFalse(panel.undoButton.interactable);
            Assert.IsTrue(panel.redoButton.interactable);
            Assert.AreEqual("1", panel.redoBadge.text);
        }
    }
}
