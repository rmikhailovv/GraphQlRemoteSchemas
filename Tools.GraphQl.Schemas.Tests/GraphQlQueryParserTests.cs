using System.Linq;
using FluentAssertions;
using Xunit;

namespace Tools.GraphQl.Schemas.Tests
{
    public class GraphQlQueryParserTests
    {
        [Fact]
        public void GIVEN_query_with_minimal_spacing_WHEN_parsing_THEN_success()
        {
            string query = @"{hero{name role}}";
            GraphQlQuerySegment[] result = GraphQlParser.Parse(query);
            GraphQlQuerySegment segment = result.Single();
            segment.QueryFields.Length.Should().Be(1);
            segment.QueryFields[0].Name.Should().Be("hero");
            segment.QueryFields[0].Definition.Should().Be("hero{name role}");
        }

        [Fact]
        public void GIVEN_query_with_comments_WHEN_parsing_THEN_success()
        {
            string query = @"{
  hero {
    name
    # Queries can have comments!
    friends {
      name
    }
  }
}";
            GraphQlQuerySegment[] result = GraphQlParser.Parse(query);
            GraphQlQuerySegment segment = result.Single();
            segment.QueryFields.Length.Should().Be(1);
            segment.QueryFields[0].Name.Should().Be("hero");
            segment.QueryFields[0].Definition.Should().Be(@"hero {
    name
    # Queries can have comments!
    friends {
      name
    }
  }
");
        }
        
        [Fact]
        public void GIVEN_query_with_big_spacing_WHEN_parsing_THEN_success()
        {
            string query = @"{

hero
             
       { 
name                   


                       role      


}



}";
            GraphQlQuerySegment[] result = GraphQlParser.Parse(query);
            GraphQlQuerySegment segment = result.Single();
            segment.QueryFields[0].Name.Should().Be("hero");
            segment.QueryFields[0].Definition.Should().Be(@"hero
             
       { 
name                   


                       role      


}



");
        }

        [Fact]
        public void GIVEN_query_with_multiple_fields_WHEN_parsing_THEN_success()
        {
            string query = @"{
hero {
    name
}
hero_evil {
    name {
        field
    }
 }}";
            GraphQlQuerySegment[] result = GraphQlParser.Parse(query);
            GraphQlQuerySegment segment = result.Single();
            segment.QueryFields.Length.Should().Be(2);
            segment.QueryFields[0].Name.Should().Be("hero");
            segment.QueryFields[0].Definition.Should().Be(@"hero {
    name
}
");
            segment.QueryFields[1].Name.Should().Be("hero_evil");
            segment.QueryFields[1].Definition.Should().Be(@"hero_evil {
    name {
        field
    }
 }");
        }
        
        [Fact]
        public void GIVEN_query_with_parameters_WHEN_parsing_THEN_success()
        {
            string query = @"{
  human(id: ""1000"") {
    name
    height(unit: FOOT)
  }
    }";
            GraphQlQuerySegment[] result = GraphQlParser.Parse(query);
            GraphQlQuerySegment segment = result.Single();
            segment.QueryFields.Length.Should().Be(1);
            segment.QueryFields[0].Name.Should().Be("human");
            segment.QueryFields[0].Definition.Should().Be(@"human(id: ""1000"") {
    name
    height(unit: FOOT)
  }
    ");
        }
        
        
        [Fact]
        public void GIVEN_query_with_aliases_spacing_WHEN_parsing_THEN_success()
        {
            string query = @"{
  empireHero: hero(episode: EMPIRE) {
    name
  }
  jediHero: hero(episode: JEDI) {
    name
  }
}";
            GraphQlQuerySegment[] result = GraphQlParser.Parse(query);
            GraphQlQuerySegment segment = result.Single();
            segment.QueryFields.Length.Should().Be(2);
            segment.QueryFields[0].Name.Should().Be("hero");
            segment.QueryFields[0].Definition.Should().Be(@"empireHero: hero(episode: EMPIRE) {
    name
  }
  ");
            segment.QueryFields[1].Name.Should().Be("hero");
            segment.QueryFields[1].Definition.Should().Be(@"jediHero: hero(episode: JEDI) {
    name
  }
