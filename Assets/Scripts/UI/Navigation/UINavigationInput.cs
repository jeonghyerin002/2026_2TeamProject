using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(UINavigationManager))]
public class UINavigationInput : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float selectionRepeatInterval = 0.15f;
    private UINavigationManager navigation;
    private UIGroupController selectionController;
    private int heldDirection;
    private float nextSelectionTime;
    private readonly UIInteractionData previous = new UIInteractionData(UIActionType.SelectPrevious);
    private readonly UIInteractionData next = new UIInteractionData(UIActionType.SelectNext);
    private readonly UIInteractionData confirm = new UIInteractionData(UIActionType.Confirm);
    private readonly UIInteractionData back = new UIInteractionData(UIActionType.Back);

    private void Awake()
    {
        navigation = GetComponent<UINavigationManager>();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || navigation == null || !navigation.isActiveAndEnabled || navigation.IsTransitioning)
        {
            ResetSelectionRepeat();
            return;
        }

        if (keyboard.qKey.wasPressedThisFrame)
        {
            ResetSelectionRepeat();
            navigation.ToggleInitialGroup();
            return;
        }

        UIGroupController controller = navigation.CurrentController;
        if (controller == null || !controller.CanReceiveInput)
        {
            ResetSelectionRepeat();
            return;
        }

        if (keyboard.eKey.wasPressedThisFrame)
        {
            ResetSelectionRepeat();
            controller.ExecuteInteraction(back);
            return;
        }

        if (keyboard.fKey.wasPressedThisFrame)
        {
            ResetSelectionRepeat();
            controller.ExecuteInteraction(confirm);
            return;
        }

        if (ProcessSelectionInput(keyboard, controller))
        {
            return;
        }

        UIGroupData group = controller.Data;
        for (int i = 0; i < group.InputBindingCount; i++)
        {
            UIInputBinding binding = group.GetInputBinding(i);
            if (binding == null || binding.Interaction == null || binding.Key == Key.None ||
                binding.Key == Key.Q || binding.Key == Key.A || binding.Key == Key.D ||
                binding.Key == Key.F || binding.Key == Key.E || !Enum.IsDefined(typeof(Key), binding.Key))
            {
                continue;
            }

            if (binding.RepeatWhileHeld ? keyboard[binding.Key].isPressed : keyboard[binding.Key].wasPressedThisFrame)
            {
                controller.ExecuteInteraction(binding.Interaction);
                return;
            }
        }
    }

    private bool ProcessSelectionInput(Keyboard keyboard, UIGroupController controller)
    {
        bool repeat = controller.Data.RepeatSelectionWhileHeld;
        int direction = (repeat ? keyboard.aKey.isPressed : keyboard.aKey.wasPressedThisFrame) ? -1 :
            (repeat ? keyboard.dKey.isPressed : keyboard.dKey.wasPressedThisFrame) ? 1 : 0;
        if (direction == 0)
        {
            ResetSelectionRepeat();
            return false;
        }

        bool pressed = direction < 0 ? keyboard.aKey.wasPressedThisFrame : keyboard.dKey.wasPressedThisFrame;
        if (!repeat || pressed || selectionController != controller || heldDirection != direction ||
            Time.unscaledTime >= nextSelectionTime)
        {
            selectionController = controller;
            heldDirection = direction;
            if (controller.ExecuteInteraction(direction < 0 ? previous : next))
            {
                nextSelectionTime = Time.unscaledTime + Mathf.Max(0.01f, selectionRepeatInterval);
            }
        }

        return true;
    }

    private void OnDisable()
    {
        ResetSelectionRepeat();
    }

    private void ResetSelectionRepeat()
    {
        selectionController = null;
        heldDirection = 0;
        nextSelectionTime = 0f;
    }
}
