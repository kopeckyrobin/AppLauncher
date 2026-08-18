using AppLauncher.Models;

namespace AppLauncher.ViewModels;

public sealed class GitFileViewModel : ObservableBase
{
    private readonly Action<GitFileViewModel> _onSelected;
    private readonly RelayCommand _selectCommand;
    private bool _isSelected;

    public GitFileViewModel(GitFileChange change, Action<GitFileViewModel> onSelected)
    {
        this.Change = change;
        this._onSelected = onSelected;
        this._selectCommand = new RelayCommand(this.Select);
    }

    public GitFileChange Change { get; }

    public string FileName
    {
        get { return this.Change.FileName; }
    }

    public string DirectoryName
    {
        get { return this.Change.DirectoryName; }
    }

    public bool HasDirectory
    {
        get { return !String.IsNullOrEmpty(this.Change.DirectoryName); }
    }

    public string StatusCode
    {
        get { return this.Change.StatusCode; }
    }

    public Color StatusColor
    {
        get
        {
            switch (this.Change.StatusCode)
            {
                case "A":
                    return Color.FromArgb("#3FB950");
                case "D":
                    return Color.FromArgb("#E5534B");
                case "U":
                    return Color.FromArgb("#8B949E");
                case "R":
                case "C":
                    return Color.FromArgb("#5B8DEF");
                default:
                    return Color.FromArgb("#D9A441");
            }
        }
    }

    public System.Windows.Input.ICommand SelectCommand
    {
        get { return this._selectCommand; }
    }

    public bool IsSelected
    {
        get { return this._isSelected; }
        set { this.SetProperty(ref this._isSelected, value); }
    }

    private void Select()
    {
        this._onSelected(this);
    }
}
