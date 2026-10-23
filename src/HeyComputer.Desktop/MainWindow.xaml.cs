using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HeyComputer.Desktop.Audio;
using HeyComputer.Desktop.Integration;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using Forms = System.Windows.Forms;

namespace HeyComputer.Desktop;

public partial class MainWindow : Window
{
    private readonly AssistantStateCoordinator _state = new();
    private readonly IAudioCaptureService _audio = new AudioCaptureService();
    private readonly DesktopAssistantRuntime _runtime;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Forms.NotifyIcon _trayIcon;
    private bool _exitRequested;
    private bool _isMuted;

    public MainWindow()
    {
        InitializeComponent();

        _runtime = new DesktopAssistantRuntime(_audio);
        _trayIcon = CreateTrayIcon();
        _state.StateChanged += OnStateChanged;
        _runtime.StateChanged += OnRuntimeStateChanged;
        _runtime.StatusChanged += OnRuntimeStatusChanged;
        _runtime.ReadinessChanged += OnReadinessChanged;
        _audio.LevelChanged += OnAudioLevelChanged;
        _audio.CaptureFailed += OnCaptureFailed;
        _audio.DevicesChanged += OnDevicesChanged;

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private Forms.NotifyIcon CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Hey Computer", null, (_, _) => ShowFromTray());
        menu.Items.Add("Mute microphone", null, async (_, _) => await ToggleMuteAsync());
        menu.Items.Add("Stop listening", null, async (_, _) => await StopListeningAsync());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, async (_, _) => await ExitAsync());

