using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Core.Systems.Bindables;
using Core.Systems.InputManagement;
using Core.Systems.Localization;
using Core.Systems.Localization.Definitions;
using Core.Systems.Localization.Interfaces;
using Core.Systems.Navigation.Canvases;
using Core.Systems.Settings;
using Core.Systems.Theming;
using Core.Utility.Attributes;
using Core.Utility.FluentUI;
using Cysharp.Threading.Tasks;
using FluentUI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Game.UI.Views.Settings
{
    /// <summary>
    /// Settings panel overlay that slides in from the left edge of the screen.
    /// Built with UIToolkit + FluentUI to match the Main Menu style.
    ///
    /// Tabs:
    ///   - Audio         (functional — volume sliders bound to SettingsService)
    ///   - Video         (functional — resolution/fullscreen/quality/vsync bound to VideoSettingsService)
    ///   - Controls      (functional — rebind rows from RebindService)
    ///   - Language      (functional — language buttons from LocalizationService)
    ///   - Accessibility (functional — UI scale + text speed bound to AccessibilityService)
    /// </summary>
    public class SettingsPanel : OverlayCanvas, ILocalizationListener
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument document;

        [Header("Styling")]
        [SerializeField] private StyleSheet _settingsStyleSheet;

        [Inject] private SettingsService _settingsService;
        [Inject] private VideoSettingsService _videoSettingsService;
        [Inject] private AccessibilityService _accessibilityService;
        [Inject] private RebindService _rebindService;
        [Inject] private LocalizationService _localizationService;
        [Inject] private IThemeService _themeService;

        private VisualElement _backdrop;
        private VisualElement _panel;

        // Tab tracking
        private readonly List<Button> _tabButtons = new();
        private readonly List<VisualElement> _tabContents = new();
        private int _activeTabIndex;

        // Binding subscriptions for cleanup
        private readonly List<IDisposable> _bindings = new();

        // Language buttons for refresh
        private VisualElement _languageContainer;

        private bool _uiBuilt = false;
        private bool _isRegisteredForLocalization = false;

        #region Lifecycle

        protected override void OnServicesInjected()
        {
            base.OnServicesInjected();
            // BuildUI() cannot be called here because UIDocument.rootVisualElement is null
            // when the GameObject starts disabled (which Overlays typically do).
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (!_uiBuilt)
            {
                BuildUI();
                _uiBuilt = true;
            }

            if (!_isRegisteredForLocalization && _localizationService != null)
            {
                _localizationService.RegisterListener(this);
                _isRegisteredForLocalization = true;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _uiBuilt = false;

            // Reset visual state so it doesn't flash on next open
            if (_backdrop != null)
            {
                _backdrop.RemoveFromClassList("settings-backdrop--open");
            }
            if (_panel != null)
            {
                _panel.RemoveFromClassList("settings-panel--open");
            }

            if (_isRegisteredForLocalization && _localizationService != null)
            {
                _localizationService.UnregisterListener(this);
                _isRegisteredForLocalization = false;
            }
        }

        /// <summary>
        /// ILocalizationListener — rebuilds all panel text in place on a language switch.
        /// Reuses BuildUI() (idempotent) rather than updating each Label individually, but
        /// restores the active tab afterward since BuildUI() otherwise always lands on tab 0.
        /// </summary>
        public void OnLanguageChanged()
        {
            if (!_uiBuilt) return;

            int previousTabIndex = _activeTabIndex;
            BuildUI();
            SwitchTab(previousTabIndex);
        }

        private string GetString(string key, string fallback)
        {
            return _localizationService != null ? _localizationService.GetString(key, fallback) : fallback;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            DisposeBindings();
        }

        #endregion

        #region UI Construction

        private void BuildUI()
        {
            if (document == null)
            {
                Debug.LogWarning("[SettingsPanel] UIDocument not assigned!");
                return;
            }

            _tabButtons.Clear();
            _tabContents.Clear();
            DisposeBindings();

            var root = document.rootVisualElement;
            root.Clear();

            if (_settingsStyleSheet != null)
            {
                root.styleSheets.Add(_settingsStyleSheet);
            }

            // Apply theme
            _themeService?.ApplyTheme(root);

            // Backdrop — clicking it closes the panel
            _backdrop = new VisualElement()
                .Classes("settings-backdrop");

            // Click-away: only close when clicking on the backdrop itself, not the panel
            _backdrop.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == _backdrop)
                {
                    SaveAndClose();
                }
            });

            // Panel container (left-aligned drawer)
            _panel = Layout.Column("SettingsPanel")
                .Classes("settings-panel");

            // Initial hidden state is handled by USS:
            //   .settings-backdrop  { opacity: 0; transition: opacity ... }
            //   .settings-panel     { translate: -100% 0; transition: translate ... }
            // Adding --open classes triggers the CSS transitions.

            // --- Header ---
            _panel.Add(BuildHeader());

            // --- Tab Bar ---
            _panel.Add(BuildTabBar());

            // --- Content Area ---
            var contentArea = new ScrollView(ScrollViewMode.Vertical)
                .Classes("settings-content", "settings-scroll");

            var audioContent = BuildAudioContent();
            var videoContent = BuildVideoContent();
            var controlsContent = BuildControlsContent();
            var languageContent = BuildLanguageContent();
            var accessibilityContent = BuildAccessibilityContent();

            _tabContents.Add(audioContent);
            _tabContents.Add(videoContent);
            _tabContents.Add(controlsContent);
            _tabContents.Add(languageContent);
            _tabContents.Add(accessibilityContent);

            contentArea.Add(audioContent);
            contentArea.Add(videoContent);
            contentArea.Add(controlsContent);
            contentArea.Add(languageContent);
            contentArea.Add(accessibilityContent);

            _panel.Add(contentArea);

            // --- Footer ---
            _panel.Add(BuildFooter());

            // --- Drop shadow on right edge ---
            var shadow = new VisualElement();
            shadow.AddToClassList("settings-panel-shadow");
            _panel.Add(shadow);

            _backdrop.Add(_panel);
            root.Add(_backdrop);

            // Activate the first tab
            SwitchTab(0);
        }

        #endregion

        #region Animation Hooks

        /// <summary>
        /// CSS-transition-driven open animation.
        /// The drawer pattern requires two distinct animations (backdrop fade + panel slide)
        /// on separate elements, which doesn't fit the single-element UIToolkitAnimationComponent
        /// model. Instead we toggle USS classes and let CSS transitions handle the visuals.
        /// </summary>
        protected override async UniTask OnOpenAnimatedAsync(CancellationToken cancellationToken)
        {
            // Schedule the class additions for the next frame so UIToolkit
            // registers the transition from the initial state.
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);

            _backdrop?.AddToClassList("settings-backdrop--open");
            _panel?.AddToClassList("settings-panel--open");

            // Wait for the longest transition to complete (panel slide = 0.35s)
            await UniTask.Delay(350, ignoreTimeScale: true, cancellationToken: cancellationToken);
        }

        protected override async UniTask OnCloseAnimatedAsync(CancellationToken cancellationToken)
        {
            _backdrop?.RemoveFromClassList("settings-backdrop--open");
            _panel?.RemoveFromClassList("settings-panel--open");

            // Wait for transitions to finish before deactivating
            await UniTask.Delay(350, ignoreTimeScale: true, cancellationToken: cancellationToken);
        }

        private VisualElement BuildHeader()
        {
            var header = Layout.Row("SettingsHeader")
                .Classes("settings-header");

            header.Add(
                new Label(GetString("SETTINGS_Title", "Settings"))
                    .Classes("settings-title")
            );

            var closeBtn = new Button(SaveAndClose) { text = "✕" };
            closeBtn.AddToClassList("settings-close-btn");
            closeBtn.focusable = true;

            header.Add(closeBtn);
            return header;
        }

        private VisualElement BuildTabBar()
        {
            var tabBar = Layout.Row("TabBar")
                .Classes("settings-tab-bar");

            // (internal id, localization key, English fallback) — the id is used for the
            // element name only, so it stays stable regardless of the current language.
            var tabs = new[]
            {
                ("Audio", "SETTINGS_TabAudio", "Audio"),
                ("Video", "SETTINGS_TabVideo", "Video"),
                ("Controls", "SETTINGS_TabControls", "Controls"),
                ("Language", "SETTINGS_TabLanguage", "Language"),
                ("Accessibility", "SETTINGS_TabAccessibility", "Accessibility"),
            };

            for (int i = 0; i < tabs.Length; i++)
            {
                int tabIndex = i; // Closure capture
                var (id, key, fallback) = tabs[i];
                var tab = new Button(() => SwitchTab(tabIndex)) { text = GetString(key, fallback) };
                tab.AddToClassList("settings-tab");
                tab.focusable = true;
                tab.name = $"Tab_{id}";

                _tabButtons.Add(tab);
                tabBar.Add(tab);
            }

            return tabBar;
        }

        private VisualElement BuildFooter()
        {
            var footer = Layout.Row("SettingsFooter")
                .Classes("settings-footer");

            var resetBtn = new Button(ResetToDefaults) { text = GetString("SETTINGS_ResetToDefaults", "Reset to Defaults") };
            resetBtn.AddToClassList("settings-reset-btn");
            resetBtn.focusable = true;
            resetBtn.name = "Btn_ResetToDefaults";

            var saveBtn = new Button(SaveAndClose) { text = GetString("SETTINGS_SaveAndClose", "Save & Close") };
            saveBtn.AddToClassList("settings-save-btn");
            saveBtn.focusable = true;
            saveBtn.name = "Btn_SaveAndClose";

            footer.Add(resetBtn);
            footer.Add(saveBtn);
            return footer;
        }

        #endregion

        #region Tab Content — Audio

        private VisualElement BuildAudioContent()
        {
            var container = Layout.Column("AudioContent")
                .Classes("settings-tab-content");

            container.Add(new Label(GetString("SETTINGS_AudioSectionHeader", "Volume")).Classes("settings-section-header"));

            if (_settingsService != null)
            {
                container.Add(BuildVolumeSlider("MasterVolume", GetString("SETTINGS_MasterVolume", "Master Volume"), _settingsService.MasterVolume));
                container.Add(BuildVolumeSlider("MusicVolume", GetString("SETTINGS_MusicVolume", "Music Volume"), _settingsService.MusicVolume));
                container.Add(BuildVolumeSlider("SfxVolume", GetString("SETTINGS_SfxVolume", "SFX Volume"), _settingsService.SfxVolume));
            }
            else
            {
                container.Add(BuildPlaceholder(GetString("SETTINGS_AudioUnavailable", "Audio settings unavailable.")));
            }

            return container;
        }

        private VisualElement BuildVolumeSlider(string id, string label, Bindable<float> bindable)
        {
            var row = Layout.Row()
                .Classes("settings-row");

            row.Add(new Label(label).Classes("settings-label"));

            var slider = new Slider(0f, 1f)
                .Classes("settings-slider");
            slider.focusable = true;
            slider.name = $"Slider_{id}";

            var valueLabel = new Label(FormatPercent(bindable.Value))
                .Classes("settings-value-label");

            // Two-way binding: slider ↔ Bindable.
            // Done manually (rather than via BindValueTwoWay(gameObject, ...)) so the
            // subscription lands in _bindings and gets disposed on every BuildUI() rebuild —
            // the GameObject-lifecycle helper otherwise leaks one subscription per panel open.
            var sub = bindable.Bind(v =>
            {
                slider.SetValueWithoutNotify(v);
                valueLabel.text = FormatPercent(v);
            });
            _bindings.Add(sub);
            slider.RegisterValueChangedCallback(evt => bindable.Value = evt.newValue);

            row.Add(slider);
            row.Add(valueLabel);
            return row;
        }

        #endregion

        #region Tab Content — Video

        // Internal QualitySettings level names (project-defined, e.g. "Mobile"/"PC") relabeled for
        // display — a desktop player seeing "Mobile" reads as broken even though it's a legitimate
        // low tier. Falls back to the raw name for any level not covered here.
        private static readonly Dictionary<string, string> s_QualityDisplayNames = new()
        {
            { "Mobile", "Low" },
            { "PC", "High" },
        };

        private VisualElement BuildVideoContent()
        {
            var container = Layout.Column("VideoContent")
                .Classes("settings-tab-content");

            container.Add(new Label(GetString("SETTINGS_VideoSectionHeader", "Display")).Classes("settings-section-header"));

            if (_videoSettingsService != null)
            {
                container.Add(BuildResolutionDropdown());
                container.Add(BuildFullscreenToggle());
                container.Add(BuildQualityDropdown());
                container.Add(BuildVSyncToggle());
            }
            else
            {
                container.Add(BuildPlaceholder(GetString("SETTINGS_VideoUnavailable", "Video settings unavailable.")));
            }

            return container;
        }

        private VisualElement BuildResolutionDropdown()
        {
            var row = Layout.Row().Classes("settings-row");
            row.Add(new Label(GetString("SETTINGS_Resolution", "Resolution")).Classes("settings-label"));

            var choices = _videoSettingsService.AvailableResolutions
                .Select(r => $"{r.width} x {r.height}")
                .ToList();

            var dropdown = new DropdownField(choices, _videoSettingsService.ResolutionIndex.Value)
                .Classes("settings-dropdown");
            dropdown.focusable = true;
            dropdown.name = "Dropdown_Resolution";

            var sub = _videoSettingsService.ResolutionIndex.Bind((int i) =>
            {
                if (i >= 0 && i < choices.Count) dropdown.SetValueWithoutNotify(choices[i]);
            });
            _bindings.Add(sub);
            dropdown.RegisterValueChangedCallback(evt =>
            {
                int idx = choices.IndexOf(evt.newValue);
                if (idx >= 0) _videoSettingsService.ResolutionIndex.Value = idx;
            });

            row.Add(dropdown);
            return row;
        }

        private VisualElement BuildFullscreenToggle()
        {
            return BuildBoolToggle(
                "Toggle_Fullscreen",
                GetString("SETTINGS_Fullscreen", "Fullscreen"),
                _videoSettingsService.Fullscreen);
        }

        private VisualElement BuildQualityDropdown()
        {
            var row = Layout.Row().Classes("settings-row");
            row.Add(new Label(GetString("SETTINGS_Quality", "Quality")).Classes("settings-label"));

            var internalNames = _videoSettingsService.QualityLevelNames;
            var choices = internalNames
                .Select(n => GetString($"SETTINGS_Quality_{n}", s_QualityDisplayNames.GetValueOrDefault(n, n)))
                .ToList();

            var dropdown = new DropdownField(choices, _videoSettingsService.QualityLevel.Value)
                .Classes("settings-dropdown");
            dropdown.focusable = true;
            dropdown.name = "Dropdown_Quality";

            var sub = _videoSettingsService.QualityLevel.Bind((int i) =>
            {
                if (i >= 0 && i < choices.Count) dropdown.SetValueWithoutNotify(choices[i]);
            });
            _bindings.Add(sub);
            dropdown.RegisterValueChangedCallback(evt =>
            {
                int idx = choices.IndexOf(evt.newValue);
                if (idx >= 0) _videoSettingsService.QualityLevel.Value = idx;
            });

            row.Add(dropdown);
            return row;
        }

        private VisualElement BuildVSyncToggle()
        {
            return BuildBoolToggle(
                "Toggle_VSync",
                GetString("SETTINGS_VSync", "V-Sync"),
                _videoSettingsService.VSync);
        }

        private VisualElement BuildBoolToggle(string id, string label, Bindable<bool> bindable)
        {
            var row = Layout.Row().Classes("settings-row");
            row.Add(new Label(label).Classes("settings-label"));

            var toggle = new Toggle().Classes("settings-toggle");
            toggle.focusable = true;
            toggle.name = id;

            var sub = bindable.Bind((bool v) => toggle.SetValueWithoutNotify(v));
            _bindings.Add(sub);
            toggle.RegisterValueChangedCallback(evt => bindable.Value = evt.newValue);

            row.Add(toggle);
            return row;
        }

        #endregion

        #region Tab Content — Controls

        private VisualElement BuildControlsContent()
        {
            var container = Layout.Column("ControlsContent")
                .Classes("settings-tab-content");

            container.Add(new Label(GetString("SETTINGS_ControlsSectionHeader", "Key Bindings")).Classes("settings-section-header"));

            if (_rebindService != null && _inputReader != null && _inputReader.Actions != null)
            {
                // Detect active scheme based on connected devices
                string activeScheme = Gamepad.current != null ? "Gamepad" : "Keyboard&Mouse";

                var playerMap = _inputReader.Actions.FindActionMap("Player");
                if (playerMap != null)
                {
                    foreach (var action in playerMap.actions)
                    {
                        bool actionAdded = false;
                        for (int i = 0; i < action.bindings.Count; i++)
                        {
                            var binding = action.bindings[i];
                            
                            if (binding.isComposite)
                            {
                                bool schemeMatch = false;
                                for (int j = i + 1; j < action.bindings.Count && action.bindings[j].isPartOfComposite; j++)
                                {
                                    if (action.bindings[j].groups != null && action.bindings[j].groups.Contains(activeScheme))
                                    {
                                        schemeMatch = true;
                                        break;
                                    }
                                }

                                if (schemeMatch)
                                {
                                    var addedParts = new HashSet<string>();
                                    for (int j = i + 1; j < action.bindings.Count && action.bindings[j].isPartOfComposite; j++)
                                    {
                                        if (action.bindings[j].groups != null && action.bindings[j].groups.Contains(activeScheme))
                                        {
                                            string pName = action.bindings[j].name;
                                            if (!addedParts.Contains(pName))
                                            {
                                                container.Add(BuildRebindRow(action.name, j, pName));
                                                addedParts.Add(pName);
                                            }
                                        }
                                    }
                                    actionAdded = true;
                                }
                                
                                while (i + 1 < action.bindings.Count && action.bindings[i + 1].isPartOfComposite) i++;
                            }
                            else if (binding.groups != null && binding.groups.Contains(activeScheme))
                            {
                                container.Add(BuildRebindRow(action.name, i));
                                actionAdded = true;
                            }

                            if (actionAdded) break;
                        }
                    }
                }
            }
            else
            {
                container.Add(BuildPlaceholder(GetString("SETTINGS_ControlsUnavailable", "Controls settings unavailable.")));
            }

            return container;
        }

        private VisualElement BuildRebindRow(string actionName, int bindingIndex, string partName = null)
        {
            var row = Layout.Row()
                .Classes("rebind-row");

            // Friendly action name (split PascalCase into words)
            string displayName = System.Text.RegularExpressions.Regex.Replace(
                actionName, "([a-z])([A-Z])", "$1 $2");

            if (!string.IsNullOrEmpty(partName))
            {
                string partDisplay = System.Text.RegularExpressions.Regex.Replace(
                    partName, "([a-z])([A-Z])", "$1 $2");
                if (partDisplay.Length > 0)
                    partDisplay = char.ToUpper(partDisplay[0]) + partDisplay.Substring(1);
                
                displayName = $"{displayName} {partDisplay}";
            }

            row.Add(new Label(displayName).Classes("rebind-action"));

            string currentBinding = _rebindService.GetBindingDisplayString(actionName, bindingIndex);
            if (string.IsNullOrEmpty(currentBinding))
            {
                currentBinding = "—";
            }

            var keyBtn = new Button() { text = currentBinding };
            keyBtn.AddToClassList("rebind-key-btn");
            keyBtn.focusable = true;
            keyBtn.name = $"Rebind_{actionName}";

            keyBtn.clicked += () =>
            {
                keyBtn.text = GetString("SETTINGS_PressAKey", "Press a key...");
                keyBtn.AddToClassList("rebind-key-btn--listening");

                _rebindService.StartRebind(
                    actionName,
                    bindingIndex,
                    onComplete: newBinding =>
                    {
                        keyBtn.text = newBinding;
                        keyBtn.RemoveFromClassList("rebind-key-btn--listening");
                    },
                    onCancel: () =>
                    {
                        keyBtn.text = currentBinding;
                        keyBtn.RemoveFromClassList("rebind-key-btn--listening");
                    }
                );
            };

            row.Add(keyBtn);
            return row;
        }

        #endregion

        #region Tab Content — Language

        private VisualElement BuildLanguageContent()
        {
            var container = Layout.Column("LanguageContent")
                .Classes("settings-tab-content");

            container.Add(new Label(GetString("SETTINGS_LanguageSectionHeader", "Language")).Classes("settings-section-header"));

            _languageContainer = Layout.Column("LanguageOptions");

            if (_localizationService != null)
            {
                var settings = _localizationService.Settings;
                if (settings != null)
                {
                    foreach (var langInfo in settings.AvailableLanguages)
                    {
                        _languageContainer.Add(BuildLanguageOption(langInfo));
                    }
                }
            }
            else
            {
                _languageContainer.Add(BuildPlaceholder(GetString("SETTINGS_LanguageUnavailable", "Language settings unavailable.")));
            }

            container.Add(_languageContainer);

            return container;
        }

        private VisualElement BuildLanguageOption(LanguageInfo langInfo)
        {
            bool isSelected = _localizationService != null &&
                              _localizationService.CurrentLanguage == langInfo.Language;

            var option = new Button(() => SelectLanguage(langInfo))
                .Classes("language-option");
            option.focusable = true;
            option.name = $"Lang_{langInfo.LanguageCode}";

            if (isSelected)
            {
                option.AddToClassList("language-option--selected");
            }

            option.Add(new Label(langInfo.DisplayName).Classes("language-name"));

            if (isSelected)
            {
                option.Add(new Label("✓").Classes("language-check"));
            }

            return option;
        }

        private void SelectLanguage(LanguageInfo langInfo)
        {
            if (_localizationService == null) return;

            _localizationService.SetLanguage(langInfo.Language);

            // Also update the SettingsService language bindable
            if (_settingsService != null)
            {
                _settingsService.Language.Value = langInfo.LanguageCode;
            }

            // Rebuild language options to reflect the new selection
            RefreshLanguageOptions();
        }

        private void RefreshLanguageOptions()
        {
            if (_languageContainer == null || _localizationService == null) return;

            _languageContainer.Clear();

            var settings = _localizationService.Settings;
            if (settings != null)
            {
                foreach (var langInfo in settings.AvailableLanguages)
                {
                    _languageContainer.Add(BuildLanguageOption(langInfo));
                }
            }
        }

        #endregion

        #region Tab Content — Accessibility

        /// <summary>
        /// v1 scope is deliberately limited to the two levers with a real hook in the codebase
        /// today (PanelSettings.scale, ITypewriterEffect.CharactersPerSecond). Colorblind mode /
        /// high-contrast UI need a new theming mechanism first (IThemeService has no accessibility
        /// hooks) and are intentionally not promised here.
        /// </summary>
        private VisualElement BuildAccessibilityContent()
        {
            var container = Layout.Column("AccessibilityContent")
                .Classes("settings-tab-content");

            container.Add(new Label(GetString("SETTINGS_AccessibilitySectionHeader", "Accessibility")).Classes("settings-section-header"));

            if (_accessibilityService != null)
            {
                container.Add(BuildUIScaleSlider());
                container.Add(BuildTextSpeedSlider());
            }
            else
            {
                container.Add(BuildPlaceholder(GetString("SETTINGS_AccessibilityUnavailable", "Accessibility settings unavailable.")));
            }

            return container;
        }

        private VisualElement BuildUIScaleSlider()
        {
            var row = Layout.Row().Classes("settings-row");
            row.Add(new Label(GetString("SETTINGS_UIScale", "UI Scale")).Classes("settings-label"));

            var slider = new Slider(0.75f, 1.5f).Classes("settings-slider");
            slider.focusable = true;
            slider.name = "Slider_UIScale";

            var valueLabel = new Label(FormatScale(_accessibilityService.UIScale.Value))
                .Classes("settings-value-label");

            var sub = _accessibilityService.UIScale.Bind((float v) =>
            {
                slider.SetValueWithoutNotify(v);
                valueLabel.text = FormatScale(v);
            });
            _bindings.Add(sub);
            slider.RegisterValueChangedCallback(evt => _accessibilityService.UIScale.Value = evt.newValue);

            row.Add(slider);
            row.Add(valueLabel);
            return row;
        }

        private VisualElement BuildTextSpeedSlider()
        {
            var row = Layout.Row().Classes("settings-row");
            row.Add(new Label(GetString("SETTINGS_TextSpeed", "Text Speed")).Classes("settings-label"));

            var slider = new Slider(10f, 60f).Classes("settings-slider");
            slider.focusable = true;
            slider.name = "Slider_TextSpeed";

            var valueLabel = new Label(FormatCps(_accessibilityService.TextSpeed.Value))
                .Classes("settings-value-label");

            var sub = _accessibilityService.TextSpeed.Bind((float v) =>
            {
                slider.SetValueWithoutNotify(v);
                valueLabel.text = FormatCps(v);
            });
            _bindings.Add(sub);
            slider.RegisterValueChangedCallback(evt => _accessibilityService.TextSpeed.Value = evt.newValue);

            row.Add(slider);
            row.Add(valueLabel);
            return row;
        }

        #endregion

        #region Tab Switching

        private void SwitchTab(int index)
        {
            if (index < 0 || index >= _tabContents.Count) return;

            _activeTabIndex = index;

            // Update tab button styles
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                if (i == index)
                {
                    _tabButtons[i].AddToClassList("settings-tab--active");
                }
                else
                {
                    _tabButtons[i].RemoveFromClassList("settings-tab--active");
                }
            }

            // Show/hide content
            for (int i = 0; i < _tabContents.Count; i++)
            {
                _tabContents[i].style.display = (i == index)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }

            // Focus first interactable element in the active tab
            _tabContents[index].FocusFirstInteractableOnLayout();
        }

        #endregion

        #region Actions

        private void SaveAndClose()
        {
            // SettingsService auto-persists on value change, so no explicit save needed.
            // Just close the overlay.
            CloseOverlay();
        }

        private void ResetToDefaults()
        {
            if (_settingsService != null)
            {
                _settingsService.MasterVolume.Value = 1f;
                _settingsService.MusicVolume.Value = 1f;
                _settingsService.SfxVolume.Value = 1f;
            }

            if (_videoSettingsService != null)
            {
                // Mutates the same Bindables the dropdowns/toggles are already subscribed to,
                // so their SetValueWithoutNotify callbacks fire automatically — no manual UI
                // refresh needed here, unlike Controls below.
                _videoSettingsService.ResetToDefaults();
            }

            if (_accessibilityService != null)
            {
                _accessibilityService.UIScale.Value = AccessibilityService.DefaultUIScale;
                _accessibilityService.TextSpeed.Value = AccessibilityService.DefaultTextSpeed;
            }

            if (_rebindService != null)
            {
                _rebindService.ResetAllBindings();
                // Rebuild controls tab to show default bindings
                RebuildControlsTab();
            }

            if (_localizationService != null)
            {
                var defaultLang = _localizationService.Settings?.DefaultLanguage
                                  ?? SystemLanguage.English;
                _localizationService.SetLanguage(defaultLang);

                // Keep the persisted SettingsService.Language in sync, same as SelectLanguage() —
                // otherwise the reset language doesn't survive a relaunch.
                if (_settingsService != null)
                {
                    var defaultLangInfo = _localizationService.Settings?.AvailableLanguages
                        .FirstOrDefault(l => l.Language == defaultLang);
                    if (defaultLangInfo != null)
                    {
                        _settingsService.Language.Value = defaultLangInfo.LanguageCode;
                    }
                }

                RefreshLanguageOptions();
            }
        }

        private void RebuildControlsTab()
        {
            // Find and rebuild the controls content
            int controlsIndex = 2; // Controls is the third tab (index 2)
            if (controlsIndex < _tabContents.Count)
            {
                var parent = _tabContents[controlsIndex].parent;
                int childIndex = parent.IndexOf(_tabContents[controlsIndex]);
                _tabContents[controlsIndex].RemoveFromHierarchy();
                _tabContents.RemoveAt(controlsIndex);

                var newControls = BuildControlsContent();
                _tabContents.Insert(controlsIndex, newControls);
                parent.Insert(childIndex, newControls);

                if (_activeTabIndex == controlsIndex)
                {
                    SwitchTab(controlsIndex);
                }
            }
        }


        #endregion

        #region Helpers

        private static VisualElement BuildPlaceholder(string message)
        {
            var placeholder = Layout.Column()
                .Classes("settings-placeholder");

            placeholder.Add(
                new Label(message).Classes("settings-placeholder-text")
            );

            return placeholder;
        }

        private static string FormatPercent(float value)
        {
            return $"{Mathf.RoundToInt(value * 100)}%";
        }

        private static string FormatScale(float value)
        {
            return $"{value:0.00}x";
        }

        private static string FormatCps(float value)
        {
            return $"{Mathf.RoundToInt(value)} cps";
        }

        private void DisposeBindings()
        {
            foreach (var binding in _bindings)
            {
                binding?.Dispose();
            }
            _bindings.Clear();
        }

        #endregion
    }
}
