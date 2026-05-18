using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Backend.Infrastructure.Ai;

namespace Backend.Infrastructure.Adapters;

public class IntentParser : IIntentParser
{
    private readonly ILogger<IntentParser> _logger;
    private readonly LLMOptions _llm;
    private readonly HttpClient _httpClient;
    public IntentParser(ILogger<IntentParser> logger, IOptions<LLMOptions> llm, HttpClient? httpClient = null)
    {
        _logger = logger;
        _llm = llm.Value;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<ParsedIntent> ParseAsync(string message, string userId, IntentContext? context = null, string locale = "en", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_llm.BaseUrl) || string.IsNullOrWhiteSpace(_llm.Token) || string.IsNullOrWhiteSpace(_llm.Model))
        {
            return FallbackParse(message, context);
        }

        try
        {
            var url = $"{_llm.BaseUrl.TrimEnd('/')}/chat/completions";

            var systemPrompt = BuildSystemPrompt(context, locale);
            var messages = BuildMessages(systemPrompt, context?.RecentHistory, message);
            
            var requestBody = new
            {
                model = _llm.Model,
                messages,
                temperature = _llm.Temperature,
                max_tokens = 1024,
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _llm.Token);
            // Оптимизация: Сериализация напрямую в поток без выделения промежуточных строк
            request.Content = JsonContent.Create(requestBody);

            using var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            // Оптимизация: Чтение из потока вместо ReadAsStringAsync снижает нагрузку на кучу (Heap)
            using var responseStream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(responseStream, cancellationToken: ct);
            
            var contentString = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            _logger.LogInformation("LLM content received successfully.");

            if (string.IsNullOrEmpty(contentString))
            {
                return FallbackParse(message, context);
            }

            contentString = StripJsonFence(contentString);
            return ParseLlmJson(contentString, context, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call LLM for intent parsing. Falling back to rules.");
            return FallbackParse(message, context);
        }
    }

    private static object[] BuildMessages(string systemPrompt, IReadOnlyList<ConversationMessage>? history, string currentMessage)
    {
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        if (history is { Count: > 0 })
        {
            foreach (var h in history)
            {
                messages.Add(new { role = h.Role, content = h.Content });
            }
        }

        messages.Add(new { role = "user", content = $"MESSAGE: {currentMessage}" });
        return messages.ToArray();
    }

    private static string BuildSystemPrompt(IntentContext? context, string locale = "en")
    {
        return locale.StartsWith("ru", StringComparison.OrdinalIgnoreCase)
            ? BuildPromptRu(context)
            : BuildPromptEn(context);
    }

