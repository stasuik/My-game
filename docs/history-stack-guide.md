# Инструкция и документация по модулю HistoryStack и Undo/Redo

Модуль `RoadBuilder.Core.History` предоставляет систему управления историей действий пользователя для VR-редактора дорог «Road Builder VR».

## 1. Состав модуля

1. `IReversibleCommand` — контракт обратимой команды (`Execute()`, `Undo()`, `Description`).
2. `IHistoryManager` — интерфейс для подписчиков (UI-панели на контроллере, тултипы, счетчики).
3. `HistoryStack` — двунаправленный кольцевой буфер с жестким ограничением максимальной емкости (`_maxCapacity = 50`) для предотвращения утечек памяти и просадок GC.
4. `UndoRedoManager` — синглтон-сервис сцены Unity (`MonoBehaviour`), обеспечивающий выполнение, откат, повтор команд и рассылку событий `OnHistoryChanged`.
5. `RoadSegmentCommand` — типовая реализация команды манипуляции дорожными сегментами на базе снимков `RoadActionSnapshot`.

## 2. Пример использования

```csharp
// Регистрация нового действия:
var command = new RoadSegmentCommand(
    "Укладка 2-полосной дороги",
    beforeSnapshot,
    afterSnapshot,
    snapshot => SceneController.ApplySnapshot(snapshot)
);

UndoRedoManager.Instance.RegisterCommand(command);

// Отмена:
if (UndoRedoManager.Instance.CanUndo)
{
    UndoRedoManager.Instance.Undo();
}

// Повтор:
if (UndoRedoManager.Instance.CanRedo)
{
    UndoRedoManager.Instance.Redo();
}
```
