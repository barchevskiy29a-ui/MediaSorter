using System;
using System.Windows.Forms;

namespace MediaSorter.Helpers;

public class OpenFolderDialog
{
    public string Title { get; set; } = "Выберите папку";
    public string? InitialDirectory { get; set; }
    public string? FolderName { get; private set; }

    public bool? ShowDialog()
    {
        var dialog = new FolderBrowserDialogWrapper
        {
            Description = Title,
            InitialDirectory = InitialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            ShowNewFolderButton = true
        };

        var result = dialog.ShowDialog();
        if (result == System.Windows.Forms.DialogResult.OK)
        {
            FolderName = dialog.SelectedPath;
            return true;
        }
        return false;
    }
}

public class FolderBrowserDialogWrapper : IDisposable
{
    public string Description { get; set; } = "";
    
    public string? InitialDirectory 
    { 
        get => _initialDirectory;
        set => _initialDirectory = value ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    }
    private string? _initialDirectory;

    public bool ShowNewFolderButton { get; set; } = true;
    private string? _selectedPath;

    public string SelectedPath => _selectedPath ?? "";
    
    public System.Windows.Forms.DialogResult ShowDialog()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = Description,
            SelectedPath = InitialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            ShowNewFolderButton = true
        };

        var result = dialog.ShowDialog();
        if (result == System.Windows.Forms.DialogResult.OK)
        {
            _selectedPath = dialog.SelectedPath;
        }
        return result;
    }

    public void Dispose() { }
}