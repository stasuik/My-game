using System;
using NUnit.Framework;
using RoadBuilder.Core.History;
using UnityEngine;

namespace RoadBuilder.Tests
{
    [TestFixture]
    public class HistoryStackTests
    {
        private class MockCommand : IReversibleCommand
        {
            public string Description { get; }
            public bool IsExecuted { get; private set; }
            public int ExecuteCount { get; private set; }
            public int UndoCount { get; private set; }

            public MockCommand(string description)
            {
                Description = description;
                IsExecuted = true; // Считается первично выполненной при регистрации
            }

            public bool Execute()
            {
                IsExecuted = true;
                ExecuteCount++;
                return true;
            }

            public bool Undo()
            {
                IsExecuted = false;
                UndoCount++;
                return true;
            }
        }

        [Test]
        public void Test_InitialState_StackIsEmpty()
        {
            var stack = new HistoryStack(10);
            Assert.AreEqual(0, stack.UndoCount);
            Assert.AreEqual(0, stack.RedoCount);
            Assert.IsFalse(stack.CanUndo);
            Assert.IsFalse(stack.CanRedo);
        }

        [Test]
        public void Test_Push_IncrementsUndoCount_AndClearsRedo()
        {
            var stack = new HistoryStack(10);
            var cmd = new MockCommand("cmd1");
            stack.Push(cmd);

            Assert.AreEqual(1, stack.UndoCount);
            Assert.AreEqual(0, stack.RedoCount);
            Assert.IsTrue(stack.CanUndo);
            Assert.IsFalse(stack.CanRedo);
        }

        [Test]
        public void Test_UndoRedo_SingleCommand()
        {
            var stack = new HistoryStack(10);
            var cmd = new MockCommand("Road 1");
            stack.Push(cmd);

            // Undo
            var undone = stack.PopUndo();
            Assert.AreSame(cmd, undone);
            Assert.AreEqual(0, stack.UndoCount);
            Assert.AreEqual(1, stack.RedoCount);
            Assert.IsFalse(stack.CanUndo);
            Assert.IsTrue(stack.CanRedo);

            // Redo
            var redone = stack.PopRedo();
            Assert.AreSame(cmd, redone);
            Assert.AreEqual(1, stack.UndoCount);
            Assert.AreEqual(0, stack.RedoCount);
            Assert.IsTrue(stack.CanUndo);
            Assert.IsFalse(stack.CanRedo);
        }

        [Test]
        public void Test_MultipleCommands_UndoInReverseOrder()
        {
            var stack = new HistoryStack(10);
            var cmd1 = new MockCommand("1");
            var cmd2 = new MockCommand("2");
            var cmd3 = new MockCommand("3");

            stack.Push(cmd1);
            stack.Push(cmd2);
            stack.Push(cmd3);

            Assert.AreEqual(3, stack.UndoCount);
            Assert.AreSame(cmd3, stack.PopUndo());
            Assert.AreSame(cmd2, stack.PopUndo());
            Assert.AreSame(cmd1, stack.PopUndo());
            Assert.IsFalse(stack.CanUndo);
            Assert.AreEqual(3, stack.RedoCount);
        }

        [Test]
        public void Test_EmptyStack_UndoRedoReturnsNull()
        {
            var stack = new HistoryStack(10);
            Assert.IsNull(stack.PopUndo());
            Assert.IsNull(stack.PopRedo());
            Assert.IsNull(stack.PeekUndo());
            Assert.IsNull(stack.PeekRedo());
        }

        [Test]
        public void Test_CapacityLimit_EvictsOldestFIFO()
        {
            const int capacity = 3;
            var stack = new HistoryStack(capacity);

            var cmd1 = new MockCommand("1");
            var cmd2 = new MockCommand("2");
            var cmd3 = new MockCommand("3");
            var cmd4 = new MockCommand("4");

            stack.Push(cmd1);
            stack.Push(cmd2);
            stack.Push(cmd3);
            stack.Push(cmd4); // cmd1 должно быть вытеснено

            Assert.AreEqual(3, stack.UndoCount);
            Assert.AreSame(cmd4, stack.PopUndo());
            Assert.AreSame(cmd3, stack.PopUndo());
            Assert.AreSame(cmd2, stack.PopUndo());
            Assert.IsNull(stack.PopUndo()); // cmd1 больше нет
        }

        [Test]
        public void Test_PushAfterUndo_ClearsRedoStack()
        {
            var stack = new HistoryStack(10);
            var cmd1 = new MockCommand("1");
            var cmd2 = new MockCommand("2");
            var cmd3 = new MockCommand("3");

            stack.Push(cmd1);
            stack.Push(cmd2);
            stack.PopUndo(); // Redo содержит cmd2
            Assert.AreEqual(1, stack.RedoCount);

            stack.Push(cmd3); // Новая ветка действий
            Assert.AreEqual(0, stack.RedoCount);
            Assert.IsFalse(stack.CanRedo);
            Assert.AreEqual(2, stack.UndoCount);
        }

        [Test]
        public void Test_Clear_ResetsBothStacks()
        {
            var stack = new HistoryStack(10);
            stack.Push(new MockCommand("1"));
            stack.Push(new MockCommand("2"));
            stack.PopUndo();

            Assert.AreEqual(1, stack.UndoCount);
            Assert.AreEqual(1, stack.RedoCount);

            stack.Clear();
            Assert.AreEqual(0, stack.UndoCount);
            Assert.AreEqual(0, stack.RedoCount);
            Assert.IsFalse(stack.CanUndo);
            Assert.IsFalse(stack.CanRedo);
        }

        [Test]
        public void Test_SpawnObjectCommand_ExecuteAndUndo()
        {
            var go = new GameObject("TestRoadSegment");
            try
            {
                var cmd = new SpawnObjectCommand(go, "Дорога 2 полосы");
                Assert.IsTrue(go.activeSelf);

                cmd.Undo();
                Assert.IsFalse(go.activeSelf, "После Undo объект должен быть деактивирован");

                cmd.Execute();
                Assert.IsTrue(go.activeSelf, "После Redo (Execute) объект должен быть восстановлен");
            }
            finally
            {
                GameObject.DestroyImmediate(go);
            }
        }

        [Test]
        public void Test_Stress_RapidUndoRedoCycles()
        {
            var stack = new HistoryStack(50);
            var cmd = new MockCommand("StressTest");
            stack.Push(cmd);

            // Имитация быстрой серии из 1000 нажатий Undo/Redo
            for (int i = 0; i < 1000; i++)
            {
                var undone = stack.PopUndo();
                Assert.AreSame(cmd, undone);
                var redone = stack.PopRedo();
                Assert.AreSame(cmd, redone);
            }

            Assert.AreEqual(1, stack.UndoCount);
            Assert.AreEqual(0, stack.RedoCount);
            Assert.IsTrue(stack.CanUndo);
            Assert.IsFalse(stack.CanRedo);
        }

        [Test]
        public void Test_Capacity_55Items_Limit50()
        {
            var stack = new HistoryStack(50);
            for (int i = 1; i <= 55; i++)
            {
                stack.Push(new MockCommand($"Item {i}"));
            }

            Assert.AreEqual(50, stack.UndoCount);
            // Последний добавленный (55) должен быть на вершине Undo
            Assert.AreEqual("Item 55", stack.PeekUndo().Description);
        }
    }
}