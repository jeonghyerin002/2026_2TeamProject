using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 기존 단독 인벤토리 입력. Navigation 연결 시 UINavigationInput에서 공통 입력을 처리한다.
/// </summary>
public class InventoryInput : MonoBehaviour
{
    [Tooltip("연결하면 기존 입력을 중지한다. Manager에 UINavigationInput을 추가한다.")]
    [SerializeField] private UINavigationManager navigationManager;
    [SerializeField] private InventoryCarouselUI carouselUI;


    private void Update()
    {
        if (navigationManager != null)
        {
            return;
        }

        if (carouselUI == null)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (keyboard.qKey.wasPressedThisFrame)
        {
            carouselUI.ToggleOpen();
        }

        if (!carouselUI.IsOpen)
        {
            return;
        }

        if (keyboard.aKey.isPressed)
        {
            carouselUI.MoveLeft();
        }
        else if (keyboard.dKey.isPressed)
        {
            carouselUI.MoveRight();
        }
    }

}
