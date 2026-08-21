// -----------------------------------------------------------------------------
// SmartSearchQuery.cs
//
// Parses the text entered in BrickView's Smart Search field into independent
// search criteria.
//
// Supported syntax:
// - Unqualified text searches model name and tags.
// - name:value searches the model name.
// - tag:value searches tags.
// - is:favorite searches favorite models.
// - is:not-favorite searches non-favorite models.
// - A leading '-' negates a criterion.
// - Quoted text is treated as one search token.
//
// Responsibilities:
// - Converts raw search text into SmartSearchCriterion instances.
// - Identifies supported search fields and special "is:" expressions.
// - Handles negated criteria.
// - Preserves quoted text as a single search token.
//
// The query parser contains no UI or persistence responsibilities.
// SmartSearchEngine is responsible for evaluating the parsed criteria against
// BrickView model items.
// -----------------------------------------------------------------------------

namespace BrickView;

/// <summary>
/// Represents a parsed Smart Search query and its individual search criteria.
/// </summary>
public sealed class SmartSearchQuery {
    /// <summary>
    /// Gets an empty search query containing no criteria.
    /// </summary>
    public static SmartSearchQuery Empty {
        get;
    } =
        new SmartSearchQuery(
            Array.Empty<SmartSearchCriterion>());

    /// <summary>
    /// Initializes a parsed search query with the supplied criteria.
    /// </summary>
    /// <param name="criteria">
    /// The search criteria represented by the query.
    /// </param>
    private SmartSearchQuery(
        IReadOnlyList<SmartSearchCriterion> criteria) {
        Criteria =
            criteria;
    }

    /// <summary>
    /// Gets the individual criteria contained in the parsed query.
    /// </summary>
    public IReadOnlyList<SmartSearchCriterion> Criteria {
        get;
    }

    /// <summary>
    /// Gets a value indicating whether the query contains no search criteria.
    /// </summary>
    public bool IsEmpty {
        get {
            return Criteria.Count == 0;
        }
    }

    /// <summary>
    /// Parses raw Smart Search text into a structured query.
    /// </summary>
    /// <param name="searchText">
    /// The text entered by the user. Null, empty and whitespace-only input
    /// produces <see cref="Empty"/>.
    /// </param>
    /// <returns>
    /// A parsed <see cref="SmartSearchQuery"/> containing the recognized
    /// search criteria.
    /// </returns>
    public static SmartSearchQuery Parse(
        string? searchText) {
        if (string.IsNullOrWhiteSpace(
                searchText)) {
            return Empty;
        }

        List<SmartSearchCriterion> criteria =
            new List<SmartSearchCriterion>();

        foreach (string token
                 in Tokenize(searchText)) {

            SmartSearchCriterion? criterion =
                ParseToken(
                    token);

            if (criterion is not null) {
                criteria.Add(
                    criterion);
            }
        }

        if (criteria.Count == 0) {
            return Empty;
        }

        return new SmartSearchQuery(
            criteria);
    }

    /// <summary>
    /// Parses one token into a search criterion.
    /// </summary>
    /// <param name="token">
    /// The individual token extracted from the search text.
    /// </param>
    /// <returns>
    /// A parsed criterion, or <see langword="null"/> when the token does not
    /// contain a usable search expression.
    /// </returns>
    private static SmartSearchCriterion? ParseToken(
        string token) {
        if (string.IsNullOrWhiteSpace(
                token)) {
            return null;
        }

        bool isNegated =
            token[0] == '-';

        // Remove the leading negation marker before interpreting the field
        // and value. The negation state is retained separately on the criterion.
        string expression =
            isNegated
                ? token[1..]
                : token;

        if (string.IsNullOrWhiteSpace(
                expression)) {
            return null;
        }

        int separatorIndex =
            expression.IndexOf(
                ':');

        if (separatorIndex <= 0) {
            return new SmartSearchCriterion(
                SmartSearchField.Text,
                expression,
                isNegated);
        }

        string field =
            expression[..separatorIndex]
                .Trim()
                .ToLowerInvariant();

        string value =
            expression[(separatorIndex + 1)..]
                .Trim();

        if (string.IsNullOrWhiteSpace(
                value)) {
            return new SmartSearchCriterion(
                SmartSearchField.Text,
                expression,
                isNegated);
        }

        switch (field) {
            case "name":

                return new SmartSearchCriterion(
                    SmartSearchField.Name,
                    value,
                    isNegated);

            case "tag":

                return new SmartSearchCriterion(
                    SmartSearchField.Tag,
                    value,
                    isNegated);

            case "is":

                return ParseIsCriterion(
                    value,
                    isNegated);

            default:

                // Unknown field prefixes are treated as ordinary text so an
                // unsupported search expression does not silently disappear.
                return new SmartSearchCriterion(
                    SmartSearchField.Text,
                    expression,
                    isNegated);
        }
    }

