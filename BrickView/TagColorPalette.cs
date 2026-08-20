// -----------------------------------------------------------------------------
// TagColorPalette.cs
//
// Defines the automatic color palette used by BrickView tags.
//
// Users never choose tag colors themselves. BrickView assigns colors
// automatically and keeps the assigned color stable through persistence.
//
// The palette contains deliberately muted colors that fit BrickView's dark UI.
// Once the fixed palette is exhausted, additional colors are generated
// deterministically from the tag name.
//
// Generated colors are validated against the tag text color so that the text
// remains readable. A minimum WCAG contrast ratio of 4.5:1 is required.
//
// -----------------------------------------------------------------------------

namespace BrickView;

public sealed class TagColorDefinition {
    public string BackgroundColor { get; }

    public string BorderColor { get; }

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

public static class TagColorPalette {
    private const double MinimumTextContrastRatio =
        4.5;

    private const double GoldenAngle =
        137.50776405003785;

    private const double GeneratedSaturation =
        0.42;

    private const double GeneratedLightness =
        0.28;

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

        // First use the fixed palette. This preserves the carefully selected
        // BrickView colors for the normal tag catalog.
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

        // All fixed colors are currently in use. Generate an additional color
        // instead of reusing an existing color.
        return GenerateAdditionalColor(
            tagName,
            usedBackgroundColors);
    }

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

        // Try multiple hues around the deterministic starting point. This
        // allows us to find a color that is both readable and sufficiently
        // different from colors already assigned to other tags.
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

        // The generated color space above is deliberately large enough that
        // reaching this point should be practically impossible. If it does
        // happen, use a deterministic fallback that still satisfies the
        // text-contrast requirement.
        return GenerateFallbackColor(
            stableIndex,
            usedBackgroundColors);
    }

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

        // This final fallback is intentionally extremely dark and therefore
        // guarantees readable light text.
        const string fallbackBackground =
            "#30343B";

        const string fallbackBorder =
            "#68717D";

        return new TagColorDefinition(
            fallbackBackground,
            fallbackBorder);
    }

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

    private static double GetRelativeLuminance(
        string hexColor) {

        (double red,
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

    private static int GetStableIndex(
        string tagName) {

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