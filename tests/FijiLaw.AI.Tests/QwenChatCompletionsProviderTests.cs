using System.Net;
using System.Text;
using System.Text.Json;
using FijiLaw.AI;

namespace FijiLaw.AI.Tests;

public sealed class QwenChatCompletionsProviderTests
{
    [Fact]
    public async Task GenerateGuidanceAsync_UsesConfiguredEndpointAndReturnsContent()
    {
        var handler = new CapturingHandler("""
            {"choices":[{"message":{"role":"assistant","content":"  Seek qualified legal review.  "}}]}
            """);
        var provider = new QwenChatCompletionsProvider(
            new HttpClient(handler),
            "test-key",
            "https://workspace.ap-southeast-1.maas.aliyuncs.com/compatible-mode/v1",
            "qwen-plus");

        var result = await provider.GenerateGuidanceAsync(new LegalModelRequest(
            "A tenancy dispute.",
            "Notice requirements",
            "high",
            ["Residential Tenancies Act source"],
            ["Notice date"]));

        Assert.Equal("Seek qualified legal review.", result);
        Assert.Equal("qwen:qwen-plus", provider.Name);
        Assert.Equal(
            "https://workspace.ap-southeast-1.maas.aliyuncs.com/compatible-mode/v1/chat/completions",
            handler.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test-key", handler.AuthorizationParameter);

        using var payload = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("qwen-plus", payload.RootElement.GetProperty("model").GetString());
        var messages = payload.RootElement.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Contains("Residential Tenancies Act source", messages[1].GetProperty("content").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://insecure.example/v1")]
    [InlineData("not-a-url")]
    public void InvalidOrInsecureEndpoint_DisablesProvider(string? baseUrl)
    {
        var provider = new QwenChatCompletionsProvider(new HttpClient(), "test-key", baseUrl, "qwen-plus");

        Assert.False(provider.IsEnabled);
    }

    private sealed class CapturingHandler(string responseJson) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
