using UnityEngine;
using UnityEngine.UI;

public enum BuildMenuChoiceKind { Tile, Segment }

/// <summary>
/// Один вариант в панели меню: плитка («Дорога», «Фонарь») или пункт сегмента.
/// Это обычный компонент, а не класс в коде: вариант лежит в сцене, его видно в инспекторе,
/// кнопку можно привязать к своему скрипту как угодно.
/// </summary>
public class BuildMenuChoice : MonoBehaviour
{
    [Header("Данные")]
    public string label;
    public string[] keywords;
    public string crumbGroup = "";
    public BuildMenuChoiceKind kind = BuildMenuChoiceKind.Tile;

    [Header("Связи (заполняются при сборке панели)")]
    public Image border;
    public Image fill;
    public Text text;
    public BuildMenuStubControl owner;
    public int index;

    [Header("Цвета")]
    public Color fillNormal = Color.white;
    public Color fillSelected = Color.white;
    public Color textNormal = Color.white;
    public Color textSelected = Color.white;

    public void ApplyStyle(bool on)
    {
        if (border != null) border.color = on ? BuildMenuPanel.Orange : BuildMenuPanel.Clear;
        if (fill != null) fill.color = on ? fillSelected : fillNormal;
        if (text != null)
        {
            text.color = on ? textSelected : textNormal;
            text.fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
        }
    }
}
