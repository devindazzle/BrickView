// -----------------------------------------------------------------------------
// TagColorPalette.cs
//
// Defines the automatic color system used by BrickView tags.
//
// Responsibilities:
// - Defines TagColorDefinition, which stores a tag's background and border colors.
// - Provides BrickView's fixed palette of deliberately muted tag colors.
// - Selects colors deterministically so the same tag name receives the same
//   generated color.
// - Avoids reusing an already assigned background color.
// - Generates additional colors when the fixed palette is exhausted.
// - Validates generated colors against BrickView's light tag text color.
// - Provides a deterministic fallback when the normal generated color space
//   cannot produce a suitable unused color.
//
// Users never choose tag colors themselves. BrickView assigns colors
// automatically and keeps the assigned color stable through persistence.
//
// Generated colors must provide a minimum WCAG contrast ratio of 4.5:1 against
// the tag text color (#F3F3F3).
// -----------------------------------------------------------------------------

namespace BrickView;

/// <summary>
/// Defines the background and border colors assigned to a BrickView tag.
/// </summary>
public sealed class TagColorDefinition {
    /// <summary>
    /// Gets the hexadecimal background color of the tag.
    /// </summary>
    public string BackgroundColor {
        get;
    }

    /// <summary>
    /// Gets the hexadecimal border color of the tag.
    /// </summary>
    public string BorderColor {
        get;
    }

    /// <summary>
    /// Initializes a new tag color definition.
    /// </summary>
    /// <param name="backgroundColor">
    /// The hexadecimal background color.
    /// </param>
    /// <param name="borderColor">
    /// The hexadecimal border color.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when either color is null, empty or consists only of whitespace.
    /// </exception>
    public TagColorDefinition(
        string backgroundColor,
        string borderColor) {
        if (string.IsNullOrWhiteSpace(
                backgroundColor)) {
            throw new ArgumentException(
                "A tag background color cannot be empty.",
                nameof(backgroundColor));
        }

        if (string.IsNullOrWhiteSpace(
                borderColor)) {
            throw new ArgumentException(
                "A tag border color cannot be empty.",
                nameof(borderColor));
        }

        BackgroundColor =
            backgroundColor;

        BorderColor =
            borderColor;
    }
}

/// <summary>
/// Provides the fixed and deterministic generated color palette used by
/// BrickView tags.
/// </summary>
public static class TagColorPalette {
    private const double MinimumTextContrastRatio =
        4.5;

    private const double GoldenAngle =
        137.50776405003785;

    private const double GeneratedSaturation =
        0.42;

    private const double GeneratedLightness =
        0.28;

    /// <summary>
    /// The fixed BrickView palette used before additional colors are generated.
    /// </summary>
    private static readonly IReadOnlyList<TagColorDefinition> colors =
        new List<TagColorDefinition> {
            new TagColorDefinition(
                "#3B617B",
                "#6E9DB8"),

            new TagColorDefinition(
                "#5E4F7F",
                "#9785BD"),

            new TagColorDefinition(
                "#31736D",
                "#61AFA6"),

            new TagColorDefinition(
                "#526F4E",
                "#83A77E"),

            new TagColorDefinition(
                "#825D3E",
                "#BD8B5B"),

            new TagColorDefinition(
                "#81484D",
                "#C17178"),

            new TagColorDefinition(
                "#7A4F67",
                "#B87999"),

            new TagColorDefinition(
                "#3C6D77",
                "#69AEBB"),

            new TagColorDefinition(
                "#4C5B87",
                "#7D91C4"),

            new TagColorDefinition(
                "#6D7041",
                "#A4A75F"),

            new TagColorDefinition(
                "#70513D",
                "#A97B59"),

            new TagColorDefinition(
                "#596474",
                "#899AAC")
        };

    /// <summary>
    /// Gets a deterministic, unused color for the specified tag name.
    /// </summary>
    /// <param name="tagName">
    /// The name of the tag that requires a color.
    /// </param>
    /// <param name="existingTags">
    /// The tag definitions whose background colors are already in use.
    /// </param>
    /// <returns>
    /// A tag color definition that does not reuse an existing background color.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="tagName"/> is null, empty or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="existingTags"/> is null.
    /// </exception>
    public static TagColorDefinition GetColor(
        string tagName,
        IEnumerable<TagDefinition> existingTags) {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            tagName);

        ArgumentNullException.ThrowIfNull(
            existingTags);

        List<TagDefinition> existingTagList =
            existingTags.ToList();

        HashSet<string> usedBackgroundColors =
            new HashSet<string>(
                existingTagList
                    .Where(
                        tag =>
                            !string.IsNullOrWhiteSpace(
                                tag.BackgroundColor))
                    .Select(
                        tag =>
                            tag.BackgroundColor),
                StringComparer.OrdinalIgnoreCase);

        int startIndex =
            GetStableIndex(
                tagName);

