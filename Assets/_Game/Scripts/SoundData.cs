using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    public enum MusicId : byte
    {
        MainMenu,
        Gameplay
    }

    public enum SfxId : byte
    {
        UiClick,
        ColonySelect,
        ColonyDisappear,
        AntSpawn,
        AntEat,
        AntJump,
        PixelCollect,
        BoosterAddTray,
        BoosterShuffle,
        BoosterPick,
        BoosterClearColor,
        Victory,
        Failed
    }

    [Serializable]
    public sealed class MusicEntry
    {
        public MusicId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        public bool loop = true;
    }

    [Serializable]
    public sealed class SfxEntry
    {
        public SfxId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Min(0f)] public float cooldown;
    }

    [CreateAssetMenu(fileName = "SoundData", menuName = "Colony Flow/Sound Data")]
    public sealed class SoundData : ScriptableObject
    {
        [Header("Global Settings")]
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.75f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.75f;
        [SerializeField, Min(0f)] private float defaultCrossfadeDuration = 0.35f;

        [Header("Clips")]
        [SerializeField] private List<MusicEntry> music = new List<MusicEntry>();
        [SerializeField] private List<SfxEntry> sfx = new List<SfxEntry>();

        public float MusicVolume => musicVolume;
        public float SfxVolume => sfxVolume;
        public float DefaultCrossfadeDuration => defaultCrossfadeDuration;

        public bool TryGetMusic(MusicId id, out MusicEntry entry)
        {
            for (int i = 0; i < music.Count; i++)
            {
                entry = music[i];
                if (entry != null && entry.id == id)
                    return true;
            }
            entry = null;
            return false;
        }

        public bool TryGetSfx(SfxId id, out SfxEntry entry)
        {
            for (int i = 0; i < sfx.Count; i++)
            {
                entry = sfx[i];
                if (entry != null && entry.id == id)
                    return true;
            }
            entry = null;
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateUniqueIds(music, entry => entry.id, "Music");
            ValidateUniqueIds(sfx, entry => entry.id, "SFX");
        }

        private void ValidateUniqueIds<TEntry, TId>(List<TEntry> entries,
            Func<TEntry, TId> getId, string label) where TEntry : class
        {
            var ids = new HashSet<TId>();
            for (int i = 0; i < entries.Count; i++)
            {
                TEntry entry = entries[i];
                if (entry == null)
                    continue;
                TId id = getId(entry);
                if (!ids.Add(id))
                    Debug.LogWarning($"{name} has duplicate {label} id {id}.", this);
            }
        }
#endif
    }
}
