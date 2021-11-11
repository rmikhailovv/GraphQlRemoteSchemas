namespace Tools.GraphQl.Schemas
{
    public class GraphQlField
    {
        public string Name { get; set; }
        public string Definition { get; set; }
        public string[] ParametersUsed { get; set; }
        internal bool IsRemoved { get; set; }
    }
}