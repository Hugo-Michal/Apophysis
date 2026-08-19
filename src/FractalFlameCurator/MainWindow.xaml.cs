using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FractalFlameCurator.Generation;
using FractalFlameCurator.Models;
using FractalFlameCurator.Pipeline;
using FractalFlameCurator.Rendering;
using FractalFlameCurator.Storage;
using Forms = System.Windows.Forms;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfButton = System.Windows.Controls.Button;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfPoint = System.Windows.Point;

namespace FractalFlameCurator;

public partial class MainWindow : Window
{
    private readonly CpuFlameRenderer _renderer = new();
    private readonly ContinuousRenderService _renderService;
    private ContinuousRenderOptions? _sessionOptions;
    private SourceArchive? _archive;
    private RatingStore? _ratingStore;
    private RenderedArtifact? _current;
    private RenderedArtifact? _lastRated;
    private RenderedArtifact? _deferredAfterUndo;
    private double _zoomScale = 1;
    private int _imagePixelWidth;
    private int _imagePixelHeight;
    private bool _fitToViewport = true;
    private bool _applyingZoom;
    private bool _closing;

    public MainWindow()
    {
        InitializeComponent();
        _renderService = new ContinuousRenderService(new FlameGenerator(), _renderer);
        _renderService.ImageReady += RenderService_ImageReady;
        _renderService.RenderFailed += RenderService_RenderFailed;
        OutputDirectoryTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ApophysisCurator");
        SeedTextBox.Text = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
        BatchLimitTextBox.Text = "100";
        WorkersTextBox.Text = Math.Max(1, Math.Min(4, Environment.ProcessorCount)).ToString(CultureInfo.InvariantCulture);
        QueueCapacityTextBox.Text = "4";
        SampleBudgetTextBox.Text = "20000000";
        OversampleComboBox.SelectedIndex = 0;
        FilterRadiusTextBox.Text = "0.5";
        GammaTextBox.Text = "2.2";
        BrightnessTextBox.Text = "1.0";
        VibrancyTextBox.Text = "1.0";
        PaletteComboBox.ItemsSource = PaletteDefinition.BuiltIns;
        PaletteComboBox.SelectedIndex = 0;
        BackendTextBlock.Text = $"Backend: {_renderer.Status.Backend} · {_renderer.Status.Device}\n{_renderer.Status.Detail}";
        CompositionTarget.Rendering += UpdateStatus;
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var outputDirectory = OutputDirectoryTextBox.Text.Trim();
            var palette = (PaletteComboBox.SelectedItem as PaletteDefinition) ?? PaletteDefinition.Monochrome;
            _sessionOptions = new ContinuousRenderOptions
            {
                OutputDirectory = outputDirectory,
                BatchLimit = ParseInt(BatchLimitTextBox, 100, 1, 1_000_000),
                WorkerCount = ParseInt(WorkersTextBox, 1, 1, Math.Max(1, Environment.ProcessorCount)),
                QueueCapacity = ParseInt(QueueCapacityTextBox, 4, 1, 64),
                Seed = ParseLong(SeedTextBox, DateTime.UtcNow.Ticks),
                Palette = palette,
                RenderSettings = new RenderSettings
                {
                    Width = 2048,
                    Height = 2048,
                    SampleBudget = ParseInt(SampleBudgetTextBox, 20_000_000, 100, 500_000_000),
                    Oversample = ParseInt((OversampleComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString(), 1, 1, 3),
                    FilterRadius = ParseDouble(FilterRadiusTextBox, 0.5, 0, 3),
                    Gamma = ParseDouble(GammaTextBox, 2.2, 0.1, 8),
                    Brightness = ParseDouble(BrightnessTextBox, 1, 0.05, 5),
                    Vibrancy = ParseDouble(VibrancyTextBox, 1, 0, 1),
                    PaletteName = palette.Name
                }
            };
            _archive = new SourceArchive(outputDirectory);
            _ratingStore = new RatingStore(outputDirectory);
            _renderService.Start(_sessionOptions);
            EmptyPreviewTextBlock.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(this, exception.Message, "Could not start rendering", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Pause_Click(object sender, RoutedEventArgs e) => _renderService.Pause();
    private void Resume_Click(object sender, RoutedEventArgs e) => _renderService.Resume();
    private async void Stop_Click(object sender, RoutedEventArgs e) => await _renderService.StopAsync();

    private void RenderService_ImageReady(RenderedArtifact artifact)
    {
        Dispatcher.Invoke(() =>
        {
            if (_current is not null) return;
            if (_renderService.TryDequeueReady(out var next)) ShowArtifact(next);
        });
    }

    private void RenderService_RenderFailed(Exception exception)
    {
        Dispatcher.Invoke(() => CurrentTextBlock.Text = $"Render failure: {exception.Message}");
    }

    private void ShowArtifact(RenderedArtifact artifact)
    {
        _current = artifact;
        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(artifact.ImagePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        PreviewImage.Source = bitmap;
        _imagePixelWidth = bitmap.PixelWidth;
        _imagePixelHeight = bitmap.PixelHeight;
        EmptyPreviewTextBlock.Visibility = Visibility.Collapsed;
        CurrentTextBlock.Text = $"Current: {artifact.BaseName}\nSeed: {artifact.Seed}";
        if (_fitToViewport) ApplyFitZoom();
        else ApplyZoom(_zoomScale);
    }

    private void ShowNextReady()
    {
        if (_deferredAfterUndo is { } deferred)
        {
            _deferredAfterUndo = null;
            ShowArtifact(deferred);
        }
        else if (_renderService.TryDequeueReady(out var next)) ShowArtifact(next);
        else
        {
            _current = null;
            PreviewImage.Source = null;
            EmptyPreviewTextBlock.Visibility = Visibility.Visible;
        }
    }

    private void Rating_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null || _ratingStore is null) return;
        var rating = int.Parse(((WpfButton)sender).Tag.ToString()!, CultureInfo.InvariantCulture);
        try
        {
            _ratingStore.Rate(_current.ImagePath, rating);
            _lastRated = _current;
            ShowNextReady();
        }
        catch (Exception exception) { System.Windows.MessageBox.Show(this, exception.Message, "Could not save rating", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void Skip_Click(object sender, RoutedEventArgs e) { _lastRated = null; ShowNextReady(); }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_lastRated is null || _ratingStore?.Undo() != true) return;
        _deferredAfterUndo = _current;
        ShowArtifact(_lastRated);
        _lastRated = null;
    }

    private void ZoomFit_Click(object sender, RoutedEventArgs e)
    {
        _fitToViewport = true;
        ApplyFitZoom();
    }

    private void ActualSize_Click(object sender, RoutedEventArgs e)
    {
        _fitToViewport = false;
        ApplyZoom(1);
    }

    private void PreviewScroll_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_current is null || _imagePixelWidth <= 0 || _imagePixelHeight <= 0) return;
        _fitToViewport = false;
        var pointer = e.GetPosition(PreviewScroll);
        var multiplier = e.Delta > 0 ? 1.2 : 1 / 1.2;
        ApplyZoom(_zoomScale * multiplier, pointer);
        e.Handled = true;
    }

    private void PreviewScroll_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_fitToViewport && _current is not null) ApplyFitZoom();
    }

    private void ApplyFitZoom()
    {
        if (_imagePixelWidth <= 0 || _imagePixelHeight <= 0) return;
        var viewportWidth = Math.Max(1, PreviewScroll.ViewportWidth);
        var viewportHeight = Math.Max(1, PreviewScroll.ViewportHeight);
        var scale = Math.Min(viewportWidth / _imagePixelWidth, viewportHeight / _imagePixelHeight);
        ApplyZoom(Math.Clamp(scale, 0.05, 8));
    }

    private void ApplyZoom(double requestedScale, WpfPoint? anchor = null)
    {
        if (_applyingZoom || _imagePixelWidth <= 0 || _imagePixelHeight <= 0) return;
        _applyingZoom = true;
        try
        {
            var oldScale = Math.Max(0.05, _zoomScale);
            var anchorPoint = anchor ?? new WpfPoint(PreviewScroll.ViewportWidth / 2, PreviewScroll.ViewportHeight / 2);
            var imageOrigin = PreviewImage.TranslatePoint(new WpfPoint(0, 0), PreviewScroll);
            var imageX = Math.Clamp((anchorPoint.X - imageOrigin.X) / oldScale, 0, _imagePixelWidth);
            var imageY = Math.Clamp((anchorPoint.Y - imageOrigin.Y) / oldScale, 0, _imagePixelHeight);
            _zoomScale = Math.Clamp(requestedScale, 0.05, 8);
            PreviewImage.Width = _imagePixelWidth * _zoomScale;
            PreviewImage.Height = _imagePixelHeight * _zoomScale;
            PreviewImage.Stretch = System.Windows.Media.Stretch.Fill;
            PreviewScroll.UpdateLayout();
            if (anchor is not null)
            {
                var newOrigin = PreviewImage.TranslatePoint(new WpfPoint(0, 0), PreviewScroll);
                var desiredOriginX = anchorPoint.X - imageX * _zoomScale;
                var desiredOriginY = anchorPoint.Y - imageY * _zoomScale;
                PreviewScroll.ScrollToHorizontalOffset(PreviewScroll.HorizontalOffset + newOrigin.X - desiredOriginX);
                PreviewScroll.ScrollToVerticalOffset(PreviewScroll.VerticalOffset + newOrigin.Y - desiredOriginY);
            }
        }
        finally { _applyingZoom = false; }
    }
    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog { SelectedPath = OutputDirectoryTextBox.Text };
        if (dialog.ShowDialog() == Forms.DialogResult.OK) OutputDirectoryTextBox.Text = dialog.SelectedPath;
    }

    private void Window_KeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key >= Key.D1 && e.Key <= Key.D5) { Rating_Click(new WpfButton { Tag = (int)e.Key - (int)Key.D0 }, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.S) { Skip_Click(sender, e); e.Handled = true; }
        else if (e.Key == Key.U) { Undo_Click(sender, e); e.Handled = true; }
        else if (e.Key == Key.P) { if (_renderService.Status.IsPaused) Resume_Click(sender, e); else Pause_Click(sender, e); e.Handled = true; }
        else if (e.Key == Key.Escape) { Stop_Click(sender, e); e.Handled = true; }
    }

    private void UpdateStatus(object? sender, EventArgs e)
    {
        var status = _renderService.Status;
        QueueTextBlock.Text = $"Queue: {status.QueueDepth}/{_sessionOptions?.QueueCapacity ?? 0} · ready {status.ReadyCount}\nCompleted: {status.Completed} · failures: {status.Failed}";
        var sampleProgress = status.ActiveSampleBudget > 0 ? $" · samples {status.ActiveSamples:N0}/{status.ActiveSampleBudget:N0}" : string.Empty;
        SessionTextBlock.Text = status.IsRunning ? $"Session: {(status.IsPaused ? "PAUSED" : "RUNNING")} · {status.Elapsed:hh\\:mm\\:ss} · limit {status.BatchLimit}{sampleProgress}" : "Session: idle";
        RatingCountTextBlock.Text = _ratingStore is null ? "Rated: 0" : $"Rated: {_ratingStore.RatedImageCount()} · folders image-only: {_ratingStore.RatingFoldersContainImagesOnly()}";
    }

    private async void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_closing) return;
        e.Cancel = true;
        _closing = true;
        CompositionTarget.Rendering -= UpdateStatus;
        await _renderService.StopAsync();
        Close();
    }

    private static int ParseInt(WpfTextBox box, int fallback, int minimum, int maximum) => ParseInt(box.Text, fallback, minimum, maximum);
    private static int ParseInt(string? value, int fallback, int minimum, int maximum) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? Math.Clamp(parsed, minimum, maximum) : fallback;
    private static long ParseLong(WpfTextBox box, long fallback) => long.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    private static double ParseDouble(WpfTextBox box, double fallback, double minimum, double maximum) => double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? Math.Clamp(parsed, minimum, maximum) : fallback;
}
