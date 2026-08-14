using System.ComponentModel.DataAnnotations;
using System.IO.Compression;
using System.Text;
using AssignmentSystem.Api.DTOs;
using AssignmentSystem.Api.Models;
using AssignmentSystem.Api.Services;
using Xunit;

namespace AssignmentSystem.Tests;

public class ValidationAndUploadRulesTests
{
    [Fact]
    public void AssignmentRequest_RejectsUnknownStatusAndInvalidIdentifiers()
    {
        var request = new AssignmentRequest(
            "Title", "Description", DateTime.UtcNow.AddDays(1), 20,
            CourseId: 0, SubjectId: 0, Status: (AssignmentStatus)99);

        var errors = Validate(request);

        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(AssignmentRequest.CourseId)));
        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(AssignmentRequest.SubjectId)));
        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(AssignmentRequest.Status)));
    }

    [Fact]
    public void SubmissionRequest_RejectsNonHttpFileUrl()
    {
        var errors = Validate(new SubmissionRequest("Answer", "ftp://example.com/file.zip"));

        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(SubmissionRequest.FileUrl)));
    }

    [Theory]
    [InlineData("work.exe", 100)]
    [InlineData("work.pdf", 0)]
    [InlineData("work.pdf", UploadRules.MaximumFileBytes + 1)]
    public void UploadMetadata_RejectsUnsupportedOrInvalidFiles(string fileName, long length)
    {
        Assert.False(UploadRules.TryValidateMetadata(fileName, length, out _, out _));
    }

    [Fact]
    public void UploadContent_RejectsExecutableRenamedAsPdf()
    {
        using var stream = new MemoryStream("MZ executable"u8.ToArray());

        Assert.False(UploadRules.HasValidContent(".pdf", stream));
    }

    [Fact]
    public void UploadContent_AcceptsPdfSignature()
    {
        using var stream = new MemoryStream("%PDF-1.7\n"u8.ToArray());

        Assert.True(UploadRules.HasValidContent(".pdf", stream));
    }

    [Fact]
    public void UploadContent_RejectsZipRenamedAsDocxWithoutWordDocument()
    {
        using var stream = CreateArchive(("notes.txt", "hello"));

        Assert.False(UploadRules.HasValidContent(".docx", stream));
    }

    [Fact]
    public void UploadContent_AcceptsMinimalDocxArchive()
    {
        using var stream = CreateArchive(
            ("[Content_Types].xml", "<Types />"),
            ("word/document.xml", "<document />"));

        Assert.True(UploadRules.HasValidContent(".docx", stream));
    }

    private static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);
        return results;
    }

    private static MemoryStream CreateArchive(params (string Name, string Content)[] entries)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }

        stream.Position = 0;
        return stream;
    }
}
