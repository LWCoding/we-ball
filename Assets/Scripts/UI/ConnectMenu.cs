using System;
using UnityEngine;
using UnityEngine.UI;
using WeBall.Networking;

namespace WeBall.UI
{
    /// <summary>
    /// The first screen: host a new game or join one with a 6-character code.
    /// </summary>
    public class ConnectMenu : MonoBehaviour
    {
        const int JoinCodeLength = 6;

        [SerializeField] Button hostButton;
        [SerializeField] Button joinButton;
        [SerializeField] InputField joinCodeInput;
        [SerializeField] Text statusText;

        void Start()
        {
            hostButton.onClick.AddListener(Host);
            joinButton.onClick.AddListener(Join);

            joinCodeInput.characterLimit = JoinCodeLength;
            joinCodeInput.onValidateInput += (_, _, c) => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '\0';

            ConnectionManager.Instance.ConnectionFailed += ShowFailure;
            SetBusy(false, ConnectionManager.LastDisconnectMessage);

#if DEVELOPMENT_BUILD && !UNITY_EDITOR
            // Testing shortcut: launch a development build with "-join CODE" to join automatically (once).
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-join");
            if (!usedJoinArgument && index >= 0 && index + 1 < args.Length)
            {
                usedJoinArgument = true;
                joinCodeInput.text = args[index + 1];
                Join();
            }
#endif
        }

        static bool usedJoinArgument;

        void OnDestroy()
        {
            if (ConnectionManager.Instance != null)
                ConnectionManager.Instance.ConnectionFailed -= ShowFailure;
        }

        async void Host()
        {
            SetBusy(true, "Creating game...");
            try
            {
                await ConnectionManager.Instance.StartHostAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ShowFailure($"Could not host a game: {e.Message}");
            }
        }

        async void Join()
        {
            string code = joinCodeInput.text.Trim().ToUpperInvariant();
            if (code.Length != JoinCodeLength)
            {
                statusText.text = $"Enter the {JoinCodeLength}-character code from the host.";
                return;
            }

            SetBusy(true, "Joining...");
            try
            {
                await ConnectionManager.Instance.StartClientAsync(code);
                if (this != null) statusText.text = "Connecting...";
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ShowFailure($"Could not join game {code}: {e.Message}");
            }
        }

        void ShowFailure(string message)
        {
            // The scene may have changed while we were awaiting.
            if (this != null) SetBusy(false, message);
        }

        void SetBusy(bool busy, string status)
        {
            hostButton.interactable = !busy;
            joinButton.interactable = !busy;
            joinCodeInput.interactable = !busy;
            statusText.text = status;
        }
    }
}
