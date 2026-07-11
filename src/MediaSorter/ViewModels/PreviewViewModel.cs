using CommunityToolkit.Mvvm.ComponentModel;
using MediaSorter.Models;
using MediaSorter.Services.Organization;

namespace MediaSorter.ViewModels;

public partial class PreviewViewModel : ObservableObject
{
    public PreviewViewModel(List<MovePlan> plans)
    {
        var groups = plans
            .GroupBy(p => p.TargetFolderName)
            .OrderBy(g => g.Key)
            .Select(g => new PreviewGroupViewModel(g.Key, g.Count(), g.First().DateTaken))
            .ToList();

        Moves = groups;
        TotalToMove = groups.Sum(m => m.Count);
        TotalFolders = groups.Count;
    }

    public IReadOnlyList<PreviewGroupViewModel> Moves { get; }
    public int TotalToMove { get; }
    public int TotalFolders { get; }
}

public partial class PreviewGroupViewModel : ObservableObject
{
    public PreviewGroupViewModel(string folderName, int count, DateOnly? date)
    {
        FolderName = folderName;
        Count = count;
        Date = date;
    }

    public string FolderName { get; }
    public int Count { get; }
    public DateOnly? Date { get; }
}