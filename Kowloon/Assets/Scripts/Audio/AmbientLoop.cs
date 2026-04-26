using UnityEngine;

/// <summary>
/// Plays an ambient audio clip on loop from startup. Drop on any persistent
/// GameObject and assign the clip + volume in the inspector.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AmbientLoop : MonoBehaviour
{
    [Header("Audio")]
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 0.5f;

    private AudioSource _src;

    void Awake()
    {
        _src              = GetComponent<AudioSource>();
        _src.clip         = clip;
        _src.loop         = true;
        _src.playOnAwake  = false;
        _src.spatialBlend = 0f;
        _src.volume       = volume;
        if (clip != null) _src.Play();
    }

    void OnValidate()
    {
        if (_src != null) _src.volume = volume;
    }
}