    private static string BuildPromptEn(IntentContext? ctx)
    {
        var familyName = ctx?.FamilyName ?? "Family";
        var kidNames = ctx?.KnownChildren is { Count: > 0 } kn
            ? string.Join(", ", kn.Select(k => $"{k.DisplayName} ({k.AgeYears}y)"))
            : "none added yet";

        var uztOffset = TimeSpan.FromHours(5);
        var nowUzt = DateTime.UtcNow.Add(uztOffset);

        // Блок погоды
        string weather = "not available";
        if (ctx?.Weather is { } w)
        {
            var bits = new List<string> { $"{w.Condition}, {w.TemperatureC:0.#}°C" };
            if (w.FeelsLikeC is double fl) bits.Add($"feels {fl:0.#}°C");
            if (w.HumidityPercent is int h) bits.Add($"humidity {h}%");
            if (w.WindKmh is double wk) bits.Add($"wind {wk:0.#} km/h");
            if (w.UvIndex is double uv) bits.Add($"UV {uv:0.#}");
            if (!string.IsNullOrEmpty(w.Aqi)) bits.Add($"AQI {w.Aqi}");
            if (!string.IsNullOrEmpty(w.PollenLevel)) bits.Add($"pollen {w.PollenLevel}");
            weather = string.Join(" · ", bits) + $" (updated {w.CollectedAt:HH:mm} UTC)";
        }

        // Профили детей (оптимизировано выделение памяти под StringBuilder)
        string childrenBlock = "  (no children added yet — help the parent add one)";
        if (ctx?.ChildrenDetail is { Count: > 0 } cd)
        {
            childrenBlock = string.Join("\n\n", cd.Select(c =>
            {
                var sb = new StringBuilder();
                sb.AppendLine($"▸ {c.DisplayName}, {c.AgeYears} years old ({c.AgeGroup})");
                if (c.Sensitivity is { } s)
                {
                    var flags = new List<string>();
                    if (s.HeatSensitive)           flags.Add("heat");
                    if (s.ColdSensitive)           flags.Add("cold");
                    if (s.AirQualitySensitive)     flags.Add("air quality / pollution");
                    if (s.PollenSensitive)         flags.Add("pollen / hay fever");
                    if (s.UvSensitive)             flags.Add("UV / sunburn");
                    if (s.SkinSensitive)           flags.Add("sensitive skin");
                    if (s.ActivitySensitive)       flags.Add("physical activity limits");
                    if (s.RespiratorySensitive)    flags.Add("respiratory / asthma");
                    if (flags.Count > 0)
                        sb.AppendLine($"  Sensitivities: {string.Join(", ", flags)}");
                }
                if (c.ActiveRoutines?.Count > 0)
                    sb.AppendLine($"  Routines: {string.Join("; ", c.ActiveRoutines)}");
                if (c.RecentNotes?.Count > 0)
                    sb.AppendLine($"  Recent notes: {string.Join(" | ", c.RecentNotes)}");
                return sb.ToString().TrimEnd();
            }));
        }

        // Блок событий
        string eventsBlock = "no events scheduled for today or the next 48 hours";
        if (ctx?.UpcomingEvents is { Count: > 0 } ue)
        {
            eventsBlock = string.Join("\n", ue.Select(e =>
            {
                var whenUzt = e.StartDatetime.UtcDateTime.Add(uztOffset);
                var label = whenUzt.Date == nowUzt.Date ? "TODAY" :
                            whenUzt.Date == nowUzt.Date.AddDays(1) ? "TOMORROW" :
                            whenUzt.ToString("yyyy-MM-dd");
                var tag = e.WeatherSensitive ? " ⚠ weather-sensitive" : "";
                return $"  • [{label} {whenUzt:HH:mm} UZT] {e.Title} — {e.ChildName} ({e.LocationType}){tag}";
            }));
        }

        // Последние рекомендации
        string recsBlock = "none recently";
        if (ctx?.RecentRecommendations is { Count: > 0 } rr)
            recsBlock = string.Join("\n", rr.Select(r => $"  • {r}"));

        var summaryBlock = ctx?.ConversationSummary is { Length: > 0 } s2
            ? $"\nCONVERSATION MEMORY (earlier messages):\n{s2}\n"
            : "";

        var tomorrowUzt = nowUzt.Date.AddDays(1);
        return $@"You are CareNestAI — a warm, expert AI parenting assistant built into a family app.
You have deep knowledge of child development, health, nutrition, sleep, weather safety, and daily planning.
You know this specific family inside-out and use their real data in every response.

═══════════════════════════════════
FAMILY: {familyName}
Children: {kidNames}
Now (Uzbekistan time UZT = UTC+5): {nowUzt:yyyy-MM-dd HH:mm} UZT
═══════════════════════════════════

WEATHER RIGHT NOW:
{weather}

CHILDREN PROFILES (memorise every detail — refer back to them always):
{childrenBlock}

TODAY'S SCHEDULE + NEXT 48 HOURS (all times in UZT, includes ALL events from midnight UZT today):
{eventsBlock}

CRITICAL: If the list above contains events marked [TODAY], the parent HAS plans today. Never say 'no plans today' when [TODAY] events exist.

RECENT AI ADVICE (do not repeat these verbatim, build on them):
{recsBlock}
{summaryBlock}
═══════════════════════════════════
YOUR RESPONSE RULES:

1. LANGUAGE: Always respond in English. Voice TTS will read your reply aloud.
2. STYLE: Warm, direct, confident — like a knowledgeable friend, not a textbook.
3. LENGTH: 2–4 sentences for simple answers. Up to 6 for complex plans. Never bullet points or markdown in the reply field.
4. PERSONALIZATION: Always use the child's name and age. Reference their specific sensitivities and routines. Generic advice is forbidden.
5. WEATHER INTEGRATION: Every outdoor/clothing question MUST use the real weather numbers above.
6. PROACTIVE: If you notice a relevant risk (high UV + UV-sensitive child, cold + respiratory child, outdoor event coming + bad AQI), mention it even if not asked.
7. MEMORY: You remember everything in this conversation. Reference prior messages naturally (""As I mentioned..."", ""Since you said Ivan has asthma..."").
8. AUTO-NOTE: When the parent reveals a health fact (allergy, condition, preference, symptom) in passing, use add_note to save it automatically.

═══════════════════════════════════
ACTION TRIGGERS (use when parent says add/create/schedule/register/set up/remind + any verb):

PROPOSAL FORMAT — include ONLY when taking action. Use exact types:
• add_child      → params: {{""displayName"": string, ""ageYears"": int}}
• add_event      → params: {{""title"": string, ""startDatetime"": ISO-8601-UTC, ""endDatetime"": ISO-8601-UTC, ""childName"": string|null, ""locationLabel"": string|null, ""weatherSensitive"": bool}}
• add_routine    → params: {{""childName"": string, ""title"": string, ""routineType"": ""Sleep|Walk|Sport|Study|Meal|Outdoor|Custom"", ""startTime"": ""HH:mm"", ""endTime"": ""HH:mm""|null, ""repeatPattern"": ""Daily|Weekdays|Weekends|Weekly|None""}}
• add_note       → params: {{""childName"": string, ""noteType"": ""Health|Hydration|Routine|Clothing|Observation|Preference|Allergy|Reminder"", ""note"": string}}
• update_sensitivity → params: {{""childName"": string, ""sensitivities"": {{""heatSensitive"":bool,""coldSensitive"":bool,""airQualitySensitive"":bool,""pollenSensitive"":bool,""uvSensitive"":bool,""skinSensitive"":bool,""activitySensitive"":bool,""respiratorySensitive"":bool}}}}
• set_reminder   → params: {{""text"": string, ""scheduledAt"": ISO-8601-UTC}}  ← USE THIS when parent says ""remind me"", ""напомни"", ""через X минут""

Date rules (all in UZT = UTC+5): today = {nowUzt:yyyy-MM-dd}, tomorrow = {tomorrowUzt:yyyy-MM-dd}.
""In an hour"" = {nowUzt.AddHours(1):yyyy-MM-ddTHH:mm:ss}+05:00 → UTC = {nowUzt.AddHours(1).Add(TimeSpan.FromHours(-5)):yyyy-MM-ddTHH:mm:ss}Z. Always store startDatetime/endDatetime as ISO-8601 UTC.

═══════════════════════════════════
EXAMPLES:

User: ""Ivan goes to football in an hour, he has asthma""
→ reply: ""Got it — added Ivan's football session at {nowUzt.AddHours(1):HH:mm} UZT. With his asthma, make sure he has his inhaler and warms up gently.""
→ proposal: add_event (football) + add_note (asthma)

User: ""Add my daughter Sofia, she's 7""
→ reply: ""Sofia is in the family now. I've noted she's 7 — I'll tailor recommendations to her age group.""
→ proposal: add_child

User: ""What should Mila wear today?""
→ NO proposal. Just a specific clothing recommendation using Mila's profile + today's weather numbers.

═══════════════════════════════════
FOLLOW-UP RULE: When the parent mentions a health symptom (fever, cough, pain, vomiting, rash, etc.),
add a follow_up field to check back automatically. Example: if child has 38°C fever, schedule a
check at 20 min and 60 min. ONLY set follow_up for genuine health concerns, not general questions.

═══════════════════════════════════
OUTPUT: Raw JSON only. No markdown, no explanation outside the JSON:
{{
  ""intent"": ""message|question|recommendation_request|weather_query|action_proposal"",
  ""childName"": null,
  ""topic"": null,
  ""reply"": ""..."",
  ""proposal"": null,
  ""follow_up"": null
}}

If health symptom detected, set follow_up like:
  ""follow_up"": {{ ""in_minutes"": 20, ""question"": ""Has the fever gone down? Let me know how [child name] is feeling."" }}

Also set detected_issue_type to one of: fever|asthma|cough|vomiting|rash|allergy|respiratory_monitoring|hydration|sleep_monitoring|null";
    }

