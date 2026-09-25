using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyFlow
{
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    public sealed class SoundManager : Singleton<SoundManager>
    {
        [SerializeField] private SoundData soundData;
        [SerializeField] private PlayerDataManager playerDataManager;

        private readonly Dictionary<SfxId, float> lastPlayedAt =
            new Dictionary<SfxId, float>();
        private readonly HashSet<MusicId> warnedMissingMusic = new HashSet<MusicId>();
        private readonly HashSet<SfxId> warnedMissingSfx = new HashSet<SfxId>();

        private AudioSource musicSource;
        private AudioSource sfxSource;
        private Coroutine musicTransition;
        private MusicId? currentMusic;

        public bool IsMusicEnabled => playerDataManager == null ||
                                      playerDataManager.Data.musicEnabled;
        public bool IsSfxEnabled => playerDataManager == null ||
                                    playerDataManager.Data.sfxEnabled;

        protected override void Awake()
        {
            base.Awake();
            if (!enabled)
                return;

            if (playerDataManager == null)
                playerDataManager = PlayerDataManager.Instance;

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;
        }

        public void PlayMusic(MusicId id, float fadeDuration = -1f)
        {
            if (!TryGetMusic(id, out MusicEntry entry))
                return;

            if (currentMusic == id && musicSource.clip == entry.clip)
            {
                if (IsMusicEnabled && !musicSource.isPlaying)
                    musicSource.Play();
                return;
            }

            StopMusicTransition();
            float duration = ResolveFadeDuration(fadeDuration);
            if (duration <= 0f || musicSource.clip == null)
                ApplyMusic(id, entry);
            else
                musicTransition = StartCoroutine(ChangeMusicRoutine(id, entry, duration));
        }

        public void StopMusic(float fadeDuration = -1f)
        {
            StopMusicTransition();
            float duration = ResolveFadeDuration(fadeDuration);
            if (duration <= 0f || musicSource.clip == null)
            {
                ClearMusic();
                return;
            }
            musicTransition = StartCoroutine(StopMusicRoutine(duration));
        }

        public void PlaySfx(SfxId id)
        {
            if (!IsSfxEnabled || !TryGetSfx(id, out SfxEntry entry))
                return;

            float now = Time.unscaledTime;
            if (entry.cooldown > 0f && lastPlayedAt.TryGetValue(id, out float lastTime) &&
                now - lastTime < entry.cooldown)
                return;

            lastPlayedAt[id] = now;
            sfxSource.PlayOneShot(entry.clip, soundData.SfxVolume * entry.volume);
        }

        public void SetMusicEnabled(bool enabledValue)
        {
            playerDataManager?.SetMusicEnabled(enabledValue);
            if (!enabledValue)
            {
                musicSource.Pause();
                return;
            }

            if (musicSource.clip != null && !musicSource.isPlaying)
                musicSource.Play();
        }

        public void SetSfxEnabled(bool enabledValue)
        {
            playerDataManager?.SetSfxEnabled(enabledValue);
            if (!enabledValue)
                sfxSource.Stop();
        }

        private bool TryGetMusic(MusicId id, out MusicEntry entry)
        {
            entry = null;
            if (soundData != null && soundData.TryGetMusic(id, out entry) &&
                entry != null && entry.clip != null)
                return true;

            WarnMissingMusic(id);
            return false;
        }

        private bool TryGetSfx(SfxId id, out SfxEntry entry)
        {
            entry = null;
            if (soundData != null && soundData.TryGetSfx(id, out entry) &&
                entry != null && entry.clip != null)
                return true;

            WarnMissingSfx(id);
            return false;
        }

        private void ApplyMusic(MusicId id, MusicEntry entry)
        {
            currentMusic = id;
            musicSource.clip = entry.clip;
            musicSource.loop = entry.loop;
            musicSource.volume = soundData.MusicVolume * entry.volume;
            if (IsMusicEnabled)
                musicSource.Play();
        }

        private IEnumerator ChangeMusicRoutine(MusicId id, MusicEntry entry, float duration)
        {
            float halfDuration = duration * 0.5f;
            yield return FadeVolume(musicSource.volume, 0f, halfDuration);
            ApplyMusic(id, entry);
            float targetVolume = soundData.MusicVolume * entry.volume;
            musicSource.volume = 0f;
            yield return FadeVolume(0f, targetVolume, halfDuration);
            musicTransition = null;
        }

        private IEnumerator StopMusicRoutine(float duration)
        {
            yield return FadeVolume(musicSource.volume, 0f, duration);
            ClearMusic();
            musicTransition = null;
        }

        private IEnumerator FadeVolume(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                musicSource.volume = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            musicSource.volume = to;
        }

        private float ResolveFadeDuration(float requested)
        {
            return requested >= 0f
                ? requested
                : soundData != null ? soundData.DefaultCrossfadeDuration : 0f;
        }

        private void StopMusicTransition()
        {
            if (musicTransition == null)
                return;
            StopCoroutine(musicTransition);
            musicTransition = null;
        }

        private void ClearMusic()
        {
            musicSource.Stop();
            musicSource.clip = null;
            currentMusic = null;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void WarnMissingMusic(MusicId id)
        {
            if (warnedMissingMusic.Add(id))
                Debug.LogWarning($"No AudioClip is assigned for MusicId.{id}.", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void WarnMissingSfx(SfxId id)
        {
            if (warnedMissingSfx.Add(id))
                Debug.LogWarning($"No AudioClip is assigned for SfxId.{id}.", this);
        }
    }
}
