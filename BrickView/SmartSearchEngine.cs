// -----------------------------------------------------------------------------
// SmartSearchEngine.cs
//
// Executes parsed Smart Search queries against BrickView model items.
//
// Matching rules:
// - Every criterion must match (AND semantics).
// - Unqualified text searches the model name and assigned tags.
// - name:value searches the model name only.
// - tag:value performs an exact tag-name match.
// - is:favorite matches favorite models.
// - is:not-favorite matches non-favorite models.
// - A leading '-' negates a criterion.
//
// The engine contains no UI or persistence responsibilities.
// -----------------------------------------------------------------------------

namespace BrickView;

public sealed class SmartSearchEngine {
    public IEnumerable<IoFileListItem> Search(
        IEnumerable<IoFileListItem> items,
        SmartSearchQuery query) {
        ArgumentNullException.ThrowIfNull(
            items);

        ArgumentNullException.ThrowIfNull(
            query);

        if (query.IsEmpty) {
            return items;
        }

        return items.Where(
            item =>
                Matches(
                    item,
                    query));
    }

    private static bool Matches(
        IoFileListItem item,
        SmartSearchQuery query) {
        foreach (SmartSearchCriterion criterion
                 in query.Criteria) {

            if (!MatchesCriterion(
                    item,
                    criterion)) {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesCriterion(
        IoFileListItem item,
        SmartSearchCriterion criterion) {
        bool matches =
            criterion.Field switch {
                SmartSearchField.Name =>
                    MatchesName(
                        item,
                        criterion.Value),

                SmartSearchField.Tag =>
                    MatchesTag(
                        item,
                        criterion.Value),

                SmartSearchField.Favorite =>
                    item.IsFavorite,

                _ =>
                    MatchesText(
                        item,
                        criterion.Value)
            };

        return criterion.IsNegated
            ? !matches
            : matches;
    }

    private static bool MatchesName(
        IoFileListItem item,
        string value) {
        return item.FileName.Contains(
            value,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesTag(
        IoFileListItem item,
        string value) {
        return item.Tags.Any(
            tag =>
                tag.Name.Equals(
                    value,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesText(
        IoFileListItem item,
        string value) {
        if (item.FileName.Contains(
                value,
                StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        return item.Tags.Any(
            tag =>
                tag.Name.Contains(
                    value,
                    StringComparison.OrdinalIgnoreCase));
    }
}