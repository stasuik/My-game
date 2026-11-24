using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RoadBuilder.Core.History;
using RoadBuilder.Core.Telemetry;
using RoadBuilder.Core.FX;
using RoadBuilder.Core.UI;

namespace RoadBuilder.Tests
{
    [TestFixture]
    public class VRDissolveAndTelemetryTests
    {
        private GameObject _telemetryGo;
        private UndoRedoTelemetryManager _telemetryManager;

        [SetUp]
        public void SetUp()
        {
            _telemetryGo = new GameObject("[TestTelemetryManager]");
            _telemetryManager = _telemetryGo.AddComponent<UndoRedoTelemetryManager>();
            _telemetryManager.ClearBuffer();
        }

        [TearDown]
        public void TearDown()
        {
            if (_telemetryGo != null)
            {
                UnityEngine.Object.DestroyImmediate(_telemetryGo);
            }
            UndoRedoManager.Instance.ClearHistory();
        }

        [Test]
        public void Telemetry_Tracks_UndoTriggered_Correctly()
        {
            var dispatchedList = new List<TelemetryEventPayload>();
            _telemetryManager.OnEventDispatched += (payload) => dispatchedList.Add(payload);

            _telemetryManager.TrackUndoTriggered("Создание: Дорога 2 полосы", 2, 1, 1.45f);

            Assert.AreEqual(1, dispatchedList.Count);
            var ev = dispatchedList[0];
            Assert.AreEqual(TelemetryEventNames.UndoTriggered, ev.EventName);
            Assert.AreEqual("Создание: Дорога 2 полосы", ev.ActionName);
            Assert.AreEqual(2, ev.UndoStackSize);
            Assert.AreEqual(1, ev.RedoStackSize);
            Assert.IsTrue(ev.Success);
            Assert.Greater(ev.Timestamp, 0);
        }

        [Test]
        public void Telemetry_Tracks_EmptyStack_Failures()
        {
            _telemetryManager.TrackUndoFailedEmpty();
            _telemetryManager.TrackRedoFailedEmpty();

            var buffer = _telemetryManager.BufferedEvents;
            Assert.AreEqual(2, buffer.Count);
            Assert.AreEqual(TelemetryEventNames.UndoFailedEmptyStack, buffer[0].EventName);
            Assert.IsFalse(buffer[0].Success);
            Assert.AreEqual(TelemetryEventNames.RedoFailedEmptyStack, buffer[1].EventName);
            Assert.IsFalse(buffer[1].Success);
        }

        [Test]
        public void Telemetry_IntegratesWith_UndoRedoManager_Execution()
        {
            var dummyObj = new GameObject("TestRoadSegment");
            var cmd = new SpawnObjectCommand(dummyObj, "Секция шоссе");

            UndoRedoManager.Instance.ClearHistory();
            UndoRedoManager.Instance.RegisterCommand(cmd);

            Assert.IsTrue(UndoRedoManager.Instance.CanUndo);
            bool undoOk = UndoRedoManager.Instance.Undo();
            Assert.IsTrue(undoOk);

            var events = _telemetryManager.BufferedEvents;
            Assert.IsTrue(events.Count > 0);
            var lastEvent = events[events.Count - 1];
            Assert.AreEqual(TelemetryEventNames.UndoTriggered, lastEvent.EventName);
            Assert.AreEqual(cmd.Description, lastEvent.ActionName);

            bool redoOk = UndoRedoManager.Instance.Redo();
            Assert.IsTrue(redoOk);
            lastEvent = events[events.Count - 1];
            Assert.AreEqual(TelemetryEventNames.RedoTriggered, lastEvent.EventName);

            UnityEngine.Object.DestroyImmediate(dummyObj);
        }

        [Test]
        public void DissolvableSpawnCommand_TogglesTargetAndInstantiatesEffect()
        {
            var go = new GameObject("DissolveTestItem");
            var dissolvableCmd = new DissolvableSpawnCommand(go, "Тестовый проп");

            Assert.IsNotNull(go.GetComponent<VRDissolveEffect>());
            Assert.AreEqual("Создание: Тестовый проп", dissolvableCmd.Description);

            // В режиме редактора / тестов (not playing) переключает SetActive
            dissolvableCmd.Undo();
            Assert.IsFalse(go.activeSelf);

            dissolvableCmd.Execute();
            Assert.IsTrue(go.activeSelf);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void VRPanelPBROptimizer_ConfiguresMaterialPropertiesCorrectly()
        {
            var panelObj = new GameObject("VRPanelTest");
            var optimizer = panelObj.AddComponent<VRPanelPBROptimizer>();

            Assert.AreEqual(0.85f, optimizer.Metallic, 0.001f);
            Assert.AreEqual(0.65f, optimizer.Smoothness, 0.001f);
            Assert.Greater(optimizer.PanelBaseColor.a, 0.5f);

            UnityEngine.Object.DestroyImmediate(panelObj);
        }
    }
}
