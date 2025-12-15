using Core.Systems.SaveSystem.Persisters;
using UnityEngine;

namespace Game.Systems.SaveSystem
{
    /// <summary>
    /// Game-specific persister for Mystery of the Scarab.
    /// Extends LocalDiskPersister with game-specific configuration.
    /// </summary>
    public class ScarabSavePersister : LocalDiskPersister
    {
        private const string SAVE_DIRECTORY_NAME = "MysteryOfTheScarab";
        private const string SAVE_FILE_EXTENSION = ".scarab";

        public ScarabSavePersister() 
            : base(GetSaveDirectory(), SAVE_FILE_EXTENSION)
        {
            Debug.Log($"[ScarabSavePersister] Initialized with save directory: {GetSaveDirectory()}");
        }

        /// <summary>
        /// Get the game-specific save directory path
        /// </summary>
        private static string GetSaveDirectory()
        {
            return System.IO.Path.Combine(
                Application.persistentDataPath, 
                SAVE_DIRECTORY_NAME
            );
        }

        // TODO: Add game-specific helper methods if needed
        // Example:
        // public void SaveQuickSave()
        // {
        //     SetSaveFile("quicksave");
        //     Save().Forget();
        // }
        
        // public async UniTask LoadQuickSave()
        // {
        //     SetSaveFile("quicksave");
        //     await Load();
        // }
        
        // public bool HasQuickSave()
        // {
        //     var saves = GetAvailableSaveFiles();
        //     return saves.Contains("quicksave");
        // }
    }
}
