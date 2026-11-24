using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoadBuilder.Core.Telemetry
{
    /// <summary>
    /// Сбор и диспетчеризация продуктовой телеметрии и аналитики Undo / Redo в VR
    /// </summary>
    public class UndoRedoTelemetryManager : MonoBehaviour
    {
        private static UndoRedoTelemetryManager _instance;

        public static UndoRedoTelemetryManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<UndoRedoTelemetryManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[UndoRedoTelemetryManager]");
                        _instance = go.AddComponent<UndoRedoTelemetryManager>();
                    }
                }
                return _instance;
            }
        }

        [Header("Конфигурация")]
        [SerializeField] private bool _enableConsoleLog = true;
        [SerializeField] private bool _enableMemoryBuffer = true;
        [SerializeField] private int _maxBufferSize = 200;

        private readonly List<TelemetryEventPayload> _bufferedEvents = new List<TelemetryEventPayload>();

        public IReadOnlyList<TelemetryEventPayload> BufferedEvents => _bufferedEvents;
        public event Action<TelemetryEventPayload> OnEventDispatched;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void TrackUndoTriggered(string actionName, int remainingUndoCount, int remainingRedoCount, float executionDurationMs)
        {
            var payload = new TelemetryEventPayload(
                TelemetryEventNames.UndoTriggered,
                actionName,
                remainingUndoCount,
                remainingRedoCount,
                executionDurationMs,
                success: true
            );
            Dispatch(payload);
        }

        public void TrackRedoTriggered(string actionName, int remainingUndoCount, int remainingRedoCount, float executionDurationMs)
        {
            var payload = new TelemetryEventPayload(
                TelemetryEventNames.RedoTriggered,
                actionName,
                remainingUndoCount,
                remainingRedoCount,
                executionDurationMs,
                success: true
            );
            Dispatch(payload);
        }

        public void TrackUndoFailedEmpty()
        {
            var payload = new TelemetryEventPayload(
                TelemetryEventNames.UndoFailedEmptyStack,
                "none",
                0,
                0,
                0f,
                success: false
            );
            Dispatch(payload);
        }

        public void TrackRedoFailedEmpty()
        {
            var payload = new TelemetryEventPayload(
                TelemetryEventNames.RedoFailedEmptyStack,
                "none",
                0,
                0,
                0f,
                success: false
            );
            Dispatch(payload);
        }

        public void TrackStackCapacityEviction(string evictedAction, int currentCapacity)
        {
            var payload = new TelemetryEventPayload(
                TelemetryEventNames.StackCapacityEviction,
                evictedAction,
                currentCapacity,
                0,
                0f,
                success: true
            );
            Dispatch(payload);
        }

        private void Dispatch(TelemetryEventPayload payload)
        {
            if (_enableMemoryBuffer)
            {
                if (_bufferedEvents.Count >= _maxBufferSize)
                {
                    _bufferedEvents.RemoveAt(0);
                }
                _bufferedEvents.Add(payload);
            }

            if (_enableConsoleLog)
            {
                Debug.Log($"[Telemetry] {payload.EventName} | Action: {payload.ActionName} | UndoLeft: {payload.UndoStackSize} | RedoLeft: {payload.RedoStackSize} | Time: {payload.ExecutionDurationMs:F2}ms");
            }

            OnEventDispatched?.Invoke(payload);
        }

        public void ClearBuffer()
        {
            _bufferedEvents.Clear();
        }
    }

    public static class TelemetryEventNames
    {
        public const string UndoTriggered = "undo_triggered";
        public const string RedoTriggered = "redo_triggered";
        public const string UndoFailedEmptyStack = "undo_failed_empty_stack";
        public const string RedoFailedEmptyStack = "redo_failed_empty_stack";
        public const string StackCapacityEviction = "stack_capacity_eviction";
    }

    [Serializable]
    public struct TelemetryEventPayload
    {
        public string EventName;
        public string ActionName;
        public int UndoStackSize;
        public int RedoStackSize;
        public float ExecutionDurationMs;
        public bool Success;
        public long Timestamp;

        public TelemetryEventPayload(
            string eventName,
            string actionName,
            int undoStackSize,
            int redoStackSize,
            float executionDurationMs,
            bool success)
        {
            EventName = eventName;
            ActionName = actionName;
            UndoStackSize = undoStackSize;
            RedoStackSize = redoStackSize;
            ExecutionDurationMs = executionDurationMs;
            Success = success;
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
