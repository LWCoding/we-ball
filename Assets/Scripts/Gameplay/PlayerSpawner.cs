using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeBall.Gameplay
{
    /// <summary>
    /// Server-only: spawns a ball for every player once they've all loaded the Game scene,
    /// plus a ball for anyone who joins mid-game.
    /// </summary>
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] NetworkObject playerPrefab;
        [SerializeField] Transform[] spawnPoints;

        int spawnedCount;

        static NetworkManager Network => NetworkManager.Singleton;

        // Awake, not Start: the load-completed event can fire before Start runs (e.g. when the host is alone).
        void Awake()
        {
            if (Network == null || !Network.IsServer) return;

            Network.SceneManager.OnLoadEventCompleted += HandleLoadCompleted;
            Network.OnClientConnectedCallback += SpawnPlayer;
        }

        void OnDestroy()
        {
            if (Network == null) return;

            if (Network.SceneManager != null) Network.SceneManager.OnLoadEventCompleted -= HandleLoadCompleted;
            Network.OnClientConnectedCallback -= SpawnPlayer;
        }

        void HandleLoadCompleted(string sceneName, LoadSceneMode mode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            if (sceneName != gameObject.scene.name) return;

            foreach (ulong clientId in clientsCompleted)
                SpawnPlayer(clientId);
        }

        void SpawnPlayer(ulong clientId)
        {
            if (Network.SpawnManager.GetPlayerNetworkObject(clientId) != null) return;

            Transform point = spawnPoints[spawnedCount++ % spawnPoints.Length];
            Network.SpawnManager.InstantiateAndSpawn(playerPrefab, clientId,
                destroyWithScene: true, isPlayerObject: true, position: point.position);
        }
    }
}
