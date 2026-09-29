using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeBall.Networking
{
    /// <summary>
    /// Owns the network session: signs in to Unity Services, creates or joins a Relay allocation
    /// (which is what produces the 6-character join code), and starts Netcode as host or client.
    /// Lives on the NetworkManager GameObject, so it persists across scene loads.
    /// </summary>
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public class ConnectionManager : MonoBehaviour
    {
        // Secure WebSockets on every platform: browsers (WebGL/itch.io) can't use UDP, and a web player
        // can only talk to a desktop player if both use the same protocol.
        const string RelayConnectionType = "wss";

        [Tooltip("Maximum players per session, including the host.")]
        [SerializeField] int maxPlayers = 4;

        public static ConnectionManager Instance { get; private set; }

        /// <summary>Why the last session ended, shown by the connect menu. Empty if the player left on purpose.</summary>
        public static string LastDisconnectMessage { get; private set; } = "";

        /// <summary>Raised when a connection attempt fails while still on the connect screen.</summary>
        public event Action<string> ConnectionFailed;

        public int MaxPlayers => maxPlayers;
        public string JoinCode { get; private set; } = "";

        NetworkManager networkManager;
        bool leavingOnPurpose;
        bool quitting;

        void Awake()
        {
            Instance = this;
            networkManager = GetComponent<NetworkManager>();
            GetComponent<UnityTransport>().UseWebSockets = true;
            // Don't make everyone wait long for a player whose game is paused (e.g. a background browser tab);
            // PlayerSpawner still gives them a ball whenever they finish loading.
            networkManager.NetworkConfig.LoadSceneTimeOut = 15;
            networkManager.OnClientStopped += HandleClientStopped;
            Application.quitting += () => quitting = true;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (networkManager != null) networkManager.OnClientStopped -= HandleClientStopped;
        }

        public async Task StartHostAsync()
        {
            await EnsureSignedInAsync();

            // The host takes one slot, so Relay only needs room for the other players.
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            GetComponent<UnityTransport>().SetRelayServerData(allocation.ToRelayServerData(RelayConnectionType));
            if (!networkManager.StartHost())
                throw new Exception("Netcode failed to start the host.");

            // Clients that connect later are synced to whatever scene the host is in.
            networkManager.SceneManager.LoadScene(SceneNames.Lobby, LoadSceneMode.Single);
        }

        public async Task StartClientAsync(string joinCode)
        {
            await EnsureSignedInAsync();

            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            JoinCode = joinCode;

            GetComponent<UnityTransport>().SetRelayServerData(allocation.ToRelayServerData(RelayConnectionType));
            if (!networkManager.StartClient())
                throw new Exception("Netcode failed to start the client.");
        }

        /// <summary>Leaves the session. If this is the host, every client is disconnected too.</summary>
        public void Leave()
        {
            leavingOnPurpose = true;
            networkManager.Shutdown();
        }

        static async Task EnsureSignedInAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                var options = new InitializationOptions();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // Give each running copy its own anonymous player so several copies can play together
                // on one machine (e.g. Multiplayer Play Mode virtual players share the same saved login otherwise).
                options.SetProfile("dev_" + Guid.NewGuid().ToString("N").Substring(0, 8));
#endif
                await UnityServices.InitializeAsync(options);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        void HandleClientStopped(bool wasHost)
        {
            if (quitting) return;

            string reason = networkManager.DisconnectReason;
            if (string.IsNullOrEmpty(reason))
                reason = leavingOnPurpose || wasHost ? "" : "Lost connection to the host.";
            leavingOnPurpose = false;

            if (SceneManager.GetActiveScene().name == SceneNames.Connect)
            {
                // Never made it out of the connect screen, so just report the failure.
                ConnectionFailed?.Invoke(string.IsNullOrEmpty(reason) ? "Could not connect to the host." : reason);
                return;
            }

            // The Connect scene has its own NetworkManager, so throw this one away and start fresh.
            LastDisconnectMessage = reason;
            Destroy(gameObject);
            SceneManager.LoadScene(SceneNames.Connect);
        }
    }
}
