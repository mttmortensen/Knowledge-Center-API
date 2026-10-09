using System.Text;
using Knowledge_Center_API.Models.Learn;

namespace Knowledge_Center_API.Services.Core
{
    /*
     * Builds the single-file .md export of a Learn session, in timeline order.
     *
     * Each entry is preceded by an HTML comment anchor ("<!-- entry 12 · ... -->")
     * so @answers(12) references in the exported text still point somewhere a
     * reader can find.
     */
    public static class LearnMarkdownExporter
    {
        public static string Build(LearnSessionDetailsDto session)
        {
            // Lines always end in '\n' whatever the host OS; entry sources already
            // do, and StringBuilder's line helper would mix in "\r\n" on Windows.
            var md = new StringBuilder();
            void Line(string text = "") => md.Append(text).Append('\n');

            Line($"# {session.Title}");
            Line();

            var meta = new List<string>();
            if (!string.IsNullOrWhiteSpace(session.Topic)) meta.Add($"Topic: {session.Topic}");
            if (!string.IsNullOrWhiteSpace(session.NodeTitle)) meta.Add($"Knowledge Node: {session.NodeTitle}");
            meta.Add($"Created {session.CreatedAt:yyyy-MM-dd}");
            Line($"> {string.Join(" · ", meta)}");
            Line();

            foreach (var entry in session.Entries.OrderBy(e => e.Position))
            {
                var parsed = LearnSourceParser.Parse(entry.Source);

                Line($"<!-- entry {entry.EntryId} · {parsed.EntryType} · {entry.CreatedAt:yyyy-MM-dd HH:mm} -->");

                Line(entry.Source.TrimEnd());

                Line();
            }

            return md.ToString();
        }
    }
}
