using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using NUnit.Framework;

// Save format: JSON round trip, the one-time BinaryFormatter migration, and broken files.
// Run: tools/run-tests.sh (or Window → General → Test Runner → EditMode).
public class SaveSystemTests
{
    string dir;

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(
            Path.GetTempPath(),
            "soulmates-save-" + System.Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(dir);
        SaveSystem.Folder = dir;
    }

    [TearDown]
    public void TearDown()
    {
        SaveSystem.Folder = null;
        Directory.Delete(dir, true);
    }

    string Json => Path.Combine(dir, "level.json");
    string Legacy => Path.Combine(dir, "level.pz");

#pragma warning disable SYSLIB0011 // writes a save the way builds before 2026-10 did
    void WriteLegacy(SavedData data)
    {
        using (var stream = new FileStream(Legacy, FileMode.Create))
            new BinaryFormatter().Serialize(stream, data);
    }
#pragma warning restore SYSLIB0011

    [Test]
    public void NoSaveLoadsAsNull()
    {
        Assert.IsNull(SaveSystem.LoadData());
        Assert.AreEqual(0, SaveSystem.GetStars(1));
    }

    [Test]
    public void SaveRoundTripsAsJson()
    {
        SaveSystem.SaveData(5);
        SaveSystem.SaveStars(3, 2);
        SaveSystem.SaveStars(3, 1); // a lower rating never overwrites the best

        Assert.IsTrue(File.Exists(Json));
        Assert.IsFalse(File.Exists(Legacy));
        SavedData data = SaveSystem.LoadData();
        Assert.AreEqual(5, data.level);
        Assert.AreEqual(2, SaveSystem.GetStars(3));
    }

    [Test]
    public void LegacySaveIsMigratedOnce()
    {
        var old = new SavedData(7);
        old.stars[2] = 3;
        old.stars[9] = 1;
        WriteLegacy(old);

        SavedData data = SaveSystem.LoadData();
        Assert.AreEqual(7, data.level);
        Assert.AreEqual(3, data.stars[2]);
        Assert.AreEqual(1, data.stars[9]);
        Assert.IsTrue(File.Exists(Json), "converted to level.json");
        Assert.IsFalse(File.Exists(Legacy), "old file moved out of the way");
        Assert.IsTrue(File.Exists(Legacy + ".migrated"), "old file kept as a backup");

        // later loads read the JSON
        Assert.AreEqual(7, SaveSystem.LoadData().level);
        Assert.AreEqual(3, SaveSystem.GetStars(2));
    }

    [Test]
    public void LegacySaveWithoutStarsKeepsProgress()
    {
        var old = new SavedData(4);
        old.stars = null; // saves from before the star ratings existed
        WriteLegacy(old);

        Assert.AreEqual(4, SaveSystem.LoadData().level);
        Assert.AreEqual(0, SaveSystem.GetStars(1));
        SaveSystem.SaveStars(1, 3);
        Assert.AreEqual(3, SaveSystem.GetStars(1));
        Assert.AreEqual(4, SaveSystem.LoadData().level);
    }

    [Test]
    public void BrokenFilesLoadAsEmptyWithoutThrowing()
    {
        File.WriteAllText(Json, "{ not json");
        Assert.IsNull(SaveSystem.LoadData());

        File.Delete(Json);
        File.WriteAllBytes(Legacy, new byte[] { 1, 2, 3 });
        Assert.IsNull(SaveSystem.LoadData());
        Assert.IsTrue(File.Exists(Legacy), "an unreadable old save is left in place");
    }
}
