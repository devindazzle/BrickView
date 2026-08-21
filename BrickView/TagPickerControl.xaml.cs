// -----------------------------------------------------------------------------
// TagPickerControl.xaml.cs
//
// Provides the interaction logic for BrickView's compact tag picker.
//
// Responsibilities:
// - Displays the available tags supplied by the shared TagService.
// - Filters tags according to the user's search text.
// - Assigns existing tags to the currently selected model.
// - Creates and assigns a new tag from the search field.
// - Confirms and performs global tag deletion.
// - Notifies MainWindow when a tag has been deleted globally.
//
// TagService remains responsible for tag business rules, tag storage and
// persistence. This control is responsible only for presentation-level
// interaction and for coordinating calls to TagService.
//
// A model can have a maximum of three tags. The picker therefore prevents
// opening when the target model has already reached that limit.
//
// Global tag deletion is confirmed here before the destructive operation is
// performed by TagService. After a successful deletion the control raises
// TagDeleted so MainWindow can refresh the tag snapshots of affected models.
// -----------------------------------------------------------------------------

using System.Windows;
using System.Windows.Controls;

namespace BrickView;

/// <summary>
/// Provides the compact tag-picker UI and coordinates user interaction with
/// the shared <see cref="TagService"/>.
/// </summary>
public partial class TagPickerControl : UserControl {
    private TagService? tagService;

    private IoFileListItem? targetItem;

    private readonly List<TagDefinition> allTags;

    /// <summary>
    /// Identifies the routed event raised after a tag has been deleted globally.
    /// </summary>
    public static readonly RoutedEvent TagDeletedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(TagDeleted),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(TagPickerControl));

    /// <summary>
    /// Occurs after a tag has been deleted globally through the picker.
    /// </summary>
    /// <remarks>
    /// The event bubbles through the WPF visual tree so the owning window can
    /// refresh the tag snapshots of visible model items.
    /// </remarks>
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

    /// <summary>
    /// Gets or sets the shared tag service used by the picker.
    /// </summary>
    /// <remarks>
    /// Assigning the service immediately refreshes the list of available tags.
    /// </remarks>
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

    /// <summary>
    /// Initializes the tag-picker control and its in-memory tag list.
    /// </summary>
    public TagPickerControl() {
        InitializeComponent();

        allTags =
            new List<TagDefinition>();
    }

    /// <summary>
    /// Opens the tag picker for the specified model and positions it relative
    /// to the supplied UI element.
    /// </summary>
    /// <param name="placementTarget">
    /// The UI element relative to which the picker should be displayed.
    /// </param>
    /// <param name="item">
    /// The model that will receive the selected tag.
    /// </param>
    /// <remarks>
    /// The picker is not opened when the tag service is unavailable, the model
    /// has no stable identity or the model already contains three tags.
    /// </remarks>
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

    /// <summary>
    /// Refreshes the visible tag list whenever the search text changes.
    /// </summary>
    /// <param name="sender">
    /// The search text box that raised the event.
    /// </param>
    /// <param name="e">
    /// Text-change event data supplied by WPF.
    /// </param>
    private void SearchTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e) {
        RefreshTagList();
    }

    /// <summary>
    /// Assigns the tag represented by the clicked tag chip to the current model.
    /// </summary>
    /// <param name="sender">
    /// The tag-chip button that was clicked.
    /// </param>
    /// <param name="e">
    /// Routed event data supplied by WPF.
    /// </param>
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

    /// <summary>
    /// Requests confirmation and, when confirmed, deletes the tag globally.
    /// </summary>
    /// <param name="sender">
    /// The delete button belonging to the selected tag.
    /// </param>
    /// <param name="e">
    /// Routed event data supplied by WPF.
    /// </param>
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

    /// <summary>
    /// Creates and assigns a new tag using the current search text.
    /// </summary>
    /// <param name="sender">
    /// The create-tag button that was clicked.
    /// </param>
    /// <param name="e">
    /// Routed event data supplied by WPF.
    /// </param>
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

    /// <summary>
    /// Adds the specified tag to the current model through TagService and
    /// refreshes the model's local tag snapshot when the operation succeeds.
    /// </summary>
    /// <param name="tagName">
    /// The name of the tag to assign to the current model.
    /// </param>
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

    /// <summary>
    /// Displays a confirmation dialog and deletes the specified tag globally
    /// when the user confirms the destructive operation.
    /// </summary>
    /// <param name="tag">
    /// The tag to delete.
    /// </param>
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

        // Notify the owning window so it can refresh the tag snapshots of
        // other visible models affected by the global deletion.
        RaiseEvent(
            new RoutedEventArgs(
                TagDeletedEvent,
                this));
    }

    /// <summary>
    /// Rebuilds the available tag list from TagService, applies the current
    /// search filter and updates the create-tag controls.
    /// </summary>
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

    /// <summary>
    /// Updates the visibility and text of the create-tag controls according
    /// to the current search text and whether a matching tag already exists.
    /// </summary>
    /// <param name="searchText">
    /// The normalized search text currently entered by the user.
    /// </param>
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

    /// <summary>
    /// Closes the tag picker and clears its current model target.
    /// </summary>
    private void Close() {
        TagPickerPopup.IsOpen =
            false;

        targetItem =
            null;
    }
}