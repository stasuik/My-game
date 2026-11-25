using System;
using UnityEngine;

namespace RoadBuilder.Core.History
{
    /// <summary>
    /// Базовая реализация команды модификации/создания участка дороги
    /// </summary>
    public class RoadSegmentCommand : IReversibleCommand
    {
        private readonly RoadActionSnapshot _beforeState;
        private readonly RoadActionSnapshot _afterState;
        private readonly Action<RoadActionSnapshot> _applyStateAction;

        public string Description { get; }
        public RoadActionSnapshot BeforeState => _beforeState;
        public RoadActionSnapshot AfterState => _afterState;

        public RoadSegmentCommand(
            string description,
            RoadActionSnapshot beforeState,
            RoadActionSnapshot afterState,
            Action<RoadActionSnapshot> applyStateAction)
        {
            Description = description ?? "Действие с участком дороги";
            _beforeState = beforeState;
            _afterState = afterState;
            _applyStateAction = applyStateAction ?? throw new ArgumentNullException(nameof(applyStateAction));
        }

        public bool Execute()
        {
            _applyStateAction(_afterState);
            return true;
        }

        public bool Undo()
        {
            _applyStateAction(_beforeState);
            return true;
        }
    }
}
