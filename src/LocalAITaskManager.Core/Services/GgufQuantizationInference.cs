using System.Text.RegularExpressions;

namespace LocalAITaskManager.Core.Services;

public static partial class GgufQuantizationInference
{
    // Matches common GGUF quantization levels delimited by separators (-, ., _, space) or boundaries
    [GeneratedRegex(@"(?i)(?:^|[-._\s])(Q[2-8]_[K0-9]_[A-Z0-9]+|Q[2-8]_[K0-9]|Q[2-8]_[0-9]|IQ[1-4]_[A-Z0-9]+|F16|BF16|F32)(?:[-._\s]|\.gguf$|$)", RegexOptions.Compiled)]
    private static partial Regex QuantRegex();

    public static string? InferFromFileName(string? fileNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(fileNameOrPath))
        {
            return null;
        }

        string fileName = Path.GetFileName(fileNameOrPath);
        Match match = QuantRegex().Match(fileName);

        if (!match.Success)
        {
            return null;
        }

        string matched = match.Groups[1].Value;

        // Double-check sanity: ensure it's not a false positive like pure digits or plain word
        return matched.ToUpperInvariant();
    }
}
