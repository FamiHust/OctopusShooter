using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource longSFXSource;

    [Header("Library")]
    [SerializeField] private AudioLibraryString library;

    private const string KEY_BGM = Const.player_bgm_volume_key;
    private const string KEY_SFX = Const.player_sfx_volume_key;
    private readonly Dictionary<string, float> sfxNextPlayTimeByKey = new Dictionary<string, float>();
    private Coroutine longSfxFadeCoroutine;
    private Coroutine longSfxLoopCoroutine;
    private string currentLongSfxKey;
    private float currentLongSfxVolumeMultiplier = 1f;
    private bool isLongSfxLoopActive;
    private bool isLongSfxPaused;
    private float longSfxLoopStartAtSeconds;
    private float longSfxLoopEndAtSeconds = -1f;

    public float BgmVolume { get; private set; } = 1f;
    public float SfxVolume { get; private set; } = 1f;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        ConfigurePauseIndependentAudio();

        LoadSettings();
        ApplyVolumes();

        // Phát nhạc nền BGM xuyên suốt, loop vô hạn
        PlayBGM(Const.BGM, true);
    }

    private void OnDestroy()
    {
        StopLongSfxLoopCoroutine();
        CancelLongSfxFadeCoroutine();
    }

    private void ConfigurePauseIndependentAudio()
    {
        if (bgmSource != null)
        {
            bgmSource.ignoreListenerPause = true;
        }

        if (sfxSource != null)
        {
            sfxSource.ignoreListenerPause = true;
        }

        if (longSFXSource != null)
        {
            longSFXSource.ignoreListenerPause = true;
        }

        AudioListener.pause = false;
    }

    // ========= BGM =========
    public void PlayBGM(string key, bool loop = true)
    {
        if (bgmSource == null || key == null) return;
        var entry = library.Get(key);
        if (entry == null || entry.clip == null) return;
        
        bgmSource.clip = entry.clip;
        bgmSource.loop = loop;
        
        // Apply pitch from library
        bgmSource.pitch = entry.randomPitch
            ? Random.Range(entry.pitchMin, entry.pitchMax)
            : 1f;
        
        // Apply volume from library combined with global BGM volume
        bgmSource.volume = entry.volume * BgmVolume;
        
        bgmSource.Play();
    }
    public void StopBGM()
    {
        if (bgmSource == null) return;
        bgmSource.Stop();
    }
    public void StopLongSFX()
    {
        if (longSFXSource == null) return;

        StopLongSfxLoopCoroutine();
        CancelLongSfxFadeCoroutine();

        longSFXSource.Stop();
        ResetLongSfxRuntimeState();
    }
    public void StopInGameSound()
    {
        StopBGM();
        StopLongSFX();
    }
    public void PlayLongSFX(string key,float volume=1f)
    {
        if (longSFXSource == null || library == null) { return; }
        if (string.IsNullOrEmpty(key)) return;

        StopLongSfxLoopCoroutine();
        CancelLongSfxFadeCoroutine();

        var entry = library.Get(key);
        if(entry == null || entry.clip==null) return;

        isLongSfxLoopActive = false;
        isLongSfxPaused = false;
        longSfxLoopStartAtSeconds = 0f;
        longSfxLoopEndAtSeconds = -1f;
        currentLongSfxKey = key;
        currentLongSfxVolumeMultiplier = Mathf.Max(0f, volume);
        longSFXSource.clip = entry.clip;
        longSFXSource.loop = false;
        RefreshLongSfxVolumeIfPlaying();
        longSFXSource.timeSamples = 0;
        longSFXSource.Play();
    }

    // Mobile-friendly looping: restart from head before silent tail instead of runtime reverse processing.
    public void PlayLongSFXLoopFromStart(string key, float volume = 1f, float restartAtSeconds = -1f)
    {
        PlayLongSFXLoopSegment(key, volume, 0f, restartAtSeconds);
    }

    // Loop region mode: play clip from the beginning once, then jump from loopEnd to loopStart.
    public void PlayLongSFXLoopSegment(string key, float volume = 1f, float loopStartSeconds = 0f, float loopEndSeconds = -1f)
    {
        if (longSFXSource == null || library == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        var entry = library.Get(key);
        if (entry == null || entry.clip == null)
        {
            return;
        }

        StopLongSfxLoopCoroutine();
        CancelLongSfxFadeCoroutine();

        currentLongSfxKey = key;
        currentLongSfxVolumeMultiplier = Mathf.Max(0f, volume);
        isLongSfxPaused = false;

        longSFXSource.clip = entry.clip;
        longSFXSource.loop = false;
        RefreshLongSfxVolumeIfPlaying();
        longSFXSource.timeSamples = 0;
        longSFXSource.Play();

        float clipLength = Mathf.Max(0.02f, entry.clip.length);
        float maxLoopStart = Mathf.Max(0f, clipLength - 0.02f);
        longSfxLoopStartAtSeconds = Mathf.Clamp(loopStartSeconds, 0f, maxLoopStart);

        float maxLoopEnd = Mathf.Max(longSfxLoopStartAtSeconds + 0.02f, clipLength - 0.01f);
        longSfxLoopEndAtSeconds = loopEndSeconds > 0f
            ? Mathf.Clamp(loopEndSeconds, longSfxLoopStartAtSeconds + 0.02f, maxLoopEnd)
            : -1f;

        isLongSfxLoopActive = longSfxLoopEndAtSeconds > longSfxLoopStartAtSeconds + 0.01f;

        if (isLongSfxLoopActive)
        {
            longSfxLoopCoroutine = StartCoroutine(LongSfxLoopRoutine());
        }
    }

    public void PlayLongSFXBidirectionalLoop(string key, float volume = 1f)
    {
        // Backward-compatible alias. Runtime reverse generation is intentionally removed for mobile perf.
        PlayLongSFXLoopFromStart(key, volume, -1f);
    }

    public bool IsLongSFXPlaying(string key = null)
    {
        if (longSFXSource == null || !longSFXSource.isPlaying)
        {
            return false;
        }

        if (string.IsNullOrEmpty(key))
        {
            return true;
        }

        return string.Equals(currentLongSfxKey, key);
    }

    public void PauseLongSFX()
    {
        if (longSFXSource == null)
        {
            return;
        }

        isLongSfxPaused = true;

        if (longSFXSource.isPlaying)
        {
            longSFXSource.Pause();
        }
    }

    public void ResumeLongSFX()
    {
        if (longSFXSource == null)
        {
            return;
        }

        isLongSfxPaused = false;

        if (longSFXSource.clip != null)
        {
            longSFXSource.UnPause();
        }
    }

    public void StopLongSFXSmooth(float fadeDuration = 0.32f)
    {
        if (longSFXSource == null)
        {
            return;
        }

        if (!longSFXSource.isPlaying)
        {
            ResetLongSfxRuntimeState();
            return;
        }

        float safeDuration = Mathf.Max(0f, fadeDuration);
        if (safeDuration <= 0.001f)
        {
            StopLongSFX();
            return;
        }

        StopLongSfxLoopCoroutine();
        CancelLongSfxFadeCoroutine();
        longSfxFadeCoroutine = StartCoroutine(FadeOutLongSfxRoutine(safeDuration));
    }
    // ✅ Overload with custom pitch
    public void PlayBGM(string key, float pitch, bool loop = true)
    {
        if (bgmSource == null || key == null) return;
        var entry = library.Get(key);
        if (entry == null || entry.clip == null) return;
        
        bgmSource.clip = entry.clip;
        bgmSource.loop = loop;
        bgmSource.pitch = pitch;
        bgmSource.volume = entry.volume * BgmVolume;
        bgmSource.Play();
    }

    // ========= SFX =========
    public void PlaySFX(string key)
    {
        if (sfxSource == null || library == null) return;
        if (string.IsNullOrEmpty(key)) return;

        var entry = library.Get(key);
        if (entry == null || entry.clip == null) return;

        sfxSource.pitch = entry.randomPitch
            ? Random.Range(entry.pitchMin, entry.pitchMax)
            : 1f;

        sfxSource.PlayOneShot(entry.clip, entry.volume * SfxVolume);
    }
    

    // ✅ Overload with custom pitch
    public void PlaySFX(string key, float pitch)
    {
        if (sfxSource == null || library == null) return;
        if (string.IsNullOrEmpty(key)) return;

        var entry = library.Get(key);
        if (entry == null || entry.clip == null) return;

        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(entry.clip, entry.volume * SfxVolume);
    }
    
    // ✅ Overload with custom pitch and volume multiplier
    public void PlaySFX(string key, float pitch, float volumeMultiplier)
    {
        if (sfxSource == null || library == null) return;
        if (string.IsNullOrEmpty(key)) return;

        var entry = library.Get(key);
        if (entry == null || entry.clip == null) return;

        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(entry.clip, entry.volume * SfxVolume * volumeMultiplier);
    }

    // Global cooldown theo key để tránh đè âm khi nhiều shooter bắn cùng lúc.
    public bool TryPlaySFXWithCooldown(string key, float cooldown, float pitch = 1f, float volumeMultiplier = 1f)
    {
        if (string.IsNullOrEmpty(key)) return false;

        float safeCooldown = Mathf.Max(0f, cooldown);
        if (safeCooldown > 0f && sfxNextPlayTimeByKey.TryGetValue(key, out float nextPlayableTime))
        {
            if (Time.time < nextPlayableTime)
            {
                return false;
            }
        }

        PlaySFX(key, pitch, volumeMultiplier);
        sfxNextPlayTimeByKey[key] = Time.time + safeCooldown;
        return true;
    }

    // ========= Volume =========
    public void SetMusicVolume(bool isOn)
    {
        BgmVolume = isOn ? 1f : 0f;
        PlayerPrefs.SetFloat(KEY_BGM, BgmVolume);
        PlayerPrefs.Save();
        ApplyVolumes();
    }

    public void SetSfxVolume(bool isOn)
    {
        SfxVolume = isOn ? 1f : 0f;
        PlayerPrefs.SetFloat(KEY_SFX, SfxVolume);
        PlayerPrefs.Save();

        RefreshLongSfxVolumeIfPlaying();
    }

    public void ApplyVolumes()
    {
        if (bgmSource != null && bgmSource.clip != null)
        {
            // Get current BGM entry to reapply its volume setting
            string currentBgmKey = FindKeyByClip(bgmSource.clip);
            if (!string.IsNullOrEmpty(currentBgmKey))
            {
                var entry = library.Get(currentBgmKey);
                if (entry != null)
                {
                    bgmSource.volume = entry.volume * BgmVolume;
                    return;
                }
            }
            // Fallback if key not found
            bgmSource.volume = BgmVolume;
        }
    }
    
    private string FindKeyByClip(AudioClip clip)
    {
        if (library == null || library.entries == null) return null;
        var entry = library.entries.Find(e => e.clip == clip);
        return entry?.key;
    }

    private void LoadSettings()
    {
        BgmVolume = PlayerPrefs.GetFloat(KEY_BGM, 1f);
        SfxVolume = PlayerPrefs.GetFloat(KEY_SFX, 1f);
    }

    private void CancelLongSfxFadeCoroutine()
    {
        if (longSfxFadeCoroutine == null)
        {
            return;
        }

        StopCoroutine(longSfxFadeCoroutine);
        longSfxFadeCoroutine = null;
    }

    private IEnumerator FadeOutLongSfxRoutine(float duration)
    {
        float elapsed = 0f;
        float fromVolume = longSFXSource != null ? Mathf.Max(0f, longSFXSource.volume) : 0f;

        while (longSFXSource != null && longSFXSource.isPlaying && elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            longSFXSource.volume = Mathf.Lerp(fromVolume, 0f, t);
            yield return null;
        }

        if (longSFXSource != null)
        {
            longSFXSource.Stop();
        }

        ResetLongSfxRuntimeState();
        longSfxFadeCoroutine = null;
    }

    private IEnumerator LongSfxLoopRoutine()
    {
        int stagnantFrameCount = 0;
        int lastTimeSamples = -1;

        while (isLongSfxLoopActive)
        {
            if (longSFXSource == null)
            {
                ResetLongSfxRuntimeState();
                yield break;
            }

            if (longSfxFadeCoroutine != null)
            {
                yield return null;
                continue;
            }

            if (isLongSfxPaused)
            {
                stagnantFrameCount = 0;
                lastTimeSamples = -1;
                yield return null;
                continue;
            }

            if (longSFXSource.clip == null)
            {
                RestartLongSfxAtLoopStart();
                stagnantFrameCount = 0;
                lastTimeSamples = -1;
                yield return null;
                continue;
            }

            int currentSamples = longSFXSource.timeSamples;
            if (currentSamples == lastTimeSamples)
            {
                stagnantFrameCount++;
            }
            else
            {
                stagnantFrameCount = 0;
                lastTimeSamples = currentSamples;
            }

            bool isSegmentFinished = !longSFXSource.isPlaying;
            bool isPlaybackStalled = stagnantFrameCount >= 8;
            bool shouldRestartByLoopTime = longSfxLoopEndAtSeconds > longSfxLoopStartAtSeconds && longSFXSource.time >= longSfxLoopEndAtSeconds;
            if (isSegmentFinished || isPlaybackStalled || shouldRestartByLoopTime)
            {
                RestartLongSfxAtLoopStart();
                stagnantFrameCount = 0;
                lastTimeSamples = -1;
            }

            yield return null;
        }
    }

    private void RestartLongSfxAtLoopStart()
    {
        if (longSFXSource == null || library == null || string.IsNullOrEmpty(currentLongSfxKey))
        {
            ResetLongSfxRuntimeState();
            return;
        }

        var entry = library.Get(currentLongSfxKey);
        if (entry == null || entry.clip == null)
        {
            ResetLongSfxRuntimeState();
            return;
        }

        longSFXSource.clip = entry.clip;
        longSFXSource.loop = false;
        RefreshLongSfxVolumeIfPlaying();

        int loopSample = Mathf.Clamp(
            Mathf.RoundToInt(longSfxLoopStartAtSeconds * entry.clip.frequency),
            0,
            Mathf.Max(0, entry.clip.samples - 1)
        );

        longSFXSource.timeSamples = loopSample;

        if (!longSFXSource.isPlaying)
        {
            longSFXSource.Play();
        }
    }

    private void ResetLongSfxRuntimeState()
    {
        currentLongSfxKey = null;
        currentLongSfxVolumeMultiplier = 1f;
        isLongSfxLoopActive = false;
        isLongSfxPaused = false;
        longSfxLoopStartAtSeconds = 0f;
        longSfxLoopEndAtSeconds = -1f;
        longSfxLoopCoroutine = null;
    }

    private void StopLongSfxLoopCoroutine()
    {
        if (longSfxLoopCoroutine == null)
        {
            return;
        }

        StopCoroutine(longSfxLoopCoroutine);
        longSfxLoopCoroutine = null;
    }

    private void RefreshLongSfxVolumeIfPlaying()
    {
        if (longSFXSource == null || longSFXSource.clip == null || string.IsNullOrEmpty(currentLongSfxKey))
        {
            return;
        }

        var entry = library != null ? library.Get(currentLongSfxKey) : null;
        if (entry == null)
        {
            return;
        }

        longSFXSource.volume = entry.volume * SfxVolume * Mathf.Max(0f, currentLongSfxVolumeMultiplier);
    }
}
