using Core.Systems.SaveSystem;
using Core.Systems.SaveSystem.Interfaces;
using Core.Systems.SaveSystem.Persisters;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Core.Systems.Logging;
using Core.Systems.Services.Interfaces;
using System;
using NetCore.Interfaces;

namespace Game.Systems.SaveSystem
{
    /// <summary>
    /// Game-specific SaveManager for Mystery of the Scarab.
    /// Extends the base SaveService to handle game-specific save/load logic.
    /// </summary>
    public class MysteryOfTheScarabSaveManager : SaveService
    {
        // Not currently read — kept for save-related logic that may need the current
        // session mode later (e.g. gating cloud vs local save behavior per mode).
        private readonly ISessionService _session;

        // Public API for other systems
        public event Action OnGameLoaded;

        public MysteryOfTheScarabSaveManager(IPersister persister, ILoggerService logger = null, ISessionService session = null)
            : base(persister, logger)
        {
            _session = session;
        }

        protected override void BeforeLoad()
        {
            _logger?.Log(this, "BeforeLoad - Preparing to load save data");
            // Clear any runtime cache here if we had one
        }

        protected override async UniTask CustomLoad()
        {
            _logger?.Log(this, "CustomLoad - Applying loaded data to game state");
            
            // In a real scenario, we would push data TO the game systems here.
            // Since we don't have those systems centrally, we dispatch an event.
            OnGameLoaded?.Invoke();
            
            await UniTask.CompletedTask;
        }

        protected override void InitializeVarListeners()
        {
            // No-op: the old auto-save-on-return-to-menu listener (via IGameStateManager)
            // was never actually wired up (its bind method was defined but never called) —
            // removed rather than ported. ISessionService has no equivalent "Menu" mode;
            // revisit as an explicit follow-up if auto-save-on-session-end is wanted.
        }

        // --- Public API for Game Systems ---

        public void SaveLevelProgress(string levelId, int stars)
        {
            if (!isLoaded) return;
            persister.SetProgressElement("levels", levelId, "stars", stars);
            // Optional: Auto-save on major progress
            SaveNow().Forget();
        }
        
        public int GetLevelStars(string levelId)
        {
             if (!isLoaded) return 0;
             return persister.GetProgressElement("levels", levelId, "stars") ?? 0;
        }

        public void SavePreference(string key, string value)
        {
            if (!isLoaded) return;
            persister.SetPreference(key, value);
            SaveNow().Forget();
        }

        public string GetPreference(string key, string defaultValue = "")
        {
            if (!isLoaded) return defaultValue;
            return persister.GetPreference(key) ?? defaultValue;
        }
    }
}
