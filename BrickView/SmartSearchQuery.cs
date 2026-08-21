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
// The query parser contains no UI or persistence responsibilities.
// -----------------------------------------------------------------------------

namespace BrickView;

public sealed class SmartSearchQuery {
    public static SmartSearchQuery Empty {
        get;
    } =
        new SmartSearchQuery(
            Array.Empty<SmartSearchCriterion>());

    private SmartSearchQuery(
        IReadOnlyList<SmartSearchCriterion> criteria) {
        Criteria =
            criteria;
    }

    public IReadOnlyList<SmartSearchCriterion> Criteria {
        get;
    }

    public bool IsEmpty {
        get {
            return Criteria.Count == 0;
        }
    }

    public bool RequiresModelData {
        get {
            return Criteria.Any(
                criterion =>
                    criterion.Field !=
                    SmartSearchField.Name);
        }
    }

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

    private static SmartSearchCriterion? ParseToken(
        string token) {
        if (string.IsNullOrWhiteSpace(
                token)) {
            return null;
        }

        bool isNegated =
            token[0] == '-';

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

                return new SmartSearchCriterion(
                    SmartSearchField.Text,
                    expression,
                    isNegated);
        }
    }

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

public enum SmartSearchField {
    Text,
    Name,
    Tag,
    Favorite
}

public sealed class SmartSearchCriterion {
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

    public SmartSearchField Field {
        get;
    }

    public string Value {
        get;
    }

    public bool IsNegated {
        get;
    }
}