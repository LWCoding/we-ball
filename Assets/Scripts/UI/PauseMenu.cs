using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WeBall.Networking;

namespace WeBall.UI
{
    /// <summary>
    /// In-game menu toggled with Escape. The game keeps running for everyone else (it's online),
    /// so this only blocks the local player's input. Quitting returns to the Connect screen;
    /// if the host quits, the session ends for everyone.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] Button resumeButton;
        [SerializeField] Button quitButton;
        [SerializeField] Text joinCodeText;

        public static bool IsOpen { get; private set; }

        InputAction toggleAction;

        void Awake()
        {
            resumeButton.onClick.AddListener(() => SetOpen(false));
            quitButton.onClick.AddListener(() => ConnectionManager.Instance.Leave());

            toggleAction = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            toggleAction.AddBinding("<Gamepad>/start");
            toggleAction.performed += _ => SetOpen(!IsOpen);

            SetOpen(false);
        }

        void OnEnable() => toggleAction.Enable();
        void OnDisable() => toggleAction.Disable();

        void OnDestroy()
        {
            toggleAction.Dispose();
            IsOpen = false;
        }

        void SetOpen(bool open)
        {
            IsOpen = open;
            panel.SetActive(open);

            // Shown so players can invite someone mid-game.
            if (open && ConnectionManager.Instance != null)
                joinCodeText.text = $"Join code: {ConnectionManager.Instance.JoinCode}";
        }
    }
}
