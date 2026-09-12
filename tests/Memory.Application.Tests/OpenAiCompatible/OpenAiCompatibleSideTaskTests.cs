namespace Memory.Application.Tests.OpenAiCompatible;

using Memory.Application.OpenAiCompatible;

public sealed class OpenAiCompatibleSideTaskTests
{
    [Fact]
    public void Detect_reads_metadata_task()
    {
        Assert.Equal(
            "title_generation",
            OpenAiCompatibleSideTask.Detect("title_generation", "Ahoj"));
    }

    [Fact]
    public void Detect_recognizes_open_webui_title_prompt()
    {
        var prompt = """
            ### Task:
            Generate a concise, 3-5 word title with an emoji summarizing the chat history.
            JSON format: { "title": "your concise title here" }
            <chat_history>
            USER: Ahoj
            </chat_history>
            """;

        Assert.Equal("title_generation", OpenAiCompatibleSideTask.Detect(null, prompt));
    }

    [Fact]
    public void Detect_recognizes_open_webui_tags_prompt()
    {
        var prompt = """
            ### Task:
            Generate 1-3 broad tags categorizing the main themes of the chat history
            JSON format: { "tags": ["tag1", "tag2", "tag3"] }
            <chat_history>
            USER: Ahoj
            </chat_history>
            """;

        Assert.Equal("tags_generation", OpenAiCompatibleSideTask.Detect(null, prompt));
    }

    [Fact]
    public void Detect_ignores_ordinary_chat()
    {
        Assert.Null(OpenAiCompatibleSideTask.Detect(null, "Jak se jmenuju?"));
    }
}
