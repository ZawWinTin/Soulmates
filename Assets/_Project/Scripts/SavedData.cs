using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SavedData
{
    public int level;

    public float volume;

    // Best star rating (0..3) per scene build index (Level01 = 1 … Level09 = 9). Old save files
    // serialized before this field existed deserialize it as null — SaveSystem rebuilds it on demand,
    // so existing progress is never lost.
    public int[] stars;

    public SavedData(int l)
    {
        level = l;
        stars = new int[32];
    }
}