");
        }
        
        [Fact]
        public void GIVEN_query_with_aliases_and_fragments_spacing_WHEN_parsing_THEN_success()
        {
            string query = @"{
  leftComparison: hero(episode: EMPIRE) {
    ...comparisonFields
  }
  hero(episode: JEDI) {
    ...comparisonFields
  }
}

fragment comparisonFields on Character {
  name
  appearsIn
  friends {
    name
  }
}";
            GraphQlQuerySegment[] result = GraphQlParser.Parse(query);
            result.Length.Should().Be(2);

            GraphQlQuerySegment fragment = result[1];
            fragment.IsFragment.Should().BeTrue();
            fragment.FragmentType.Should().Be("Character");
            fragment.Segment.Should().Be(@"

fragment comparisonFields on Character {
  name
  appearsIn
  friends {
    name
  }
}");

            GraphQlQuerySegment segment = result[0];
            segment.QueryFields.Length.Should().Be(2);
            segment.QueryFields[0].Name.Should().Be("hero");
            segment.QueryFields[0].Definition.Should().Be(@"leftComparison: hero(episode: EMPIRE) {
    ...comparisonFields
  }
  ");
            segment.QueryFields[1].Name.Should().Be("hero");
            segment.QueryFields[1].Definition.Should().Be(@"hero(episode: JEDI) {
    ...comparisonFields
  }
");
        }
        
        [Fact]
        public void GIVEN_query_with_parameters_and_fragments_WHEN_parsing_THEN_success()
        {
            string query = @"query HeroComparison($first: Int = 3) {
  leftComparison: hero(episode: EMPIRE) {
    ...comparisonFields
  }
  rightComparison: hero(episode: JEDI) {
    ...comparisonFields
  }
}

fragment comparisonFields on Character {
  name
  friendsConnection(first: $first) {
    totalCount
    edges {
      node {
        name
      }
    }
  }
}";
            GraphQlQuerySegment[] result = GraphQlParser.Parse(query);
            result.Length.Should().Be(2);

            GraphQlQuerySegment fragment = result[1];
            fragment.IsFragment.Should().BeTrue();
            fragment.FragmentType.Should().Be("Character");
            fragment.FragmentParametersUsage.Length.Should().Be(1);
            fragment.FragmentParametersUsage[0].Should().Be("first");
            fragment.Segment.Should().Be(@"

fragment comparisonFields on Character {
  name
  friendsConnection(first: $first) {
    totalCount
    edges {
      node {
        name
      }
    }
  }
}");

            GraphQlQuerySegment segment = result[0];
            segment.QueryFields.Length.Should().Be(2);
            segment.QueryFields[0].Name.Should().Be("hero");
            segment.QueryFields[0].Definition.Should().Be(@"leftComparison: hero(episode: EMPIRE) {
    ...comparisonFields
  }
  ");
            segment.QueryFields[1].Name.Should().Be("hero");
            segment.QueryFields[1].Definition.Should().Be(@"rightComparison: hero(episode: JEDI) {
    ...comparisonFields
  }
");
        }
        
        [Fact]
        public void GIVEN_named_query_with_minimal_spacing_WHEN_parsing_THEN_success()
        {
            string query = @"query test{hero{name role}}";
            GraphQlQuerySegment[] result = GraphQlParser.Parse(query);
            GraphQlQuerySegment segment = result.Single();
            segment.QueryFields.Length.Should().Be(1);
            segment.QueryFields[0].Name.Should().Be("hero");
            segment.QueryFields[0].Definition.Should().Be("hero{name role}");
        }
        
        [Fact]
        public void GIVEN_query_with_directives_WHEN_parsing_THEN_success()
        {
            string query = @"{
  search(text: ""an"") {
    __typename
    ... on Human {
      name
    }
    ... on Droid {
      name
    }
    ... on Starship {
      name
    }
  }
}";
            GraphQlQuerySegment[] result = GraphQlParser.Parse(query);
            GraphQlQuerySegment segment = result.Single();
            segment.QueryFields.Length.Should().Be(1);
            segment.QueryFields[0].Name.Should().Be("search");
            segment.QueryFields[0].Definition.Should().Be(@"search(text: ""an"") {
    __typename
    ... on Human {
      name
    }
    ... on Droid {
      name
    }
    ... on Starship {
      name
    }
  }
");
        }
    }
}