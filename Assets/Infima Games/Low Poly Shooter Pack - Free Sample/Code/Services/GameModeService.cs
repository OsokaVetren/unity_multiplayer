// Copyright 2021, Infima Games. All Rights Reserved.

using Mirror;

namespace InfimaGames.LowPolyShooterPack
{
    /// <summary>
    /// Game Mode Service.
    /// В мультиплеере возвращает ЛОКАЛЬНОГО игрока, а не первого попавшегося.
    /// </summary>
    public class GameModeService : IGameModeService
    {
        #region FIELDS

        /// <summary>
        /// The Player Character (cached local player).
        /// </summary>
        private CharacterBehaviour playerCharacter;

        #endregion

        #region FUNCTIONS

        public CharacterBehaviour GetPlayerCharacter()
        {
            // Если кэш ещё жив — возвращаем
            if (playerCharacter != null)
                return playerCharacter;

            // В мультиплеере ищем именно ЛОКАЛЬНОГО игрока
            if (NetworkClient.localPlayer != null)
            {
                playerCharacter = NetworkClient.localPlayer.GetComponentInChildren<CharacterBehaviour>();
                if (playerCharacter != null)
                    return playerCharacter;
            }

            // Fallback для сингл-плеера или если ещё нет localPlayer
            playerCharacter = UnityEngine.Object.FindAnyObjectByType<CharacterBehaviour>();
            return playerCharacter;
        }

        /// <summary>
        /// Сброс кэша. Вызывается при смене игрока / переподключении.
        /// </summary>
        public void ClearCache()
        {
            playerCharacter = null;
        }

        #endregion
    }
}
