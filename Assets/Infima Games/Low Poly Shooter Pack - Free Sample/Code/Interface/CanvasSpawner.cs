// Copyright 2021, Infima Games. All Rights Reserved.
// MODIFIED for Mirror multiplayer support.

using UnityEngine;
using Mirror;

namespace InfimaGames.LowPolyShooterPack.Interface
{
    /// <summary>
    /// Player Interface.
    /// Modified: only spawns the canvas for the LOCAL player.
    /// </summary>
    public class CanvasSpawner : MonoBehaviour
    {
        #region FIELDS SERIALIZED

        [Header("Settings")]
        
        [Tooltip("Canvas prefab spawned at start. Displays the player's user interface.")]
        [SerializeField]
        private GameObject canvasPrefab;

        #endregion

        #region UNITY FUNCTIONS

        /// <summary>
        /// Start instead of Awake so NetworkIdentity has time to initialize.
        /// </summary>
        private void Start()
        {
            // Only spawn UI canvas for the local player
            NetworkIdentity netId = GetComponentInParent<NetworkIdentity>();
            if (netId != null && !netId.isLocalPlayer)
                return;

            // Spawn Interface.
            if (canvasPrefab != null)
                Instantiate(canvasPrefab);
        }

        #endregion
    }
}
