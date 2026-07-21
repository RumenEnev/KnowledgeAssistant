using System.ComponentModel;

namespace KnowledgeAssistant.Wpf.Models
{
    /// <summary>Selectable topic shown as a checkbox item when adding a document.</summary>
    public class TopicSelectionItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