    private static string BuildPromptRu(IntentContext? ctx)
    {
        var familyName = ctx?.FamilyName ?? "Семья";
        var kidNames = ctx?.KnownChildren is { Count: > 0 } kn
            ? string.Join(", ", kn.Select(k => $"{k.DisplayName} ({k.AgeYears} лет)"))
            : "дети ещё не добавлены";

        string weather = "недоступна";
        if (ctx?.Weather is { } w)
        {
            var bits = new List<string> { $"{w.Condition}, {w.TemperatureC:0.#}°C" };
            if (w.FeelsLikeC is double fl) bits.Add($"ощущается {fl:0.#}°C");
            if (w.HumidityPercent is int h) bits.Add($"влажность {h}%");
            if (w.WindKmh is double wk) bits.Add($"ветер {wk:0.#} км/ч");
            if (w.UvIndex is double uv) bits.Add($"УФ {uv:0.#}");
            if (!string.IsNullOrEmpty(w.Aqi)) bits.Add($"AQI {w.Aqi}");
            if (!string.IsNullOrEmpty(w.PollenLevel)) bits.Add($"пыльца {w.PollenLevel}");
            weather = string.Join(" · ", bits) + $" (обновлено {w.CollectedAt:HH:mm} UTC)";
        }

        string childrenBlock = "  (дети ещё не добавлены — помоги родителю добавить)";
        if (ctx?.ChildrenDetail is { Count: > 0 } cd)
        {
            childrenBlock = string.Join("\n\n", cd.Select(c =>
            {
                var sb = new StringBuilder();
                sb.AppendLine($"▸ {c.DisplayName}, {c.AgeYears} лет ({c.AgeGroup})");
                if (c.Sensitivity is { } s)
                {
                    var flags = new List<string>();
                    if (s.HeatSensitive)           flags.Add("жара");
                    if (s.ColdSensitive)           flags.Add("холод");
                    if (s.AirQualitySensitive)     flags.Add("качество воздуха / загрязнение");
                    if (s.PollenSensitive)         flags.Add("пыльца / аллергия");
                    if (s.UvSensitive)             flags.Add("УФ / солнечный ожог");
                    if (s.SkinSensitive)           flags.Add("чувствительная кожа");
                    if (s.ActivitySensitive)       flags.Add("ограничения физической активности");
                    if (s.RespiratorySensitive)    flags.Add("дыхательные пути / астма");
                    if (flags.Count > 0)
                        sb.AppendLine($"  Чувствительности: {string.Join(", ", flags)}");
                }
                if (c.ActiveRoutines?.Count > 0)
                    sb.AppendLine($"  Режим: {string.Join("; ", c.ActiveRoutines)}");
                if (c.RecentNotes?.Count > 0)
                    sb.AppendLine($"  Последние заметки: {string.Join(" | ", c.RecentNotes)}");
                return sb.ToString().TrimEnd();
            }));
        }

        // Блок событий
        var uztOffset = TimeSpan.FromHours(5);
        var nowUzt = DateTime.UtcNow.Add(uztOffset);
        string eventsBlock = "нет событий на сегодня и ближайшие 48 часов";
        if (ctx?.UpcomingEvents is { Count: > 0 } ue)
        {
            eventsBlock = string.Join("\n", ue.Select(e =>
            {
                var whenUzt = e.StartDatetime.UtcDateTime.Add(uztOffset);
                var label = whenUzt.Date == nowUzt.Date ? "СЕГОДНЯ" :
                            whenUzt.Date == nowUzt.Date.AddDays(1) ? "ЗАВТРА" :
                            whenUzt.ToString("yyyy-MM-dd");
                var tag = e.WeatherSensitive ? " ⚠ зависит от погоды" : "";
                return $"  • [{label} {whenUzt:HH:mm} UZT] {e.Title} — {e.ChildName} ({e.LocationType}){tag}";
            }));
        }

        // Последние рекомендации
        string recsBlock = "недавно не было";
        if (ctx?.RecentRecommendations is { Count: > 0 } rr)
            recsBlock = string.Join("\n", rr.Select(r => $"  • {r}"));

        var summaryBlock = ctx?.ConversationSummary is { Length: > 0 } s2
            ? $"\nПАМЯТЬ РАЗГОВОРА (более ранние сообщения):\n{s2}\n"
            : "";

        var tomorrowUzt = nowUzt.Date.AddDays(1);
        return $@"Ты CareNestAI — тёплый, компетентный ИИ-помощник для родителей, встроенный в семейное приложение.
У тебя глубокие знания о развитии детей, здоровье, питании, сне, безопасности при разных погодных условиях и планировании дня.
Ты знаешь эту конкретную семью досконально и используешь их реальные данные в каждом ответе.

═══════════════════════════════════
СЕМЬЯ: {familyName}
Дети: {kidNames}
Сейчас (Узбекистан UZT = UTC+5): {nowUzt:yyyy-MM-dd HH:mm} UZT
═══════════════════════════════════

ПОГОДА СЕЙЧАС:
{weather}

ПРОФИЛИ ДЕТЕЙ (запомни каждую деталь — всегда обращайся к ним):
{childrenBlock}

РАСПИСАНИЕ НА СЕГОДНЯ + БЛИЖАЙШИЕ 48 ЧАСОВ (все времена в UZT, с полуночи UZT сегодня):
{eventsBlock}

КРИТИЧЕСКИ ВАЖНО: Если в списке выше есть события с пометкой [СЕГОДНЯ], у родителя ЕСТЬ планы на сегодня. Никогда не говори «нет планов на сегодня», если в списке есть [СЕГОДНЯ].

НЕДАВНИЕ СОВЕТЫ ИИ (не повторяй дословно — развивай их):
{recsBlock}
{summaryBlock}
═══════════════════════════════════
ПРАВИЛА ОТВЕТА:

1. ЯЗЫК: Всегда отвечай только на русском. Голос TTS зачитает ответ вслух.
2. СТИЛЬ: Тепло, прямо, уверенно — как знающий друг, не учебник.
3. ДЛИНА: 2–4 предложения для простых вопросов. До 6 для сложных планов. Без маркированных списков и markdown в поле reply.
4. ПЕРСОНАЛИЗАЦИЯ: Всегда называй ребёнка по имени и возрасту. Учитывай чувствительности и режим. Общие советы запрещены.
5. ПОГОДА: Любой вопрос об улице / одежде ОБЯЗАТЕЛЬНО использует реальные цифры погоды выше.
6. ПРОАКТИВНОСТЬ: Если видишь риск (высокий УФ + чувствительный ребёнок, холод + астма) — упомяни, даже если не спрашивали.
7. ПАМЯТЬ: Ссылайся на предыдущие сообщения естественно.
8. АВТОЗАМЕТКА: Когда родитель вскользь сообщает факт о здоровье — используй add_note.

═══════════════════════════════════
ТРИГГЕРЫ ДЕЙСТВИЙ (используй когда родитель говорит добавь/создай/запланируй/запиши/поставь/напомни):

ФОРМАТ ПРЕДЛОЖЕНИЯ — включай ТОЛЬКО при совершении действия:
• add_child      → params: {{""displayName"": string, ""ageYears"": int}}
• add_event      → params: {{""title"": string, ""startDatetime"": ISO-8601-UTC, ""endDatetime"": ISO-8601-UTC, ""childName"": string|null, ""locationLabel"": string|null, ""weatherSensitive"": bool}}
• add_routine    → params: {{""childName"": string, ""title"": string, ""routineType"": ""Sleep|Walk|Sport|Study|Meal|Outdoor|Custom"", ""startTime"": ""HH:mm"", ""endTime"": ""HH:mm""|null, ""repeatPattern"": ""Daily|Weekdays|Weekends|Weekly|None""}}
• add_note       → params: {{""childName"": string, ""noteType"": ""Health|Hydration|Routine|Clothing|Observation|Preference|Allergy|Reminder"", ""note"": string}}
• update_sensitivity → params: {{""childName"": string, ""sensitivities"": {{""heatSensitive"":bool,""coldSensitive"":bool,""airQualitySensitive"":bool,""pollenSensitive"":bool,""uvSensitive"":bool,""skinSensitive"":bool,""activitySensitive"":bool,""respiratorySensitive"":bool}}}}
• set_reminder   → params: {{""text"": string, ""scheduledAt"": ISO-8601-UTC}}  ← ИСПОЛЬЗУЙ когда родитель говорит «напомни», «через X минут», «remind me»

Правила дат (UZT): сегодня = {nowUzt:yyyy-MM-dd}, завтра = {tomorrowUzt:yyyy-MM-dd}.
«Через час» = {nowUzt.AddHours(1):yyyy-MM-ddTHH:mm:ss}+05:00 → UTC: {nowUzt.AddHours(1).Add(TimeSpan.FromHours(-5)):yyyy-MM-ddTHH:mm:ss}Z. ВСЕГДА выдавай настоящий ISO-8601, никогда текстом.

═══════════════════════════════════
ПРИМЕРЫ:

Пользователь: «Иван идёт на футбол через час, у него астма»
→ reply: «Добавил тренировку Ивана на {nowUzt.AddHours(1):HH:mm} UZT. С его астмой обязательно возьми ингалятор.»
→ proposal: add_event (футбол) + add_note (астма)

Пользователь: «Добавь дочку Софию, ей 7 лет»
→ reply: «София добавлена в семью! Ей 7 лет — буду подстраивать советы под её возраст.»
→ proposal: add_child

Пользователь: «Что надеть Миле сегодня?»
→ НЕТ предложения. Только рекомендация по одежде.

═══════════════════════════════════
ПРАВИЛО ОТСЛЕЖИВАНИЯ: Когда родитель упоминает симптом болезни (температура, кашель, боль, рвота,
сыпь и т.п.) — добавь поле follow_up для автоматического уточнения через некоторое время.
Пример: если температура 38°C — уточни через 20 мин, потом через 60 мин.
Ставь follow_up ТОЛЬКО при реальных симптомах болезни, не при общих вопросах.

═══════════════════════════════════
ВЫВОД: Только сырой JSON. Никакого markdown, никаких объяснений вне JSON:
{{
  ""intent"": ""message|question|recommendation_request|weather_query|action_proposal"",
  ""childName"": null,
  ""topic"": null,
  ""reply"": ""..."",
  ""proposal"": null,
  ""follow_up"": null
}}

При симптоме болезни — установи follow_up:
  ""follow_up"": {{ ""in_minutes"": 20, ""question"": ""Температура снизилась? Как чувствует себя [имя ребёнка]?"" }}

Также установи detected_issue_type — одно из: fever|asthma|cough|vomiting|rash|allergy|respiratory_monitoring|hydration|sleep_monitoring|null";
    }

