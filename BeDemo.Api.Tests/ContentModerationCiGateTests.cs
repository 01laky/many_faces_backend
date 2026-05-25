using System.Reflection;
using FluentAssertions;
using BeDemo.Api.Services;
namespace BeDemo.Api.Tests;

/// <summary>
/// Security hardening v2 <b>PI-10</b>: keeps CI moderation-security filter aligned with decorated test classes.
/// </summary>
[Trait(ContentModerationCiGate.XunitTraitName, ContentModerationCiGate.XunitTraitCategory)]
public sealed class ContentModerationCiGateTests
{
	[Fact]
	public void Xunit_filter_expression_matches_trait_category_gate()
	{
		ContentModerationCiGate.XunitFilterExpression
			.Should()
			.Be($"{ContentModerationCiGate.XunitTraitName}={ContentModerationCiGate.XunitTraitCategory}");
	}

	[Theory]
	[MemberData(nameof(RequiredSecurityTypes))]
	public void Required_security_test_classes_declare_ModerationSecurity_trait(Type testClass)
	{
		var traitData = CustomAttributeData.GetCustomAttributes(testClass)
			.Where(c => c.AttributeType == typeof(TraitAttribute))
			.ToList();
		traitData.Should().Contain(
			c =>
				c.ConstructorArguments[0].Value as string == ContentModerationCiGate.XunitTraitName &&
				c.ConstructorArguments[1].Value as string == ContentModerationCiGate.XunitTraitCategory,
			because: $"PI-10 CI gate must include {testClass.Name}");
	}

	[Fact]
	public void Required_security_test_class_name_list_matches_decorated_types()
	{
		var fromGate = ContentModerationCiGate.RequiredSecurityTestClassNames.OrderBy(n => n).ToList();
		var fromTypes = RequiredSecurityTypes().Select(t => t[0]).Cast<Type>().Select(t => t.Name).OrderBy(n => n).ToList();
		fromGate.Should().Equal(fromTypes);
	}

	public static IEnumerable<object[]> RequiredSecurityTypes() =>
	[
		[typeof(ContentModerationSecurityEdgeTests)],
		[typeof(ContentModerationUnicodeSpoofingTests)],
		[typeof(ContentModerationTrustBoundaryTests)],
		[typeof(ContentModerationPayloadLogRedactionTests)],
		[typeof(ContentModerationCiGateTests)],
		[typeof(ContentModerationPreviewTextTests)],
		[typeof(ContentModerationProductionPathTests)],
	];
}
