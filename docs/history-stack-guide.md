# HistoryStack · подключение Undo/Redo к инструменту строительства

Гайд для разработчиков Road Builder VR. История записывает **одно подтверждённое действие игрока** за вызов `RegisterCommand`. Для размещения используйте `SpawnObjectCommand` и общий `UndoRedoManager` сцены.

## Quick Start

Добавьте `using RoadBuilder.Core.History;` в начало файла. После успешного подтверждения спавна выполните две строки:

```csharp
var command = new SpawnObjectCommand(spawnedGameObject, "Фонарь");
UndoRedoManager.Instance.RegisterCommand(command);
```

К этому моменту `spawnedGameObject` уже создан, активен и настроен в сцене. `RegisterCommand` записывает выполненное действие. Метод `Execute()` команды вызывается менеджером при Redo. Для серии объектов, созданных одним жестом, передайте общий корневой объект.

## Пример 1 · Одиночный объект

Вызовите `PlaceLamp` с финальным префабом и позой после проверки места установки. Превью регистрировать не нужно.

```csharp
using RoadBuilder.Core.History;
using UnityEngine;

public static class LampPlacement
{
    public static GameObject PlaceLamp(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        GameObject lamp = Object.Instantiate(prefab, position, rotation);
        UndoRedoManager.Instance.RegisterCommand(new SpawnObjectCommand(lamp, "Фонарь"));
        return lamp;
    }
}
```

`Undo()` деактивирует фонарь, а `Redo()` активирует тот же экземпляр с прежними настройками. Пока действие можно повторить, экземпляр остаётся в сцене в выключенном состоянии. [Точка регистрации в VR-контроллере](../Assets/VRConstructionController.cs)

## Пример 2 · Дорожная цепочка одним действием

Создайте отдельный активный `chainRoot` для этого жеста. Под ним должны находиться все новые сегменты и опоры. Существующая дорога остаётся вне контейнера. Перед вызовом инициализируйте оба `SnapPoint` каждого сегмента и передайте сегменты в порядке прокладки. `startTarget` и `endTarget` — необязательные торцы существующей дороги.

В примере инструмент проверил место и геометрию до вызова. Помощник соединяет точки, откатывает всю попытку при конфликте и регистрирует только успешное размещение.

```csharp
using System.Collections.Generic;
using RoadBuilder.Core.History;
using UnityEngine;

public static class RoadChainPlacement
{
    public static bool TryCommit(GameObject chainRoot, IReadOnlyList<RoadSegment> segments,
        SnapPoint startTarget = null, SnapPoint endTarget = null)
    {
        if (segments.Count == 0)
            return Cancel(chainRoot);

        SnapPoint previous = startTarget;
        foreach (RoadSegment segment in segments)
        {
            if (previous != null && !segment.StartPoint.Connect(previous))
                return Cancel(chainRoot);

            previous = segment.EndPoint;
        }

        if (endTarget != null && !previous.Connect(endTarget))
            return Cancel(chainRoot);

        UndoRedoManager.Instance.RegisterCommand(new SpawnObjectCommand(chainRoot, "Укладка дороги"));
        return true;
    }

    private static bool Cancel(GameObject chainRoot)
    {
        chainRoot.SetActive(false);
        Object.Destroy(chainRoot);
        return false;
    }
}
```

Создавайте команду **после стыковки**. В ветке `dev` конструктор `SpawnObjectCommand` запоминает внутренние стыки цепочки и её связи с существующей дорогой. Undo отключает весь контейнер и освобождает торцы. Redo включает контейнер и восстанавливает пары. Если одну из пар восстановить не удалось, контейнер снова отключается, ранее восстановленные в этой попытке связи снимаются, а `Redo()` возвращает `false`.

Если инструмент использует штатный `VRRoadBuilder`, регистрация уже находится внутри `BuildFinalRoad` и финальной ветки `PlaceRoadArc`. Пример выше предназначен для инструмента, который сам создаёт и стыкует сегменты. [Штатный построитель](../Assets/VRRoadBuilder.cs) · [Команда спавна](../Assets/Scripts/Core/History/SpawnObjectCommand.cs)

## Вызовы Undo/Redo из интерфейса

Кнопки используют `CanUndo` и `CanRedo`, а при нажатии проверяют результат `Undo()` или `Redo()`. Пустой стек и неудачное выполнение дают `false`. При ошибке команда остаётся в исходном стеке.

```csharp
using RoadBuilder.Core.History;
using UnityEngine;
using UnityEngine.UI;

public sealed class HistoryButtons : MonoBehaviour
{
    [SerializeField] private Button undoButton;
    [SerializeField] private Button redoButton;

    private UndoRedoManager history;

    private void OnEnable()
    {
        history = UndoRedoManager.Instance;
        history.OnHistoryChanged += UpdateButtons;
        UpdateButtons(history.CanUndo, history.CanRedo);
    }

    private void OnDisable()
    {
        if (history != null)
            history.OnHistoryChanged -= UpdateButtons;
    }

    public void UndoLastAction() => history.Undo();
    public void RedoLastAction() => history.Redo();

    private void UpdateButtons(bool canUndo, bool canRedo)
    {
        undoButton.interactable = canUndo;
        redoButton.interactable = canRedo;
    }
}
```

Привяжите `UndoLastAction` и `RedoLastAction` к `On Click` соответствующих кнопок. Если интерфейс показывает результат операции, выводите сообщение об успехе только при `true`. При полной замене редактируемой сцены используйте `UndoRedoManager.Instance.ClearHistory()`.

## Типовые ошибки интеграции

| Ошибка | Как исправить |
|---|---|
| Превью или неудачный спавн попал в Undo | Регистрируйте действие только после успешной фиксации |
| Дорога отменяется по одному сегменту | Сложите новые объекты одного жеста под общий корень и зарегистрируйте одну команду |
| Одно действие требует двух Undo | Оставьте одну регистрацию. Проверьте, регистрирует ли команду штатный построитель |
| Redo вернул геометрию без стыков | Сначала сформируйте связи, затем создайте `SpawnObjectCommand` |
| Undo уничтожает объект | Оставьте жизненный цикл объекта команде. Undo деактивирует экземпляр, а Redo возвращает тот же объект |
| После Undo и новой постройки Redo недоступен | Менеджер начинает новую ветку истории и удаляет отменённые объекты из отброшенной ветки |
| Для нового инструмента создан свой `HistoryStack` | Используйте `UndoRedoManager.Instance`, чтобы команды попадали в общую историю VR-интерфейса |
| Изменение существующей дороги зарегистрировано как спавн | Для перемещения, редактирования или смены формы применяйте обратимую команду со снимками до и после, например `RoadSegmentCommand` |

## Проверка при подключении инструмента

1. В Play Mode поставьте одиночный объект. Проверьте, что один Undo скрывает его, а один Redo возвращает.
2. Постройте цепочку из двух или более сегментов с примыканием к существующей дороге. Один Undo должен скрыть новые сегменты и освободить стыки. Один Redo должен вернуть цепочку и стыки. Существующая дорога остаётся в сцене.
3. После Undo выполните другое подтверждённое размещение. Кнопка Redo должна отключиться. После отменённого жеста или неудачной установки размер истории не должен меняться.

`HistoryStack` хранит команды Undo/Redo с настраиваемым лимитом Undo, по умолчанию 50. `UndoRedoManager` выполняет команды и уведомляет интерфейс через `OnHistoryChanged(bool canUndo, bool canRedo)`. [Стек истории](../Assets/Scripts/Core/History/HistoryStack.cs) · [Менеджер](../Assets/Scripts/Core/History/UndoRedoManager.cs)