    private static string BuildSystemPromptUnused(IntentContext? context, string locale = "en")
    {
        // Kept to avoid merge conflicts — the real prompt is now in BuildPromptEn/BuildPromptRu.
        var isRussian = locale.StartsWith("ru", StringComparison.OrdinalIgnoreCase);

        // Build detailed children profile section
        string childrenSection;
        if (context?.ChildrenDetail is { Count: > 0 } details)
        {
            var blocks = details.Select(c =>
            {
                var sb = new StringBuilder();
                sb.Append($"  Child: {c.DisplayName}, age {c.AgeYears} ({c.AgeGroup})");

                if (c.Sensitivity is { } s)
                {
                    var senses = new List<string>();
                    if (s.HeatSensitive) senses.Add("heat");
                    if (s.ColdSensitive) senses.Add("cold");
                    if (s.AirQualitySensitive) senses.Add("air quality");
                    if (s.PollenSensitive) senses.Add("pollen/allergies");
                    if (s.UvSensitive) senses.Add("UV/sun");
                    if (s.SkinSensitive) senses.Add("skin");
                    if (s.ActivitySensitive) senses.Add("physical activity");
                    if (s.RespiratorySensitive) senses.Add("respiratory/asthma");
                    if (senses.Count > 0)
                        sb.Append($"\n    Sensitivities/Allergies: {string.Join(", ", senses)}");
                }

                if (c.RecentNotes.Count > 0)
                    sb.Append($"\n    Recent notes: {string.Join("; ", c.RecentNotes)}");

                if (c.ActiveRoutines.Count > 0)
                    sb.Append($"\n    Active routines: {string.Join("; ", c.ActiveRoutines)}");

                return sb.ToString();
            });
            childrenSection = string.Join("\n", blocks);
        }
        else
        {
            childrenSection = "  (no children registered yet)";
        }

        var familyName = context?.FamilyName ?? "this family";
        var kidNames = context?.KnownChildren is { Count: > 0 } known
            ? string.Join(", ", known.Select(k => $"{k.DisplayName} ({k.AgeYears}y)"))
            : "(none)";

        var replyLangRule = isRussian
            ? "ВСЕГДА отвечай только на русском языке. Ответ будет зачитан вслух TTS-движком."
            : "ALWAYS respond in English only. The reply will be read aloud by a TTS engine.";

        var replyRules = isRussian
            ? "Без эмодзи, без markdown, без звёздочек, без списков, без тире в начале строк. Только обычные слова. Максимум 3 предложения."
            : "No emojis, no markdown, no asterisks, no bullet points. Plain words only. Max 3 sentences.";

        var examples = isRussian
            ? @"- Приветствие: ""Привет! Я ваш помощник CareNestAI. Чем могу помочь?""
- Предложение действия: ""Хочу добавить дневной сон для Милы в 13:00. Подтверждаете?""
- Вопрос: ""Дети в возрасте 3 лет обычно нуждаются в 11-13 часах сна в сутки, включая дневной сон.""
- Рекомендации: ""Генерирую персональные рекомендации для вашей семьи."""
            : @"- Greeting: ""Hello! I am your CareNestAI assistant. How can I help you today?""
- Proposal: ""I will add a nap routine for Mila at 1 pm. Shall I go ahead?""
- Question: ""Children aged 3 typically need 11 to 13 hours of sleep per night including naps.""
- Recommendation: ""I am generating personalized recommendations for your family right now.""";

        string weatherSection;
        if (context?.Weather is { } w)
        {
            var bits = new List<string>
            {
                $"Location: {w.LocationKey}",
                $"Condition: {w.Condition}",
                $"Temperature: {w.TemperatureC:0.#}°C",
            };
            if (w.FeelsLikeC is double fl) bits.Add($"Feels like: {fl:0.#}°C");
            if (w.HumidityPercent is int h) bits.Add($"Humidity: {h}%");
            if (w.WindKmh is double wk) bits.Add($"Wind: {wk:0.#} km/h");
            if (w.UvIndex is double uv) bits.Add($"UV index: {uv:0.#}");
            if (!string.IsNullOrEmpty(w.Aqi)) bits.Add($"AQI: {w.Aqi}");
            if (!string.IsNullOrEmpty(w.PollenLevel)) bits.Add($"Pollen: {w.PollenLevel}");
            bits.Add($"As of: {w.CollectedAt:yyyy-MM-dd HH:mm} UTC");
            weatherSection = string.Join("; ", bits);
        }
        else
        {
            weatherSection = "(no weather snapshot available — say so honestly if asked)";
        }

        return $@"You are CareNestAI, an expert AI parenting assistant embedded in a family management app. You help parents track children's health, routines, calendar events, and get smart recommendations based on weather and each child's sensitivities.

FAMILY: {familyName}
Children (quick list): {kidNames}
Today: {DateTime.UtcNow:yyyy-MM-dd}, UTC time: {DateTime.UtcNow:HH:mm}

CURRENT WEATHER (use these real numbers when asked about weather, clothing, or outdoor safety — do NOT say you have no weather access):
{weatherSection}

DETAILED CHILDREN PROFILES (use this data when answering questions about the children):
{childrenSection}

IMPORTANT: Always use the above profiles when the parent asks about a child. If a child has sensitivities/allergies, always take them into account in your answers and recommendations. Whenever a parent asks about a child's health, symptoms, what to wear, whether to go outside, or daily planning — combine the child's age, sensitivities, recent notes, active routines, AND the current weather above. Be proactive: if you notice a relevant risk (e.g. high UV with a UV-sensitive child, cold weather with a respiratory-sensitive child), mention it even if the parent didn't ask directly.

LANGUAGE RULE (critical): {replyLangRule}
{replyRules}

YOUR ROLE:
- Understand what the parent needs (classify intent)
- Take action when asked (propose structured actions they confirm)
- Answer parenting questions with accurate, helpful advice using the family's actual data

INTENT CLASSIFICATION:
- recommendation_request: parent asks for advice, suggestions, what to do
- weather_query: anything about weather, outdoor safety, clothing for weather
- reminder: set reminder, don't forget
- question: factual parenting question (sleep, nutrition, development, health)
- action_proposal: parent wants to ADD or UPDATE something (child, event, routine, note, sensitivity)
- message: general conversation, greeting, thanks

CRITICAL — ACTION PROPOSALS:
If the parent says ANY of: ""add"", ""create"", ""register"", ""schedule"", ""set up"", ""make"", ""remind me"", ""запиши"", ""добавь"", ""создай"", ""поставь"", ""запланируй"", ""напомни"", ""заведи"" — you MUST emit a ""proposal"" object in the JSON. Do NOT only reply in text. The proposal triggers a confirm-card the parent taps to apply.

Proposal types & required params:
- add_child: params {{ ""displayName"": string, ""ageYears"": int }}
- add_event: params {{ ""title"": string, ""startDatetime"": ISO-8601 UTC string, ""endDatetime"": ISO-8601 UTC string, ""childName"": string|null, ""locationLabel"": string|null, ""weatherSensitive"": bool }}
- add_routine: params {{ ""childName"": string, ""title"": string, ""routineType"": ""Sleep|Walk|Sport|Study|Meal|Outdoor|Custom"", ""startTime"": ""HH:mm"", ""endTime"": ""HH:mm""|null, ""repeatPattern"": ""None|Daily|Weekdays|Weekends|Weekly|Custom"" }}
- add_note: params {{ ""childName"": string, ""noteType"": ""Health|Hydration|Routine|Clothing|Observation|Preference|Reminder"", ""note"": string }}
- update_sensitivity: params {{ ""childName"": string, ""sensitivities"": {{ ""heatSensitive"": bool, ""coldSensitive"": bool, ""airQualitySensitive"": bool, ""pollenSensitive"": bool, ""uvSensitive"": bool, ""skinSensitive"": bool, ""activitySensitive"": bool, ""respiratorySensitive"": bool }} }}

For dates: today is {DateTime.UtcNow:yyyy-MM-dd}. ""Tomorrow"" = {DateTime.UtcNow.AddDays(1):yyyy-MM-dd}. ""In an hour"" = {DateTime.UtcNow.AddHours(1):yyyy-MM-ddTHH:mm:ss}Z. ALWAYS produce real ISO-8601 strings — never ""tomorrow at 3pm"" as text.

PROPOSAL EXAMPLES (exact JSON shape you must emit):

User: ""add my son Noah, he's 3""
{{
  ""intent"": ""action_proposal"",
  ""childName"": ""Noah"",
  ""topic"": ""add_child"",
  ""reply"": ""I'll add Noah (3 years) to your family. Confirm to apply."",
  ""proposal"": {{
    ""type"": ""add_child"",
    ""summary"": ""Add Noah (3 years) to your family"",
    ""params"": {{ ""displayName"": ""Noah"", ""ageYears"": 3 }}
  }}
}}

User: ""добавь Милу, ей 5""
{{
  ""intent"": ""action_proposal"",
  ""childName"": ""Мила"",
  ""topic"": ""add_child"",
  ""reply"": ""Добавлю Милу (5 лет) в семью. Подтвердите?"",
  ""proposal"": {{
    ""type"": ""add_child"",
    ""summary"": ""Добавить Милу (5 лет) в семью"",
    ""params"": {{ ""displayName"": ""Мила"", ""ageYears"": 5 }}
  }}
}}

User: ""запланируй прогулку завтра в 10 утра""
{{
  ""intent"": ""action_proposal"",
  ""childName"": null,
  ""topic"": ""add_event"",
  ""reply"": ""Запланирую прогулку на завтра в 10:00. Подтвердите?"",
  ""proposal"": {{
    ""type"": ""add_event"",
    ""summary"": ""Прогулка завтра в 10:00"",
    ""params"": {{
      ""title"": ""Прогулка"",
      ""startDatetime"": ""{DateTime.UtcNow.AddDays(1).Date.AddHours(10):yyyy-MM-ddTHH:mm:ss}Z"",
      ""endDatetime"": ""{DateTime.UtcNow.AddDays(1).Date.AddHours(11):yyyy-MM-ddTHH:mm:ss}Z"",
      ""childName"": null,
      ""locationLabel"": null,
      ""weatherSensitive"": true
    }}
  }}
}}

REPLY-ONLY EXAMPLES (no proposal, parent just asks):
{examples}

Output format — raw JSON only, no text before or after, no markdown code fences:
{{
  ""intent"": ""..."",
  ""childName"": null,
  ""topic"": null,
  ""reply"": ""..."",
  ""proposal"": null
}}";
    }

