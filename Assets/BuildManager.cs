using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildManager : MonoBehaviour
{
    [Header("Каталог всех объектов")]
    public List<BuildableItem> catalog = new List<BuildableItem>();

    private int currentIndex = 0;

    public BuildableItem CurrentItem
    {
        get
        {
            if (catalog != null && catalog.Count > 0 && currentIndex < catalog.Count)
                return catalog[currentIndex];
            return null;
        }
    }

    // Этот метод вызывается кнопками из VR-меню (передаем ID элемента из списка)
    public void SetBuildMode(int index)
    {
        if (index >= 0 && index < catalog.Count)
        {
            currentIndex = index;
            Debug.Log("Выбран режим строительства: " + catalog[currentIndex].itemName);
        }
    }
}
