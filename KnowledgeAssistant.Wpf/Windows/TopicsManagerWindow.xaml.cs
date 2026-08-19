using KnowledgeAssistant.Domain.Documents;
using KnowledgeAssistant.Wpf.Messages.Documents;
using MessageServices;
using MessageServices.Messages;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace KnowledgeAssistant.Wpf.Windows
{
    public partial class TopicsManagerWindow : Window, INotifyPropertyChanged, IMessageServiceSubscriber
    {
        private readonly MessageService _messageService;

        private List<Topic> _allTopics = new();

        private string? _newTopicName;
        private string? _statusMessage;
        private int? _editingTopicId;
        private int? _selectedParentId;
        private bool _isSaving;

        // Drag-and-drop state
        private Point _dragStartPoint;
        private TopicNode? _dragCandidate;

        public TopicsManagerWindow(MessageService messageService)
        {
            InitializeComponent();
            DataContext = this;

            _messageService = messageService;
            _messageService.Subscribe<TopicsUpdatedEvent>(this, TopicsUpdatedEventReceived);
            _messageService.Subscribe<UserMessage>(this, UserMessageReceived);

            RebuildParentOptions();
        }

        public ObservableCollection<TopicNode> RootTopics { get; } = new ObservableCollection<TopicNode>();

        public ObservableCollection<ParentOption> ParentOptions { get; } = new ObservableCollection<ParentOption>();

        public string? NewTopicName
        {
            get => _newTopicName;
            set { _newTopicName = value; OnPropertyChanged(nameof(NewTopicName)); }
        }

        public string? StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); }
        }

        public int? EditingTopicId
        {
            get => _editingTopicId;
            set
            {
                _editingTopicId = value;
                OnPropertyChanged(nameof(EditingTopicId));
                OnPropertyChanged(nameof(FormHeader));
                OnPropertyChanged(nameof(SubmitButtonText));
                OnPropertyChanged(nameof(CancelButtonVisibility));
            }
        }

        public int? SelectedParentId
        {
            get => _selectedParentId;
            set { _selectedParentId = value; OnPropertyChanged(nameof(SelectedParentId)); }
        }

        public string FormHeader => EditingTopicId is null ? "Add Topic" : "Rename Topic";

        public bool IsSaving
        {
            get => _isSaving;
            set
            {
                _isSaving = value;
                OnPropertyChanged(nameof(IsSaving));
                OnPropertyChanged(nameof(SubmitButtonText));
                OnPropertyChanged(nameof(CanSubmit));
            }
        }

        public bool CanSubmit => !IsSaving;

        public string SubmitButtonText
        {
            get
            {
                if (IsSaving)
                    return EditingTopicId is null ? "Adding..." : "Saving...";

                return EditingTopicId is null ? "Add Topic" : "Save";
            }
        }

        public Visibility CancelButtonVisibility => EditingTopicId is null ? Visibility.Collapsed : Visibility.Visible;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void TopicsManagerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _messageService.Publish(new GetTopicsRequest());
        }

        private void TopicsUpdatedEventReceived(MessageBase message)
        {
            if (message is TopicsUpdatedEvent @event)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _allTopics = @event.Topics.ToList();
                    RebuildTree();
                    RebuildParentOptions();

                    IsSaving = false;
                    StatusMessage = $"{_allTopics.Count} topic(s) loaded.";
                });
            }
        }

        private void UserMessageReceived(MessageBase message)
        {
            if (message is UserMessage { Title: "Add Topic Failed" or "Update Topic Failed" or "Delete Topic Failed" })
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => IsSaving = false);
            }
        }

        /// <summary>
        /// Rebuilds the tree of TopicNode objects bound to the TreeView from the flat
        /// list of topics, preserving each node's expanded/collapsed state where possible.
        /// </summary>
        private void RebuildTree()
        {
            var wasExpanded = new HashSet<int>();
            CollectExpandedIds(RootTopics, wasExpanded);

            RootTopics.Clear();

            var nodesById = _allTopics.ToDictionary(t => t.Id, t => new TopicNode(t));

            foreach (var node in nodesById.Values.OrderBy(n => n.Name))
            {
                node.IsExpanded = wasExpanded.Count == 0 || wasExpanded.Contains(node.Id);

                if (node.ParentId is int parentId && nodesById.TryGetValue(parentId, out var parentNode))
                {
                    parentNode.Children.Add(node);
                }
                else
                {
                    RootTopics.Add(node);
                }
            }
        }

        private static void CollectExpandedIds(IEnumerable<TopicNode> nodes, HashSet<int> result)
        {
            foreach (var node in nodes)
            {
                if (node.IsExpanded)
                {
                    result.Add(node.Id);
                }

                CollectExpandedIds(node.Children, result);
            }
        }

        /// <summary>
        /// Rebuilds the flat, indented list used by the "parent" ComboBox in the form.
        /// Excludes the topic currently being edited and all of its descendants, since a
        /// topic can never become its own ancestor.
        /// </summary>
        private void RebuildParentOptions()
        {
            ParentOptions.Clear();
            ParentOptions.Add(new ParentOption { Id = null, DisplayName = "(No parent — top level)" });

            var excludedIds = EditingTopicId is int editingId
                ? GetSelfAndDescendantIds(editingId)
                : new HashSet<int>();

            void AddChildren(int? parentId, int depth)
            {
                foreach (var topic in _allTopics.Where(t => t.ParentId == parentId).OrderBy(t => t.Name))
                {
                    if (excludedIds.Contains(topic.Id))
                    {
                        continue;
                    }

                    var indent = depth > 0 ? new string(' ', depth * 3) + "└ " : string.Empty;
                    ParentOptions.Add(new ParentOption { Id = topic.Id, DisplayName = indent + topic.Name });

                    AddChildren(topic.Id, depth + 1);
                }
            }

            AddChildren(null, 0);
        }

        private HashSet<int> GetSelfAndDescendantIds(int topicId)
        {
            var result = new HashSet<int> { topicId };
            var queue = new Queue<int>();
            queue.Enqueue(topicId);

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                foreach (var child in _allTopics.Where(t => t.ParentId == currentId))
                {
                    if (result.Add(child.Id))
                    {
                        queue.Enqueue(child.Id);
                    }
                }
            }

            return result;
        }

        private void SubmitTopic_Click(object sender, RoutedEventArgs e)
        {
            if (IsSaving)
            {
                return;
            }

            var name = NewTopicName?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Topic name is required.", "Manage Topics", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (EditingTopicId is int selfId && SelectedParentId == selfId)
            {
                MessageBox.Show("A topic cannot be its own parent.", "Manage Topics", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsSaving = true;
            if (EditingTopicId is int topicId)
            {
                _messageService.Publish(new UpdateTopicRequest(topicId, name, SelectedParentId));
            }
            else
            {
                _messageService.Publish(new CreateTopicRequest(name, SelectedParentId));
            }

            ClearForm();
        }

        private void EditTopic_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: TopicNode node })
            {
                return;
            }

            EditingTopicId = node.Id;
            NewTopicName = node.Name;
            RebuildParentOptions();
            SelectedParentId = node.ParentId;
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            EditingTopicId = null;
            NewTopicName = string.Empty;
            SelectedParentId = null;
            RebuildParentOptions();
        }

        private void DeleteTopic_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: TopicNode node })
            {
                return;
            }

            var childWarning = node.Children.Count > 0
                ? $"\n\nThis topic has {node.Children.Count} subtopic(s)."
                : string.Empty;

            var result = MessageBox.Show(
                $"Are you sure you want to delete '{node.Name}'?{childWarning}",
                "Delete Topic",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            if (EditingTopicId == node.Id)
            {
                ClearForm();
            }

            _messageService.Publish(new DeleteTopicRequest(node.Id));
        }

        // --- Drag-and-drop reparenting ---

        private void TreeViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _dragCandidate = (sender as FrameworkElement)?.DataContext as TopicNode;
        }

        private void TreeViewItem_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragCandidate is null)
            {
                return;
            }

            var position = e.GetPosition(null);
            if (Math.Abs(position.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(position.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            var nodeToDrag = _dragCandidate;
            _dragCandidate = null;

            if (sender is DependencyObject source)
            {
                DragDrop.DoDragDrop(source, nodeToDrag, DragDropEffects.Move);
            }
        }

        private void TreeViewItem_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(typeof(TopicNode)) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void TreeViewItem_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;

            if (e.Data.GetData(typeof(TopicNode)) is not TopicNode draggedNode)
            {
                return;
            }

            if (sender is not FrameworkElement { DataContext: TopicNode targetNode })
            {
                return;
            }

            if (draggedNode.Id == targetNode.Id || draggedNode.ParentId == targetNode.Id)
            {
                return;
            }

            if (IsDescendant(draggedNode, targetNode.Id))
            {
                MessageBox.Show("Can't move a topic into one of its own subtopics.", "Manage Topics", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _messageService.Publish(new UpdateTopicRequest(draggedNode.Id, draggedNode.Name, targetNode.Id));
        }

        private void TopicsTree_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(typeof(TopicNode)) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void TopicsTree_Drop(object sender, DragEventArgs e)
        {
            // Only handles drops on empty tree space; drops on an item are already
            // handled (and marked e.Handled = true) by TreeViewItem_Drop above.
            if (e.Handled)
            {
                return;
            }

            if (e.Data.GetData(typeof(TopicNode)) is not TopicNode draggedNode)
            {
                return;
            }

            if (draggedNode.ParentId is null)
            {
                return;
            }

            _messageService.Publish(new UpdateTopicRequest(draggedNode.Id, draggedNode.Name, null));
        }

        private static bool IsDescendant(TopicNode candidateAncestor, int nodeId)
        {
            foreach (var child in candidateAncestor.Children)
            {
                if (child.Id == nodeId || IsDescendant(child, nodeId))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public class TopicNode : INotifyPropertyChanged
    {
        private bool _isExpanded = true;

        public TopicNode(Topic topic)
        {
            Topic = topic;
        }

        public Topic Topic { get; }

        public int Id => Topic.Id;

        public string Name => Topic.Name;

        public int? ParentId => Topic.ParentId;

        public ObservableCollection<TopicNode> Children { get; } = new ObservableCollection<TopicNode>();

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

    public class ParentOption
    {
        public int? Id { get; init; }

        public string DisplayName { get; init; } = string.Empty;
    }
}