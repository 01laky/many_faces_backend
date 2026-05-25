using BeDemo.Api.Validation.Files;
using FluentAssertions;

namespace BeDemo.Api.Tests.Validation.Files;

public sealed class FileValidatorTests
{
	private readonly FileValidator _sut = new();

	[Fact]
	public async Task Png_header_is_valid()
	{
		var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0 };
		await using var stream = new MemoryStream(bytes);
		var (ok, code) = await _sut.ValidateImageAsync(stream, "x.png");
		ok.Should().BeTrue();
		code.Should().BeNull();
	}

	[Fact]
	public async Task Random_bytes_fail()
	{
		await using var stream = new MemoryStream([1, 2, 3, 4]);
		var (ok, code) = await _sut.ValidateImageAsync(stream, "x.bin");
		ok.Should().BeFalse();
		code.Should().Be("val_file_format");
	}
}
