using System.Windows;
using System.Collections.Generic;
using MediaSorter.Models;
using MediaSorter.ViewModels;

namespace MediaSorter.Views;

public partial class PreviewWindow : Window
{
    public PreviewWindow()
    {
        InitializeComponent();
    }

    public PreviewWindow(List<MovePlan> plans) : this()
    {
        DataContext = new PreviewViewModel(plans);
    }
}