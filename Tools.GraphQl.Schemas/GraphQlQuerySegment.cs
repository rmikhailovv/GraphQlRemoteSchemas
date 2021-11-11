namespace Tools.GraphQl.Schemas
{
    public class GraphQlQuerySegment
    {
        public int Start { get; set; }
        public int Length { get; set; }
        public int BodyStart { get; set; }
        public int BodyLength { get; set; }
        public string Segment { get; set; }
        public string Body { get; set; }
        public GraphQlField[] QueryFields { get; set; }
        public GraphQlParameterDefinition[] ParametersDefinitions { get; set; }
        public string[] FragmentParametersUsage { get; set; }
        public bool IsFragment { get; set; }
        public string FragmentType { get; set; }
        public bool IsRemoved { get; set; }
    }
}