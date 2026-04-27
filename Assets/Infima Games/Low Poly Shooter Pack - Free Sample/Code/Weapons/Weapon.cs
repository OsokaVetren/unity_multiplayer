// Copyright 2021, Infima Games. All Rights Reserved.
// MODIFIED for Mirror multiplayer support.

using UnityEngine;
using Mirror;

namespace InfimaGames.LowPolyShooterPack
{
    /// <summary>
    /// Weapon. This class handles most of the things that weapons need.
    /// Modified: camera and character references are resolved from the owning player hierarchy,
    /// not from a global singleton (which finds the wrong player in multiplayer).
    /// </summary>
    public class Weapon : WeaponBehaviour
    {
        #region FIELDS SERIALIZED

        [Header("Firing")]

        [Tooltip("Is this weapon automatic? If yes, then holding down the firing button will continuously fire.")]
        [SerializeField]
        private bool automatic;

        [Tooltip("How fast the projectiles are.")]
        [SerializeField]
        private float projectileImpulse = 400.0f;

        [Tooltip("Amount of shots this weapon can shoot in a minute. It determines how fast the weapon shoots.")]
        [SerializeField]
        private int roundsPerMinutes = 200;

        [Tooltip("Mask of things recognized when firing.")]
        [SerializeField]
        private LayerMask mask;

        [Tooltip("Maximum distance at which this weapon can fire accurately. Shots beyond this distance will not use linetracing for accuracy.")]
        [SerializeField]
        private float maximumDistance = 500.0f;

        [Header("Animation")]

        [Tooltip("Transform that represents the weapon's ejection port, meaning the part of the weapon that casings shoot from.")]
        [SerializeField]
        private Transform socketEjection;

        [Header("Resources")]

        [Tooltip("Casing Prefab.")]
        [SerializeField]
        private GameObject prefabCasing;

        [Tooltip("Projectile Prefab. This is the prefab spawned when the weapon shoots.")]
        [SerializeField]
        private GameObject prefabProjectile;

        [Tooltip("The AnimatorController a player character needs to use while wielding this weapon.")]
        [SerializeField]
        public RuntimeAnimatorController controller;

        [Tooltip("Weapon Body Texture.")]
        [SerializeField]
        private Sprite spriteBody;

        [Header("Audio Clips Holster")]

        [Tooltip("Holster Audio Clip.")]
        [SerializeField]
        private AudioClip audioClipHolster;

        [Tooltip("Unholster Audio Clip.")]
        [SerializeField]
        private AudioClip audioClipUnholster;

        [Header("Audio Clips Reloads")]

        [Tooltip("Reload Audio Clip.")]
        [SerializeField]
        private AudioClip audioClipReload;

        [Tooltip("Reload Empty Audio Clip.")]
        [SerializeField]
        private AudioClip audioClipReloadEmpty;

        [Header("Audio Clips Other")]

        [Tooltip("AudioClip played when this weapon is fired without any ammunition.")]
        [SerializeField]
        private AudioClip audioClipFireEmpty;

        #endregion

        #region FIELDS

        private Animator animator;
        private WeaponAttachmentManagerBehaviour attachmentManager;
        private int ammunitionCurrent;

        #region Attachment Behaviours
        private MagazineBehaviour magazineBehaviour;
        private MuzzleBehaviour muzzleBehaviour;
        #endregion

        /// <summary>
        /// The CharacterBehaviour that owns this weapon (found via parent hierarchy).
        /// </summary>
        private CharacterBehaviour characterBehaviour;

        /// <summary>
        /// The camera of the owning player.
        /// </summary>
        private Transform playerCamera;

        /// <summary>
        /// Cached NetworkIdentity of the owner.
        /// </summary>
        private NetworkIdentity ownerNetIdentity;

        #endregion

        #region UNITY

        protected override void Awake()
        {
            animator = GetComponent<Animator>();
            attachmentManager = GetComponent<WeaponAttachmentManagerBehaviour>();

            // Resolve owner from hierarchy instead of global singleton
            ownerNetIdentity = GetComponentInParent<NetworkIdentity>();
            characterBehaviour = GetComponentInParent<CharacterBehaviour>();

            if (characterBehaviour != null)
            {
                Camera cam = characterBehaviour.GetCameraWorld();
                if (cam != null)
                    playerCamera = cam.transform;
            }
        }

        protected override void Start()
        {
            EnsureAttachmentsInitialized();
        }

