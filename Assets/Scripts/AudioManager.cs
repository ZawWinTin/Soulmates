using UnityEngine.Audio;
using UnityEngine;
using UnityEngine.UI;
using System;

public class AudioManager : MonoBehaviour
{
    public Sound[] sounds;

    public static AudioManager instance;
    public AudioMixer audioMixer;

    [Range(0f, 1f)]
    public static float gameVolume;

    private bool isVolumeChanged=false;

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

        DontDestroyOnLoad(gameObject);  //For preventing to restart sound everytime when Scene load

        foreach(Sound s in sounds)  //Adding AudioSource Component in this Audio Manager
        {
            s.source=gameObject.AddComponent<AudioSource>();
            s.source.clip = s.clip;
            s.source.volume = s.volume;
            s.source.pitch = s.pitch;
            s.source.loop = s.loop;
        }
    }

    void Start()
    {
        Play("Theme");
    }

    public void Play(string name)
    {
        Sound s=Array.Find(sounds, sound => sound.name == name);
        if (s == null)  //If Can't Find name of sound
        {
            Debug.LogWarning("Sound: " + name + " not found!");
            return;
        }
        /*if (PauseMenu.isGamePaused)
        {
            s.source.pitch *= .5f;
        }*/
        s.source.Play();
    }

    public void SetVolume(float vol)
    {
        isVolumeChanged = true;
        gameVolume = vol;
    }

    void Update()
    {
        //audioMixer.GetFloat("volume");
        if (isVolumeChanged)
        {
            isVolumeChanged = false;
            AudioSource[] addedSounds=gameObject.GetComponents<AudioSource>();
            for(int i=0;i< addedSounds.Length;i++) {
                addedSounds[i].volume = sounds[i].volume * gameVolume;     //Adject with original volume
            }                
        }
    }
}
