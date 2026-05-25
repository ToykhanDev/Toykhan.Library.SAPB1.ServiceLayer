using Toykhan.Library.SapB1.ServiceLayer;
using Toykhan.Library.SapB1.ServiceLayer.Internal;

namespace Toykhan.Library.SapB1.ServiceLayer.Tests;

public sealed class SapErrorTranslatorTests
{
    private readonly ISapErrorTranslator _translator = new SapErrorTranslator();

    [Fact]
    public void Translate_NumericCode_NestedMessage_ParsesCorrectly()
    {
        const string json = """
            {
              "error": {
                "code": 100,
                "message": {
                  "value": "Business Partner already exists",
                  "lang": "en-US"
                }
              }
            }
            """;

        var ex = _translator.Translate(json);

        Assert.Equal("100", ex.SapErrorCode);
        Assert.Equal("Business Partner already exists", ex.Message);
        Assert.False(ex.IsUnauthorized);
    }

    [Fact]
    public void Translate_StringCode_FlatMessage_ParsesCorrectly()
    {
        const string json = """
            {
              "error": {
                "code": "-2028",
                "message": "Session expired. Please login again."
              }
            }
            """;

        var ex = _translator.Translate(json);

        Assert.Equal("-2028", ex.SapErrorCode);
        Assert.Contains("Session expired", ex.Message);
        Assert.True(ex.IsUnauthorized);
    }

    [Fact]
    public void Translate_UnauthorizedCode_401_IsUnauthorizedTrue()
    {
        const string json = """{"error": {"code": "401", "message": "Unauthorized"}}""";

        var ex = _translator.Translate(json);

        Assert.True(ex.IsUnauthorized);
    }

    [Fact]
    public void Translate_EmptyBody_ReturnsGenericException()
    {
        var ex = _translator.Translate(string.Empty);

        Assert.Equal("unknown", ex.SapErrorCode);
        Assert.NotEmpty(ex.Message);
    }

    [Fact]
    public void Translate_InvalidJson_ReturnsGenericExceptionWithPreview()
    {
        var ex = _translator.Translate("not-json");

        Assert.Equal("unknown", ex.SapErrorCode);
        Assert.Contains("not-json", ex.Message);
    }

    [Fact]
    public void Translate_WithCause_SetsInnerException()
    {
        var cause = new Exception("HTTP failure");
        var ex = _translator.Translate("{}", cause);

        Assert.Same(cause, ex.InnerException);
    }

    [Fact]
    public void Translate_LongBody_TruncatesPreview()
    {
        var longBody = new string('x', 300);
        var ex = _translator.Translate(longBody);

        Assert.True(ex.Message.Length < 300);
    }
}
