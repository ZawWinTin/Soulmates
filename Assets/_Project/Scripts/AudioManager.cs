using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{
    public Sound[] sounds;

    public static AudioManager instance;
    public AudioMixer audioMixer;

    [Range(0f, 1f)]
    public static float gameVolume;

    void Awake()
    {
        //For preventing to duplicate Audio Manager
        if (instance == null)
            instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject); //For preventing to restart sound everytime when Scene load

        foreach (Sound s in sounds) //Adding AudioSource Component in this Audio Manager
        {
            s.source = gameObject.AddComponent<AudioSource>();
            s.source.outputAudioMixerGroup = s.audioMixerGroup;
            s.source.clip = s.clip;
            s.source.volume = s.volume;
            s.source.pitch = s.pitch;
            s.source.loop = s.loop;
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        ApplySavedVolumes(); // restore the player's saved music/SFX levels before anything plays
        Play("Theme1");
        HookButtonClicks(); // wire the scene the Audio Manager was created in
    }

    // The options panel (which owns the sliders) starts inactive, so it can't restore volumes at
    // launch. This always-active singleton does it: push the saved dB values onto the mixer's
    // exposed "music" / "sfx" params. The mixer is reached via the sounds' shared mixer group, so
    // no extra inspector wiring is needed.
    private void ApplySavedVolumes()
    {
        AudioMixer mixer = audioMixer;
        if (mixer == null)
        {
            foreach (Sound s in sounds)
            {
                if (s.audioMixerGroup != null)
                {
                    mixer = s.audioMixerGroup.audioMixer;
                    break;
                }
            }
        }
        if (mixer == null)
            return;

        mixer.SetFloat("music", PlayerPrefs.GetFloat(OptionsMenu.MusicPrefKey, 0f));
        mixer.SetFloat("sfx", PlayerPrefs.GetFloat(OptionsMenu.SFXPrefKey, 0f));
    }

    // Every scene load: attach the UI click sound to all buttons (including ones in panels that
    // start inactive, like Pause / Win). Buttons are per-scene and destroyed on unload, so the
    // singleton re-hooks fresh ones each load without ever double-adding.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HookButtonClicks();
    }

    private void HookButtonClicks()
    {
        if (instance != this)
            return; // only the surviving singleton hooks (a duplicate destroys itself in Awake)
        foreach (Button button in FindObjectsOfType<Button>(true))
        {
            button.onClick.AddListener(PlayClick);
        }
    }

    private void PlayClick()
    {
        Play("Click");
    }

    public void Play(string name)
    {
        Sound s = Array.Find(sounds, sound => sound.name == name);
        if (s == null) //If Can't Find name of sound
        {
            Debug.LogWarning("Sound: " + name + " not found!");
            return;
        }
        else
        {
            s.source.Play();
        }
    }
}
