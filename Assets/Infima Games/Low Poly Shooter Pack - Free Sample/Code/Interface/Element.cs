// Copyright 2021, Infima Games. All Rights Reserved.
// MODIFIED for Mirror multiplayer support.

using UnityEngine;

namespace InfimaGames.LowPolyShooterPack.Interface
{
    /// <summary>
    /// Interface Element.
    /// Modified: uses deferred resolution of player character — 
    /// GameModeService now correctly returns the local player in multiplayer.
    /// Also adds null-safety for cases where the local player hasn't spawned yet.
    /// </summary>
    public abstract class Element : MonoBehaviour
    {
        #region FIELDS
        
        /// <summary>
        /// Game Mode Service.
        /// </summary>
        protected IGameModeService gameModeService;
        
        /// <summary>
        /// Player Character.
        /// </summary>
        protected CharacterBehaviour playerCharacter;
        /// <summary>
        /// Player Character Inventory.
        /// </summary>
        protected InventoryBehaviour playerCharacterInventory;

        /// <summary>
        /// Equipped Weapon.
        /// </summary>
        protected WeaponBehaviour equippedWeapon;
        
        #endregion

        #region UNITY

        /// <summary>
        /// Awake.
        /// </summary>
        protected virtual void Awake()
        {
            //Get Game Mode Service. Very useful to get Game Mode references.
            gameModeService = ServiceLocator.Current.Get<IGameModeService>();
        }
        
        /// <summary>
        /// Update.
        /// </summary>
        private void Update()
        {
            // Deferred resolution: try to get the local player character if we don't have one yet
            if (playerCharacter == null && gameModeService != null)
            {
                playerCharacter = gameModeService.GetPlayerCharacter();
                if (playerCharacter != null)
                    playerCharacterInventory = playerCharacter.GetInventory();
            }

            //Ignore if we don't have an Inventory.
            if (Equals(playerCharacterInventory, null))
                return;

            //Get Equipped Weapon.
            equippedWeapon = playerCharacterInventory.GetEquipped();
            
            //Tick.
            Tick();
        }

        #endregion

        #region METHODS

        /// <summary>
        /// Tick.
        /// </summary>
        protected virtual void Tick() {}

        #endregion
    }
}
