using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class LockboxKeypadButton : MonoBehaviour
{
    [Tooltip("A digit, or one of: CLEAR, ENTER, CLOSE.")]
    [SerializeField] private string action = "0";

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Activate);
    }

    private void Activate()
    {
        LockboxKeypadController keypad = FindAnyObjectByType<LockboxKeypadController>();
        if (keypad == null)
            return;

        switch (action)
        {
            case "CLEAR":
                keypad.ClearCode();
                break;
            case "ENTER":
                keypad.SubmitCode();
                break;
            case "CLOSE":
                keypad.CloseKeypad();
                break;
            default:
                keypad.PressDigit(action);
                break;
        }
    }
}
