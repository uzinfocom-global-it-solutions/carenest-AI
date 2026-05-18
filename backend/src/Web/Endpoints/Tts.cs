using Backend.Web.Infrastructure;

namespace Backend.Web.Endpoints;

public class Tts : IEndpointGroup
{
    public static string? RoutePrefix => "/tts";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet(Synthesize, "synthesize");
    }

    [EndpointSummary("Synthesize Speech")]
    [EndpointDescription("Converts text to speech audio (MP3) using Google TTS. Returns audio/mpeg stream. No auth required for easy testing.")]
    public static async Task<IResult> Synthesize(
        string text,
        string lang,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Results.BadRequest("text is required");

        lang = string.IsNullOrWhiteSpace(lang) ? "en" : lang;

        if (text.Length > 200)
            text = text[..200];

        try
        {
            var client = httpClientFactory.CreateClient("weather");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

            var encoded = Uri.EscapeDataString(text);
            var url = $"https://translate.google.com/translate_tts?ie=UTF-8&q={encoded}&tl={lang}&client=gtx";

            var response = await client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            return Results.File(bytes, "audio/mpeg", $"tts_{lang}.mp3");
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "TTS synthesis failed",
                detail: ex.Message,
                statusCode: 503);
        }
    }
}
