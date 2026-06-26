using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildManager : MonoBehaviour
{
    // Перечисление всех типов объектов, которые мы можем строить
    public enum BuildMode { Road, BusStop, Border }

    [Header("Текущий режим строительства")]
    public BuildMode currentMode = BuildMode.Road;

    [Header("Префабы объектов для установки")]
    public GameObject busStopPrefab; // Сюда позже закинем 3D-модель остановки
    public GameObject borderPrefab;  // Сюда позже закинем 3D-модель бордюра

    // Метод, который будут вызывать кнопки из VR-меню
    public void SetBuildMode(int modeIndex)
    {
        currentMode = (BuildMode)modeIndex;
        Debug.Log("Режим строительства изменен на: " + currentMode);
    }
}
