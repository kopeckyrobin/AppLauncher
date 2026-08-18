using AppLauncher.Models;

namespace AppLauncher.Views;

public sealed class DiffLineTemplateSelector : DataTemplateSelector
{
    public DataTemplate? HunkTemplate { get; set; }

    public DataTemplate? LineTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        if (item is DiffLine line && line.IsHunk && this.HunkTemplate is not null)
        {
            return this.HunkTemplate;
        }

        return this.LineTemplate!;
    }
}

public sealed class DiffRowTemplateSelector : DataTemplateSelector
{
    public DataTemplate? HunkTemplate { get; set; }

    public DataTemplate? RowTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        if (item is DiffRow row && row.IsHunk && this.HunkTemplate is not null)
        {
            return this.HunkTemplate;
        }

        return this.RowTemplate!;
    }
}
