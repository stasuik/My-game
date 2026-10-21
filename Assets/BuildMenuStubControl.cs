using UnityEngine;
using UnityEngine.UI;

public enum BuildMenuStubMode { Stepper, Segment, Check }

/// <summary>
/// Заглушка панели: степпер, сегмент или чекбокс. Меняет вид, но на постройку не влияет.
/// Состояние лежит в компоненте, поэтому работает и в панели, собранной в сцене.
/// </summary>
public class BuildMenuStubControl : MonoBehaviour
{
    [Header("Данные")]
    public BuildMenuStubMode mode = BuildMenuStubMode.Stepper;
    public string label = "";
    public int value = 1;
    public int minValue = 1;
    public int maxValue = 999;
    public string suffix = " м";
    public bool on;

    [Header("Связи (заполняются при сборке панели)")]
    public Text valueLabel;
    public BuildMenuChoice[] options;
    public Image ring;
    public Image inner;
    public Image dot;

    public void Plus() { SetValue(value + 1); }

    public void Minus() { SetValue(value - 1); }

    public void SetValue(int v)
    {
        value = Mathf.Clamp(v, minValue, maxValue);
        if (valueLabel != null) valueLabel.text = value + suffix;
    }

    public void Select(int index)
    {
        value = Mathf.Clamp(index, minValue, maxValue);
        if (options == null) return;
        for (int i = 0; i < options.Length; i++)
            if (options[i] != null) options[i].ApplyStyle(i == index);
    }

    public void Toggle()
    {
        on = !on;
        Refresh();
    }

    public void Refresh()
    {
        if (ring != null) ring.color = on ? BuildMenuPanel.Orange : BuildMenuPanel.TileSel;
        if (inner != null) inner.color = on ? BuildMenuPanel.TileCol : BuildMenuPanel.Cream;
        if (dot != null) dot.enabled = on;
    }
}
