using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class VRMenuController : MonoBehaviour
{
    [Header("Ссылки на UI")]
    public GameObject menuCanvas;        // Ссылка на Canvas меню

    [Header("Ссылки на VR компоненты")]
    public Transform playerHead;         // Ссылка на Main Camera (голову игрока)

    [Header("Настройка кнопки новой системы ввода")]
    [Tooltip("Перетащите сюда экшен: XRI RightHand Interaction/SecondaryButton или Menu")]
    public InputActionProperty toggleMenuAction;

    [Header("Настройки позиции меню")]
    public float spawnDistance = 1.5f;
    public float spawnHeightOffset = -0.2f;

    private void OnEnable()
    {
        // Активируем чтение кнопки
        toggleMenuAction.action.Enable();
        toggleMenuAction.action.started += OnTriggerToggle;

        if (menuCanvas != null) menuCanvas.SetActive(false);
    }

    private void OnDisable()
    {
        toggleMenuAction.action.Disable();
        toggleMenuAction.action.started -= OnTriggerToggle;
    }

    private void OnTriggerToggle(InputAction.CallbackContext context)
    {
        Debug.Log("Физическая кнопка B была успешно нажата!");
        ToggleMenu();
    }

    public void ToggleMenu()
    {
        if (menuCanvas == null) return;

        bool isCurrentlyActive = menuCanvas.activeSelf;

        if (!isCurrentlyActive)
        {
            PositionMenuInFront();
            menuCanvas.SetActive(true);
        }
        else
        {
            menuCanvas.SetActive(false);
        }
    }

    public void CloseMenu()
    {
        if (menuCanvas != null) menuCanvas.SetActive(false);
    }

    private void PositionMenuInFront()
    {
        if (playerHead == null) return;

        Vector3 headForward = playerHead.forward;
        headForward.y = 0;
        headForward.Normalize();

        Vector3 targetPosition = playerHead.position + (headForward * spawnDistance);
        targetPosition.y += spawnHeightOffset;

        menuCanvas.transform.position = targetPosition;

        menuCanvas.transform.LookAt(new Vector3(playerHead.position.x, targetPosition.y, playerHead.position.z));
        menuCanvas.transform.Rotate(0, 180, 0);
    }
}

