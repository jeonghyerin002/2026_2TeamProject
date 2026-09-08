using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 키보드 입력을 감지하고 InventoryCarouselUI에 동작을 요청
/// </summary>
public class InventoryInput : MonoBehaviour
{
    [SerializeField] private InventoryCarouselUI carouselUI;


    private void Update()
    {
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
