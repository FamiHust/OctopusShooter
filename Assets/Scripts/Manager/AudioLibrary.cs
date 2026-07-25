using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AudioLibraryString", menuName = "Audio/Audio Library (String)")]
public class AudioLibraryString : ScriptableObject
{
    public List<AudioEntryString> entries = new List<AudioEntryString>();

    public AudioEntryString Get(string key)
    {
        return entries.Find(x => x.key == key);
    }
}

[Serializable]
public class AudioEntryString
{
    public string key;
    public AudioClip clip;

    [Range(0f, 1f)] public float volume = 1f;

    public bool randomPitch = false;
    [Range(0.5f, 2f)] public float pitchMin = 0.95f;
    [Range(0.5f, 2f)] public float pitchMax = 1.05f;
}
