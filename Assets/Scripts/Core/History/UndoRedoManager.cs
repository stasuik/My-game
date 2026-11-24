using System;
using UnityEngine;

namespace RoadBuilder.Core.History
{
    /// <summary>
    /// Центральный сервис управления операциями отмены (Undo) и повтора (Redo) в VR
    /// </summary>
    public class UndoRedoManager : MonoBehaviour, IHistoryManager
    {
        private static UndoRedoManager _instance;

        public static UndoRedoManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<UndoRedoManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[UndoRedoManager]");
                        _instance = go.AddComponent<UndoRedoManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Настройки истории")]
        [SerializeField] private int _historyLimit = HistoryStack.DefaultCapacity;

        private HistoryStack _historyStack;

        public bool CanUndo => History.CanUndo;
        public bool CanRedo => History.CanRedo;
        public int UndoCount => History.UndoCount;
        public int RedoCount => History.RedoCount;

        private HistoryStack History
        {
            get
            {
                if (_historyStack == null)
                {
                    _historyStack = new HistoryStack(_historyLimit);
                }
                return _historyStack;
            }
        }

        public event Action<bool, bool> OnHistoryChanged;
        public event Action<IReversibleCommand> OnCommandExecuted;
        public event Action<IReversibleCommand> OnCommandUndone;
        public event Action<IReversibleCommand> OnCommandRedone;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (_historyStack == null)
            {
                _historyStack = new HistoryStack(_historyLimit);
            }
        }

        /// <summary>
        /// Инициализация для стендовых и модульных тестов без сцены
        /// </summary>
        public void Initialize(int limit = HistoryStack.DefaultCapacity)
        {
            _historyLimit = limit;
            _historyStack = new HistoryStack(_historyLimit);
            NotifyHistoryChanged();
        }

        public void RegisterCommand(IReversibleCommand command)
        {
            if (command == null)
            {
                return;
            }

            if (_historyStack == null)
            {
                _historyStack = new HistoryStack(_historyLimit);
            }

            _historyStack.Push(command);
            OnCommandExecuted?.Invoke(command);
            NotifyHistoryChanged();
        }

        public bool Undo()
        {
            if (!CanUndo)
            {
                RoadBuilder.Core.Telemetry.UndoRedoTelemetryManager.Instance.TrackUndoFailedEmpty();
                return false;
            }

            var command = _historyStack.PopUndo();
            if (command == null)
            {
                RoadBuilder.Core.Telemetry.UndoRedoTelemetryManager.Instance.TrackUndoFailedEmpty();
                return false;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool success = false;
            try
            {
                success = command.Undo();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UndoRedoManager] Ошибка при отмене команды '{command.Description}': {ex.Message}");
            }
            sw.Stop();

            if (success)
            {
                OnCommandUndone?.Invoke(command);
                RoadBuilder.Core.Telemetry.UndoRedoTelemetryManager.Instance.TrackUndoTriggered(
                    command.Description,
                    _historyStack.UndoCount,
                    _historyStack.RedoCount,
                    (float)sw.Elapsed.TotalMilliseconds
                );
            }

            NotifyHistoryChanged();
            return success;
        }

        public bool Redo()
        {
            if (!CanRedo)
            {
                RoadBuilder.Core.Telemetry.UndoRedoTelemetryManager.Instance.TrackRedoFailedEmpty();
                return false;
            }

            var command = _historyStack.PopRedo();
            if (command == null)
            {
                RoadBuilder.Core.Telemetry.UndoRedoTelemetryManager.Instance.TrackRedoFailedEmpty();
                return false;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool success = false;
            try
            {
                success = command.Execute();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UndoRedoManager] Ошибка при повторе команды '{command.Description}': {ex.Message}");
            }
            sw.Stop();

            if (success)
            {
                OnCommandRedone?.Invoke(command);
                RoadBuilder.Core.Telemetry.UndoRedoTelemetryManager.Instance.TrackRedoTriggered(
                    command.Description,
                    _historyStack.UndoCount,
                    _historyStack.RedoCount,
                    (float)sw.Elapsed.TotalMilliseconds
                );
            }

            NotifyHistoryChanged();
            return success;
        }

        public void ClearHistory()
        {
            if (_historyStack != null)
            {
                _historyStack.Clear();
                NotifyHistoryChanged();
            }
        }

        public string GetUndoActionName()
        {
            return _historyStack?.PeekUndo()?.Description ?? string.Empty;
        }

        public string GetRedoActionName()
        {
            return _historyStack?.PeekRedo()?.Description ?? string.Empty;
        }

        private void NotifyHistoryChanged()
        {
            OnHistoryChanged?.Invoke(CanUndo, CanRedo);
        }
    }
}
