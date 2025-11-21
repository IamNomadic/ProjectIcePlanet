// AudioManager.cs
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Music")]
    [SerializeField] public AudioClip MenuMusic;
    [SerializeField] private AudioClip GameplayMusic;

    [Header("UI Audio Clips")]
    [SerializeField] private AudioClip mapOpenSound;
    [SerializeField] private AudioClip mapCloseSound;
    [SerializeField] private AudioClip invOpenSound;
    [SerializeField] private AudioClip invSelectedSound;
    [SerializeField] private AudioClip invCraftedSound;
    [SerializeField] private AudioClip invFailCraftSound;
    [SerializeField] private AudioClip invCloseSound;

    [Header("Audio Source")]
    [SerializeField] private AudioSource sfxAudioSource;
    [SerializeField] private AudioSource musicAudioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Do not auto-play here. SettingsController will call PlayMusic after loading settings.
    }

    // Play music with linear volume (0..1)
    public void PlayMusic(AudioClip clip, float linearVolume)
    {
        if (musicAudioSource == null || clip == null) return;

        musicAudioSource.clip = clip;
        musicAudioSource.volume = Mathf.Clamp01(linearVolume);
        musicAudioSource.loop = true;
        if (!musicAudioSource.isPlaying)
            musicAudioSource.Play();
    }

    public void StopMusic()
    {
        if (musicAudioSource == null) return;
        musicAudioSource.Stop();
    }

    public void PlayMapOpenSound() => PlayClip(mapOpenSound);
    public void PlayMapCloseSound() => PlayClip(mapCloseSound);
    public void PlayOpenSound() => PlayClip(invOpenSound);
    public void PlaySelectedSound() => PlayClip(invSelectedSound);
    public void PlayCraftedSound() => PlayClip(invCraftedSound);
    public void PlayFailCraftSound() => PlayClip(invFailCraftSound);
    public void PlayCloseSound() => PlayClip(invCloseSound);

    private void PlayClip(AudioClip clip)
    {
        if (clip == null || sfxAudioSource == null) return;
        sfxAudioSource.PlayOneShot(clip);
    }

    // Expose menu/gameplay clips for SettingsController usage
    public AudioClip GetMenuMusic() => MenuMusic;
    public AudioClip GetGameplayMusic() => GameplayMusic;
}
