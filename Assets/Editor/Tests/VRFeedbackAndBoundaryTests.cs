using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using RoadBuilder.Core.History;
using RoadBuilder.Core.UI;
using RoadBuilder.Core.Feedback;

namespace RoadBuilder.Tests
{
    [TestFixture]
    public class VRFeedbackAndBoundaryTests
    {
        private class MockActionCommand : IReversibleCommand
        {
            public string Description { get; }
            public bool IsExecuted { get; private set; }
            public int ExecuteCount { get; private set; }
            public int UndoCount { get; private set; }

            public MockActionCommand(string description)
            {
                Description = description;
                IsExecuted = true;
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

        private GameObject managerObj;
        private UndoRedoManager undoRedoManager;
        private GameObject toastObj;
        private VRToastSystem toastSystem;
        private GameObject feedbackObj;
        private VRFeedbackManager feedbackManager;

        [SetUp]
        public void SetUp()
        {
            managerObj = new GameObject("UndoRedoManager_Test");
            undoRedoManager = managerObj.AddComponent<UndoRedoManager>();
            undoRedoManager.Initialize(5); // малый лимит для граничных тестов

            toastObj = new GameObject("VRToastSystem_Test", typeof(RectTransform));
            toastSystem = toastObj.AddComponent<VRToastSystem>();
            toastSystem.EnsureUIStructure();

            feedbackObj = new GameObject("VRFeedbackManager_Test");
            feedbackManager = feedbackObj.AddComponent<VRFeedbackManager>();
            feedbackManager.InitializeAudio();
        }

        [TearDown]
        public void TearDown()
        {
            if (managerObj != null) UnityEngine.Object.DestroyImmediate(managerObj);
            if (toastObj != null) UnityEngine.Object.DestroyImmediate(toastObj);
            if (feedbackObj != null) UnityEngine.Object.DestroyImmediate(feedbackObj);
        }

        #region Граничные тесты стека истории (Задача 5.2 - Варя)

        [Test]
        public void Test_EmptyStack_UndoReturnsFalseAndDoesNotThrow()
        {
            Assert.IsFalse(undoRedoManager.CanUndo);
            bool result = false;
            Assert.DoesNotThrow(() =>
            {
                result = undoRedoManager.Undo();
            });
            Assert.IsFalse(result, "Undo на пустом стеке должен возвращать false без исключений");
        }

        [Test]
        public void Test_EmptyStack_RedoReturnsFalseAndDoesNotThrow()
        {
            Assert.IsFalse(undoRedoManager.CanRedo);
            bool result = false;
            Assert.DoesNotThrow(() =>
            {
                result = undoRedoManager.Redo();
            });
            Assert.IsFalse(result, "Redo на пустом future-стеке должен возвращать false без исключений");
        }

        [Test]
        public void Test_CapacityLimit_EvictsOldestFIFO_PreservesIntegrity()
        {
            const int limit = 3;
            undoRedoManager.Initialize(limit);

            var c1 = new MockActionCommand("Road 1");
            var c2 = new MockActionCommand("Road 2");
            var c3 = new MockActionCommand("Road 3");
            var c4 = new MockActionCommand("Road 4");
            var c5 = new MockActionCommand("Road 5");

            undoRedoManager.RegisterCommand(c1);
            undoRedoManager.RegisterCommand(c2);
            undoRedoManager.RegisterCommand(c3);
            undoRedoManager.RegisterCommand(c4);
            undoRedoManager.RegisterCommand(c5);

            Assert.AreEqual(limit, undoRedoManager.UndoCount, "Размер стека не должен превышать capacity");
            Assert.AreEqual("Road 5", undoRedoManager.GetUndoActionName());

            undoRedoManager.Undo(); // откат 5
            Assert.AreEqual("Road 4", undoRedoManager.GetUndoActionName());
            undoRedoManager.Undo(); // откат 4
            Assert.AreEqual("Road 3", undoRedoManager.GetUndoActionName());
            undoRedoManager.Undo(); // откат 3

            Assert.IsFalse(undoRedoManager.CanUndo, "Команды 1 и 2 должны быть вытеснены по FIFO");
        }

        [Test]
        public void Test_ConcurrentOrRapidCommands_ConsistencyMaintained()
        {
            undoRedoManager.Initialize(50);
            const int iterations = 100;

            for (int i = 0; i < iterations; i++)
            {
                undoRedoManager.RegisterCommand(new MockActionCommand($"Rapid {i}"));
                if (i % 3 == 0)
                {
                    undoRedoManager.Undo();
                }
                if (i % 5 == 0 && undoRedoManager.CanRedo)
                {
                    undoRedoManager.Redo();
                }
            }

            Assert.IsTrue(undoRedoManager.UndoCount >= 0);
            Assert.IsTrue(undoRedoManager.RedoCount >= 0);
            Assert.IsTrue(undoRedoManager.UndoCount <= 50);
        }

        [Test]
        public void Test_Clear_ResetsBothStacks_CanUndoRedoFalse()
        {
            undoRedoManager.RegisterCommand(new MockActionCommand("Action A"));
            undoRedoManager.RegisterCommand(new MockActionCommand("Action B"));
            undoRedoManager.Undo();

            Assert.IsTrue(undoRedoManager.CanUndo);
            Assert.IsTrue(undoRedoManager.CanRedo);

            undoRedoManager.ClearHistory();

            Assert.IsFalse(undoRedoManager.CanUndo);
            Assert.IsFalse(undoRedoManager.CanRedo);
            Assert.AreEqual(0, undoRedoManager.UndoCount);
            Assert.AreEqual(0, undoRedoManager.RedoCount);
        }

        #endregion

        #region Тесты Toast-системы и подсказок (Задача 4.1 - Паша)

        [Test]
        public void Test_ToastSystem_AntiSpam_UpdatesExistingMessageWithoutOverlap()
        {
            toastSystem.ShowUndoSuccess("Дорога 1");
            Assert.IsTrue(toastSystem.IsActive);
            Assert.AreEqual("Отмена: Дорога 1", toastSystem.CurrentMessage);
            Assert.AreEqual(1, toastSystem.NotificationCount);

            // Быстрый второй вызов
            toastSystem.ShowRedoSuccess("Дорога 2");
            Assert.IsTrue(toastSystem.IsActive);
            Assert.AreEqual("Повтор: Дорога 2", toastSystem.CurrentMessage);
            Assert.AreEqual(2, toastSystem.NotificationCount, "Счетчик уведомлений инкрементируется, плашка переиспользуется");

            // Вызов при пустом стеке
            toastSystem.ShowUndoEmpty();
            Assert.AreEqual("Нечего отменять", toastSystem.CurrentMessage);
            Assert.AreEqual(ToastType.Warning, toastSystem.CurrentType);
        }

        [Test]
        public void Test_VRTooltipTrigger_Attach_ConfiguresCorrectly()
        {
            var btnObj = new GameObject("TestButton");
            try
            {
                var trigger = VRTooltipTrigger.Attach(btnObj, "Отменить последнее действие [Ctrl+Z]");
                Assert.IsNotNull(trigger);
                Assert.AreEqual("Отменить последнее действие [Ctrl+Z]", trigger.tooltipText);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(btnObj);
            }
        }

        #endregion

        #region Тесты аудио, вибрации и 3D-кнопок (Задача 4.2 - Диана)

        [Test]
        public void Test_FeedbackManager_SuppressesAudioOnEmptyStack()
        {
            int initialSuccess = feedbackManager.SuccessfulAudioPlayCount;
            int initialSuppressed = feedbackManager.SuppressedAudioCount;

            feedbackManager.PlayEmptyFailure();

            Assert.AreEqual(initialSuccess, feedbackManager.SuccessfulAudioPlayCount, "Звук не должен проигрываться при пустом стеке");
            Assert.AreEqual(initialSuppressed + 1, feedbackManager.SuppressedAudioCount, "Отказ должен учитываться в подавленных звуках");
        }

        [Test]
        public void Test_FeedbackManager_PlaysSuccessAudioAndHaptics()
        {
            int initialAudio = feedbackManager.SuccessfulAudioPlayCount;
            int initialHaptics = feedbackManager.HapticTriggerCount;

            feedbackManager.PlayUndoSuccess();
            feedbackManager.PlayRedoSuccess();

            Assert.AreEqual(initialAudio + 2, feedbackManager.SuccessfulAudioPlayCount);
            Assert.AreEqual(initialHaptics + 2, feedbackManager.HapticTriggerCount);
        }

        [Test]
        public void Test_VR3DButton_TactileMarkerAndDepthCalculation()
        {
            var btnObj = new GameObject("VR3DButton_Test");
            try
            {
                var btn = btnObj.AddComponent<VR3DButton>();
                btn.interactable = true;

                float pressedOffset = btn.CalculateDepressionOffset(true);
                float unpressedOffset = btn.CalculateDepressionOffset(false);

                Assert.AreEqual(0.005f, pressedOffset, 0.0001f, "Ход нажатия должен составлять 5 мм (0.005м)");
                Assert.AreEqual(0f, unpressedOffset, 0.0001f, "В отпущенном состоянии смещение 0");

                btn.interactable = false;
                float disabledOffset = btn.CalculateDepressionOffset(true);
                Assert.AreEqual(0f, disabledOffset, 0.0001f, "Для неактивной кнопки ход блокируется");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(btnObj);
            }
        }

        #endregion
    }
}