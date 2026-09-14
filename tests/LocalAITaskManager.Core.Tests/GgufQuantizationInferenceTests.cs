using LocalAITaskManager.Core.Services;
using Xunit;

namespace LocalAITaskManager.Core.Tests;

public class GgufQuantizationInferenceTests
{
    [Theory]
    [InlineData("model-Q4_K_M.gguf", "Q4_K_M")]
    [InlineData("model-q8_0.gguf", "Q8_0")]
    [InlineData("model-IQ4_XS.gguf", "IQ4_XS")]
    [InlineData("model-f16.gguf", "F16")]
    [InlineData("model-bf16.gguf", "BF16")]
    [InlineData("Qwen2.5-Coder-14B-Instruct-Q4_K_M.gguf", "Q4_K_M")]
    [InlineData("Ornith-1.5-35B-heretic-Q4_K_M.gguf", "Q4_K_M")]
    [InlineData(@"C:\AI Models\deepseek-r1-Q5_K_S.gguf", "Q5_K_S")]
    [InlineData("mistral-7b-v0.1.Q4_0.gguf", "Q4_0")]
    public void InferFromFileName_ValidPatterns_ReturnsNormalizedQuant(string input, string expected)
    {
        string? result = GgufQuantizationInference.InferFromFileName(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("qwen-model.gguf")]
    [InlineData("Qwen4.gguf")]
    [InlineData("llama-3.2-3b.gguf")]
    [InlineData("model.bin")]
    [InlineData("")]
    [InlineData(null)]
    public void InferFromFileName_NegativeCases_ReturnsNull(string? input)
    {
        string? result = GgufQuantizationInference.InferFromFileName(input);
        Assert.Null(result);
    }
}
