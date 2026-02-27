using UnityEngine;
using Cysharp.Threading.Tasks;
using Core.Systems.SaveSystem.Persisters;
using Core.Systems.SaveSystem.Data;
using System.IO;

public class SaveSystemTest : MonoBehaviour
{
    private SecureLocalDiskPersister persister;
    private string testFile = "save.json"; // Persister uses 'save' + extension

    async void Start()
    {
        Debug.Log("--- Starting Save System Verification ---");
        
        string path = Path.Combine(Application.persistentDataPath, "SaveTest");
        if (Directory.Exists(path)) Directory.Delete(path, true);
        
        // 1. Initialize Persister (Encryption ON)
        persister = new SecureLocalDiskPersister(path, ".json", true);
        // persister.SetSaveFile(testFile); // Method does not exist, uses default 'save.json'
        
        // 2. Set Data
        Debug.Log("Step 1: Setting Data...");
        persister.SetProgressElement("level.1.stars", 3);
        persister.SetPreference("volume", "1.0");
        
        // 3. Save
        Debug.Log("Step 2: Saving...");
        await persister.Save();
        
        // 4. Verify file exists
        if (File.Exists(Path.Combine(path, testFile)))
        {
             Debug.Log("SUCCESS: File created.");
        }
        else
        {
             Debug.LogError($"FAILURE: File not created at {Path.Combine(path, testFile)}");
             return;
        }

        // 5. Clear Memory
        persister = new SecureLocalDiskPersister(path, ".json", true);
        
        // 6. Load
        Debug.Log("Step 3: Loading...");
        await persister.Load();
        
        // 7. Verify Data
        int? stars = persister.GetProgressElement("level.1.stars");
        string volume = persister.GetPreference("volume");
        
        if (stars == 3 && volume == "1.0")
        {
            Debug.Log($"SUCCESS: Data Verified! Stars: {stars}, Volume: {volume}");
        }
        else
        {
            Debug.LogError($"FAILURE: Data Mismatch! Stars: {stars}, Volume: {volume}");
        }
        
        Debug.Log("--- Verification Complete ---");
    }
}
