// -----------------------------------------------------------------------------
// TagPickerControl.xaml.cs
//
// Provides the interaction logic for BrickView's compact tag picker.
//
// The control displays the existing tags from the shared TagService and allows
// the user to assign an existing tag or create a new tag directly from the
// picker.
//
// TagService remains responsible for tag business rules and persistence.
// This control is responsible only for presentation and user interaction.
//
// Global tag deletion is confirmed here before the TagService performs the
// destructive operation. After a successful deletion the control raises
// TagDeleted so MainWindow can refresh the tag snapshots of all visible models.
// -----------------------------------------------------------------------------

using System.Windows;
using System.Windows.Controls;

namespace BrickView;

public partial class TagPickerControl : UserControl {
    private TagService? tagService;

    private IoFileListItem? targetItem;

    private readonly List<TagDefinition> allTags;

    public static readonly RoutedEvent TagDeletedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(TagDeleted),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(TagPickerControl));

    public event RoutedEventHandler TagDeleted {
        add {
            AddHandler(
                TagDeletedEvent,
                value);
        }

        remove {
            RemoveHandler(
                TagDeletedEvent,
                value);
        }
    }

    public TagService? TagService {
        get {
            return tagService;
        }

        set {
            tagService =
                value;

            RefreshTagList();
        }
    }

    public TagPickerControl() {
        InitializeComponent();

        allTags =
            new List<TagDefinition>();
    }

    public void OpenFor(
        FrameworkElement placementTarget,
        IoFileListItem item) {
        ArgumentNullException.ThrowIfNull(
            placementTarget);

        ArgumentNullException.ThrowIfNull(
            item);

        if (tagService is null) {
            return;
        }

        if (item.ModelIdentity is null) {
            return;
        }

        // The TagService enforces a maximum of three tags per model. The
        // picker therefore cannot be opened once the model already has three.
        if (item.Tags.Count >= 3) {
            return;
        }

        targetItem =
            item;

        TagPickerPopup.PlacementTarget =
            placementTarget;

        SearchTextBox.Clear();

        RefreshTagList();

        TagPickerPopup.IsOpen =
            true;

        SearchTextBox.Focus();
    }

    private void SearchTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e) {
        RefreshTagList();
    }

    private void TagChip_Click(
        object sender,
        RoutedEventArgs e) {
        if (sender is not Button button) {
            return;
        }

        if (button.DataContext
            is not TagDefinition tag) {
            return;
        }

        AddTagToTarget(
            tag.Name);

        e.Handled =
            true;
    }

    private void DeleteTagButton_Click(
        object sender,
        RoutedEventArgs e) {
        if (sender is not Button button) {
            return;
        }

        if (button.DataContext
            is not TagDefinition tag) {
            return;
        }

        ConfirmAndDeleteTag(
            tag);

        e.Handled =
            true;
    }

    private void CreateTagButton_Click(
        object sender,
        RoutedEventArgs e) {
        string tagName =
            SearchTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(
                tagName)) {
            return;
        }

        AddTagToTarget(
            tagName);
    }

    private void AddTagToTarget(
        string tagName) {
        if (tagService is null ||
            targetItem is null ||
            targetItem.ModelIdentity is null) {
            return;
        }

        if (targetItem.Tags.Count >= 3) {
            Close();

            return;
        }

        bool added =
            tagService.AddTag(
                targetItem.ModelIdentity,
                tagName);

        if (added) {
            // Refresh the item's read-only tag snapshot immediately so the
            // card reflects the newly assigned tag without reloading the folder.
            targetItem.SetTags(
                tagService.GetTags(
                    targetItem.ModelIdentity));
        }

        Close();
    }

    private void ConfirmAndDeleteTag(
        TagDefinition tag) {
        if (tagService is null) {
            return;
        }

        int modelCount =
            tagService.GetModelCountUsingTag(
                tag.Name);

        string usageText;

        if (modelCount == 1) {
            usageText =
                "This tag is currently used by 1 model.";
        }
        else {
            usageText =
                $"This tag is currently used by {modelCount} models.";
        }

        string message =
            $"Are you sure you want to delete the tag \"{tag.Name}\"?\n\n" +
            usageText +
            "\nDeleting the tag will remove it from all affected models.\n\n" +
            "This action cannot be undone.";

        MessageBoxResult result =
            MessageBox.Show(
                message,
                "Delete tag",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

        if (result != MessageBoxResult.Yes) {
            return;
        }

        tagService.DeleteTag(
            tag.Name);

        RefreshTagList();

        RaiseEvent(
            new RoutedEventArgs(
                TagDeletedEvent,
                this));
    }

    private void RefreshTagList() {
        allTags.Clear();

        if (tagService is not null) {
            allTags.AddRange(
                tagService.GetAllTags()
                    .OrderBy(
                        tag => tag.Name,
                        StringComparer.OrdinalIgnoreCase));
        }

        string searchText =
            SearchTextBox?.Text.Trim() ??
            string.Empty;

        IEnumerable<TagDefinition> filteredTags =
            allTags;

        if (!string.IsNullOrWhiteSpace(
                searchText)) {
            filteredTags =
                allTags.Where(
                    tag =>
                        tag.Name.Contains(
                            searchText,
                            StringComparison.OrdinalIgnoreCase));
        }

        TagItemsControl.ItemsSource =
            filteredTags.ToList();

        UpdateCreateTagButton(
            searchText);
    }

    private void UpdateCreateTagButton(
        string searchText) {
        if (string.IsNullOrWhiteSpace(
                searchText) ||
            tagService is null) {
            CreateTagButton.Visibility =
                Visibility.Collapsed;

            CreateTagSeparator.Visibility =
                Visibility.Collapsed;

            return;
        }

        bool existingTag =
            tagService.TryGetTag(
                searchText,
                out _);

        if (existingTag) {
            CreateTagButton.Visibility =
                Visibility.Collapsed;

            CreateTagSeparator.Visibility =
                Visibility.Collapsed;

            return;
        }

        CreateTagText.Text =
            $"+ Create \"{searchText}\"";

        CreateTagSeparator.Visibility =
            Visibility.Visible;

        CreateTagButton.Visibility =
            Visibility.Visible;
    }

    private void Close() {
        TagPickerPopup.IsOpen =
            false;

        targetItem =
            null;
    }
}