using System.Collections.ObjectModel;
using System.ComponentModel;

namespace KnowledgeAssistant.Wpf.Models;

public class TopicSelectionNode : INotifyPropertyChanged
{
    private bool _isExpanded = true;

    public TopicSelectionNode(TopicSelectionItem item)
    {
        Item = item;
        Item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TopicSelectionItem.IsSelected))
            {
                OnPropertyChanged(nameof(IsSelected));
            }
        };
    }

    public TopicSelectionItem Item { get; }

    public int Id => Item.Id;

    public string Name => Item.Name;

    public bool IsSelected
    {
        get => Item.IsSelected;
        set => Item.IsSelected = value;
    }

    public ObservableCollection<TopicSelectionNode> Children { get; } = new ObservableCollection<TopicSelectionNode>();

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}