using System.Text.RegularExpressions;

namespace A2S.Domain.Aggregates.Meals;

/// <summary>
/// The one way an ingredient name becomes a shopping-list key: trim, collapse spaces, lower-case,
/// and drop a trailing "s" only when the singular is a word we know ("eggs" becomes "egg";
/// "oats" stays "oats"). Nothing cleverer: no stemming, no synonyms.
/// </summary>
public static class IngredientName
{
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Singular names the seed recipes use. A plural is only singularised when its singular is here,
    /// so words that end in "s" by nature ("oats", "beansprouts") are left alone.
    /// </summary>
    public static readonly IReadOnlySet<string> Vocabulary = new HashSet<string>(StringComparer.Ordinal)
    {
        "bagel",
        "banana",
        "bay leaf",
        "carrot",
        "cheddar",
        "chia seed",
        "chicken breast",
        "chicken thigh",
        "chipotle pepper in adobo",
        "clove",
        "cos lettuce leaf",
        "egg",
        "flaked almond",
        "flour tortilla",
        "garlic",
        "garlic clove",
        "lemon",
        "lime",
        "mature cheddar",
        "mixed berry",
        "onion",
        "red onion",
        "red pepper",
        "sesame seed",
        "smoked back bacon",
        "spring onion",
        "sweet potato",
        "tomato",
        "tortilla"
    };

    public static string Normalise(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var cleaned = Spaces.Replace(name.Trim(), " ").ToLowerInvariant();
        if (cleaned.Length > 1 && cleaned.EndsWith('s'))
        {
            var singular = cleaned[..^1];
            if (Vocabulary.Contains(singular))
            {
                return singular;
            }
        }

        return cleaned;
    }
}