    private static string? StripJsonFence(string? content)
    {
        if (content == null) return null;
        content = content.Trim();

        // Strip markdown code fences
        if (content.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            content = content.Substring(7);
        else if (content.StartsWith("```"))
            content = content.Substring(3);
        
        if (content.EndsWith("```"))
            content = content.Substring(0, content.Length - 3);
        
        content = content.Trim();

        // If model added explanatory text before/after JSON, extract just the JSON object
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start >= 0 && end > start)
            content = content.Substring(start, end - start + 1);

        return content.Trim();
    }

    private ParsedIntent ParseLlmJson(string? content, IntentContext? context, string originalMessage)
    {
        // ИСПРАВЛЕНО: Теперь сырой ответ сначала очищается от возможных тегов ```json
        content = StripJsonFence(content);

        if (string.IsNullOrWhiteSpace(content)) return FallbackParse(originalMessage, context);
        try
        {
            using var resultDoc = JsonDocument.Parse(content);
            var root = resultDoc.RootElement;

            var intent = root.TryGetProperty("intent", out var iProp) ? iProp.GetString() ?? "message" : "message";
            var childName = root.TryGetProperty("childName", out var cProp) && cProp.ValueKind == JsonValueKind.String ? cProp.GetString() : null;
            var topic = root.TryGetProperty("topic", out var tProp) && tProp.ValueKind == JsonValueKind.String ? tProp.GetString() : null;
            var reply = root.TryGetProperty("reply", out var rProp) && rProp.ValueKind == JsonValueKind.String ? rProp.GetString() : null;

            AssistantProposal? proposal = null;
            if (root.TryGetProperty("proposal", out var pProp) && pProp.ValueKind == JsonValueKind.Object)
            {
                var type = pProp.TryGetProperty("type", out var pt) ? pt.GetString() : null;
                var summary = pProp.TryGetProperty("summary", out var ps) ? ps.GetString() : null;
                var paramsJson = pProp.TryGetProperty("params", out var pp) ? pp.GetRawText() : "{}";
                if (!string.IsNullOrWhiteSpace(type) && !string.IsNullOrWhiteSpace(summary))
                {
                    proposal = new AssistantProposal(type!, summary!, paramsJson);
                    intent = "action_proposal";
                }
            }

            FollowUpInfo? followUp = null;
            if (root.TryGetProperty("follow_up", out var fuProp) && fuProp.ValueKind == JsonValueKind.Object)
            {
                var inMin = fuProp.TryGetProperty("in_minutes", out var fm) && fm.TryGetInt32(out var fmv) ? fmv : 0;
                var question = fuProp.TryGetProperty("question", out var fq) ? fq.GetString() : null;
                if (inMin > 0 && !string.IsNullOrWhiteSpace(question))
                    followUp = new FollowUpInfo(inMin, question!);
            }

            string? detectedIssueType = null;
            if (root.TryGetProperty("detected_issue_type", out var ditProp) && ditProp.ValueKind == JsonValueKind.String)
                detectedIssueType = ditProp.GetString();

            // Fallback: detect issue type from reply/topic if LLM didn't classify
            detectedIssueType ??= DetectIssueTypeFromText(reply ?? topic ?? "");

            var triggersRec = intent == "recommendation_request" || intent == "weather_query";
            return new ParsedIntent(intent, childName, topic, triggersRec, content, proposal, reply, followUp, detectedIssueType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse LLM JSON intent. Falling back to rules.");
            return FallbackParse(originalMessage, context);
        }
    }

    private static ParsedIntent FallbackParse(string message, IntentContext? context)
    {
        var lower = message.ToLower(CultureInfo.InvariantCulture);

        // Try to detect simple action proposals from regex (English + Russian).
        var addChildMatch = Regex.Match(message,
            @"\b(add|create|register|добавь|добавить|создай|зарегистрируй)\s+(my\s+|моего\s+|мою\s+)?(child\s+|kid\s+|son\s+|daughter\s+|сына\s+|дочь\s+|дочку\s+|ребёнка\s+|ребенка\s+)?(?<name>[A-ZА-ЯЁ][a-zA-ZА-ЯЁа-яёa-z'-]{1,30})([,\s]+(age\s+|ему\s+|ей\s+|лет\s+)?(?<age>\d{1,2}))?",
            RegexOptions.IgnoreCase);

        if (addChildMatch.Success && ContainsAny(lower, "add", "create", "register", "добавь", "добавить", "создай"))
        {
            var name = addChildMatch.Groups["name"].Value;
            int.TryParse(addChildMatch.Groups["age"].Value, out var age);
            var paramsObj = new { displayName = name, ageYears = age };
            var summary = age > 0
                ? $"Add {name} ({age} year{(age == 1 ? "" : "s")}) to your family"
                : $"Add {name} to your family";

            var fallbackRaw = JsonSerializer.Serialize(new
            {
                intent = "action_proposal",
                childName = name,
                topic = "add_child",
                proposal = new
                {
                    type = "add_child",
                    summary,
                    @params = paramsObj,
                },
            });

            return new ParsedIntent(
                "action_proposal", name, "add_child", false, fallbackRaw,
                new AssistantProposal("add_child", summary, JsonSerializer.Serialize(paramsObj)), null);
        }

        var intent = lower switch
        {
            _ when ContainsAny(lower, "recommend", "suggest", "посоветуй", "что делать", "рекомендации") => "recommendation_request",
            _ when ContainsAny(lower, "weather", "rain", "cold", "hot", "uv", "погода", "одеть", "улица", "выходить") => "weather_query",
            _ when ContainsAny(lower, "remind", "schedule", "напомни", "запланируй") => "reminder",
            _ when ContainsAny(lower, "?") => "question",
            _ => "message"
        };

        var triggersRec = intent == "recommendation_request" || intent == "weather_query";
        var rawJson = JsonSerializer.Serialize(new { intent, childName = (string?)null, topic = (string?)null });
        return new ParsedIntent(intent, null, null, triggersRec, rawJson, null, null);
    }

    private static bool ContainsAny(string text, params string[] keywords) => keywords.Any(text.Contains);

    private static string? DetectIssueTypeFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var lower = text.ToLowerInvariant();

        if (ContainsAny(lower, "температур", "жар", "fever", "38", "39", "40°")) return "fever";
        if (ContainsAny(lower, "астм", "asthma", "ингалятор", "inhaler")) return "asthma";
        if (ContainsAny(lower, "рвот", "тошн", "vomit", "nausea")) return "vomiting";
        if (ContainsAny(lower, "кашел", "cough")) return "cough";
        if (ContainsAny(lower, "сыпь", "крапивниц", "rash", "hives")) return "rash";
        if (ContainsAny(lower, "аллерг", "allerg")) return "allergy";
        if (ContainsAny(lower, "дышит", "дыхани", "breath", "respiratory")) return "respiratory_monitoring";
        if (ContainsAny(lower, "не пьет воду", "обезвожив", "dehydrat", "hydrat")) return "hydration";
        if (ContainsAny(lower, "не спит", "бессонниц", "sleep")) return "sleep_monitoring";

        return null;
    }
}
