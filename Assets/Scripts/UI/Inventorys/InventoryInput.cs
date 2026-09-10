using UnityEngine;
using UnityEngine.InputSystem;
using System;

/// <summary>
/// 키보드 입력을 현재 UI Group에 전달한다. Manager 미연결 시 기존 인벤토리 입력을 유지한다.
/// </summary>
public class InventoryInput : MonoBehaviour
{
    [Tooltip("연결하면 Group 입력만 처리한다. 항상 활성인 입력용 GameObject에 하나만 배치한다.")]
    [SerializeField] private UINavigationManager navigationManager;
    [SerializeField] private Key toggleKey = Key.Q;
    [SerializeField] private InventoryCarouselUI carouselUI;


    private void Update()
    {
        if (navigationManager != null)
        {
            ProcessGroupInput();
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

    private void ProcessGroupInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !navigationManager.isActiveAndEnabled)
        {
            return;
        }

        if (toggleKey != Key.None && Enum.IsDefined(typeof(Key), toggleKey) && keyboard[toggleKey].wasPressedThisFrame)
        {
            navigationManager.ToggleInitialGroup();
            return;
        }

        UIGroupController controller = navigationManager.CurrentController;
        if (controller == null || !controller.CanReceiveInput)
        {
            return;
        }

        UIGroupData group = controller.Data;
        for (int i = 0; i < group.InputBindingCount; i++)
        {
            UIInputBinding binding = group.GetInputBinding(i);
            if (binding == null || binding.Interaction == null || binding.Key == Key.None ||
                !Enum.IsDefined(typeof(Key), binding.Key))
            {
                continue;
            }

            if (binding.RepeatWhileHeld ? keyboard[binding.Key].isPressed : keyboard[binding.Key].wasPressedThisFrame)
            {
                controller.ExecuteInteraction(binding.Interaction);
                // 한 프레임에 하나만 처리해 이동 입력이 다음 Group으로 전파되지 않게 한다.
                return;
            }
        }
    }
}