        /// <summary>
        /// FIX: Ленивая инициализация attachments — Start() может не быть вызван
        /// если оружие было деактивировано при Init() и позже активировано через Equip().
        /// OnEnable гарантирует инициализацию при каждой активации.
        /// </summary>
        private void OnEnable()
        {
            EnsureAttachmentsInitialized();
        }

        private void EnsureAttachmentsInitialized()
        {
            if (attachmentManager == null)
                attachmentManager = GetComponent<WeaponAttachmentManagerBehaviour>();

            if (attachmentManager == null) return;

            if (magazineBehaviour == null)
                magazineBehaviour = attachmentManager.GetEquippedMagazine();

            if (muzzleBehaviour == null)
                muzzleBehaviour = attachmentManager.GetEquippedMuzzle();

            if (magazineBehaviour != null && ammunitionCurrent <= 0)
                ammunitionCurrent = magazineBehaviour.GetAmmunitionTotal();
        }

        #endregion

        #region GETTERS

        public override Animator GetAnimator() => animator;
        public override Sprite GetSpriteBody() => spriteBody;
        public override AudioClip GetAudioClipHolster() => audioClipHolster;
        public override AudioClip GetAudioClipUnholster() => audioClipUnholster;
        public override AudioClip GetAudioClipReload() => audioClipReload;
        public override AudioClip GetAudioClipReloadEmpty() => audioClipReloadEmpty;
        public override AudioClip GetAudioClipFireEmpty() => audioClipFireEmpty;
        public override AudioClip GetAudioClipFire() => muzzleBehaviour.GetAudioClipFire();
        public override int GetAmmunitionCurrent() => ammunitionCurrent;
        public override int GetAmmunitionTotal() => magazineBehaviour.GetAmmunitionTotal();
        public override bool IsAutomatic() => automatic;
        public override float GetRateOfFire() => roundsPerMinutes;
        public override bool IsFull() => ammunitionCurrent == magazineBehaviour.GetAmmunitionTotal();
        public override bool HasAmmunition() => ammunitionCurrent > 0;
        public override RuntimeAnimatorController GetAnimatorController() => controller;
        public override WeaponAttachmentManagerBehaviour GetAttachmentManager() => attachmentManager;

        #endregion

        #region METHODS

        public override void Reload()
        {
            // FIX: Гарантируем инициализацию
            EnsureAttachmentsInitialized();

            if (animator != null)
                animator.Play(HasAmmunition() ? "Reload" : "Reload Empty", 0, 0.0f);
        }

        public override void Fire(float spreadMultiplier = 1.0f)
        {
            // FIX: Гарантируем инициализацию при вызове Fire()
            EnsureAttachmentsInitialized();

            if (muzzleBehaviour == null)
            {
                Debug.LogWarning($"[Weapon] Fire() called but muzzleBehaviour is null on {gameObject.name}");
                return;
            }

            // Muzzle flash, weapon animation and ammo decrement play on ALL clients
            // so that every player sees the fire effect.
            Transform muzzleSocket = muzzleBehaviour.GetSocket();

            const string stateName = "Fire";
            if (animator != null)
                animator.Play(stateName, 0, 0.0f);

            if (magazineBehaviour != null)
                ammunitionCurrent = Mathf.Clamp(ammunitionCurrent - 1, 0, magazineBehaviour.GetAmmunitionTotal());

            muzzleBehaviour.Effect();

            // Projectile is cosmetic (not authoritative), spawned only by the local player
            if (ownerNetIdentity != null && !ownerNetIdentity.isLocalPlayer)
                return;

            if (playerCamera == null)
                return;

            Quaternion rotation = Quaternion.LookRotation(playerCamera.forward * 1000.0f - muzzleSocket.position);

            if (Physics.Raycast(new Ray(playerCamera.position, playerCamera.forward),
                out RaycastHit hit, maximumDistance, mask))
                rotation = Quaternion.LookRotation(hit.point - muzzleSocket.position);

            GameObject projectile = Instantiate(prefabProjectile, muzzleSocket.position, rotation);
            projectile.GetComponent<Rigidbody>().linearVelocity = projectile.transform.forward * projectileImpulse;
        }

        public override void FillAmmunition(int amount)
        {
            ammunitionCurrent = amount != 0 ? Mathf.Clamp(ammunitionCurrent + amount,
                0, GetAmmunitionTotal()) : magazineBehaviour.GetAmmunitionTotal();
        }

        public override void EjectCasing()
        {
            // Casings are purely visual — spawn on all clients
            if (prefabCasing != null && socketEjection != null)
                Instantiate(prefabCasing, socketEjection.position, socketEjection.rotation);
        }

        #endregion
    }
}
