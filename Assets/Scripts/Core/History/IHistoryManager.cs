using System;

namespace RoadBuilder.Core.History
{
    /// <summary>
    /// Контракт сервиса управления историей Undo/Redo
    /// </summary>
    public interface IHistoryManager
    {
        /// <summary>
        /// Доступна ли операция отмены
        /// </summary>
        bool CanUndo { get; }

        /// <summary>
        /// Доступна ли операция повтора
        /// </summary>
        bool CanRedo { get; }

        /// <summary>
        /// Текущее количество команд в стеке Undo
        /// </summary>
        int UndoCount { get; }

        /// <summary>
        /// Текущее количество команд в стеке Redo
        /// </summary>
        int RedoCount { get; }

        /// <summary>
        /// Событие изменения состояния истории (canUndo, canRedo)
        /// </summary>
        event Action<bool, bool> OnHistoryChanged;

        /// <summary>
        /// Зарегистрировать новое выполненное действие в историю
        /// </summary>
        void RegisterCommand(IReversibleCommand command);

        /// <summary>
        /// Отменить последнее действие
        /// </summary>
        bool Undo();

        /// <summary>
        /// Повторить последнее отмененное действие
        /// </summary>
        bool Redo();

        /// <summary>
        /// Очистить всю историю
        /// </summary>
        void ClearHistory();

        /// <summary>
        /// Получить название следующего отменяемого действия
        /// </summary>
        string GetUndoActionName();

        /// <summary>
        /// Получить название следующего повторяемого действия
        /// </summary>
        string GetRedoActionName();
    }
}
