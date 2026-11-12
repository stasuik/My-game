using System;

namespace RoadBuilder.Core.History
{
    /// <summary>
    /// Интерфейс обратимой команды (паттерн Command)
    /// </summary>
    public interface IReversibleCommand
    {
        /// <summary>
        /// Человекочитаемое описание действия (для всплывающих подсказок и логов)
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Выполнить команду (первично или при Redo)
        /// </summary>
        /// <returns>True если операция завершилась успешно</returns>
        bool Execute();

        /// <summary>
        /// Отменить команду (Undo)
        /// </summary>
        /// <returns>True если откат завершился успешно</returns>
        bool Undo();
    }
}
