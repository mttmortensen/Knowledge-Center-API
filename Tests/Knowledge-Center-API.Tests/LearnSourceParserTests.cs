using Knowledge_Center_API.Models.Learn;
using Knowledge_Center_API.Services.Core;

namespace Knowledge_Center_API.Tests
{
    public class LearnSourceParserTests
    {
        [Theory]
        [InlineData("Plain note with **bold**", "note")]
        [InlineData("## Heading\nmore", "note")]
        [InlineData("?? Why is the sky blue?", "question")]
        [InlineData("  ?? leading whitespace still counts", "question")]
        [InlineData("? single mark is not a question", "note")]
        [InlineData("text then ?? later", "note")]
        [InlineData("```sql\nSELECT 1;\n```", "code")]
        [InlineData("![a diagram](https://x/uploads/a.png)", "image")]
        [InlineData("![a diagram](https://x/uploads/a.png)\nand some text", "note")]
        [InlineData("@answers(12) because...", "followup")]
        [InlineData("@answers(abc) not a number", "note")]
        [InlineData("@log(34)", "note")]
        public void Parse_DetectsEntryType(string source, string expectedType)
        {
            Assert.Equal(expectedType, LearnSourceParser.Parse(source).EntryType);
        }

        [Fact]
        public void Parse_ExtractsAnswersTarget()
        {
            var parsed = LearnSourceParser.Parse("@answers(42) The answer is 42.");
            Assert.Equal(42, parsed.AnswersEntryId);
        }

        [Fact]
        public void Annotate_MarksQuestionsAnsweredOnlyWhenAFollowUpTargetsThem()
        {
            var entries = new List<LearnEntry>
            {
                new() { EntryId = 1, Source = "?? first" },
                new() { EntryId = 2, Source = "?? second" },
                new() { EntryId = 3, Source = "@answers(1) yes" },
                new() { EntryId = 4, Source = "@answers(99) target gone" }
            };

            LearnSourceParser.Annotate(entries);

            Assert.Equal("answered", entries[0].QuestionStatus);
            Assert.Equal("open", entries[1].QuestionStatus);
            Assert.Null(entries[2].QuestionStatus);
            Assert.Equal(1, entries[2].AnswersEntryId);
            Assert.Equal(99, entries[3].AnswersEntryId);
        }
    }
}
