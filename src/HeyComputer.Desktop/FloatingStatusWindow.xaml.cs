using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace HeyComputer.Desktop;

public partial class FloatingStatusWindow : Window
{
    private bool _exitRequested;

    public FloatingStatusWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => MoveToDefaultPosition();
        Closing += OnClosing;
    }

    public event EventHandler? OpenDashboardRequested;

    public void UpdateState(AssistantState state, string? activity = null)
    {
        StateText.Text = state.ToDisplayText();
        StateDot.Fill = new SolidColorBrush(GetStateColor(state));
        if (!string.IsNullOrWhiteSpace(activity))
        {
            ActivityText.Text = activity.Trim();
        }
    }

    public void ShowPanel()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    public void CloseForExit()
    {
        _exitRequested = true;
        Close();
    }

    private void MoveToDefaultPosition()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - 18;
        Top = workArea.Bottom - ActualHeight - 18;
    }

    private void PanelBorder_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.Controls.Button)
        {
            OpenDashboardRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void HideButton_OnClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Hide();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_exitRequested)
        {
            e.Cancel = true;
            Hide();
        }
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
}
