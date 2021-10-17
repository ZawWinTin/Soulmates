using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

public static class SaveSystem
{
    public static void SaveData(int level)
    {
        BinaryFormatter formatter = new BinaryFormatter();
        string path = Application.persistentDataPath + "/level.pz";
        FileStream stream = new FileStream(path, FileMode.Create);

        SavedData data = new SavedData(level);

        formatter.Serialize(stream, data);
        stream.Close();
    }

    public static SavedData LoadData()
    {
        string path= Application.persistentDataPath + "/level.pz";
        if (File.Exists(path))
        {
            BinaryFormatter formatter = new BinaryFormatter();
            FileStream stream = new FileStream(path, FileMode.Open);

            SavedData data =formatter.Deserialize(stream) as SavedData;
            stream.Close();
            
            return data;
        }
        else
        {
            return null;
        }
    }
}
