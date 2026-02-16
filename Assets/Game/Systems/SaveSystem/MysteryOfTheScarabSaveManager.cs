using Core.Systems.SaveSystem;
using Core.Systems.SaveSystem.Interfaces;
using Core.Systems.SaveSystem.Persisters;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Core.Systems.Logging;
using Core.Systems.Services.Interfaces;
using System;
using Core; // For IGameStateManager

namespace Game.Systems.SaveSystem
{
    /// <summary>
    /// Game-specific SaveManager for Mystery of the Scarab.
    /// Extends the base SaveService to handle game-specific save/load logic.
    /// </summary>
    public class MysteryOfTheScarabSaveManager : SaveService
    {
        private IGameStateManager _gameStateManager;

        // Public API for other systems
        public event Action OnGameLoaded;

        public MysteryOfTheScarabSaveManager(IPersister persister, ILoggerService logger = null) 
            : base(persister, logger)
        {
        }
        
        /// <summary>
        /// Inject dependencies that weren't available at construction time if needed, 
        /// or use a Setup method.
        /// </summary>
        public void BindGameStateManager(IGameStateManager gameStateManager)
        {
            _gameStateManager = gameStateManager;
            if (isInitialized)
            {
                // If we're already initialized, hook up listeners now
                HookGameStateListeners();
            }
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
            _logger?.Log(this, "InitializeVarListeners - Setting up auto-save listeners");
            HookGameStateListeners();
        }

        private void HookGameStateListeners()
        {
            if (_gameStateManager != null)
            {
                _gameStateManager.OnStateChanged -= OnGameStateChanged;
                _gameStateManager.OnStateChanged += OnGameStateChanged;
            }
        }

        private void OnGameStateChanged(GameState newState)
        {
            // Auto-save when returning to menu
            if (newState == GameState.Menu && isLoaded)
            {
                 SaveNow().Forget();
            }
        }
        
        public override void Dispose()
        {
            if (_gameStateManager != null)
            {
                _gameStateManager.OnStateChanged -= OnGameStateChanged;
            }
            base.Dispose();
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
