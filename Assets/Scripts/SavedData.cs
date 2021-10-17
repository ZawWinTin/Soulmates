using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SavedData
{
    public int level;

    public float volume;

    public SavedData(int l)
    {
        level = l;        
    }
}
