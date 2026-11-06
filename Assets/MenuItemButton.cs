using UnityEngine;
using UnityEngine.UI;

public class MenuItemButton : MonoBehaviour
{
    public BuildManager buildManager;
    public VRMenuController menuController;
    public int itemIndex;

    public void OnClick()
    {
        if (!Application.isPlaying) return;
        buildManager.SetBuildMode(itemIndex);
        menuController.CloseMenu();
    }

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);
    }
}
