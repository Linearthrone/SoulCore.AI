using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using House.ChatDesktop.Services;
using SoulCore.Core.Persona;

namespace House.ChatDesktop;

public partial class PersonaWizardWindow : Window
{
    private readonly SoulCorePersonaClient _client;
    private readonly bool _isCreate;
    private readonly PersonaPackInfo? _editTarget;
    private readonly PersonaTraitScalesInfo _traits = new();
    private readonly List<StackPanel> _groupPanels = new();
    private readonly List<TraitSliderRow> _rows = new();
    private IReadOnlyList<PersonaWizardStep> _steps = Array.Empty<PersonaWizardStep>();
    private IReadOnlyList<PersonaPackInfo> _hostPacks;

    private int _step;
    private PersonaTemplateInfo? _selectedTemplate;
    private bool _personaIdTouched;
    private bool _suggestingPersonaId;
    private bool _saving;
    private bool _drafting;
    private bool _retrying;
    private bool _loadingModels;
    private bool _bindingModelCombo;
    private string? _selectedInferenceModel;
    private string? _hostDefaultModel;
    private string? _resolvedForThisPack;

    public PersonaPackInfo? SavedPersona { get; private set; }
    public string? ResultNotice { get; private set; }

    /// <summary>Avalonia / design-time loader entry.</summary>
    public PersonaWizardWindow()
        : this(new SoulCorePersonaClient(), Array.Empty<PersonaPackInfo>(), editTarget: null)
    {
    }

    public PersonaWizardWindow(
        SoulCorePersonaClient client,
        IReadOnlyList<PersonaPackInfo> hostPacks,
        PersonaPackInfo? editTarget = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _hostPacks = hostPacks ?? Array.Empty<PersonaPackInfo>();
        _editTarget = editTarget;
        _isCreate = editTarget is null;

        InitializeComponent();
        PhysicalDescriptionBox.MaxLength = PersonaPack.PhysicalDescriptionMaxLength;
        BuildTraitSliders();
        PointerPressed += Wizard_PointerPressed;

        Title = _isCreate ? "Create persona" : "Edit persona";
        TitleText.Text = Title;
        _steps = PersonaWizardLogic.BuildWizardSteps(_isCreate);

        PersonaIdBox.TextChanged += (_, _) =>
        {
            if (!_suggestingPersonaId)
                _personaIdTouched = true;
        };

        if (_isCreate)
        {
            BindHostTemplates();
            _selectedInferenceModel = null;
            _resolvedForThisPack = null;
            _step = 0;
        }
        else
        {
            LoadEditTarget(_editTarget!);
            _step = 0;
        }

        BindModelPicker(Array.Empty<string>());
        ShowStep();
        _ = LoadInferenceModelsAsync();
    }