    /// <summary>
    /// Parses the value of an <c>is:</c> search expression.
    /// </summary>
    /// <param name="value">
    /// The value following the <c>is:</c> prefix.
    /// </param>
    /// <param name="isNegated">
    /// Indicates whether the complete expression was prefixed with '-'.
    /// </param>
    /// <returns>
    /// A Favorite criterion for supported values, or a text criterion when
    /// the <c>is:</c> value is unknown.
    /// </returns>
    private static SmartSearchCriterion? ParseIsCriterion(
        string value,
        bool isNegated) {
        if (value.Equals(
                "favorite",
                StringComparison.OrdinalIgnoreCase)) {
            return new SmartSearchCriterion(
                SmartSearchField.Favorite,
                string.Empty,
                isNegated);
        }

        if (value.Equals(
                "not-favorite",
                StringComparison.OrdinalIgnoreCase)) {
            // "is:not-favorite" already expresses the negative Favorite state.
            // An additional leading '-' therefore reverses that state.
            return new SmartSearchCriterion(
                SmartSearchField.Favorite,
                string.Empty,
                !isNegated);
        }

        return new SmartSearchCriterion(
            SmartSearchField.Text,
            $"is:{value}",
            isNegated);
    }

    /// <summary>
    /// Splits search text into tokens while keeping text enclosed in double
    /// quotes together as one token.
    /// </summary>
    /// <param name="searchText">
    /// The raw search text to tokenize.
    /// </param>
    /// <returns>
    /// The individual search tokens in their original order.
    /// </returns>
    private static IEnumerable<string> Tokenize(
        string searchText) {
        List<string> tokens =
            new List<string>();

        System.Text.StringBuilder token =
            new System.Text.StringBuilder();

        bool insideQuotes =
            false;

        foreach (char character
                 in searchText) {

            if (character == '"') {
                insideQuotes =
                    !insideQuotes;

                continue;
            }

            // Whitespace separates tokens only when it occurs outside a
            // quoted expression.
            if (char.IsWhiteSpace(character) &&
                !insideQuotes) {

                if (token.Length > 0) {
                    tokens.Add(
                        token.ToString());

                    token.Clear();
                }

                continue;
            }

            token.Append(
                character);
        }

        if (token.Length > 0) {
            tokens.Add(
                token.ToString());
        }

        return tokens;
    }
}

/// <summary>
/// Identifies the field against which a Smart Search criterion should be evaluated.
/// </summary>
public enum SmartSearchField {
    /// <summary>
    /// Performs a general text search against model name and tags.
    /// </summary>
    Text,

    /// <summary>
    /// Searches the model name only.
    /// </summary>
    Name,

    /// <summary>
    /// Searches assigned tag names.
    /// </summary>
    Tag,

    /// <summary>
    /// Searches the model's Favorite state.
    /// </summary>
    Favorite
}

/// <summary>
/// Represents one parsed Smart Search criterion.
/// </summary>
public sealed class SmartSearchCriterion {
    /// <summary>
    /// Initializes a search criterion.
    /// </summary>
    /// <param name="field">
    /// The field against which the criterion should be evaluated.
    /// </param>
    /// <param name="value">
    /// The value to match.
    /// </param>
    /// <param name="isNegated">
    /// Indicates whether the criterion is negated.
    /// </param>
    public SmartSearchCriterion(
        SmartSearchField field,
        string value,
        bool isNegated) {
        Field =
            field;

        Value =
            value;

        IsNegated =
            isNegated;
    }

    /// <summary>
    /// Gets the field against which the criterion should be evaluated.
    /// </summary>
    public SmartSearchField Field {
        get;
    }

    /// <summary>
    /// Gets the value used when evaluating the criterion.
    /// </summary>
    public string Value {
        get;
    }

    /// <summary>
    /// Gets a value indicating whether the criterion is negated.
    /// </summary>
    public bool IsNegated {
        get;
    }
}