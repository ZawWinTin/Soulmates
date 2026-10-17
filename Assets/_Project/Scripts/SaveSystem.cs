using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

public static class SaveSystem
{
    // Saves are JSON (JsonUtility). Builds before 2026-10 wrote level.pz with BinaryFormatter, which .NET
    // deprecates as unsafe: LoadData reads that file once, rewrites it as level.json and renames it to
    // level.pz.migrated (kept as a backup). Drop LoadLegacy once old saves no longer matter.
    // Read lazily: Unity refuses persistentDataPath during object construction. Tests point it at a temp folder.
    static string folder;
    public static string Folder
    {
        get => folder ?? Application.persistentDataPath;
        set => folder = value;
    }
    static string Path => Folder + "/level.json";
    static string LegacyPath => Folder + "/level.pz";

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
            return File.Exists(LegacyPath) ? MigrateLegacy() : null;

        try
        {
            return JsonUtility.FromJson<SavedData>(File.ReadAllText(path));
        }
        catch (System.Exception e)
        {
            // A truncated or corrupt file must NOT throw: this is read at menu
            // Awake (PlayOptions) and during the cloud-save merge, so an exception
            // here breaks level-select init and cloud recovery. Treat it as "no
            // local save" — callers fall back to defaults, and for a signed-in
            // player the conservative cloud merge restores real progress on top.
            // The bad file is left in place; the next successful Write replaces it.
            Debug.LogWarning(
                "[SaveSystem.LoadData] could not read save, treating as empty. " + e.Message
            );
            return null;
        }
    }

    // One-time upgrade of a BinaryFormatter save. If anything fails, the old file stays where it is and the
    // next LoadData tries again; a save that can't be read at all is treated as empty, as before.
    static SavedData MigrateLegacy()
    {
        SavedData data;
        try
        {
            data = LoadLegacy();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning(
                "[SaveSystem.MigrateLegacy] could not read old save, treating as empty. "
                    + e.Message
            );
            return null;
        }
        if (data == null)
            return null;

        if (TryWrite(data))
        {
            try
            {
                string backup = LegacyPath + ".migrated";
                if (File.Exists(backup))
                    File.Delete(backup);
                File.Move(LegacyPath, backup);
            }
            catch (System.Exception e)
            {
                // level.json exists now and wins on the next load, so a leftover level.pz is harmless.
                Debug.LogWarning(
                    "[SaveSystem.MigrateLegacy] saved as JSON, but could not rename the old file. "
                        + e.Message
                );
            }
            Debug.Log("[SaveSystem.MigrateLegacy] old save converted to " + Path);
        }
        return data;
    }

#pragma warning disable SYSLIB0011 // BinaryFormatter: only to read saves written before 2026-10.
    static SavedData LoadLegacy()
    {
        var formatter = new BinaryFormatter();
        using (FileStream stream = new FileStream(LegacyPath, FileMode.Open))
            return formatter.Deserialize(stream) as SavedData;
    }
#pragma warning restore SYSLIB0011

    static void Write(SavedData data)
    {
        if (!TryWrite(data))
            // The write didn't happen — do NOT notify the cloud, or last-write-wins
            // on z-core could overwrite good progress with a save we never made.
            return;

        // Every successful local save also goes to the z-games site as a cloud
        // save (WebGL only — it's a harmless Debug.Log everywhere else).
        GameBridge.NotifyProgressSaved();
    }

    static bool TryWrite(SavedData data)
    {
        string path = Path;
        string tmp = path + ".tmp";
        Debug.Log("[SaveSystem.Write] Saved at: " + path);

        // Serialize to a temp file first, then swap it in. FileMode.Create on the
        // real path would truncate the existing save BEFORE writing — if
        // serialization threw partway, the good save would be left corrupt. This
        // way a failed write leaves the previous save untouched.
        try
        {
            File.WriteAllText(tmp, JsonUtility.ToJson(data));

            if (File.Exists(path))
                File.Delete(path);
            File.Move(tmp, path);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning(
                "[SaveSystem.Write] write failed, keeping previous save. " + e.Message
            );
            try
            {
                if (File.Exists(tmp))
                    File.Delete(tmp);
            }
            catch { }
            return false;
        }
        return true;
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
