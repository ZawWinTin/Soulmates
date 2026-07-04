using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class OptionsMenu : MonoBehaviour
{
    public AudioMixer audioMixer;

    // PlayerPrefs keys — volumes are stored as the raw slider value (decibels, -80..+20).
    public const string MusicPrefKey = "MusicVolume";
    public const string SFXPrefKey = "SFXVolume";

    private bool isSFXPlaying;
    private float delay = 1.2f;
    private AudioSource sfxAudioSource;

    private void Awake()
    {
        sfxAudioSource = GetComponent<AudioSource>();
        isSFXPlaying = false;
    }

    // The options panel starts inactive (saved volumes are applied at launch by AudioManager).
    // When the player opens it, move each slider's handle to the saved value without firing the
    // SFX test blip — the sliders self-identify by the method they call (SetMusic / SetSFX).
    private void OnEnable()
    {
        foreach (Slider slider in GetComponentsInChildren<Slider>(true))
        {
            for (int i = 0; i < slider.onValueChanged.GetPersistentEventCount(); i++)
            {
                string method = slider.onValueChanged.GetPersistentMethodName(i);
                if (method == nameof(SetMusic))
                    slider.SetValueWithoutNotify(PlayerPrefs.GetFloat(MusicPrefKey, 0f));
                else if (method == nameof(SetSFX))
                    slider.SetValueWithoutNotify(PlayerPrefs.GetFloat(SFXPrefKey, 0f));
            }
        }
    }

    public void SetMusic(float volume)
    {
        audioMixer.SetFloat("music", volume);
        PlayerPrefs.SetFloat(MusicPrefKey, volume);
        PlayerPrefs.Save();
    }

    public void SetSFX(float volume)
    {
        StartCoroutine(TestSFXAudio());
        audioMixer.SetFloat("sfx", volume);
        PlayerPrefs.SetFloat(SFXPrefKey, volume);
        PlayerPrefs.Save();
    }

    IEnumerator TestSFXAudio()
    {
        if (isSFXPlaying)
        {
            yield return new WaitForSeconds(delay);
        }
        else
        {
            isSFXPlaying = true;

            sfxAudioSource.Play();
            yield return new WaitForSeconds(delay);

            isSFXPlaying = false;
        }
    }
}