        // Start with the fixed palette so the normal BrickView tag catalog
        // uses the deliberately selected standard colors.
        for (int offset = 0;
             offset < colors.Count;
             offset++) {

            int index =
                (startIndex + offset) %
                colors.Count;

            TagColorDefinition candidate =
                colors[index];

            if (!usedBackgroundColors.Contains(
                    candidate.BackgroundColor)) {

                return candidate;
            }
        }

        // All fixed colors are currently assigned, so generate a new
        // deterministic color rather than reusing an existing one.
        return GenerateAdditionalColor(
            tagName,
            usedBackgroundColors);
    }

    /// <summary>
    /// Generates an additional deterministic tag color when all fixed palette
    /// colors are already in use.
    /// </summary>
    /// <param name="tagName">
    /// The name of the tag requiring a color.
    /// </param>
    /// <param name="usedBackgroundColors">
    /// The background colors that cannot be reused.
    /// </param>
    /// <returns>
    /// A generated tag color with sufficient text contrast.
    /// </returns>
    private static TagColorDefinition GenerateAdditionalColor(
        string tagName,
        ISet<string> usedBackgroundColors) {
        int stableIndex =
            GetStableIndex(
                tagName);

        double baseHue =
            (
                stableIndex *
                GoldenAngle) %
            360.0;

        // Try multiple hues around the deterministic starting point so the
        // generated color is both readable and different from existing colors.
        for (int attempt = 0;
             attempt < 360;
             attempt++) {

            double hue =
                (
                    baseHue +
                    attempt *
                    11.0) %
                360.0;

            string backgroundColor =
                HslToHex(
                    hue,
                    GeneratedSaturation,
                    GeneratedLightness);

            if (usedBackgroundColors.Contains(
                    backgroundColor)) {
                continue;
            }

            if (!HasSufficientTextContrast(
                    backgroundColor)) {
                continue;
            }

            string borderColor =
                HslToHex(
                    hue,
                    GeneratedSaturation,
                    0.43);

            return new TagColorDefinition(
                backgroundColor,
                borderColor);
        }

        // The generated color space is deliberately large. This fallback
        // protects the method if all generated candidates are unsuitable.
        return GenerateFallbackColor(
            stableIndex,
            usedBackgroundColors);
    }

    /// <summary>
    /// Generates a deterministic fallback color by searching a constrained
    /// range of lightness and hue values.
    /// </summary>
    /// <param name="stableIndex">
    /// The stable index derived from the tag name.
    /// </param>
    /// <param name="usedBackgroundColors">
    /// The background colors that cannot be reused.
    /// </param>
    /// <returns>
    /// A tag color that satisfies the text-contrast requirement, or the final
    /// hard-coded fallback when no generated candidate is available.
    /// </returns>
    private static TagColorDefinition GenerateFallbackColor(
        int stableIndex,
        ISet<string> usedBackgroundColors) {
        for (int lightnessStep = 24;
             lightnessStep <= 32;
             lightnessStep++) {

            double lightness =
                lightnessStep /
                100.0;

            for (int hueStep = 0;
                 hueStep < 360;
                 hueStep += 5) {

                double hue =
                    (
                        stableIndex *
                        17.0 +
                        hueStep) %
                    360.0;

                string backgroundColor =
                    HslToHex(
                        hue,
                        GeneratedSaturation,
                        lightness);

                if (usedBackgroundColors.Contains(
                        backgroundColor)) {
                    continue;
                }

                if (!HasSufficientTextContrast(
                        backgroundColor)) {
                    continue;
                }

                string borderColor =
                    HslToHex(
                        hue,
                        GeneratedSaturation,
                        Math.Min(
                            lightness + 0.15,
                            0.50));

                return new TagColorDefinition(
                    backgroundColor,
                    borderColor);
            }
        }

        // This final fallback is intentionally very dark, which guarantees
        // readable light text even if the generated search space is exhausted.
        const string fallbackBackground =
            "#30343B";

        const string fallbackBorder =
            "#68717D";

        return new TagColorDefinition(
            fallbackBackground,
            fallbackBorder);
    }

    /// <summary>
    /// Determines whether the specified background color provides the required
    /// WCAG contrast ratio against BrickView's light tag text color.
    /// </summary>
    /// <param name="backgroundColor">
    /// The hexadecimal background color to validate.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the contrast ratio is at least 4.5:1;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    private static bool HasSufficientTextContrast(
        string backgroundColor) {
        double backgroundLuminance =
            GetRelativeLuminance(
                backgroundColor);

        double textLuminance =
            GetRelativeLuminance(
                "#F3F3F3");

        double lighter =
            Math.Max(
                backgroundLuminance,
                textLuminance);

        double darker =
            Math.Min(
                backgroundLuminance,
                textLuminance);

        double contrastRatio =
            (
                lighter + 0.05) /
            (
                darker + 0.05);

        return contrastRatio >=
               MinimumTextContrastRatio;
    }

    /// <summary>
    /// Calculates the WCAG relative luminance of a hexadecimal RGB color.
    /// </summary>
    /// <param name="hexColor">
    /// The hexadecimal RGB color to evaluate.
    /// </param>
    /// <returns>
    /// The relative luminance value of the color.
    /// </returns>
    private static double GetRelativeLuminance(
        string hexColor) {
        (
            double red,
            double green,
            double blue) =
            HexToRgb(
                hexColor);

        double linearRed =
            ConvertToLinearColor(
                red);

        double linearGreen =
            ConvertToLinearColor(
                green);

        double linearBlue =
            ConvertToLinearColor(
                blue);

        return
            0.2126 * linearRed +
            0.7152 * linearGreen +
            0.0722 * linearBlue;
    }

    /// <summary>
    /// Converts one normalized sRGB color component to linear RGB space for
    /// use in WCAG luminance calculations.
    /// </summary>
    /// <param name="value">
    /// The normalized sRGB component value.
    /// </param>
    /// <returns>
    /// The corresponding linear RGB component value.
    /// </returns>
    private static double ConvertToLinearColor(
        double value) {
        if (value <= 0.03928) {
            return value / 12.92;
        }

        return Math.Pow(
            (
                value + 0.055) /
            1.055,
            2.4);
    }

    /// <summary>
    /// Converts a six-digit hexadecimal RGB color into normalized RGB components.
    /// </summary>
    /// <param name="hexColor">
    /// The hexadecimal RGB color, with or without a leading '#'.
    /// </param>
    /// <returns>
    /// A tuple containing normalized red, green and blue components.
    /// </returns>
    private static (
        double red,
        double green,
        double blue)
        HexToRgb(
            string hexColor) {
        string value =
            hexColor.TrimStart(
                '#');

        int red =
            Convert.ToInt32(
                value.Substring(
                    0,
                    2),
                16);

        int green =
            Convert.ToInt32(
                value.Substring(
                    2,
                    2),
                16);

        int blue =
            Convert.ToInt32(
                value.Substring(
                    4,
                    2),
                16);

        return (
            red / 255.0,
            green / 255.0,
            blue / 255.0);
    }

    /// <summary>
    /// Converts an HSL color value into a six-digit hexadecimal RGB color.
    /// </summary>
    /// <param name="hue">
    /// The hue in degrees.
    /// </param>
    /// <param name="saturation">
    /// The saturation from 0 to 1.
    /// </param>
    /// <param name="lightness">
    /// The lightness from 0 to 1.
    /// </param>
    /// <returns>
    /// The corresponding hexadecimal RGB color.
    /// </returns>
    private static string HslToHex(
        double hue,
        double saturation,
        double lightness) {
        hue =
            hue % 360.0;

        if (hue < 0) {
            hue += 360.0;
        }

        double chroma =
            (
                1.0 -
                Math.Abs(
                    2.0 * lightness -
                    1.0)) *
            saturation;

        double hueSegment =
            hue / 60.0;

        double secondary =
            chroma *
            (
                1.0 -
                Math.Abs(
                    hueSegment % 2.0 -
                    1.0));

        double redPrime;
        double greenPrime;
        double bluePrime;

        if (hueSegment < 1.0) {
            redPrime = chroma;
            greenPrime = secondary;
            bluePrime = 0.0;
        }
        else if (hueSegment < 2.0) {
            redPrime = secondary;
            greenPrime = chroma;
            bluePrime = 0.0;
        }
        else if (hueSegment < 3.0) {
            redPrime = 0.0;
            greenPrime = chroma;
            bluePrime = secondary;
        }
        else if (hueSegment < 4.0) {
            redPrime = 0.0;
            greenPrime = secondary;
            bluePrime = chroma;
        }
        else if (hueSegment < 5.0) {
            redPrime = secondary;
            greenPrime = 0.0;
            bluePrime = chroma;
        }
        else {
            redPrime = chroma;
            greenPrime = 0.0;
            bluePrime = secondary;
        }

        double match =
            lightness -
            chroma / 2.0;

        int red =
            Convert.ToInt32(
                Math.Round(
                    (
                        redPrime + match) *
                    255.0));

        int green =
            Convert.ToInt32(
                Math.Round(
                    (
                        greenPrime + match) *
                    255.0));

        int blue =
            Convert.ToInt32(
                Math.Round(
                    (
                        bluePrime + match) *
                    255.0));

        return
            $"#{red:X2}{green:X2}{blue:X2}";
    }

    /// <summary>
    /// Calculates a deterministic palette index from a tag name.
    /// </summary>
    /// <param name="tagName">
    /// The tag name from which the stable index should be calculated.
    /// </param>
    /// <returns>
    /// A stable index within the fixed palette range.
    /// </returns>
    private static int GetStableIndex(
        string tagName) {
        // FNV-1a-style hashing provides a small, deterministic and
        // inexpensive value for distributing tag names across the palette.
        const uint offsetBasis =
            2166136261;

        const uint prime =
            16777619;

        uint hash =
            offsetBasis;

        foreach (char character
                 in tagName.ToLowerInvariant()) {

            hash ^=
                character;

            hash *=
                prime;
        }

        return (int)(
            hash %
            (uint)colors.Count);
    }
}