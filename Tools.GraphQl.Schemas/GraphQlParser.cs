using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Tools.GraphQl.Schemas
{
    public static class GraphQlParser
    {
        private static readonly Regex FragmentRegex = new Regex(
            "fragment(\\s+)([a-zA-Z0-9_]+)(\\s+)on(\\s+)([a-zA-Z0-9_]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        private static readonly Regex ParametersDefinitionRegex = new Regex(
            "(\\s+)?\\$([a-zA-Z0-9_]+)(\\s+)?\\:(\\s+)?([a-zA-Z0-9_]+)(\\s+)?!?(\\s+)?(,|(\\s+)?=(\\s+)?[a-zA-Z0-9_\\\"\\!]+(\\s+)?,?)?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        internal static readonly Regex ParametersUsageRegex = new Regex(
            "(\\s+)?([a-zA-Z0-9_]+)(\\s+)?:(\\s+)?\\$([a-zA-Z0-9_]+)(\\s+)?(,?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        private static readonly Dictionary<char, char> BracePairs = new Dictionary<char, char>
        {
            { '{', '}' }, { '(', ')' }
        };

        public static GraphQlQuerySegment[] Parse(string query)
        {
            var result = new List<GraphQlQuerySegment>();
            int startIndex = 0;
            for (int i = 0; i < query.Length; i++)
                if (query[i] == '{')
                {
                    int endIndex = FindClosingBracePosition(query, i, '{');
                    int length = endIndex - startIndex + 1;
                    int bodyLength = endIndex - i - 1;
                    int bodyStart = i + 1;
                    string body = query.Substring(bodyStart, bodyLength);
                    string segmentContent = query.Substring(startIndex, length);

                    var segment = new GraphQlQuerySegment
                    {
                        Start = startIndex,
                        Length = length,
                        BodyStart = bodyStart,
                        BodyLength = bodyLength,
                        Segment = segmentContent,
                        Body = body
                    };

                    Match fragmentMatch = FragmentRegex.Match(segmentContent);
                    if (fragmentMatch.Success)
                    {
                        segment.IsFragment = true;
                        segment.FragmentType = fragmentMatch.Groups[5].Value;
                        segment.FragmentParametersUsage = ParametersUsageRegex.Matches(segment.Body)
                            .Select(x => x.Groups[5].Value).ToArray();
                    }
                    else
                    {
                        segment.ParametersDefinitions = GetParameterDefinitions(segmentContent);
                        segment.QueryFields = SegmentGetFields(segment);
                    }

                    result.Add(segment);

                    startIndex = endIndex + 1;
                    i = endIndex;
                }

            return result.ToArray();
        }

        private static GraphQlField[] SegmentGetFields(GraphQlQuerySegment segment)
        {
            int startIndex = 0;
            int? aliasIndex = null;
            GraphQlField lastField = null;
            bool wordFound = false;
            string segmentBody = segment.Body;
            var result = new List<GraphQlField>();

            void HandleFieldData(int index)
            {
                int fieldDefinitionStart = aliasIndex ?? startIndex;
                int length = index - fieldDefinitionStart;
                lastField.Definition = segmentBody.Substring(fieldDefinitionStart, length);
                lastField.ParametersUsed = ParametersUsageRegex.Matches(lastField.Definition)
                    .Select(x => x.Groups[5].Value).ToArray();
                lastField = null;
                aliasIndex = null;
            }

            void HandleWordFound(int index)
            {
                string word = segmentBody.Substring(startIndex, index - startIndex);
                var field = new GraphQlField { Name = word };
                lastField = field;
                result.Add(field);
                wordFound = false;
            }
            
            for (int i = 0; i < segmentBody.Length; i++)
            {
                char ch = segmentBody[i];
                if (char.IsLetterOrDigit(ch) || ch == '_')
                {
                    if (lastField != null) HandleFieldData(i);

                    if (!wordFound)
                    {
                        wordFound = true;
                        startIndex = i;
                    }

                    if (i == segmentBody.Length - 1) HandleWordFound(i);
                }
                else if (ch == ':') // aliases handling
                {
                    aliasIndex = startIndex;
                    startIndex = i + 1;
                    wordFound = false;
                }
                else if (ch == '(' || ch == '{')
                {
                    if (wordFound) HandleWordFound(i);
                    i = FindClosingBracePosition(segmentBody, i, ch);
                }
                else
                {
                    if (wordFound) HandleWordFound(i);
                }
            }

            if (lastField != null) HandleFieldData(segmentBody.Length);

            return result.ToArray();
        }

        private static GraphQlParameterDefinition[] GetParameterDefinitions(string segmentContent)
        {
            MatchCollection variablesMatches = ParametersDefinitionRegex.Matches(segmentContent);
            if (!variablesMatches.Any()) return Array.Empty<GraphQlParameterDefinition>();
            return variablesMatches.Select(m => new GraphQlParameterDefinition
            {
                Name = m.Groups[2].Value, Type = m.Groups[5].Value, Definition = m.Value
            }).ToArray();
        }

        private static int FindClosingBracePosition(string query, int firstBraceIndex, char brace)
        {
            int count = 0;
            char closingBrace = BracePairs[brace];

            for (int i = firstBraceIndex; i < query.Length; i++)
            {
                if (query[i] == brace) count++;
                else if (query[i] == closingBrace) count--;
                if (count == 0) return i;
            }

            throw new Exception("Invalid GraphQL query: unable to find closing brace");
        }
    }
}