        var icon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Information,
            Text = "Hey Computer — stopped",
            ContextMenuStrip = menu,
            Visible = true
        };
        icon.DoubleClick += (_, _) => ShowFromTray();
        return icon;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshMicrophones();
        try
        {
            await _runtime.InitializeAsync(_lifetime.Token);
            OnReadinessChanged(_runtime, _runtime.Readiness);
        }
        catch (OperationCanceledException)
        {
            // Application shutdown owns cancellation.
        }
        catch
        {
            _state.Set(AssistantState.Error);
            PrivacyText.Text = "Local configuration could not be loaded.";
        }
    }

    private void RefreshMicrophones()
    {
        var selectedId = _audio.SelectedDeviceId;
        var devices = _audio.GetDevices();
        MicrophonePicker.ItemsSource = devices;
        MicrophonePicker.SelectedItem = devices.FirstOrDefault(device => device.Id == selectedId) ?? devices[0];
    }

    private async void StartButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await _runtime.StartAsync(_lifetime.Token);
            _isMuted = false;
        }
        catch (OperationCanceledException)
        {
            // Application shutdown owns cancellation.
        }
        catch
        {
            _state.Set(AssistantState.Error);
        }
    }

    private async void PushToTalkButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_audio.IsCapturing && !_isMuted)
        {
            PushToTalkButton.IsEnabled = false;
            try
            {
                await _runtime.RunPushToTalkAsync(_lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                // Stop, mute, or shutdown owns cancellation.
            }
            finally
            {
                PushToTalkButton.IsEnabled = _audio.IsCapturing && !_isMuted;
            }
        }
    }

    private async void MuteButton_OnClick(object sender, RoutedEventArgs e) => await ToggleMuteAsync();

    private async Task ToggleMuteAsync()
    {
        if (_isMuted)
        {
            try
            {
                await _runtime.StartAsync(_lifetime.Token);
                _isMuted = false;
            }
            catch
            {
                _state.Set(AssistantState.Error);
            }
        }
        else
        {
            await _runtime.MuteAsync(_lifetime.Token);
            _isMuted = true;
        }
    }

    private async void StopButton_OnClick(object sender, RoutedEventArgs e) => await StopListeningAsync();

    private async Task StopListeningAsync()
    {
        await _runtime.StopAsync(_lifetime.Token);
        _isMuted = false;
    }

    private async void MicrophonePicker_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MicrophonePicker.SelectedItem is not MicrophoneDevice device || device.Id == _audio.SelectedDeviceId)
        {
            return;
        }

        var restart = _audio.IsCapturing;
        if (restart)
        {
            await _runtime.StopAsync(_lifetime.Token);
        }

        _audio.SelectedDeviceId = device.Id;

        if (restart)
        {
            try
            {
                await _runtime.StartAsync(_lifetime.Token);
            }
            catch
            {
                _state.Set(AssistantState.Error);
            }
        }
    }

    private void OnStateChanged(object? sender, AssistantState state)
    {
        Dispatcher.Invoke(() =>
        {
            StateText.Text = state.ToDisplayText();
            StateDot.Fill = new SolidColorBrush(GetStateColor(state));
            PrivacyText.Text = GetPrivacyText(state);
            var trayText = $"Hey Computer — {state.ToDisplayText()}";
            _trayIcon.Text = trayText[..Math.Min(63, trayText.Length)];

            var active = _audio.IsCapturing;
            StartButton.IsEnabled = !active && !_isMuted;
            PushToTalkButton.IsEnabled = active && !_isMuted;
            MuteButton.IsEnabled = active || _isMuted;
            MuteButton.Content = _isMuted ? "Unmute" : "Mute";
            StopButton.IsEnabled = active || _isMuted;
            MicrophonePicker.IsEnabled = !active;
        });
    }

    private static Color GetStateColor(AssistantState state) => state switch
    {
        AssistantState.Waiting => Color.FromRgb(117, 230, 200),
        AssistantState.Listening or AssistantState.WakeDetected => Color.FromRgb(91, 168, 255),
        AssistantState.Transcribing or AssistantState.Thinking => Color.FromRgb(250, 194, 87),
        AssistantState.Speaking => Color.FromRgb(183, 132, 255),
        AssistantState.Muted or AssistantState.Error => Color.FromRgb(255, 105, 120),
        _ => Color.FromRgb(135, 146, 166)
    };

    private string GetPrivacyText(AssistantState state) => state switch
    {
        AssistantState.Waiting when _runtime.Readiness.WakeModelConfigured =>
            "Audio is processed locally for “Hey Computer” and is not uploaded before activation.",
        AssistantState.Waiting => "Microphone test is active locally; wake detection is not configured yet.",
        AssistantState.Listening => "The activated request is being captured.",
        AssistantState.Muted => "Microphone capture is completely stopped.",
        AssistantState.Sleeping => "No microphone audio is being captured.",
        AssistantState.Error => "Microphone capture failed; check Windows privacy and device settings.",
        _ => "The assistant is processing the activated request."
    };

    private void OnAudioLevelChanged(object? sender, float level)
    {
        Dispatcher.BeginInvoke(() => LevelMeter.Value = Math.Clamp(level * 100, 0, 100));
    }

    private void OnRuntimeStateChanged(object? sender, AssistantState state) =>
        Dispatcher.BeginInvoke(() => _state.Set(state));

    private void OnRuntimeStatusChanged(object? sender, string status) =>
        Dispatcher.BeginInvoke(() => PrivacyText.Text = status);

    private void OnReadinessChanged(object? sender, DesktopReadiness readiness) =>
        Dispatcher.BeginInvoke(() => SetupStatusText.Text = readiness.Summary);

    private async void SaveApiKeyButton_OnClick(object sender, RoutedEventArgs e)
    {
        var key = ApiKeyBox.Password;
        if (string.IsNullOrWhiteSpace(key))
        {
            PrivacyText.Text = "Enter an xAI API key before saving.";
            return;
        }

        try
        {
            await _runtime.SaveApiKeyAsync(key, _lifetime.Token);
            ApiKeyBox.Clear();
        }
        catch (OperationCanceledException)
        {
            // Application shutdown owns cancellation.
        }
        catch
        {
            PrivacyText.Text = "The API key could not be saved to Windows Credential Manager.";
        }
    }

    private async void RemoveApiKeyButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await _runtime.RemoveApiKeyAsync(_lifetime.Token);
            ApiKeyBox.Clear();
        }
        catch (OperationCanceledException)
        {
            // Application shutdown owns cancellation.
        }
        catch
        {
            PrivacyText.Text = "The API key could not be removed from Windows Credential Manager.";
        }
    }

    private void OnCaptureFailed(object? sender, string message)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _state.Set(AssistantState.Error);
            PrivacyText.Text = message;
        });
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(async () =>
        {
            var restartDefaultDevice = _audio.IsCapturing && _audio.SelectedDeviceId is null;
            if (restartDefaultDevice)
            {
                await _audio.StopAsync();
            }

            RefreshMicrophones();

            if (restartDefaultDevice && !_isMuted)
            {
                try
                {
                    await _runtime.StartAsync(_lifetime.Token);
                }
                catch
                {
                    _state.Set(AssistantState.Error);
                }
            }
        });
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested)
        {
            return;
        }

        e.Cancel = true;
        Hide();
        _trayIcon.ShowBalloonTip(1500, "Hey Computer", "Still available in the notification area.", Forms.ToolTipIcon.Info);
    }

    private void ShowFromTray()
    {
        Dispatcher.Invoke(() =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        });
    }

    private async Task ExitAsync()
    {
        _exitRequested = true;
        _lifetime.Cancel();
        await _runtime.DisposeAsync();
        await _audio.DisposeAsync();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();

        Dispatcher.Invoke(() =>
        {
            Close();
            Application.Current.Shutdown();
        });
    }

    public void PrepareForSystemShutdown()
    {
        _exitRequested = true;
        _lifetime.Cancel();
        _runtime.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _audio.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
    }
}
