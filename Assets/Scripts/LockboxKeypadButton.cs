using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class LockboxKeypadButton : MonoBehaviour
{
    [Tooltip("A digit, or one of: CLEAR, ENTER, CLOSE.")]
    [SerializeField] private string action = "0";

    [Tooltip("The keypad this button belongs to. Leave empty to use the keypad in this button's parents, " +
             "or else whichever keypad is open.")]
    [SerializeField] private LockboxKeypadController keypad;

    private void Awake()
    {
        if (keypad == null)
            keypad = GetComponentInParent<LockboxKeypadController>();

        GetComponent<Button>().onClick.AddListener(Activate);
    }

    private void Activate()
    {
        // There can be more than one keypad in a scene (lockbox, exit door), so never search the
        // whole scene: use this button's own keypad, or the one that's open.
        LockboxKeypadController target = keypad != null ? keypad : LockboxKeypadController.Active;
        if (target == null)
            return;

        switch (action)
        {
            case "CLEAR":
                target.ClearCode();
                break;
            case "ENTER":
                target.SubmitCode();
                break;
            case "CLOSE":
                target.CloseKeypad();
                break;
            default:
                target.PressDigit(action);
                break;
        }
    }
}
