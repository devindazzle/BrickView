// -----------------------------------------------------------------------------
// SmartSearchEngine.cs
//
// Executes parsed Smart Search queries against BrickView model items.
//
// Responsibilities:
// - Applies SmartSearchQuery criteria to model items.
// - Supports AND semantics between multiple criteria.
// - Matches unqualified text against model names and assigned tags.
// - Supports name-only, exact tag-name and Favorite criteria.
// - Applies criterion negation.
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
// The engine contains no UI or persistence responsibilities. Query parsing is
// handled by SmartSearchQuery, while this class is responsible only for
// evaluating parsed criteria against model items.
// -----------------------------------------------------------------------------

namespace BrickView;

/// <summary>
/// Evaluates parsed Smart Search queries against BrickView model items.
/// </summary>
public sealed class SmartSearchEngine {
    /// <summary>
    /// Filters the supplied model items according to the specified parsed
    /// Smart Search query.
    /// </summary>
    /// <param name="items">
    /// The model items to evaluate.
    /// </param>
    /// <param name="query">
    /// The parsed Smart Search query containing the criteria to evaluate.
    /// </param>
    /// <returns>
    /// An enumerable containing only the model items that satisfy the query.
    /// When the query is empty, the original <paramref name="items"/>
    /// enumerable is returned unchanged.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="items"/> or <paramref name="query"/> is null.
    /// </exception>
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

    /// <summary>
    /// Determines whether a model satisfies every criterion in a Smart Search query.
    /// </summary>
    /// <param name="item">
    /// The model item to evaluate.
    /// </param>
    /// <param name="query">
    /// The parsed query containing the criteria to evaluate.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when every criterion matches the model;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    private static bool Matches(
        IoFileListItem item,
        SmartSearchQuery query) {
        // Smart Search uses AND semantics: one failed criterion is enough
        // to exclude the model from the result.
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

    /// <summary>
    /// Evaluates one Smart Search criterion against a model item and applies
    /// its negation state when required.
    /// </summary>
    /// <param name="item">
    /// The model item to evaluate.
    /// </param>
    /// <param name="criterion">
    /// The parsed search criterion to evaluate.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the model satisfies the criterion after
    /// applying its negation state; otherwise, <see langword="false"/>.
    /// </returns>
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

        // Negation is applied after the field-specific match has been evaluated
        // so every search criterion uses the same negation behavior.
        return criterion.IsNegated
            ? !matches
            : matches;
    }

    /// <summary>
    /// Determines whether the model name contains the specified search value,
    /// using case-insensitive matching.
    /// </summary>
    /// <param name="item">
    /// The model item to evaluate.
    /// </param>
    /// <param name="value">
    /// The text to search for in the model name.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the model name contains the value;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    private static bool MatchesName(
        IoFileListItem item,
        string value) {
        return item.FileName.Contains(
            value,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the model has a tag whose name exactly matches the
    /// specified value, using case-insensitive comparison.
    /// </summary>
    /// <param name="item">
    /// The model item to evaluate.
    /// </param>
    /// <param name="value">
    /// The tag name to search for.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the model has an exact matching tag;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    private static bool MatchesTag(
        IoFileListItem item,
        string value) {
        return item.Tags.Any(
            tag =>
                tag.Name.Equals(
                    value,
                    StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Determines whether the model name or one of its assigned tag names
    /// contains the specified search value, using case-insensitive matching.
    /// </summary>
    /// <param name="item">
    /// The model item to evaluate.
    /// </param>
    /// <param name="value">
    /// The text to search for in the model name and assigned tags.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the value occurs in the model name or
    /// an assigned tag; otherwise, <see langword="false"/>.
    /// </returns>
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