using System;
using NUnit.Framework;
using RoadBuilder.Core.History;
using UnityEngine;

namespace RoadBuilder.Tests
{
    [TestFixture]
    public class UndoRedoManagerTests
    {
        private GameObject _managerGameObject;
        private UndoRedoManager _manager;

        private class TestCommand : IReversibleCommand
        {
            public string Description { get; }
            public bool IsExecuted { get; private set; }
            public int ExecuteCount { get; private set; }
            public int UndoCount { get; private set; }
            public bool ThrowOnUndo { get; set; }
            public bool ThrowOnExecute { get; set; }

            public TestCommand(string description)
            {
                Description = description;
                IsExecuted = true;
            }

            public bool Execute()
            {
                if (ThrowOnExecute)
                {
                    throw new InvalidOperationException("Simulated execute failure");
                }
                IsExecuted = true;
                ExecuteCount++;
                return true;
            }

            public bool Undo()
            {
                if (ThrowOnUndo)
                {
                    throw new InvalidOperationException("Simulated undo failure");
                }
                IsExecuted = false;
                UndoCount++;
                return true;
            }
        }

        [SetUp]
        public void SetUp()
        {
            _managerGameObject = new GameObject("[Test_UndoRedoManager]");
            _manager = _managerGameObject.AddComponent<UndoRedoManager>();
            _manager.Initialize(10);
        }

        [TearDown]
        public void TearDown()
        {
            if (_managerGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_managerGameObject);
            }
        }

        [Test]
        public void Test_InitialState_StackIsEmpty()
        {
            Assert.IsFalse(_manager.CanUndo);
            Assert.IsFalse(_manager.CanRedo);
            Assert.AreEqual(0, _manager.UndoCount);
            Assert.AreEqual(0, _manager.RedoCount);
            Assert.AreEqual(string.Empty, _manager.GetUndoActionName());
            Assert.AreEqual(string.Empty, _manager.GetRedoActionName());
        }

        [Test]
        public void Test_RegisterCommand_UpdatesCountsAndFiresEvents()
        {
            bool historyChangedFired = false;
            bool canUndoArg = false;
            bool canRedoArg = false;
            IReversibleCommand executedCommand = null;

            _manager.OnHistoryChanged += (canUndo, canRedo) =>
            {
                historyChangedFired = true;
                canUndoArg = canUndo;
                canRedoArg = canRedo;
            };
            _manager.OnCommandExecuted += cmd => executedCommand = cmd;

            var cmd = new TestCommand("Spawn Road Segment");
            _manager.RegisterCommand(cmd);

            Assert.IsTrue(_manager.CanUndo);
            Assert.IsFalse(_manager.CanRedo);
            Assert.AreEqual(1, _manager.UndoCount);
            Assert.AreEqual(0, _manager.RedoCount);
            Assert.AreEqual("Spawn Road Segment", _manager.GetUndoActionName());
            Assert.IsTrue(historyChangedFired);
            Assert.IsTrue(canUndoArg);
            Assert.IsFalse(canRedoArg);
            Assert.AreSame(cmd, executedCommand);
        }

        [Test]
        public void Test_RegisterCommand_NullCommand_IsIgnored()
        {
            bool executedFired = false;
            _manager.OnCommandExecuted += cmd => executedFired = true;

            _manager.RegisterCommand(null);

            Assert.IsFalse(_manager.CanUndo);
            Assert.AreEqual(0, _manager.UndoCount);
            Assert.IsFalse(executedFired);
        }

        [Test]
        public void Test_Undo_Success_MovesCommandToRedo()
        {
            var cmd = new TestCommand("Spawn Road");
            _manager.RegisterCommand(cmd);

            IReversibleCommand undoneCmd = null;
            _manager.OnCommandUndone += c => undoneCmd = c;

            bool result = _manager.Undo();

            Assert.IsTrue(result);
            Assert.AreEqual(1, cmd.UndoCount);
            Assert.IsFalse(cmd.IsExecuted);
            Assert.IsFalse(_manager.CanUndo);
            Assert.IsTrue(_manager.CanRedo);
            Assert.AreEqual(0, _manager.UndoCount);
            Assert.AreEqual(1, _manager.RedoCount);
            Assert.AreEqual("Spawn Road", _manager.GetRedoActionName());
            Assert.AreSame(cmd, undoneCmd);
        }

