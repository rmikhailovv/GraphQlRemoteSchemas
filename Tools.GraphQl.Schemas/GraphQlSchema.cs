using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBeProtected.Global

namespace Tools.GraphQl.Schemas
{
    public class GraphQlSchema
    {
        private static readonly HashSet<string> DefaultScalarTypes = new HashSet<string>
        {
            "String",
            "Boolean",
            "Float",
            "Int",
            "ID",
            "Id",
            "Date",
            "DateTime",
            "DateTimeOffset",
            "Seconds",
            "Milliseconds",
            "Decimal"
        };

        private static readonly Regex EmptyBracesRegex = new Regex("\\((\\s+)?\\)",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

        private static readonly Regex ExtraCommaRegex = new Regex("(,)(\\s+)?\\)",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

        public Dictionary<string, JToken> Types { get; private set; } = new Dictionary<string, JToken>();
        public Dictionary<string, JToken> QueriesFields { get; private set; } = new Dictionary<string, JToken>();
        public Dictionary<string, JToken> MutationFields { get; private set; } = new Dictionary<string, JToken>();

        public JToken Schema { get; private set; }
        public string QueriesType { get; private set; }
        public string MutationsType { get; private set; }
        public string Name { get; }

        public GraphQlSchema(string name)
        {
            Name = name;
        }

        public void LoadFromIntrospection(JToken introspection)
        {
            Schema = introspection["data"]?["__schema"];
            InitializeSchemaData();
        }

        private void InitializeSchemaData()
        {
            if (Schema == null) return;

            try
            {
                QueriesType = Schema["queryType"]?["name"]?.ToString();
            }
            catch
            {
                // ignored
            }

            try
            {
                MutationsType = Schema["mutationType"]?["name"]?.ToString();
            }
            catch
            {
                // ignored
            }

            var types = Schema["types"] as JArray;
            if (types == null) return;

            Types = types.ToDictionary(x => x["name"].ToString(), x => x);

            if (QueriesType != null)
            {
                JToken queriesTypeInfo = Types[QueriesType];
                if (queriesTypeInfo["fields"] is JArray queryFields)
                    QueriesFields = queryFields.ToDictionary(x => x["name"].ToString(), x => x);
            }

            if (MutationsType != null)
            {
                JToken mutationsTypeInfo = Types[MutationsType];
                if (mutationsTypeInfo["fields"] is JArray mutationsFields)
                    MutationFields = mutationsFields.ToDictionary(x => x["name"].ToString(), x => x);
            }
        }

        public void MergeWith(GraphQlSchema schema)
        {
            if (schema.Schema == null) return;

            if (Schema == null)
            {
                Schema = schema.Schema;
                InitializeSchemaData();
                return;
            }

            QueriesType ??= schema.QueriesType;
            MutationsType ??= schema.MutationsType;

            if (Schema["types"] is JArray typesRef)
                foreach ((string name, JToken type) in schema.Types)
                {
                    if (Types.ContainsKey(name)) continue;
                    typesRef.Add(type);
                    Types.Add(name, type);
                }

            if (QueriesType != null && Types[QueriesType]["fields"] is JArray queryFieldsRef)
                foreach ((string name, JToken field) in schema.QueriesFields)
                {
                    if (QueriesFields.ContainsKey(name)) continue;
                    queryFieldsRef.Add(field);
                }

            if (MutationsType != null && Types[MutationsType]["fields"] is JArray mutationsFieldsRef)
                foreach ((string name, JToken field) in schema.MutationFields)
                {
                    if (MutationFields.ContainsKey(name)) continue;
                    mutationsFieldsRef.Add(field);
                }

            InitializeSchemaData();
        }

        public string GetMatchingRequest(string request)
        {
            GraphQlQuerySegment[] segments = GraphQlParser.Parse(request);
            foreach (GraphQlQuerySegment segment in segments)
                if (segment.IsFragment)
                {
                    if (!Types.ContainsKey(segment.FragmentType))
                        segment.IsRemoved = true;
                }
                else
                {
                    if (segment.ParametersDefinitions?.Any() ?? false)
                        foreach (GraphQlParameterDefinition variable in segment.ParametersDefinitions)
                            if (!Types.ContainsKey(variable.Type))
                                variable.IsRemoved = true;

                    GraphQlField[] fields = segment.QueryFields;
                    foreach (GraphQlField field in fields)
                        if (!QueriesFields.ContainsKey(field.Name) && !MutationFields.ContainsKey(field.Name))
                            field.IsRemoved = true;
                }

            return RemoveUnusedParameters(EraseRemovedItems(request, segments));
        }

        public string GetNotMatchingRequest(string request)
        {
            GraphQlQuerySegment[] segments = GraphQlParser.Parse(request);
            foreach (GraphQlQuerySegment segment in segments)
                if (segment.IsFragment)
                {
                    if (Types.ContainsKey(segment.FragmentType))
                        segment.IsRemoved = true;
                }
                else
                {
                    if (segment.ParametersDefinitions?.Any() ?? false)
                        foreach (GraphQlParameterDefinition variable in segment.ParametersDefinitions)
                            if (!DefaultScalarTypes.Contains(variable.Type) && Types.ContainsKey(variable.Type))
                                variable.IsRemoved = true;

                    GraphQlField[] fields = segment.QueryFields;
                    foreach (GraphQlField field in fields)
                        if (QueriesFields.ContainsKey(field.Name) || MutationFields.ContainsKey(field.Name))
                            field.IsRemoved = true;
                }

            return RemoveUnusedParameters(EraseRemovedItems(request, segments));
        }

        private static string RemoveUnusedParameters(string query)
        {
            GraphQlQuerySegment[] segments = GraphQlParser.Parse(query);
            GraphQlParameterDefinition[] parametersDefinitions = segments
                .Where(x => x.ParametersDefinitions?.Any() ?? false).SelectMany(x => x.ParametersDefinitions).ToArray();
            string[] parametersUsages = GraphQlParser.ParametersUsageRegex.Matches(query)
                .Select(x => x.Groups[5].Value).ToArray();

            foreach (GraphQlParameterDefinition parameter in parametersDefinitions)
                if (!parametersUsages.Contains(parameter.Name))
                    parameter.IsRemoved = true;

            return EraseRemovedItems(query, segments);
        }

        private static string EraseRemovedItems(string request, GraphQlQuerySegment[] segments)
        {
            foreach (GraphQlQuerySegment segment in segments)
                if (segment.IsFragment)
                {
                    if (segment.IsRemoved) request = request.Replace(segment.Segment, "");
                }
                else
                {
                    if (segment.QueryFields == null || !segment.QueryFields.Any() ||
                        segment.QueryFields.All(f => f.IsRemoved))
                    {
                        request = request.Replace(segment.Segment, "");
                        continue;
                    }

                    foreach (GraphQlField field in segment.QueryFields)
                        if (field.IsRemoved)
                            request = request.Replace(field.Definition, "");

                    if (segment.ParametersDefinitions == null || !segment.ParametersDefinitions.Any()) continue;

                    foreach (GraphQlParameterDefinition variable in segment.ParametersDefinitions)
                        if (variable.IsRemoved)
                            request = request.Replace(variable.Definition, "");
                }

            request = ExtraCommaRegex.Replace(request, ")");
            request = EmptyBracesRegex.Replace(request, "");

            return request;
        }

        public bool RequestMatch(string request) =>
            QueriesFields.Any(q => request.Contains(q.Key)) ||
            MutationFields.Any(m => request.Contains(m.Key));

        public void MergeWithMany(GraphQlSchema[] schemas)
        {
            foreach (GraphQlSchema schema in schemas) MergeWith(schema);
        }
    }
}