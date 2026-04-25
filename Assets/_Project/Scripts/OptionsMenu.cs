using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class OptionsMenu : MonoBehaviour
{
    public AudioMixer audioMixer;

    private bool isSFXPlaying;
    private float delay = 1.2f;
    private AudioSource sfxAudioSource;

    private void Awake()
    {
        sfxAudioSource = GetComponent<AudioSource>();
        isSFXPlaying = false;
    }

    public void SetMusic(float volume)
    {
        audioMixer.SetFloat("music", volume);
    }

    public void SetSFX(float volume)
    {
        StartCoroutine(TestSFXAudio());
        audioMixer.SetFloat("sfx", volume);
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
