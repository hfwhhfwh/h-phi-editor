using Godot;
using System;

public partial class EditorSettingsPanel : PanelContainer
{
    [Export] private SpinBox _subBeatCountEdit;
    [Export] private SpinBox _verLineCountEdit;
    [Export] private Button _applyButton;
    [Export] private Button _confirmButton;
    [Export] private Button _closeButton;

    private EditorSettings _editorSettings;

    public override void _Ready()
    {
        _editorSettings = EditorSettings.Instance;
        if (_editorSettings == null)
        {
            GD.PushError($"[{Name}] EditorSettings 不可用");
            return;
        }

        ConfigureEditors();

        // UI -> Settings
        _subBeatCountEdit.ValueChanged += OnSubBeatCountChanged;
        _verLineCountEdit.ValueChanged += OnVerLineCountChanged;
        _applyButton.Pressed += OnApplyPressed;
        _confirmButton.Pressed += OnConfirmPressed;
        _closeButton.Pressed += Hide;

        // Settings -> UI
        _editorSettings.SettingChanged += OnSettingChanged;
        _editorSettings.SettingsApplied += OnSettingsApplied;
        VisibilityChanged += OnVisibilityChanged;

        RefreshUI();
    }

    public override void _ExitTree()
    {
        if (_editorSettings != null)
        {
            _editorSettings.SettingChanged -= OnSettingChanged;
            _editorSettings.SettingsApplied -= OnSettingsApplied;
        }

        if (_subBeatCountEdit != null) _subBeatCountEdit.ValueChanged -= OnSubBeatCountChanged;
        if (_verLineCountEdit != null) _verLineCountEdit.ValueChanged -= OnVerLineCountChanged;
        if (_applyButton != null) _applyButton.Pressed -= OnApplyPressed;
        if (_confirmButton != null) _confirmButton.Pressed -= OnConfirmPressed;
        if (_closeButton != null) _closeButton.Pressed -= Hide;
        VisibilityChanged -= OnVisibilityChanged;
    }

    private void ConfigureEditors()
    {
        _subBeatCountEdit.MinValue = 1;
        _subBeatCountEdit.Step = 1;
        _subBeatCountEdit.AllowLesser = false;
        _subBeatCountEdit.AllowGreater = false;

        _verLineCountEdit.MinValue = 2;
        _verLineCountEdit.Step = 1;
        _verLineCountEdit.AllowLesser = false;
        _verLineCountEdit.AllowGreater = false;
    }

    private void OnVisibilityChanged()
    {
        if (Visible) RefreshUI();
    }

    private void OnSubBeatCountChanged(double value)
    {
        _editorSettings.Set(
            nameof(EditorSettingsData.SubBeatCount), Mathf.Max(1, Mathf.RoundToInt((float)value)));
    }

    private void OnVerLineCountChanged(double value)
    {
        _editorSettings.Set(
            nameof(EditorSettingsData.VerLineCount), Mathf.Max(2, Mathf.RoundToInt((float)value)));
    }

    private void OnSettingChanged(string key, Variant value)
    {
        switch (key)
        {
            case nameof(EditorSettingsData.SubBeatCount):
                _subBeatCountEdit.SetValueNoSignal(value.AsInt32());
                break;
            case nameof(EditorSettingsData.VerLineCount):
                _verLineCountEdit.SetValueNoSignal(value.AsInt32());
                break;
        }
    }

    private void OnSettingsApplied() => RefreshUI();

    private void RefreshUI()
    {
        if (_editorSettings?.Current == null) return;

        _subBeatCountEdit.SetValueNoSignal(_editorSettings.Current.SubBeatCount);
        _verLineCountEdit.SetValueNoSignal(_editorSettings.Current.VerLineCount);
    }

    private void OnApplyPressed() => _editorSettings.Save();

    private void OnConfirmPressed()
    {
        _editorSettings.Save();
        Hide();
    }

}
