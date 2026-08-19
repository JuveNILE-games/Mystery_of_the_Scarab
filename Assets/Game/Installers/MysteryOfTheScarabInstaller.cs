using System;
using System.IO;
using System.Collections.Generic;
using Core;
using UnityEngine;
using Obvious.Soap;
using Core.Installers;
using Core.Systems.AgentNavigation;
using Core.Systems.Configuration;
using Core.Systems.Dialogue;
using Core.Systems.Dialogue.Commands;
using Core.Systems.Services.Interfaces;
using Core.Systems.Services;
using Core.Systems.SaveSystem.Configuration;
using Core.Systems.SaveSystem;
using Core.Systems.SaveSystem.Persisters;
using Core.Systems.Logging;
using Core.Systems.Environment;
using Core.Systems.SaveSystem.Interfaces;
using Core.Systems.InputManagement;
using Core.Systems.Settings;
using Core.Systems.Dialogue.Effects;
using Core.Systems.Dialogue.Events;
using Core.Systems.Dialogue.Installer;
using Core.Systems.Dialogue.Resolver;
using Core.Systems.Dialogue.Trigger;
using Core.Systems.Signals;
using Game.Systems.SaveSystem;
using NetCore.Interfaces;

namespace Game.Installers
{
    [InstallerDependsOn(typeof(GameServicesInstaller))]
    [Serializable]
    public class MysteryOfTheScarabInstaller : IGameServiceInstaller
    {
        public void Install(IServiceLocator locator)
        {
            var logger     = locator.Get<ILoggerService>();
            var envService = locator.Get<IEnvironmentService>();
            var config     = locator.Get<IConfigurationService>().Core.SaveSystemConfig;

            logger?.Log(this, "Installing Game-Specific Services for Mystery of the Scarab...");

            // ── Core Game Services ────────────────────────────────────────────────
            // ISessionService is registered by NetCoreModule during ModuleSyncBootPhase,
            // which GameServicesDiscoveryBootPhase now explicitly depends on — safe to
            // resolve eagerly here.
            var session = locator.Get<ISessionService>();
            locator.Register<IControllableRegistry>(new ControllableRegistry());

            // ── Save System ───────────────────────────────────────────────────────
            var env      = envService.Current;
            var profile  = config.GetProfileForEnvironment(env);
            var persister = CreatePersister(profile, locator, logger);
            var saveManager = new MysteryOfTheScarabSaveManager(persister, logger, session);

            locator.Register<ISaveService>(saveManager);


            // ── Dynamic NavMesh Surface (Solo only) ──────────────────────────────
            if (session?.Mode.Value == SessionMode.Solo)
            {
                var navMeshPrefab = Resources.Load<GameObject>("Prefabs/AI/DynamicNavMeshSurface");
                if (navMeshPrefab != null)
                {
                    var go = UnityEngine.Object.Instantiate(navMeshPrefab);
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    var service = go.GetComponent<DynamicNavMeshSurfaceService>();
                    locator.Register<INavMeshSurfaceService>(service);
                    locator.Register(scope =>
                        scope.Get<INavMeshSurfaceService>() as INavMeshReadinessProvider);
                    logger?.Log(this, "DynamicNavMeshSurfaceService registered for Solo session.");
                }
                else
                {
                    logger?.LogWarning(this, "DynamicNavMeshSurface prefab not found in Resources/Prefabs/. " +
                                             "NavMesh will not be available for companion AI.");
                }
            }
            else
            {
                logger?.Log(this, $"NavMesh surface registration skipped — session mode is {session?.Mode.Value}.");
            }

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
            if (locator.TryGet<AccessibilityService>(out var accessibilityService))
            {
                typewriter.CharactersPerSecond = accessibilityService.TextSpeed.Value;
                // App-lifetime binding, never disposed — same convention as SettingsService's own
                // volume bindings. Known collision, not fixed here: DialogueCommandRegistry's
                // <<speed>>/<<speed default>> commands directly overwrite CharactersPerSecond
                // per dialogue node, stomping this preference for the remainder of that node.
                accessibilityService.TextSpeed.Bind(v => typewriter.CharactersPerSecond = v, false);
            }

            var eventBus = new SoapDialogueEventBus(
                cfg.DialogueStarted, cfg.DialogueEnded, cfg.DialogueLinePresented,
                cfg.DialogueAdvanced, cfg.DialogueChoicePending, cfg.DialogueChoiceConfirmed,
                cfg.DialogueChoiceMade);

            var lineResolver = new LineResolver(cfg.CharacterRegistry, logger);

            var eventsDict = new Dictionary<string, ScriptableEventNoParam>();
            if (cfg.NamedEvents != null)
                foreach (var e in cfg.NamedEvents)
                    if (!string.IsNullOrEmpty(e.Name) && e.Event != null)
                        eventsDict[e.Name] = e.Event;

            var commandRegistry = new DialogueCommandRegistry(
                logger, eventsDict, typewriter, eventBus,
                locator.TryGet<Core.Systems.AudioSystem.AudioService>(out var audioService) ? audioService : null,
                cfg.SoundRegistry);

            // ── DialogueService ───────────────────────────────────────────────────
            var dialogueService = new DialogueService(
                logger, inputReader, eventBus, signalsBus, commandRegistry);

            locator.Register<IDialogueService>(dialogueService);
            locator.Register<IDialogueServiceConfig>(dialogueService);
            locator.Register<ILineResolver>(lineResolver);
            locator.Register<ITypewriterEffect>(typewriter);
            // Presenters (e.g. DialogueBox) inject IDialogueEventBus to subscribe to
            // OnEmotionChanged — without this registration that field would resolve to null.
            locator.Register<IDialogueEventBus>(eventBus);
            
            // ── Participant & Player Provider ─────────────────────────────────────
            var playerProvider = new TaggedLocalPlayerProvider("Player");
            locator.Register<ILocalPlayerProvider>(playerProvider);

            // ── Player Slot Provider (multiplayer-aware) ─────────────────────────
            var controllableRegistry = locator.Get<IControllableRegistry>();
            var slotProvider = new PlayerSlotProvider(controllableRegistry);
            locator.Register<IPlayerSlotProvider>(slotProvider);

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
