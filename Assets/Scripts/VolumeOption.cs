using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class VolumeOption : MonoBehaviour
{
    private static float volume=1f;
    private AudioManager audioManager;
    private Slider volumeSlider;

    void Awake()
    {
        volumeSlider = GetComponent<Slider>();
        if (volume != 1f)
        {
            volumeSlider.value = volume;
        }
    }
    void Start()
    {
        audioManager = FindObjectOfType<AudioManager>();
    }

    // Update is called once per frame
    void Update()
    {
        audioManager.SetVolume(volumeSlider.value);
        volume = volumeSlider.value;
    }
}
