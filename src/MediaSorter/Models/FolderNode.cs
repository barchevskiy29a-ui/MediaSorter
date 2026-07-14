using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace MediaSorter.Models;

public partial class FolderNode : ObservableObject
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public FolderNode? Parent { get; set; }
    public ObservableCollection<FolderNode> Children { get; set; } = new();

    private bool _isChecked = true;

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (SetProperty(ref _isChecked, value))
            {
                foreach (var child in Children)
                    child.ForceChecked(value);
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void ForceChecked(bool value)
    {
        if (SetProperty(ref _isChecked, value))
        {
            foreach (var child in Children)
                child.ForceChecked(value);
        }
    }

    public event EventHandler? CheckedChanged;
}
