using System.ComponentModel;

namespace KnowledgeAssistant.Wpf.Models
{
    public class TopicSelectionItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int? ParentId { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}