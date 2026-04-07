using RootFlow.Application.Abstractions.Search;

namespace RootFlow.Application.Chat;

public enum ChatLanguage
{
    English,
    Portuguese
}

public static class ChatLanguageDetector
{
    private static readonly HashSet<string> PortugueseHints =
    [
        "academica", "almoco", "com", "como", "curso", "curriculo", "educacao", "empresa", "experiencia",
        "formacao", "graduacao", "historico", "hoje", "jantar", "meu", "minha", "profissional", "qual",
        "quais", "resto", "segunda", "terca", "quarta", "quinta", "sexta", "treino"
    ];

    private static readonly HashSet<string> EnglishHints =
    [
        "breakfast", "candidate", "company", "current", "degree", "diet", "dinner", "education", "experience",
        "history", "lunch", "qualification", "resume", "skill", "today", "training", "what", "when", "work"
    ];

    public static ChatLanguage Detect(params string?[] texts)
    {
        var portugueseScore = 0;
        var englishScore = 0;

        foreach (var text in texts)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var original = text.Trim();
            if (ContainsPortugueseAccent(original))
            {
                portugueseScore += 2;
            }

            foreach (var token in SemanticQueryExpander.Tokenize(original))
            {
                if (PortugueseHints.Contains(token))
                {
                    portugueseScore++;
                }

                if (EnglishHints.Contains(token))
                {
                    englishScore++;
                }
            }
        }

        return englishScore > portugueseScore
            ? ChatLanguage.English
            : ChatLanguage.Portuguese;
    }

    public static string GetNoContextAnswer(ChatLanguage language)
    {
        return language == ChatLanguage.Portuguese
            ? "N\u00e3o sei com base na base de conhecimento atual."
            : "I do not know based on the current knowledge base.";
    }

    public static string GetPromptLanguageLabel(ChatLanguage language)
    {
        return language == ChatLanguage.Portuguese ? "Brazilian Portuguese (PT-BR)" : "English";
    }

    private static bool ContainsPortugueseAccent(string value)
    {
        return value.Contains('\u00e3')
            || value.Contains('\u00e1')
            || value.Contains('\u00e0')
            || value.Contains('\u00e2')
            || value.Contains('\u00e9')
            || value.Contains('\u00ea')
            || value.Contains('\u00ed')
            || value.Contains('\u00f3')
            || value.Contains('\u00f4')
            || value.Contains('\u00f5')
            || value.Contains('\u00fa')
            || value.Contains('\u00e7')
            || value.Contains('\u00c3')
            || value.Contains('\u00c1')
            || value.Contains('\u00c0')
            || value.Contains('\u00c2')
            || value.Contains('\u00c9')
            || value.Contains('\u00ca')
            || value.Contains('\u00cd')
            || value.Contains('\u00d3')
            || value.Contains('\u00d4')
            || value.Contains('\u00d5')
            || value.Contains('\u00da')
            || value.Contains('\u00c7');
    }
}
