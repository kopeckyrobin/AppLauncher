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
    private string _appliedLogText = String.Empty;
    private string _appliedLogSearch = String.Empty;
#if WINDOWS
    private bool _isLogPointerDown;
    private bool _isLogRefreshPending;
#endif

    public MainPage()
    {
        this.InitializeComponent();
#if WINDOWS
        this.LogEditor.HandlerChanged += this.OnLogEditorHandlerChanged;
#endif
        this.BindingContext = this._viewModel;
        this._viewModel.PropertyChanged += this.OnViewModelPropertyChanged;

        this._viewModel.Update.RestartGuard = this.ConfirmRestartAsync;
        this._viewModel.GitDiff.ConfirmGuard = this.ConfirmGitAsync;

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

    private async Task<bool> ConfirmGitAsync(string message)
    {
        return await this.DisplayAlertAsync("Git", message, "Pokračovat", "Zrušit");
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

        this.RefreshLog();
    }

    private void OnSelectedProjectPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(ProjectViewModel.LogText) or nameof(ProjectViewModel.SearchText))
        {
            this.RefreshLog();
        }
    }

    private void RefreshLog()
    {
        this.Dispatcher.Dispatch(() =>
        {
            ProjectViewModel? project = this._viewModel.SelectedProject;
            string text = project?.LogText ?? String.Empty;
            string search = project?.SearchText ?? String.Empty;

            if (this._appliedLogText == text && this._appliedLogSearch == search)
            {
                return;
            }

            bool searchChanged = this._appliedLogSearch != search;
            this._appliedLogText = text;
            this._appliedLogSearch = search;

#if WINDOWS
            if (this.LogEditor.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.RichEditBox box)
            {
                if (!searchChanged && (this._isLogPointerDown || box.Document.Selection.Length != 0))
                {
                    this._appliedLogText = this.LogEditor.Content.Text;
                    this._appliedLogSearch = this.LogEditor.Content.SearchText;
                    this._isLogRefreshPending = true;
                    return;
                }

                this._isLogRefreshPending = false;
                this.ApplyLogText(box, new LogContent(text, search), searchChanged && project is not null && project.HasMatches);
                return;
            }
#endif

            this.LogEditor.Content = new LogContent(text, search);
        });
    }

#if WINDOWS
    private void ApplyLogText(Microsoft.UI.Xaml.Controls.RichEditBox box, LogContent content, bool jumpToMatch)
    {
        Microsoft.UI.Xaml.Controls.ScrollViewer? viewer = FindScrollViewer(box);
        bool followEnd = viewer is null || viewer.ScrollableHeight - viewer.VerticalOffset < 8;
        double offset = viewer?.VerticalOffset ?? 0;

        this.LogEditor.Content = content;

        if (viewer is null || jumpToMatch)
        {
            return;
        }

        viewer.UpdateLayout();

        if (followEnd)
        {
            viewer.ChangeView(null, viewer.ScrollableHeight, null, true);
        }
        else
        {
            viewer.ChangeView(null, offset, null, true);
        }
    }

    private void OnLogEditorHandlerChanged(object? sender, EventArgs eventArgs)
    {
        if (this.LogEditor.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.RichEditBox box)
        {
            return;
        }

        box.AddHandler(
            Microsoft.UI.Xaml.UIElement.PointerPressedEvent,
            new Microsoft.UI.Xaml.Input.PointerEventHandler(this.OnLogPointerPressed),
            true);
        box.AddHandler(
            Microsoft.UI.Xaml.UIElement.PointerReleasedEvent,
            new Microsoft.UI.Xaml.Input.PointerEventHandler(this.OnLogPointerReleased),
            true);
        box.AddHandler(
            Microsoft.UI.Xaml.UIElement.PointerCaptureLostEvent,
            new Microsoft.UI.Xaml.Input.PointerEventHandler(this.OnLogPointerReleased),
            true);
        box.SelectionChanged += this.OnLogSelectionChanged;
    }

    private void OnLogPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs eventArgs)
    {
        this._isLogPointerDown = true;
    }

    private void OnLogPointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs eventArgs)
    {
        this._isLogPointerDown = false;
        this.FlushPendingLog();
    }

    private void OnLogSelectionChanged(object sender, Microsoft.UI.Xaml.RoutedEventArgs eventArgs)
    {
        this.FlushPendingLog();
    }

    private void FlushPendingLog()
    {
        if (!this._isLogRefreshPending || this._isLogPointerDown)
        {
            return;
        }

        if (this.LogEditor.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.RichEditBox box && box.Document.Selection.Length != 0)
        {
            return;
        }

        this.RefreshLog();
    }


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
