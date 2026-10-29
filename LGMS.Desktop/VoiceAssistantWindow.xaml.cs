using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using LGMS.Desktop.Services;
using LGMS.Desktop.ViewModels;
using LGMS.Models;

namespace LGMS.Desktop;

public partial class VoiceAssistantWindow : Window
{
    private readonly MainViewModel _mainViewModel;
    private readonly VoiceAssistantService _voiceService;
    private readonly ObservableCollection<VoiceStepItem> _steps = new();

    public VoiceAssistantWindow(MainViewModel mainViewModel, VoiceAssistantService voiceService)
    {
        InitializeComponent();

        _mainViewModel = mainViewModel;
        _voiceService = voiceService;

        StepsItemsControl.ItemsSource = _steps;

        Loaded += OnWindowLoaded;
        Closing += OnWindowClosing;
        KeyDown += OnWindowKeyDown;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        // Bind repositories
        RepoComboBox.ItemsSource = _mainViewModel.AllRepositories;
        RepoComboBox.SelectedItem = _mainViewModel.ActiveRepository;
        UpdateActiveRepoDisplay();

        // Subscribe to voice events
        _voiceService.SpeechRecognizedPartial += OnSpeechPartial;
        _voiceService.SpeechRecognizedFinal += OnSpeechFinal;
        _voiceService.StatusChanged += OnStatusChanged;
        _voiceService.ListeningStateChanged += OnListeningChanged;
        _voiceService.SpeakingStateChanged += OnSpeakingChanged;

        UpdateMuteButton();

        // Auto-start listening on open
        Dispatcher.InvokeAsync(async () =>
        {
            await Task.Delay(350);
            _voiceService.StartListening();
        });
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _voiceService.StopListening();
        _voiceService.StopSpeaking();

        _voiceService.SpeechRecognizedPartial -= OnSpeechPartial;
        _voiceService.SpeechRecognizedFinal -= OnSpeechFinal;
        _voiceService.StatusChanged -= OnStatusChanged;
        _voiceService.ListeningStateChanged -= OnListeningChanged;
        _voiceService.SpeakingStateChanged -= OnSpeakingChanged;
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
        else if (e.Key == Key.Space && Keyboard.FocusedElement is not TextBox)
        {
            e.Handled = true;
            ToggleListening();
        }
    }

    private void OnSpeechPartial(string text)
    {
        Dispatcher.Invoke(() =>
        {
            TranscriptInput.Text = text;
        });
    }

    private void OnSpeechFinal(string text)
    {
        Dispatcher.Invoke(() =>
        {
            TranscriptInput.Text = text;
            _ = ExecuteCommandAsync(text);
        });
    }

    private void OnStatusChanged(string status)
    {
        Dispatcher.Invoke(() =>
        {
            StatusLabelText.Text = status;
        });
    }

    private void OnListeningChanged(bool isListening)
    {
        Dispatcher.Invoke(() =>
        {
            var ellipse = MicButton.Template.FindName("OuterGlow", MicButton) as Ellipse;
            var inner = MicButton.Template.FindName("InnerCore", MicButton) as Ellipse;
            if (ellipse != null && inner != null)
            {
                if (isListening)
                {
                    ellipse.Fill = new SolidColorBrush(Color.FromRgb(0x88, 0x13, 0x37));
                    ellipse.Stroke = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
                    inner.Fill = new SolidColorBrush(Color.FromRgb(0xdc, 0x26, 0x26));
                }
                else
                {
                    ellipse.Fill = new SolidColorBrush(Color.FromRgb(0x0c, 0x4a, 0x6e));
                    ellipse.Stroke = new SolidColorBrush(Color.FromRgb(0x06, 0xb6, 0xd4));
                    inner.Fill = new SolidColorBrush(Color.FromRgb(0x02, 0x84, 0xc7));
                }
            }
        });
    }

    private void OnSpeakingChanged(bool isSpeaking)
    {
        Dispatcher.Invoke(() =>
        {
            if (isSpeaking)
            {
                StatusLabelText.Text = "Speaking response...";
            }
            else
            {
                StatusLabelText.Text = "Ready for next command";
            }
        });
    }

    private void OnMicButtonClicked(object sender, RoutedEventArgs e)
    {
        ToggleListening();
    }

    private void ToggleListening()
    {
        if (_voiceService.IsListening)
        {
            _voiceService.StopListening();
        }
        else
        {
            _voiceService.StopSpeaking();
            _voiceService.StartListening();
        }
    }

    private void OnExecuteClicked(object sender, RoutedEventArgs e)
    {
        var text = TranscriptInput.Text.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            _ = ExecuteCommandAsync(text);
        }
    }

    private void OnTranscriptInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var text = TranscriptInput.Text.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                _ = ExecuteCommandAsync(text);
            }
        }
    }

    private async Task ExecuteCommandAsync(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;

        StatusLabelText.Text = "Thinking & Executing...";
        WorkingSectionBorder.Visibility = Visibility.Visible;
        _steps.Clear();
        _steps.Add(new VoiceStepItem { Title = "Command Received", Description = $"\"{command}\"", Status = "completed" });
        _steps.Add(new VoiceStepItem { Title = "Resolving Action", Description = "Analyzing command against repository catalog...", Status = "running" });

        var targetOverride = RepoComboBox.SelectedItem as Repository;
        var result = await _voiceService.ProcessCommandAsync(command, targetOverride);

        _steps.Clear();
        foreach (var step in result.Steps)
        {
            _steps.Add(step);
        }

        if (result.Repository != null)
        {
            RepoComboBox.SelectedItem = result.Repository;
            UpdateActiveRepoDisplay();
        }

        if (!string.IsNullOrWhiteSpace(result.RawOutput))
        {
            RawOutputExpander.Visibility = Visibility.Visible;
            RawOutputText.Text = result.RawOutput;
        }
        else
        {
            RawOutputExpander.Visibility = Visibility.Collapsed;
        }

        if (!string.IsNullOrWhiteSpace(result.SpokenReply))
        {
            SpokenResponseCard.Visibility = Visibility.Visible;
            SpokenResponseText.Text = result.SpokenReply;
            _voiceService.Speak(result.SpokenReply);
        }
        else
        {
            SpokenResponseCard.Visibility = Visibility.Collapsed;
        }
    }

    private void OnRepoSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RepoComboBox.SelectedItem is Repository repo)
        {
            _mainViewModel.ActiveRepository = repo;
            UpdateActiveRepoDisplay();
        }
    }

    private void UpdateActiveRepoDisplay()
    {
        var repo = _mainViewModel.ActiveRepository;
        if (repo != null)
        {
            ActiveRepoBanner.Text = $"Active: {repo.Name} ({repo.CurrentBranch ?? "master"})";
        }
        else
        {
            ActiveRepoBanner.Text = "Active: [No repository selected]";
        }
    }

    private void OnToggleMuteClicked(object sender, RoutedEventArgs e)
    {
        _voiceService.IsMuted = !_voiceService.IsMuted;
        UpdateMuteButton();
    }

    private void UpdateMuteButton()
    {
        MuteButton.Content = _voiceService.IsMuted ? "🔇 Muted" : "🔊 Sound On";
    }

    private void OnSuggestionClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag != null)
        {
            var cmd = btn.Tag.ToString()!;
            TranscriptInput.Text = cmd;
            _ = ExecuteCommandAsync(cmd);
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
