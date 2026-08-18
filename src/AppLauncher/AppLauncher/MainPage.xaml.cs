using System.ComponentModel;
using AppLauncher.ViewModels;
using AppLauncher.Views;

namespace AppLauncher;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel = new();
    private readonly ChangeMarkerDrawable _markerDrawable = new();
    private ProjectViewModel? _observedProject;
    private bool _isInitialized;

    public MainPage()
    {
        this.InitializeComponent();
        this.BindingContext = this._viewModel;
        this._viewModel.PropertyChanged += this.OnViewModelPropertyChanged;

        this.MarkerView.Drawable = this._markerDrawable;
        this._viewModel.GitDiff.PropertyChanged += this.OnGitDiffPropertyChanged;
        this.InlineDiffView.Scrolled += this.OnDiffScrolled;
        this.SideDiffView.Scrolled += this.OnDiffScrolled;
    }

    public void Shutdown()
    {
        this._viewModel.Shutdown();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (this._isInitialized)
        {
            return;
        }

        this._isInitialized = true;
        this._viewModel.Initialize();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName != nameof(MainViewModel.SelectedProject))
        {
            return;
        }

        if (this._observedProject is not null)
        {
            this._observedProject.PropertyChanged -= this.OnSelectedProjectPropertyChanged;
        }

        this._observedProject = this._viewModel.SelectedProject;

        if (this._observedProject is not null)
        {
            this._observedProject.PropertyChanged += this.OnSelectedProjectPropertyChanged;
        }

        this.ScrollLogToEnd();
    }

    private void OnSelectedProjectPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(ProjectViewModel.LogText))
        {
            this.ScrollLogToEnd();
        }
    }

    private void OnGitDiffPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName != nameof(GitDiffViewModel.Markers))
        {
            return;
        }

        this._markerDrawable.Markers = this._viewModel.GitDiff.Markers;
        this._markerDrawable.ViewportStart = 0;
        this._markerDrawable.ViewportEnd = 0;
        this.MarkerView.Invalidate();
    }

    private void OnDiffScrolled(object? sender, ItemsViewScrolledEventArgs eventArgs)
    {
        int total = this._viewModel.GitDiff.IsSideBySide
            ? this._viewModel.GitDiff.SideRows.Count
            : this._viewModel.GitDiff.InlineLines.Count;

        if (total <= 0)
        {
            return;
        }

        double first = Math.Max(eventArgs.FirstVisibleItemIndex, 0);
        double last = Math.Max(eventArgs.LastVisibleItemIndex, eventArgs.FirstVisibleItemIndex);

        this._markerDrawable.ViewportStart = first / total;
        this._markerDrawable.ViewportEnd = Math.Min((last + 1) / total, 1);
        this.MarkerView.Invalidate();
    }

    private void ScrollLogToEnd()
    {
        this.Dispatcher.Dispatch(() =>
        {
            double target = this.LogScrollView.ContentSize.Height - this.LogScrollView.Height;

            if (target > 0)
            {
                this.LogScrollView.ScrollToAsync(0, target, false);
            }
        });
    }
}
