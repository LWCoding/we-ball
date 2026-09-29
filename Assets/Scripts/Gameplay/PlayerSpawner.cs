using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeBall.Gameplay
{
    /// <summary>
    /// Server-only: spawns a ball for every player once they've all loaded the Game scene (or the load timed out),
    /// then for anyone who finishes loading late or joins mid-game.
    /// </summary>
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] NetworkObject playerPrefab;
        [SerializeField] Transform[] spawnPoints;

        int spawnedCount;
        bool initialLoadDone;

        static NetworkManager Network => NetworkManager.Singleton;

        // Awake, not Start: the load-completed event can fire before Start runs (e.g. when the host is alone).
        void Awake()
        {
            if (Network == null || !Network.IsServer) return;

            Network.SceneManager.OnLoadEventCompleted += HandleLoadEventCompleted;
            Network.SceneManager.OnLoadComplete += HandleClientLoadComplete;
            Network.OnClientConnectedCallback += SpawnPlayer;
        }

        void OnDestroy()
        {
            if (Network == null) return;

            if (Network.SceneManager != null)
            {
                Network.SceneManager.OnLoadEventCompleted -= HandleLoadEventCompleted;
                Network.SceneManager.OnLoadComplete -= HandleClientLoadComplete;
            }
            Network.OnClientConnectedCallback -= SpawnPlayer;
        }

        void HandleLoadEventCompleted(string sceneName, LoadSceneMode mode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            if (sceneName != gameObject.scene.name) return;

            Debug.Log($"[PlayerSpawner] {sceneName} loaded by [{string.Join(", ", clientsCompleted)}], timed out: [{string.Join(", ", clientsTimedOut)}]");
            initialLoadDone = true;
            foreach (ulong clientId in clientsCompleted)
                SpawnPlayer(clientId);
        }

        // A client that timed out (e.g. a browser tab in the background pauses the game) still reports in
        // when it finally finishes loading, so give it a ball then.
        void HandleClientLoadComplete(ulong clientId, string sceneName, LoadSceneMode mode)
        {
            if (initialLoadDone && sceneName == gameObject.scene.name)
                SpawnPlayer(clientId);
        }

        void SpawnPlayer(ulong clientId)
        {
            if (Network.SpawnManager.GetPlayerNetworkObject(clientId) != null) return;

            Transform point = spawnPoints[spawnedCount++ % spawnPoints.Length];
            Network.SpawnManager.InstantiateAndSpawn(playerPrefab, clientId,
                destroyWithScene: true, isPlayerObject: true, position: point.position);
            Debug.Log($"[PlayerSpawner] Spawned ball for client {clientId}");
        }
    }
}
