using CheapClerk.Services;
using Xunit;

namespace CheapClerk.Tests;

public sealed class UploadRulesTests
{
    [Fact]
    public void Validate_PdfFile_Accepted()
    {
        var reason = UploadRules.Validate("document.pdf", 1024);
        Assert.Null(reason);
    }

    [Fact]
    public void Validate_PdfUppercase_Accepted()
    {
        var reason = UploadRules.Validate("document.PDF", 1024);
        Assert.Null(reason);
    }

    [Fact]
    public void Validate_ExeFile_Rejected()
    {
        var reason = UploadRules.Validate("malware.exe", 1024);
        Assert.NotNull(reason);
        Assert.Contains("unsupported file type", reason);
    }

    [Fact]
    public void Validate_EmptyFile_Rejected()
    {
        var reason = UploadRules.Validate("empty.pdf", 0);
        Assert.NotNull(reason);
        Assert.Contains("empty", reason);
    }

    [Fact]
    public void Validate_FileTooLarge_Rejected()
    {
        var reason = UploadRules.Validate("huge.pdf", 51 * 1024 * 1024);
        Assert.NotNull(reason);
        Assert.Contains("exceeds", reason);
        Assert.Contains("50", reason);
    }

    [Fact]
    public void Validate_MaxAllowedSize_Accepted()
    {
        var reason = UploadRules.Validate("large.pdf", 49 * 1024 * 1024);
        Assert.Null(reason);
    }
}
