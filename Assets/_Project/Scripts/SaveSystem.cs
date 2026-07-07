using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

public static class SaveSystem
{
    static string Path => Application.persistentDataPath + "/level.pz";

    // Save unlock progress WITHOUT clobbering star ratings (load-merge-write).
    public static void SaveData(int level)
    {
        SavedData data = LoadData() ?? new SavedData(level);
        data.level = level;
        Write(data);
    }

    // Record the best (highest) star rating earned for a level's build index.
    public static void SaveStars(int buildIndex, int count)
    {
        SavedData data = LoadData() ?? new SavedData(0);
        EnsureStars(data);
        if (buildIndex >= 0 && buildIndex < data.stars.Length && count > data.stars[buildIndex])
        {
            data.stars[buildIndex] = count;
            Write(data);
        }
    }

    // Best star rating (0..3) saved for a level's build index; 0 if never earned.
    public static int GetStars(int buildIndex)
    {
        SavedData data = LoadData();
        if (data == null || data.stars == null || buildIndex < 0 || buildIndex >= data.stars.Length)
            return 0;
        return data.stars[buildIndex];
    }

    public static SavedData LoadData()
    {
        string path = Path;
        if (!File.Exists(path))
            return null;

        BinaryFormatter formatter = new BinaryFormatter();
        using (FileStream stream = new FileStream(path, FileMode.Open))
            return formatter.Deserialize(stream) as SavedData;
    }

    static void Write(SavedData data)
    {
        BinaryFormatter formatter = new BinaryFormatter();
        string path = Path;
        Debug.Log("Saved at: " + path);
        using (FileStream stream = new FileStream(path, FileMode.Create))
            formatter.Serialize(stream, data);
    }

    // Old save files predate the stars[] field → it deserializes as null. Rebuild it so indexing is safe.
    static void EnsureStars(SavedData d)
    {
        if (d.stars == null || d.stars.Length < 32)
        {
            int[] old = d.stars;
            d.stars = new int[32];
            if (old != null)
                System.Array.Copy(old, d.stars, old.Length);
        }
    }
}
