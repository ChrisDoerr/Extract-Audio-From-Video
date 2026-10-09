using System.ComponentModel;
using System.IO;
using Microsoft.Win32;
using System.Windows;

namespace ExtractAudioFromVideo;

public partial class MainWindow : Window
{
    private readonly FfmpegAudioExtractor _audioExtractor = new();
    private string? _selectedVideoPath;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select video file",
            Filter = "Video files|*.mp4;*.mkv;*.mov;*.avi;*.webm;*.m4v;*.wmv;*.mpeg;*.mpg;*.ts;*.mts;*.flv|All files|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _selectedVideoPath = dialog.FileName;
        VideoPathTextBox.Text = _selectedVideoPath;
        OutputNameTextBox.Text = Path.GetFileNameWithoutExtension(_selectedVideoPath);
        StatusTextBlock.Text = "Ready to extract.";
        StatusTextBlock.ClearValue(System.Windows.Controls.TextBlock.ForegroundProperty);
        ProgressPanel.Visibility = Visibility.Collapsed;
        ExtractionProgressBar.IsIndeterminate = false;
        ExtractionProgressBar.Value = 0;
        ProgressPercentTextBlock.Text = "...";
        ExtractButton.IsEnabled = true;
    }

    private async void ExtractButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedVideoPath is null)
        {
            return;
        }

        var outputBaseName = OutputNameTextBox.Text.Trim();
        if (outputBaseName.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase))
        {
            outputBaseName = outputBaseName[..^4];
        }

        SetBusy(true);
        StatusTextBlock.Text = "Extracting audio...";
        StatusTextBlock.ClearValue(System.Windows.Controls.TextBlock.ForegroundProperty);
        ProgressPanel.Visibility = Visibility.Visible;
        ExtractionProgressBar.IsIndeterminate = true;
        ExtractionProgressBar.Value = 0;
        ProgressPercentTextBlock.Text = "...";
        var progress = new Progress<double?>(UpdateProgress);

        try
        {
            var outputPath = await _audioExtractor.ExtractFirstAudioAsM4aAsync(
                _selectedVideoPath,
                outputBaseName,
                progress);
            StatusTextBlock.Text = $"Done. The audio file has been saved:\n{outputPath}";
        }
        catch (ArgumentException exception)
        {
            ShowError("Invalid output name", exception.Message);
        }
        catch (Win32Exception)
        {
            ShowError(
                "ffmpeg/ffprobe not found",
                "The bundled ffmpeg or ffprobe could not be started. Verify that the application files are complete and compatible with this computer.");
        }
        catch (FfmpegExecutionException exception)
        {
            var message = exception.ErrorOutput.Contains("matches no streams", StringComparison.OrdinalIgnoreCase)
                ? "The selected video file does not contain an extractable audio track."
                : $"ffmpeg could not extract the audio.\n\n{GetDiagnostic(exception.ErrorOutput)}";
            ShowError("Extraction failed", message);
        }
        catch (UnauthorizedAccessException)
        {
            ShowError("Access denied", "The folder containing the video cannot be written to.");
        }
        catch (IOException exception)
        {
            ShowError("File error", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ShowError("Extraction failed", exception.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool isBusy)
    {
        BrowseButton.IsEnabled = !isBusy;
        OutputNameTextBox.IsEnabled = !isBusy;
        ExtractButton.IsEnabled = !isBusy && _selectedVideoPath is not null;
        ExtractButton.Content = isBusy ? "Extracting..." : "Extract Audio";
    }

    private void UpdateProgress(double? fraction)
    {
        if (fraction is null)
        {
            ExtractionProgressBar.IsIndeterminate = true;
            ProgressPercentTextBlock.Text = "...";
            return;
        }

        ExtractionProgressBar.IsIndeterminate = false;
        var percentage = Math.Clamp(fraction.Value * 100d, 0d, 100d);
        ExtractionProgressBar.Value = percentage;
        ProgressPercentTextBlock.Text = $"{percentage:0}%";
    }

    private void ShowError(string title, string message)
    {
        ProgressPanel.Visibility = Visibility.Collapsed;
        StatusTextBlock.Text = message;
        StatusTextBlock.Foreground = System.Windows.Media.Brushes.Firebrick;
        MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static string GetDiagnostic(string errorOutput)
    {
        var diagnostic = errorOutput.Trim();
        const int maxLength = 1200;

        if (diagnostic.Length > maxLength)
        {
            diagnostic = diagnostic[^maxLength..];
        }

        return string.IsNullOrEmpty(diagnostic) ? "ffmpeg hat einen Fehler gemeldet." : diagnostic;
    }
}