    private void Wizard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual visual)
        {
            var current = visual;
            while (current is not null)
            {
                if (current is Button or TextBox or Slider or ComboBox or ListBox or ScrollViewer)
                    return;
                current = current.GetVisualParent();
            }
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        BeginMoveDrag(e);
        if (DrawerDismiss.IsMostlyOffScreen(this))
            Close();
    }

    private void BuildTraitSliders()
    {
        TraitSliderHost.Children.Clear();
        _groupPanels.Clear();
        _rows.Clear();

        foreach (var group in PersonaWizardLogic.TraitGroups)
        {
            var panel = new StackPanel { Spacing = 8, IsVisible = false };
            foreach (var trait in group.Traits)
                panel.Children.Add(CreateTraitRow(trait));
            _groupPanels.Add(panel);
            TraitSliderHost.Children.Add(panel);
        }
    }

    private Control CreateTraitRow(PersonaTraitDefinition trait)
    {
        var label = new TextBlock
        {
            Text = trait.Label,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        var badge = new Border
        {
            Padding = new Thickness(6, 1),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Gray,
            IsVisible = true,
            Child = new TextBlock
            {
                Text = "Default",
                FontSize = 10
            }
        };
        var header = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(badge, Dock.Right);
        header.Children.Add(badge);
        header.Children.Add(label);

        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            Value = trait.Default,
            Margin = new Thickness(0, 0, 8, 0)
        };
        var valueText = new TextBlock
        {
            Text = trait.Default.ToString("0.00", CultureInfo.InvariantCulture),
            Width = 40,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var bar = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        Grid.SetColumn(valueText, 1);
        bar.Children.Add(slider);
        bar.Children.Add(valueText);

        var row = new TraitSliderRow(trait, slider, valueText, badge);
        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property != Slider.ValueProperty)
                return;
            _traits.Write(trait, slider.Value);
            UpdateRowChrome(row);
            RefreshBandPreview();
        };
        _rows.Add(row);

        return new StackPanel
        {
            Spacing = 2,
            Children = { header, bar }
        };
    }

    private static void UpdateRowChrome(TraitSliderRow row)
    {
        row.ValueText.Text = row.Slider.Value.ToString("0.00", CultureInfo.InvariantCulture);
        row.Badge.IsVisible = row.Trait.IsDefault(row.Slider.Value);
    }

    private void BindHostTemplates()
    {
        var templates = PersonaWizardLogic.BuildTemplates(_hostPacks);
        TemplateList.ItemsSource = templates;
        TemplateList.SelectedItem = templates.FirstOrDefault();
        _selectedTemplate = TemplateList.SelectedItem as PersonaTemplateInfo;

        var blocked = templates.Count == 0;
        HostRequiredText.IsVisible = blocked;
        RetryTemplatesButton.IsVisible = blocked;
        TemplateList.IsVisible = !blocked;
        if (blocked)
            StatusText.Text = PersonaWizardLogic.HostGalleryRequiredMessage;
    }

    private void LoadEditTarget(PersonaPackInfo pack)
    {
        DisplayNameBox.Text = pack.DisplayName;
        PersonaIdBox.Text = pack.PersonaId;
        PersonaIdBox.IsReadOnly = true;
        HumanAddressBox.Text = pack.HumanAddress ?? "Friend";
        IdentityBlurbBox.Text = pack.IdentityBlurb ?? "";
        PhysicalDescriptionBox.Text = pack.PhysicalDescription ?? "";
        _selectedInferenceModel = PersonaWizardLogic.NormalizeInferenceModel(pack.InferenceModel);
        _resolvedForThisPack = PersonaWizardLogic.NormalizeInferenceModel(pack.ResolvedInferenceModel);
        ApplyTraits(pack.Traits ?? new PersonaTraitScalesInfo());
        _personaIdTouched = true;
        RefreshModelHint();
        RefreshDescriptionCount();
    }

    private async Task LoadInferenceModelsAsync()
    {
        if (_loadingModels)
            return;

        _loadingModels = true;
        try
        {
            var snap = await _client.ListInferenceModelsAsync().ConfigureAwait(true);
            _hostDefaultModel = snap.HostModel;
            if (string.IsNullOrWhiteSpace(_resolvedForThisPack))
            {
                _resolvedForThisPack = PersonaWizardLogic.NormalizeInferenceModel(_selectedInferenceModel)
                                       ?? snap.ResolvedInferenceModel
                                       ?? snap.HostModel;
            }

            BindModelPicker(snap.Reachable ? snap.Models : Array.Empty<string>());
            if (!snap.Reachable && InferenceModelHintText is not null)
            {
                InferenceModelHintText.Text =
                    "Model list unavailable (Host unreachable). Blank still saves as Host default.";
            }
        }
        finally
        {
            _loadingModels = false;
        }
    }

    private void BindModelPicker(IReadOnlyList<string> models)
    {
        if (InferenceModelCombo is null)
            return;

        var options = PersonaWizardLogic.BuildModelPickerOptions(
            models,
            _selectedInferenceModel,
            _hostDefaultModel);
        _bindingModelCombo = true;
        try
        {
            InferenceModelCombo.ItemsSource = options;
            InferenceModelCombo.SelectedItem =
                PersonaWizardLogic.FindModelOption(options, _selectedInferenceModel);
        }
        finally
        {
            _bindingModelCombo = false;
        }

        RefreshModelHint();
    }

    private void InferenceModelCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_bindingModelCombo)
            return;

        if (InferenceModelCombo.SelectedItem is InferenceModelOption opt)
            _selectedInferenceModel = PersonaWizardLogic.NormalizeInferenceModel(opt.ModelId);
        else
            _selectedInferenceModel = null;

        _resolvedForThisPack = _selectedInferenceModel
                               ?? _hostDefaultModel
                               ?? _resolvedForThisPack;
        RefreshModelHint();
    }

    private void RefreshModelHint()
    {
        if (InferenceModelHintText is null)
            return;
        InferenceModelHintText.Text = PersonaWizardLogic.FormatModelResolutionHint(
            _selectedInferenceModel,
            _resolvedForThisPack,
            _hostDefaultModel);
    }

    private void TemplateList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedTemplate = TemplateList.SelectedItem as PersonaTemplateInfo;
    }

    private async void RetryTemplates_Click(object? sender, RoutedEventArgs e)
    {
        if (_retrying || !_isCreate)
            return;

        _retrying = true;
        StatusText.Text = "Loading Host packs…";
        RetryTemplatesButton.IsEnabled = false;
        try
        {
            var list = await _client.ListAsync().ConfigureAwait(true);
            if (!list.Reachable)
            {
                StatusText.Text = list.Detail ?? PersonaWizardLogic.HostGalleryRequiredMessage;
                return;
            }

            _hostPacks = list.Personas;
            BindHostTemplates();
            if (PersonaWizardLogic.BuildTemplates(_hostPacks).Count > 0)
                StatusText.Text = "";
        }
        finally
        {
            _retrying = false;
            RetryTemplatesButton.IsEnabled = true;
        }
    }

    private void DisplayNameBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_isCreate || _personaIdTouched || PersonaIdBox.IsReadOnly)
            return;
        _suggestingPersonaId = true;
        try
        {
            PersonaIdBox.Text = PersonaWizardLogic.SuggestPersonaId(DisplayNameBox.Text);
        }
        finally
        {
            _suggestingPersonaId = false;
        }
    }

    private void PhysicalDescriptionBox_TextChanged(object? sender, TextChangedEventArgs e) =>
        RefreshDescriptionCount();

    private void RefreshDescriptionCount()
    {
        if (PhysicalDescriptionCount is null || PhysicalDescriptionBox is null)
            return;
        var len = PhysicalDescriptionBox.Text?.Length ?? 0;
        PhysicalDescriptionCount.Text = $"{len}/{PersonaPack.PhysicalDescriptionMaxLength}";
    }

    private async void DescriptionHelp_Click(object? sender, RoutedEventArgs e)
    {
        if (_drafting || _saving)
            return;

        var decision = PersonaWizardLogic.DecideDescriptionDraft(personaAlreadyOnHost: !_isCreate);
        if (!decision.CallRoute)
        {
            StatusText.Text = decision.Status ?? PersonaWizardLogic.DescriptionDraftNeedsSavedPersona;
            return;
        }

        _drafting = true;
        DescriptionHelpButton.IsEnabled = false;
        var current = CurrentDraftFields();
        try
        {
            StatusText.Text = "Drafting physical description…";
            var draft = await _client.DraftPhysicalDescriptionAsync(_editTarget!.PersonaId).ConfigureAwait(true);
            var applied = PersonaWizardLogic.ApplyDescriptionDraft(
                current,
                draftSucceeded: draft.Ok,
                draftText: draft.Ok ? draft.Text : null);
            PhysicalDescriptionBox.Text = applied.PhysicalDescription;
            IdentityBlurbBox.Text = applied.IdentityBlurb ?? "";
            StatusText.Text = draft.Ok
                ? "Draft filled the physical description. Save to keep it."
                : draft.Detail ?? "Description draft failed. The box was left unchanged.";
        }
        finally
        {
            _drafting = false;
            DescriptionHelpButton.IsEnabled = true;
        }
    }

    private DescriptionDraftFields CurrentDraftFields()
    {
        var policy = _editTarget?.ToolPolicy;
        return new DescriptionDraftFields
        {
            IdentityBlurb = IdentityBlurbBox.Text,
            PhysicalDescription = PhysicalDescriptionBox.Text ?? "",
            AllowDesktop = policy?.AllowDesktop ?? true,
            AllowBrowser = policy?.AllowBrowser ?? true,
            AllowEmail = policy?.AllowEmail ?? true
        };
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    private void Back_Click(object? sender, RoutedEventArgs e)
    {
        if (_saving || _drafting)
            return;
        if (_step <= 0)
            return;
        _step--;
        ShowStep();
    }

    private void Next_Click(object? sender, RoutedEventArgs e)
    {
        if (_saving || _drafting)
            return;
        if (_step >= _steps.Count - 1)
            return;

        var kind = _steps[_step].Kind;
        if (kind == PersonaWizardStepKind.Template)
        {
            if (TemplateList.SelectedItem is not PersonaTemplateInfo template)
            {
                StatusText.Text = PersonaWizardLogic.BuildTemplates(_hostPacks).Count == 0
                    ? PersonaWizardLogic.HostGalleryRequiredMessage
                    : "Select a Host template (Blank / Mentor / Analyst when seeded).";
                return;
            }

            _selectedTemplate = template;
            ApplyTemplateDefaults(template);
        }
        else if (kind == PersonaWizardStepKind.Identity)
        {
            if (!ValidateIdentity(out var error))
            {
                StatusText.Text = error;
                return;
            }
        }

        _step++;
        ShowStep();
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (_saving || _drafting)
            return;
        if (!ValidateIdentity(out var error))
        {
            StatusText.Text = error;
            return;
        }

        _saving = true;
        StatusText.Text = "Saving to Host…";
        SaveButton.IsEnabled = false;
        NextButton.IsEnabled = false;
        BackButton.IsEnabled = false;

        try
        {
            var traits = ReadTraits();
            var description = PhysicalDescriptionBox.Text ?? "";
            PersonaWriteSnapshot result;
            if (_isCreate)
            {
                var templates = PersonaWizardLogic.BuildTemplates(_hostPacks);
                var template = _selectedTemplate ?? templates.FirstOrDefault();
                if (template is null)
                {
                    StatusText.Text = PersonaWizardLogic.HostGalleryRequiredMessage;
                    return;
                }

                var write = PersonaWizardLogic.BuildCreateWrite(
                    template,
                    PersonaIdBox.Text ?? "",
                    DisplayNameBox.Text ?? "",
                    HumanAddressBox.Text ?? "Friend",
                    IdentityBlurbBox.Text,
                    traits,
                    _selectedInferenceModel,
                    description);
                result = await _client.CreateAsync(write).ConfigureAwait(true);
            }
            else
            {
                var edits = new PersonaPackWrite
                {
                    PersonaId = _editTarget!.PersonaId,
                    DisplayName = DisplayNameBox.Text ?? "",
                    HumanAddress = HumanAddressBox.Text ?? "Friend",
                    IdentityBlurb = IdentityBlurbBox.Text,
                    PhysicalDescription = description,
                    InferenceModel = _selectedInferenceModel,
                    Traits = traits
                };
                var write = PersonaWizardLogic.BuildUpdateWrite(_editTarget, edits);
                result = await _client.UpdateAsync(_editTarget.PersonaId, write).ConfigureAwait(true);
            }

            if (!result.Ok || result.Persona is null)
            {
                StatusText.Text = result.Detail ?? "Host rejected save";
                return;
            }

            SavedPersona = result.Persona;
            ResultNotice = PersonaWizardLogic.BuildSaveNextTurnNotice(result.Persona.DisplayName, _isCreate);
            Close(true);
        }
        finally
        {
            _saving = false;
            SaveButton.IsEnabled = true;
            NextButton.IsEnabled = true;
            BackButton.IsEnabled = true;
        }
    }

    private void ApplyTemplateDefaults(PersonaTemplateInfo template)
    {
        if (string.IsNullOrWhiteSpace(DisplayNameBox.Text))
            DisplayNameBox.Text = "";
        HumanAddressBox.Text = template.HumanAddress;
        IdentityBlurbBox.Text = template.IdentityBlurb ?? "";
        PhysicalDescriptionBox.Text = template.PhysicalDescription ?? "";
        ApplyTraits(template.Traits);
        _personaIdTouched = false;
        if (!string.IsNullOrWhiteSpace(DisplayNameBox.Text))
        {
            _suggestingPersonaId = true;
            try
            {
                PersonaIdBox.Text = PersonaWizardLogic.SuggestPersonaId(DisplayNameBox.Text);
            }
            finally
            {
                _suggestingPersonaId = false;
            }
        }
    }

    private void ApplyTraits(PersonaTraitScalesInfo traits)
    {
        foreach (var trait in PersonaTraitCatalog.All)
            _traits.Write(trait, traits.Read(trait));
        PushTraitsToSliders();
    }

    private void PushTraitsToSliders()
    {
        foreach (var row in _rows)
        {
            var value = _traits.Read(row.Trait);
            if (Math.Abs(row.Slider.Value - value) > PersonaTraitCatalog.DefaultEpsilon)
                row.Slider.Value = value;
            UpdateRowChrome(row);
        }

        RefreshBandPreview();
    }

    private PersonaTraitScalesInfo ReadTraits()
    {
        foreach (var row in _rows)
            _traits.Write(row.Trait, row.Slider.Value);
        return _traits.Clone();
    }

    private void RefreshBandPreview()
    {
        if (BandPreviewText is null || _step < 0 || _step >= _steps.Count)
            return;
        var group = _steps[_step].Group;
        if (group is null)
            return;
        BandPreviewText.Text = PersonaWizardLogic.FormatGroupBandPreview(ReadTraits(), group);
    }

    private bool ValidateIdentity(out string error)
    {
        var name = (DisplayNameBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Display name is required.";
            return false;
        }

        var id = (PersonaIdBox.Text ?? "").Trim().ToLowerInvariant();
        if (_isCreate)
        {
            PersonaIdBox.Text = id;
            if (!PersonaWizardLogic.IsValidPersonaId(id))
            {
                error = "Persona id must be lowercase letters, digits, '-' or '_' (1–64).";
                return false;
            }

            if (_hostPacks.Any(p => string.Equals(p.PersonaId, id, StringComparison.OrdinalIgnoreCase)))
            {
                error = $"Persona id '{id}' already exists on Host.";
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(HumanAddressBox.Text))
            HumanAddressBox.Text = "Friend";

        error = "";
        return true;
    }

    private void ShowStep()
    {
        if (_steps.Count == 0)
            return;

        var step = _steps[_step];
        TemplateStep.IsVisible = step.Kind == PersonaWizardStepKind.Template;
        IdentityStep.IsVisible = step.Kind == PersonaWizardStepKind.Identity;
        ModelStep.IsVisible = step.Kind == PersonaWizardStepKind.Model;
        TraitsStep.IsVisible = step.Kind == PersonaWizardStepKind.Traits;
        CharterStep.IsVisible = step.Kind == PersonaWizardStepKind.Blurb;
        QuarantineStep.IsVisible = step.Kind == PersonaWizardStepKind.Quarantine;

        if (step.Kind == PersonaWizardStepKind.Traits && step.Group is not null)
            ShowTraitGroup(step.Group);

        var last = _steps.Count - 1;
        BackButton.IsVisible = _step > 0;
        NextButton.IsVisible = _step < last;
        SaveButton.IsVisible = step.Kind == PersonaWizardStepKind.Quarantine;

        var templateBlocked = _isCreate
                              && step.Kind == PersonaWizardStepKind.Template
                              && PersonaWizardLogic.BuildTemplates(_hostPacks).Count == 0;
        if (!_saving)
            NextButton.IsEnabled = !templateBlocked;

        StepHintText.Text = HintFor(step);

        if (step.Kind == PersonaWizardStepKind.Model)
            RefreshModelHint();
        if (step.Kind == PersonaWizardStepKind.Quarantine)
        {
            var name = (DisplayNameBox.Text ?? "").Trim();
            var id = (PersonaIdBox.Text ?? "").Trim();
            QuarantineNoticeText.Text = _isCreate
                ? PersonaWizardLogic.BuildCreateQuarantineNotice(name, id)
                : PersonaQuarantineConfirm.BuildEditQuarantineNotice(name, id);
        }

        if (!templateBlocked)
            StatusText.Text = "";
    }

    private void ShowTraitGroup(PersonaTraitGroup opened)
    {
        var groups = PersonaWizardLogic.TraitGroups;
        for (var i = 0; i < _groupPanels.Count && i < groups.Count; i++)
            _groupPanels[i].IsVisible = PersonaWizardLogic.ShowsTraitGroup(groups[i], opened);
        TraitGroupTitle.Text = opened.Title;
        RefreshBandPreview();
    }

    private string HintFor(PersonaWizardStep step)
    {
        var prefix = $"Step {_step + 1} of {_steps.Count} — ";
        return step.Kind switch
        {
            PersonaWizardStepKind.Template =>
                prefix + "pick a Host template (Blank / Mentor / Analyst when seeded).",
            PersonaWizardStepKind.Identity => _isCreate
                ? prefix + "identity and portrait. Name, Host id, human address, and physical description."
                : prefix + "identity and portrait. Name, human address, and physical description (id is fixed).",
            PersonaWizardStepKind.Model =>
                prefix + "chat model for this pack only. Blank uses the Host default.",
            PersonaWizardStepKind.Traits when step.Group?.IsMoreTraits == true =>
                prefix + "More traits. These twenty stay hidden until this step. An untouched trait saves at its catalog default.",
            PersonaWizardStepKind.Traits =>
                prefix + $"{step.Group?.Title}. One trait group. Default marks a slider on its catalog default.",
            PersonaWizardStepKind.Blurb =>
                prefix + "identity blurb. Separate from the physical description.",
            PersonaWizardStepKind.Quarantine =>
                prefix + "quarantine confirm. Memory and the local transcript stay on this id.",
            _ => throw new ArgumentOutOfRangeException(nameof(step.Kind), step.Kind, "Unhandled wizard step.")
        };
    }

    private sealed class TraitSliderRow
    {
        public TraitSliderRow(PersonaTraitDefinition trait, Slider slider, TextBlock valueText, Border badge)
        {
            Trait = trait;
            Slider = slider;
            ValueText = valueText;
            Badge = badge;
        }

        public PersonaTraitDefinition Trait { get; }
        public Slider Slider { get; }
        public TextBlock ValueText { get; }
        public Border Badge { get; }
    }
}
