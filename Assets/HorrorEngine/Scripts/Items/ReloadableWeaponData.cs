using System;
using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(menuName = "Horror Engine/Items/Reloadable Weapon")]
    public class ReloadableWeaponData : WeaponData
    {
        public ItemData AmmoItem;
        public int MaxAmmo;
        public AudioClip ShotSound;
        public AudioClip ReloadSound;
        public float ReloadDuration;

        public virtual float GetMaxAmmo()
        {
            return MaxAmmo;
        }

        public virtual float GetReloadDuration()
        {
            return ReloadDuration;
        }
    }
}