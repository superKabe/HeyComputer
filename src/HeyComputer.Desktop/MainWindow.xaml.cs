using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HeyComputer.ComputerControl;
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
    private readonly IComputerControlCapability _computerControl;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly FloatingStatusWindow _floatingPanel;
    private Forms.ToolStripMenuItem? _trayStartItem;
    private Forms.ToolStripMenuItem? _trayMuteItem;
    private Forms.ToolStripMenuItem? _trayStopItem;
    private bool _exitRequested;
    private bool _isMuted;

    public MainWindow()
    {
        InitializeComponent();

        _runtime = new DesktopAssistantRuntime(_audio);
        _computerControl = new WindowsComputerControlCapability(new LaunchAllowlist(
        [
            new LaunchTarget("spotify", "Spotify", LaunchTargetKind.Uri, "https://open.spotify.com"),
            new LaunchTarget("youtube", "YouTube", LaunchTargetKind.Uri, "https://www.youtube.com")
        ]));
        _floatingPanel = new FloatingStatusWindow();
        _floatingPanel.OpenDashboardRequested += (_, _) => ShowFromTray();
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
        AddActivity("Ready. Start listening when you want Hey Computer to use the microphone.");
    }

    private Forms.NotifyIcon CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Hey Computer", null, (_, _) => ShowFromTray());
        menu.Items.Add("Show floating status", null, (_, _) => Dispatcher.Invoke(() => _floatingPanel.ShowPanel()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        _trayStartItem = new Forms.ToolStripMenuItem("Start listening", null, async (_, _) => await StartListeningAsync());
        _trayMuteItem = new Forms.ToolStripMenuItem("Mute microphone", null, async (_, _) => await ToggleMuteAsync()) { Enabled = false };
        _trayStopItem = new Forms.ToolStripMenuItem("Stop listening", null, async (_, _) => await StopListeningAsync()) { Enabled = false };
        menu.Items.Add(_trayStartItem);
        menu.Items.Add(_trayMuteItem);
        menu.Items.Add(_trayStopItem);
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
        _floatingPanel.ShowPanel();
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
        MicrophonePicker.SelectedItem = devices.FirstOrDefault(device => device.Id == selectedId) ?? devices.FirstOrDefault();
        if (devices.Count == 0)
        {
            StartButton.IsEnabled = false;
            if (_trayStartItem is not null)
            {
                _trayStartItem.Enabled = false;
            }

            AddActivity("No microphone was found. Connect one, then reopen Hey Computer.");
        }
    }

    private async void StartButton_OnClick(object sender, RoutedEventArgs e)
        => await StartListeningAsync();

    private async Task StartListeningAsync()
    {
        try
        {
            await _runtime.StartAsync(_lifetime.Token);
            _isMuted = false;
            AddActivity("Microphone capture started.");
        }
        catch (OperationCanceledException)
        {
            // Application shutdown owns cancellation.
        }
        catch
        {
            _state.Set(AssistantState.Error);
            AddActivity("Hey Computer could not start. Check the microphone and setup status.");
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
                AddActivity("Microphone capture resumed.");
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
            AddActivity("Microphone capture muted.");
        }
    }

    private async void StopButton_OnClick(object sender, RoutedEventArgs e) => await StopListeningAsync();

    private async Task StopListeningAsync()
    {
        await _runtime.StopAsync(_lifetime.Token);
        _isMuted = false;
        AddActivity("Assistant stopped. No microphone audio is being captured.");
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
            RunningText.Text = state is AssistantState.Sleeping or AssistantState.Muted or AssistantState.Error
                ? state == AssistantState.Muted ? "Assistant is running · microphone muted" : "Assistant is stopped"
                : "Assistant is running";
            ActivityHintText.Text = state.ToDisplayText();
            _floatingPanel.UpdateState(state);
            var trayText = $"Hey Computer — {state.ToDisplayText()}";
            _trayIcon.Text = trayText[..Math.Min(63, trayText.Length)];

            var active = _audio.IsCapturing;
            StartButton.IsEnabled = !active && !_isMuted;
            PushToTalkButton.IsEnabled = active && !_isMuted;
            MuteButton.IsEnabled = active || _isMuted;
            MuteButton.Content = _isMuted ? "Unmute" : "Mute";
            StopButton.IsEnabled = active || _isMuted;
            MicrophonePicker.IsEnabled = !active;
            if (_trayStartItem is not null)
            {
                _trayStartItem.Enabled = !active && !_isMuted;
                _trayMuteItem!.Enabled = active || _isMuted;
                _trayMuteItem.Text = _isMuted ? "Unmute microphone" : "Mute microphone";
                _trayStopItem!.Enabled = active || _isMuted;
            }
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
        Dispatcher.BeginInvoke(() =>
        {
            PrivacyText.Text = status;
            _floatingPanel.UpdateState(_state.Current, status);
            AddActivity(status);
        });

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

    private async void ToggleMediaButton_OnClick(object sender, RoutedEventArgs e) =>
        await RunComputerControlAsync(new ComputerControlCommand.ToggleMediaPlayback());

    private async void CheckSpotifyButton_OnClick(object sender, RoutedEventArgs e) =>
        await RunComputerControlAsync(new ComputerControlCommand.CheckProcessRunning("Spotify"));

    private async void OpenSpotifyButton_OnClick(object sender, RoutedEventArgs e) =>
        await RunComputerControlAsync(new ComputerControlCommand.LaunchAllowlistedTarget("spotify"));

    private async void OpenYouTubeButton_OnClick(object sender, RoutedEventArgs e) =>
        await RunComputerControlAsync(new ComputerControlCommand.LaunchAllowlistedTarget("youtube"));

    private async Task RunComputerControlAsync(ComputerControlCommand command)
    {
        try
        {
            var result = await _computerControl.ExecuteAsync(
                command,
                ComputerControlRequestContext.Create(),
                _lifetime.Token);
            AddActivity(result.Summary);
            _floatingPanel.UpdateState(_state.Current, result.Summary);
        }
        catch (OperationCanceledException)
        {
            // Application shutdown owns cancellation.
        }
        catch
        {
            AddActivity("The computer control action failed.");
        }
    }

    private void OnCaptureFailed(object? sender, string message)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _state.Set(AssistantState.Error);
            PrivacyText.Text = message;
            AddActivity(message);
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
        HideToTray(showNotification: true);
    }

    private void HideButton_OnClick(object sender, RoutedEventArgs e) => HideToTray(showNotification: false);

    private void HideToTray(bool showNotification)
    {
        Hide();
        if (showNotification)
        {
            _trayIcon.ShowBalloonTip(1500, "Hey Computer", "Still available in the notification area.", Forms.ToolTipIcon.Info);
        }
    }

    private void AddActivity(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => AddActivity(message));
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        ActivityList.Items.Insert(0, $"{DateTime.Now:t}  {message.Trim()}");
        while (ActivityList.Items.Count > 20)
        {
            ActivityList.Items.RemoveAt(ActivityList.Items.Count - 1);
        }
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
        _floatingPanel.CloseForExit();

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
        _floatingPanel.CloseForExit();
    }
}
