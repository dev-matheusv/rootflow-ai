using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RootFlow.Application.Abstractions.Search;

public static class SemanticQueryExpander
{
    private static readonly Regex TokenRegex = new(@"[\p{L}\p{Nd}]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly HashSet<string> StopWords =
    [
        "a", "an", "and", "are", "as", "at", "based", "be", "by", "can", "como", "com", "da", "das", "de",
        "devo", "do", "does", "dos", "e", "em", "for", "from", "have", "how", "i", "in", "is", "it", "me",
        "meu", "minha", "my", "na", "nas", "no", "nos", "o", "of", "on", "or", "os", "para", "por", "qual",
        "quais", "que", "should", "sobre", "the", "to", "uma", "um", "what", "when", "where", "which",
        "who", "why", "with", "you", "your"
    ];

    private static readonly ExpansionRule[] ExpansionRules =
    [
        new(
            ["breakfast", "cafe", "manha", "morning"],
            ["breakfast", "cafe da manha"],
            ["breakfast", "cafe", "manha", "morning", "meal", "food", "nutrition", "diet", "menu", "refeicao", "comida"],
            0.55),
        new(
            ["lunch", "almoco", "midday", "noon"],
            ["for lunch", "at lunch", "para almoco"],
            ["lunch", "almoco", "midday", "meal", "food", "nutrition", "diet", "menu", "refeicao", "comida", "plano", "alimentar"],
            0.60),
        new(
            ["dinner", "jantar", "supper", "evening"],
            ["for dinner", "at dinner", "para jantar"],
            ["dinner", "jantar", "supper", "evening", "meal", "food", "nutrition", "diet", "menu", "refeicao", "comida"],
            0.60),
        new(
            ["eat", "eating", "comer", "food", "comida", "meal", "refeicao"],
            ["what should i eat", "o que devo comer"],
            ["food", "comida", "meal", "refeicao", "nutrition", "diet", "menu", "ingredients", "almoco", "breakfast", "dinner"],
            0.35),
        new(
            ["diet", "dieta", "nutrition", "nutricao", "meal", "alimentar"],
            ["meal plan", "diet plan", "plano alimentar"],
            ["diet", "dieta", "nutrition", "nutricao", "meal", "refeicao", "food", "comida", "breakfast", "lunch", "dinner", "cafe", "almoco", "jantar", "plano", "alimentar"],
            0.45),
        new(
            ["training", "treino", "workout", "exercise", "exercicio", "fitness", "gym"],
            ["training plan", "workout plan", "plano de treino"],
            ["training", "treino", "workout", "exercise", "exercicio", "fitness", "gym", "routine", "program", "session", "strength", "cardio", "plan"],
            0.55),
        new(
            ["resume", "cv", "curriculo", "curriculum", "vitae", "candidate", "profile"],
            ["curriculum vitae", "candidate profile", "empresa atual", "current company"],
            ["resume", "cv", "curriculo", "curriculum", "vitae", "candidate", "profile", "experience", "skills", "habilidades", "competencias", "qualifications", "company", "empresa", "current"],
            0.60),
        new(
            ["experience", "experiencia", "work", "career", "employment", "job", "role", "employer", "company"],
            ["professional experience", "professional experiences", "work experience", "work history", "current role", "current job", "experiencia profissional", "experiencias profissionais", "historico profissional", "historico de trabalho", "empresa atual"],
            ["experience", "experiencia", "work", "history", "career", "employment", "job", "role", "position", "employer", "company", "resume", "cv", "curriculo", "professional", "empresa", "atual"],
            0.60),
        new(
            ["education", "educacao", "degree", "graduation", "graduacao", "university", "college", "school", "formacao", "curso"],
            ["academic background", "educational background", "formacao academica", "education history", "curso superior"],
            ["education", "educacao", "degree", "graduation", "graduacao", "university", "college", "school", "academic", "formation", "formacao", "qualification", "qualificacao", "resume", "curriculo", "curso", "superior", "academico", "academica"],
            0.55),
        new(
            ["formacao", "educacao", "education", "degree", "graduacao", "curso", "academic", "academico", "academica"],
            ["academic background", "educational background", "formacao academica", "education history", "curso superior"],
            ["education", "educacao", "formacao", "curso", "degree", "graduacao", "academic", "academico", "academica"],
            0.70),
        new(
            ["skills", "skill", "habilidades", "competencias", "experience", "stack"],
            ["core skills", "technical skills", "work experience"],
            ["skills", "habilidades", "competencias", "experience", "qualifications", "technology", "tools", "frameworks", "languages", "certifications"],
            0.50),
        new(
            ["technology", "technologies", "tech", "stack", "tools", "frameworks", "languages"],
            ["tech stack", "technology stack", "stack tecnologica"],
            ["technology", "technologies", "tech", "stack", "tools", "frameworks", "languages", "skills", "habilidades", "competencias", "platforms"],
            0.50)
    ];

    public static ExpandedSearchQuery Expand(string queryText)
    {
        ArgumentNullException.ThrowIfNull(queryText);

        var trimmedQuery = queryText.Trim();
        var originalTokens = Tokenize(trimmedQuery)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var termWeights = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var token in originalTokens)
        {
            AddWeight(termWeights, token, 1d);
        }

        var normalizedQuery = NormalizeText(trimmedQuery);
        var tokenSet = originalTokens.ToHashSet(StringComparer.Ordinal);
        foreach (var rule in ExpansionRules)
        {
            if (!rule.Matches(tokenSet, normalizedQuery))
            {
                continue;
            }

            foreach (var term in rule.RelatedTerms)
            {
                AddWeight(termWeights, term, rule.Weight);
            }
        }

        var phrases = BuildPhrases(originalTokens);
        var retrievalTerms = termWeights
            .OrderByDescending(static entry => entry.Value)
            .ThenBy(static entry => entry.Key, StringComparer.Ordinal)
            .Select(static entry => entry.Key)
            .ToArray();

        var retrievalText = retrievalTerms.Length == 0
            ? trimmedQuery
            : $"{trimmedQuery}{Environment.NewLine}Relevant concepts: {string.Join(' ', retrievalTerms)}";

        return new ExpandedSearchQuery(
            trimmedQuery,
            retrievalText,
            originalTokens,
            termWeights,
            phrases);
    }

