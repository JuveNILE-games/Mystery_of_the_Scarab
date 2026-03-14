using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Obvious.Soap;
using Core.Installers;
using Core.Systems.Configuration;
using Core.Systems.Services.Interfaces;
using Core.Systems.Services;
using Core.Systems.SaveSystem.Configuration;
using Core.Systems.SaveSystem;
using Core.Systems.SaveSystem.Persisters;
using Core.Systems.Logging;
using Core.Systems.Environment;
using Core.Systems.SaveSystem.Interfaces;
using Core.Systems.InputManagement;
using Core.Systems.Navigation;
using Core.Systems.Theming;
using Core.Systems.Dialogue;
using Core.Systems.Dialogue.Events;
using Core.Systems.Dialogue.Resolver;
using Core.Systems.Dialogue.Commands;
using Core.Systems.Dialogue.Effects;
using Core.Systems.Dialogue.Installer;
using Core.Systems.Dialogue.Trigger;
using Core.Systems.Signals;
using Game.Systems.SaveSystem;

namespace Game.Installers
{
    [System.Serializable]
    public class MysteryOfTheScarabInstaller : IGameServiceInstaller
    {
        public void Install(IServiceLocator locator)
        {
            var logger     = locator.Get<ILoggerService>();
            var envService = locator.Get<IEnvironmentService>();
            var config     = locator.Get<IConfigurationService>().Core.SaveSystemConfig;

            logger?.Log(this, "Installing Game-Specific Services for Mystery of the Scarab...");

            // ── Save System ───────────────────────────────────────────────────────
            var env      = envService.Current;
            var profile  = config.GetProfileForEnvironment(env);
            var persister = CreatePersister(profile, locator, logger);
            var saveManager = new MysteryOfTheScarabSaveManager(persister, logger);

            locator.Register<ISaveService>(saveManager);
            locator.Register(saveManager);

            logger?.Log(this, "MysteryOfTheScarabSaveManager registered.");

            // ── Dialogue System ───────────────────────────────────────────────────
            InstallDialogue(locator, logger);
        }

        private void InstallDialogue(IServiceLocator locator, ILoggerService logger)
        {
            var cfg = Resources.Load<DialogueInstallerConfig>("Configuration/DialogueInstallerConfig");
            if (cfg == null)
            {
                logger?.LogError(null, "[Dialogue] DialogueInstallerConfig not found in Resources. " +
                                       "Create it via Assets → Create → Dialogue → Installer Config.");
                return;
            }

            var inputReader  = locator.Get<InputReader>();
            var signalsBus   = locator.Get<IEventBus>();

            // ── Pure-C# services ──────────────────────────────────────────────────
            // Shared TypewriterEffect: same instance reaches both <<speed>> command and DialogueBox.
            // It is NOT registered globally — it's passed to the adapter via Configure().
            var typewriter = new TypewriterEffect();

            var eventBus = new SoapDialogueEventBus(
                cfg.DialogueStarted, cfg.DialogueEnded, cfg.DialogueLinePresented,
                cfg.DialogueAdvanced, cfg.DialogueChoiceMade, cfg.DialogueChoicePending,
                cfg.DialogueChoiceConfirmed);

            var lineResolver = new LineResolver(cfg.CharacterRegistry, logger);

            var eventsDict = new Dictionary<string, ScriptableEventNoParam>();
            if (cfg.NamedEvents != null)
                foreach (var e in cfg.NamedEvents)
                    if (!string.IsNullOrEmpty(e.Name) && e.Event != null)
                        eventsDict[e.Name] = e.Event;

            var commandRegistry = new DialogueCommandRegistry(logger, eventsDict, typewriter);

            // ── DialogueService ───────────────────────────────────────────────────
            var dialogueService = new DialogueService(
                logger, inputReader, eventBus, signalsBus, commandRegistry);

            // Register under both interfaces so [Inject] fields on MonoBehaviours
            // (DialogueBoxPresenterAdapter, DialogueBox) can receive what they need.
            locator.Register<IDialogueService>(dialogueService);
            locator.Register<IDialogueServiceConfig>(dialogueService);

            // ── Store the resolver so the adapter can fetch it on self-registration ──
            // The adapter is a scene MonoBehaviour — it calls SetPresenter/SetRunner itself
            // in Start() via its [Inject] IDialogueServiceConfig. We expose the resolver
            // the same way so it can call Configure(lineResolver).
            locator.Register<ILineResolver>(lineResolver);

            // ── TypewriterEffect for DialogueBox.Configure() ──────────────────────
            // DialogueBox only needs the typewriter injected (nav/theme come from [Inject]).
            // We register it so any scene component can pull it if needed.
            locator.Register<ITypewriterEffect>(typewriter);
            
            // ── Participant & Player Provider ─────────────────────────────────────
            var playerProvider = new TaggedLocalPlayerProvider("Player");
            locator.Register<ILocalPlayerProvider>(playerProvider);

            logger?.Log(null, "[Dialogue] Dialogue system registered. Adapter will self-register on scene start.");
        }

        private IPersister CreatePersister(SaveProfile profile, IServiceLocator locator, ILoggerService logger)
        {
            switch (profile.persisterType)
            {
                case PersisterType.CloudHTTP:
                    var web = locator.Get<Core.Systems.WebRequest.IWebRequestService>();
                    return new CloudSavePersister(
                        profile.cloudHTTPSettings.apiBaseUrl,
                        profile.cloudHTTPSettings.gameSlug,
                        profile.cloudHTTPSettings.userIdOverride ?? "dev-user",
                        web, logger);

                case PersisterType.LocalDisk:
                default:
                    string dir = string.IsNullOrEmpty(profile.localDiskSettings.saveDirectory)
                        ? null
                        : Path.Combine(Application.persistentDataPath, profile.localDiskSettings.saveDirectory);
                    return new SecureLocalDiskPersister(
                        dir, profile.localDiskSettings.fileExtension,
                        profile.localDiskSettings.useEncryption, logger);
            }
        }
    }
}
