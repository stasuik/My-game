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
                Destroy(this);
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
            DiscardUndoneObjects();
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

            DiscardUndoneObjects();
            _historyStack.Push(command);
            OnCommandExecuted?.Invoke(command);
            NotifyHistoryChanged();
        }

        public void RecordAction(IReversibleCommand command)
        {
            RegisterCommand(command);
        }

        public bool Undo()
        {
            if (!CanUndo)
            {
                return false;
            }

            var command = _historyStack.PeekUndo();
            if (command == null)
            {
                return false;
            }

            bool success = false;
            try
            {
                success = command.Undo();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UndoRedoManager] Ошибка при отмене команды '{command.Description}': {ex.Message}");
            }

            if (success)
            {
                _historyStack.PopUndo();
                OnCommandUndone?.Invoke(command);
            }

            NotifyHistoryChanged();
            return success;
        }

        public bool Redo()
        {
            if (!CanRedo)
            {
                return false;
            }

            var command = _historyStack.PeekRedo();
            if (command == null)
            {
                return false;
            }

            bool success = false;
            try
            {
                success = command.Execute();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UndoRedoManager] Ошибка при повторе команды '{command.Description}': {ex.Message}");
            }

            if (success)
            {
                _historyStack.PopRedo();
                OnCommandRedone?.Invoke(command);
            }

            NotifyHistoryChanged();
            return success;
        }

        public void ClearHistory()
        {
            if (_historyStack != null)
            {
                DiscardUndoneObjects();
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

        private void DiscardUndoneObjects()
        {
            if (_historyStack == null) return;

            foreach (var command in _historyStack.RedoCommands)
            {
                if (command is SpawnObjectCommand spawn && spawn.TargetObject != null &&
                    !spawn.TargetObject.activeSelf)
                {
                    Destroy(spawn.TargetObject);
                }
            }
        }

        private void NotifyHistoryChanged()
        {
            OnHistoryChanged?.Invoke(CanUndo, CanRedo);
        }
    }
}

namespace Core.History
{
    public class UndoRedoManager : RoadBuilder.Core.History.UndoRedoManager {}
    public interface IReversibleCommand : RoadBuilder.Core.History.IReversibleCommand {}
}
