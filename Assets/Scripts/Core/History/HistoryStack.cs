using System;
using System.Collections.Generic;

namespace RoadBuilder.Core.History
{
    /// <summary>
    /// Кольцевой буфер / стек истории действий с ограничением емкости и управлением памятью
    /// </summary>
    public class HistoryStack
    {
        private readonly LinkedList<IReversibleCommand> _undoStack = new LinkedList<IReversibleCommand>();
        private readonly LinkedList<IReversibleCommand> _redoStack = new LinkedList<IReversibleCommand>();
        private readonly int _maxCapacity;

        public const int DefaultCapacity = 50;

        public int UndoCount => _undoStack.Count;
        public int RedoCount => _redoStack.Count;
        public int MaxCapacity => _maxCapacity;
        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public HistoryStack(int maxCapacity = DefaultCapacity)
        {
            if (maxCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCapacity), "Вместимость стека истории должна быть больше нуля.");
            }
            _maxCapacity = maxCapacity;
        }

        /// <summary>
        /// Добавить выполненную команду в стек Undo. При превышении лимита удаляет старейшие команды.
        /// При добавлении нового действия стек Redo очищается.
        /// </summary>
        public void Push(IReversibleCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            _redoStack.Clear();

            if (_undoStack.Count >= _maxCapacity)
            {
                _undoStack.RemoveFirst();
            }

            _undoStack.AddLast(command);
        }

        /// <summary>
        /// Извлечь последнюю команду для отмены (Undo) и поместить её в стек Redo
        /// </summary>
        public IReversibleCommand PopUndo()
        {
            if (!CanUndo)
            {
                return null;
            }

            var command = _undoStack.Last.Value;
            _undoStack.RemoveLast();
            _redoStack.AddLast(command);
            return command;
        }

        /// <summary>
        /// Извлечь последнюю отмененную команду для повтора (Redo) и вернуть её в стек Undo
        /// </summary>
        public IReversibleCommand PopRedo()
        {
            if (!CanRedo)
            {
                return null;
            }

            var command = _redoStack.Last.Value;
            _redoStack.RemoveLast();
            _undoStack.AddLast(command);
            return command;
        }

        /// <summary>
        /// Посмотреть следующую команду для отмены без извлечения
        /// </summary>
        public IReversibleCommand PeekUndo()
        {
            return _undoStack.Count > 0 ? _undoStack.Last.Value : null;
        }

        /// <summary>
        /// Посмотреть следующую команду для повтора без извлечения
        /// </summary>
        public IReversibleCommand PeekRedo()
        {
            return _redoStack.Count > 0 ? _redoStack.Last.Value : null;
        }

        /// <summary>
        /// Полностью очистить стек отмены и повтора
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }
}
