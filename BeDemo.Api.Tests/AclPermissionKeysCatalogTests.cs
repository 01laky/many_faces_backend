using System.Reflection;
using FluentAssertions;
using Xunit;
using BeDemo.Api.Security;

namespace BeDemo.Api.Tests;

/// <summary>Ensures permission strings stay unique and non-empty (FE mirrors the same values).</summary>
public class AclPermissionKeysCatalogTests
{
	[Fact]
	public void AllPublicStringConstants_AreNonEmptyUniqueAndStablePrefix()
	{
		var t = typeof(AclPermissionKeys);
		var values = t.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Where(f => f.FieldType == typeof(string) && f.IsLiteral)
			.Select(f => (string)f.GetRawConstantValue()!)
			.ToList();

		values.Should().NotBeEmpty();
		values.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s));
		values.Should().OnlyHaveUniqueItems();
		values.Should().OnlyContain(s => s.Contains(':', StringComparison.Ordinal));
	}
}
