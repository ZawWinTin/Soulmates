using UnityEngine;
using UnityEngine.Audio;

[System.Serializable]   //Appear in the inspector for Custom Class
public class Sound
{
    public string name;

    public AudioClip clip;

    [Range(0f, 1f)]
    public float volume;

    [Range(.1f, 3f)]
    public float pitch;

    public bool loop;

    public AudioMixerGroup audioMixerGroup;

    [HideInInspector]
    public AudioSource source;
}
