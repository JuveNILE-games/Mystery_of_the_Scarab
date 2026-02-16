using UnityEngine;
using Core.Installers;
using Core.Systems.Services.Interfaces;
using Core.Systems.SaveSystem.Configuration;
using Core.Systems.SaveSystem;
using Core.Systems.SaveSystem.Persisters;
using Core.Systems.Logging;
using Core.Systems.Environment;
using Core.Systems.SaveSystem.Interfaces;
using Game.Systems.SaveSystem;
using Core.Systems.Services;

namespace Game.Installers
{
    public class MysteryOfTheScarabInstaller : IGameServiceInstaller
    {
        public void Install(IServiceLocator locator)
        {
            var logger = locator.Get<ILoggerService>();
            var envService = locator.Get<IEnvironmentService>();
            var config = locator.Get<Core.Systems.Configuration.IConfigurationService>().Core.SaveSystemConfig;

            logger?.Log(this, " Installing Game-Specific Services for Mystery of the Scarab...");

            // --- Save System Registration ---
            // Replaces the Reflection logic in SaveSystemFactory
            
            var env = envService.Current;
            var profile = config.GetProfileForEnvironment(env);
            
            // 1. Create Persister (We can reuse the logic from Factory or duplicate it here for full decoupling)
            // For now, let's keep using SaveSystemFactory for the Persister part if possible, 
            // OR fully implement it here. Full implementation is cleaner.
            
            IPersister persister = CreatePersister(profile, locator, logger);
            
            // 2. Create Concrete Game Manager
            var saveManager = new MysteryOfTheScarabSaveManager(persister, logger);
            
            // 3. Register
            locator.Register<ISaveService>(saveManager);
            locator.Register<MysteryOfTheScarabSaveManager>(saveManager); // Register concrete too if needed
            
            logger?.Log(this, "MysteryOfTheScarabSaveManager registered.");
        }

        private IPersister CreatePersister(SaveProfile profile, IServiceLocator locator, ILoggerService logger)
        {
             // Simplified logic mirroring Factory for now. 
             // In a real refactor we might move PersisterFactory to a reusable utility.
             
             switch (profile.persisterType)
             {
                 case PersisterType.CloudHTTP:
                     var web = locator.Get<Core.Systems.WebRequest.IWebRequestService>();
                     return new CloudSavePersister(
                         profile.cloudHTTPSettings.apiBaseUrl,
                         profile.cloudHTTPSettings.gameSlug,
                         profile.cloudHTTPSettings.userIdOverride ?? "dev-user",
                         web,
                         logger
                     );
                 
                 case PersisterType.LocalDisk:
                 default:
                      string dir = string.IsNullOrEmpty(profile.localDiskSettings.saveDirectory) 
                          ? null 
                          : System.IO.Path.Combine(Application.persistentDataPath, profile.localDiskSettings.saveDirectory);
                      return new SecureLocalDiskPersister(dir, profile.localDiskSettings.fileExtension, profile.localDiskSettings.useEncryption, logger);
             }
        }
    }
}
