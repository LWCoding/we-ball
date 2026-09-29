using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WeBall.Networking;
using WeBall.Saving;

namespace WeBall.Lobby
{
    /// <summary>
    /// Waits for enough players, then lets the host pick a save slot while everyone else waits.
    /// Picking a slot moves the whole session into the Game scene.
    /// </summary>
    public class LobbyController : NetworkBehaviour
    {
        [Tooltip("How many players (including the host) must be connected before the host can start.")]
        [SerializeField] int minPlayersToStart = 2;

        [SerializeField] Text joinCodeText;
        [SerializeField] Text statusText;
        [SerializeField] GameObject saveSlotPanel;
        [SerializeField] Button[] saveSlotButtons;
        [SerializeField] Button leaveButton;

        readonly NetworkVariable<int> playerCount = new();
        readonly NetworkVariable<FixedString32Bytes> joinCode = new();
        bool starting;

        void Awake()
        {
            saveSlotPanel.SetActive(false);
            leaveButton.onClick.AddListener(() => ConnectionManager.Instance.Leave());

            for (int i = 0; i < saveSlotButtons.Length; i++)
            {
                int slot = i;
                saveSlotButtons[i].onClick.AddListener(() => ChooseSaveSlot(slot));
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                joinCode.Value = ConnectionManager.Instance.JoinCode;
                for (int i = 0; i < saveSlotButtons.Length; i++)
                    saveSlotButtons[i].GetComponentInChildren<Text>().text = SaveSystem.Describe(i);
            }

            playerCount.OnValueChanged += OnLobbyChanged;
            joinCode.OnValueChanged += OnLobbyChanged;
            Refresh();
        }

        public override void OnNetworkDespawn()
        {
            playerCount.OnValueChanged -= OnLobbyChanged;
            joinCode.OnValueChanged -= OnLobbyChanged;
        }

        void Update()
        {
            // Only sends over the network when the count actually changes.
            // The NetworkManager can be gone for a frame while leaving the session.
            if (IsServer && NetworkManager != null && NetworkManager.IsListening)
                playerCount.Value = NetworkManager.ConnectedClientsIds.Count;
        }

        void ChooseSaveSlot(int slot)
        {
            if (!IsServer || starting) return;
            starting = true;

            GameSession.Begin(slot);
            saveSlotPanel.SetActive(false);
            statusText.text = "Starting...";
            NetworkManager.SceneManager.LoadScene(SceneNames.Game, LoadSceneMode.Single);
        }

        void OnLobbyChanged<T>(T previous, T current) => Refresh();

        void Refresh()
        {
            if (starting) return;

            int count = playerCount.Value;
            bool enoughPlayers = count >= minPlayersToStart;

            joinCodeText.text = $"Join code: {joinCode.Value}";
            saveSlotPanel.SetActive(IsServer && enoughPlayers);

            if (!enoughPlayers)
                statusText.text = $"Waiting for players... ({count}/{minPlayersToStart})";
            else if (IsServer)
                statusText.text = $"{count} players connected. Choose a save slot:";
            else
                statusText.text = $"{count} players connected. Waiting for the host to choose a save slot...";
        }
    }
}