        [Test]
        public void Test_Undo_WhenCannotUndo_ReturnsFalse()
        {
            bool undoneFired = false;
            _manager.OnCommandUndone += c => undoneFired = true;

            bool result = _manager.Undo();

            Assert.IsFalse(result);
            Assert.IsFalse(undoneFired);
        }

        [Test]
        public void Test_Redo_Success_ReExecutesCommand()
        {
            var cmd = new TestCommand("Place Sign");
            _manager.RegisterCommand(cmd);
            _manager.Undo();

            IReversibleCommand redoneCmd = null;
            _manager.OnCommandRedone += c => redoneCmd = c;

            bool result = _manager.Redo();

            Assert.IsTrue(result);
            Assert.AreEqual(1, cmd.ExecuteCount);
            Assert.IsTrue(cmd.IsExecuted);
            Assert.IsTrue(_manager.CanUndo);
            Assert.IsFalse(_manager.CanRedo);
            Assert.AreEqual(1, _manager.UndoCount);
            Assert.AreEqual(0, _manager.RedoCount);
            Assert.AreSame(cmd, redoneCmd);
        }

        [Test]
        public void Test_Redo_WhenCannotRedo_ReturnsFalse()
        {
            bool redoneFired = false;
            _manager.OnCommandRedone += c => redoneFired = true;

            bool result = _manager.Redo();

            Assert.IsFalse(result);
            Assert.IsFalse(redoneFired);
        }

        [Test]
        public void Test_BranchingHistory_NewCommandClearsRedo()
        {
            var cmd1 = new TestCommand("Step 1");
            var cmd2 = new TestCommand("Step 2");
            _manager.RegisterCommand(cmd1);
            _manager.RegisterCommand(cmd2);

            _manager.Undo(); // Redo contains Step 2
            Assert.IsTrue(_manager.CanRedo);
            Assert.AreEqual(1, _manager.RedoCount);

            var cmd3 = new TestCommand("Step 3 (Branch)");
            _manager.RegisterCommand(cmd3);

            Assert.IsFalse(_manager.CanRedo);
            Assert.AreEqual(0, _manager.RedoCount);
            Assert.AreEqual(2, _manager.UndoCount);
            Assert.AreEqual("Step 3 (Branch)", _manager.GetUndoActionName());
        }

        [Test]
        public void Test_ClearHistory_ResetsAll()
        {
            _manager.RegisterCommand(new TestCommand("A"));
            _manager.RegisterCommand(new TestCommand("B"));
            _manager.Undo();

            Assert.AreEqual(1, _manager.UndoCount);
            Assert.AreEqual(1, _manager.RedoCount);

            bool changedFired = false;
            _manager.OnHistoryChanged += (u, r) =>
            {
                changedFired = true;
                Assert.IsFalse(u);
                Assert.IsFalse(r);
            };

            _manager.ClearHistory();

            Assert.AreEqual(0, _manager.UndoCount);
            Assert.AreEqual(0, _manager.RedoCount);
            Assert.IsFalse(_manager.CanUndo);
            Assert.IsFalse(_manager.CanRedo);
            Assert.IsTrue(changedFired);
        }

        [Test]
        public void Test_Undo_CatchesException_ReturnsFalse()
        {
            var cmd = new TestCommand("Faulty") { ThrowOnUndo = true };
            _manager.RegisterCommand(cmd);

            bool result = false;
            Assert.DoesNotThrow(() => { result = _manager.Undo(); });
            Assert.IsFalse(result);
        }

        [Test]
        public void Test_Redo_CatchesException_ReturnsFalse()
        {
            var cmd = new TestCommand("Faulty") { ThrowOnExecute = true };
            _manager.RegisterCommand(cmd);
            _manager.Undo();

            bool result = false;
            Assert.DoesNotThrow(() => { result = _manager.Redo(); });
            Assert.IsFalse(result);
        }

        [Test]
        public void Test_CapacityLimit_EvictsOldestCommands()
        {
            _manager.Initialize(3);
            for (int i = 1; i <= 5; i++)
            {
                _manager.RegisterCommand(new TestCommand($"Item {i}"));
            }

            Assert.AreEqual(3, _manager.UndoCount);
            Assert.AreEqual("Item 5", _manager.GetUndoActionName());

            _manager.Undo(); // 5 undone
            _manager.Undo(); // 4 undone
            _manager.Undo(); // 3 undone
            Assert.IsFalse(_manager.CanUndo);
            Assert.AreEqual(3, _manager.RedoCount);
        }
    }
}