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

        this._viewModel.Update.RestartGuard = this.ConfirmRestartAsync;

        this.MarkerView.Drawable = this._markerDrawable;
        this._viewModel.GitDiff.PropertyChanged += this.OnGitDiffPropertyChanged;
        this._viewModel.GitDiff.MatchScrollRequested += this.OnMatchScrollRequested;
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

    private async Task<bool> ConfirmRestartAsync()
    {
        if (this._viewModel.HasRunning)
        {
            bool confirmed = await this.DisplayAlertAsync(
                "Aktualizace",
                "Aktualizace ukončí všechny spuštěné aplikace. Pokračovat?",
                "Restartovat",
                "Zrušit");

            if (!confirmed)
            {
                return false;
            }
        }

        this._viewModel.Shutdown();
        return true;
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

    private void ScrollLogToEnd()
    {
        this.Dispatcher.Dispatch(() =>
        {
#if WINDOWS
            if (this.LogEditor.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.TextBox textBox)
            {
                return;
            }

            Microsoft.UI.Xaml.Controls.ScrollViewer? viewer = FindScrollViewer(textBox);

            if (viewer is null)
            {
                return;
            }

            if (this._viewModel.SelectedProject is not null && this._viewModel.SelectedProject.HasSearch)
            {
                viewer.ChangeView(null, 0, null, true);
                return;
            }

            if (textBox.SelectionLength > 0)
            {
                return;
            }

            viewer.UpdateLayout();
            viewer.ChangeView(null, viewer.ScrollableHeight, null, true);
#endif
        });
    }

#if WINDOWS
    private static Microsoft.UI.Xaml.Controls.ScrollViewer? FindScrollViewer(Microsoft.UI.Xaml.DependencyObject root)
    {
        int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);

        for (int index = 0; index < count; index++)
        {
            Microsoft.UI.Xaml.DependencyObject child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, index);

            if (child is Microsoft.UI.Xaml.Controls.ScrollViewer viewer)
            {
                return viewer;
            }

            Microsoft.UI.Xaml.Controls.ScrollViewer? nested = FindScrollViewer(child);

            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
#endif

    private void OnMatchScrollRequested(object? sender, int index)
    {
        this.Dispatcher.Dispatch(() =>
        {
            if (this._viewModel.GitDiff.IsSideBySide)
            {
                this.SideDiffView.ScrollTo(index, position: ScrollToPosition.Center, animate: false);
            }
            else
            {
                this.InlineDiffView.ScrollTo(index, position: ScrollToPosition.Center, animate: false);
            }
        });
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
}