    public static string NormalizeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static IReadOnlyList<string> Tokenize(string value)
    {
        var normalized = NormalizeText(value);
        if (normalized.Length == 0)
        {
            return Array.Empty<string>();
        }

        return TokenRegex.Matches(normalized)
            .Select(static match => match.Value)
            .Select(CanonicalizeToken)
            .Where(static token => !StopWords.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string CanonicalizeToken(string token)
    {
        if (token.Length <= 3)
        {
            return token;
        }

        if (token.EndsWith("ies", StringComparison.Ordinal) && token.Length > 4)
        {
            return $"{token[..^3]}y";
        }

        if (token.EndsWith("s", StringComparison.Ordinal)
            && !token.EndsWith("ss", StringComparison.Ordinal)
            && token.Length > 4)
        {
            return token[..^1];
        }

        return token;
    }

    private static IReadOnlyList<string> BuildPhrases(IReadOnlyList<string> tokens)
    {
        if (tokens.Count < 2)
        {
            return Array.Empty<string>();
        }

        return tokens
            .Zip(tokens.Skip(1), static (left, right) => $"{left} {right}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddWeight(IDictionary<string, double> termWeights, string value, double weight)
    {
        foreach (var token in Tokenize(value))
        {
            if (termWeights.TryGetValue(token, out var existingWeight))
            {
                termWeights[token] = Math.Max(existingWeight, weight);
            }
            else
            {
                termWeights[token] = weight;
            }
        }
    }

    private sealed record ExpansionRule(
        IReadOnlyList<string> TriggerTokens,
        IReadOnlyList<string> TriggerPhrases,
        IReadOnlyList<string> RelatedTerms,
        double Weight)
    {
        public bool Matches(HashSet<string> tokenSet, string normalizedQuery)
        {
            if (TriggerTokens.Any(tokenSet.Contains))
            {
                return true;
            }

            return TriggerPhrases.Any(phrase => normalizedQuery.Contains(NormalizeText(phrase), StringComparison.Ordinal));
        }
    }
}

public sealed record ExpandedSearchQuery(
    string OriginalText,
    string RetrievalText,
    IReadOnlyList<string> OriginalTokens,
    IReadOnlyDictionary<string, double> TermWeights,
    IReadOnlyList<string> Phrases);
