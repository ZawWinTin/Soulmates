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

        try
        {
            BinaryFormatter formatter = new BinaryFormatter();
            using (FileStream stream = new FileStream(path, FileMode.Open))
                return formatter.Deserialize(stream) as SavedData;
        }
        catch (System.Exception e)
        {
            // A truncated or corrupt file must NOT throw: this is read at menu
            // Awake (PlayOptions) and during the cloud-save merge, so an exception
            // here breaks level-select init and cloud recovery. Treat it as "no
            // local save" — callers fall back to defaults, and for a signed-in
            // player the conservative cloud merge restores real progress on top.
            // The bad file is left in place; the next successful Write replaces it.
            Debug.LogWarning("SaveSystem: could not read save, treating as empty. " + e.Message);
            return null;
        }
    }

    static void Write(SavedData data)
    {
        BinaryFormatter formatter = new BinaryFormatter();
        string path = Path;
        string tmp = path + ".tmp";
        Debug.Log("Saved at: " + path);

        // Serialize to a temp file first, then swap it in. FileMode.Create on the
        // real path would truncate the existing save BEFORE writing — if
        // serialization threw partway, the good save would be left corrupt. This
        // way a failed write leaves the previous save untouched.
        try
        {
            using (FileStream stream = new FileStream(tmp, FileMode.Create))
                formatter.Serialize(stream, data);

            if (File.Exists(path))
                File.Delete(path);
            File.Move(tmp, path);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("SaveSystem: write failed, keeping previous save. " + e.Message);
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            // The write didn't happen — do NOT notify the cloud, or last-write-wins
            // on z-core could overwrite good progress with a save we never made.
            return;
        }

        // Every successful local save also goes to the z-games site as a cloud
        // save (WebGL only — it's a harmless Debug.Log everywhere else).
        GameBridge.NotifyProgressSaved();
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
