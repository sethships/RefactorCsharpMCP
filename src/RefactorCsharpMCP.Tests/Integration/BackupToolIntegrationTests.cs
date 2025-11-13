using FluentAssertions;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Tests.Integration;

/// <summary>
/// Integration tests using real code from BackupTool project.
/// </summary>
public class BackupToolIntegrationTests
{
    [Fact]
    public void MakeFieldReadonly_OnBackupToolDbContext_ShouldMakeReadonly()
    {
        // Arrange - Real code from BackupTool Program.cs
        var sourceCode = @"namespace BackupTool
{
    using Data;
    using Logging;
    using System;

    class Program
    {
        static batEntities _dbContext;

        static Program()
        {
            _dbContext = new batEntities();
        }

        static void Main(string[] args)
        {
            int count = _dbContext.DeviceTrackings.Count();
            Console.WriteLine(count);
        }
    }
}";
        var refactoring = new MakeFieldReadonly();

        // Act
        var result = refactoring.Execute(sourceCode, "Program", "_dbContext");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("static readonly batEntities _dbContext");
        result.Message.Should().Contain("Made field '_dbContext' readonly");
    }

    [Fact]
    public void ExtractClass_OnBackupToolConstants_ShouldExtractConfiguration()
    {
        // Arrange - Simplified version of BackupTool constants
        var sourceCode = @"namespace BackupTool
{
    class Program
    {
        const string SEPERATOR = ""\t"";
        const string TABLE_NAME = ""DeviceTrackings"";
        static readonly string OUT_FILE = TABLE_NAME + "".csv"";
        static readonly string LOG_FILE = TABLE_NAME + "".log.txt"";
        const int START_PAGE = 1;
        const int PAGE_SIZE = 100;

        static void Main(string[] args)
        {
            // Use these constants
        }
    }
}";
        var refactoring = new ExtractClass();

        // Act - Extract file-related configuration
        var result = refactoring.Execute(
            sourceCode,
            "Program",
            "FileConfiguration",
            "OUT_FILE,LOG_FILE"
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("internal class FileConfiguration");
        result.RefactoredCode.Should().Contain("OUT_FILE");
        result.RefactoredCode.Should().Contain("LOG_FILE");
        result.RefactoredCode.Should().Contain("private readonly FileConfiguration _fileConfiguration");
    }

    [Fact]
    public void AnalyzeDependencies_OnBackupToolProgram_ShouldDetectFieldUsage()
    {
        // Arrange - Simplified BackupTool Program
        var sourceCode = @"namespace BackupTool
{
    using Data;

    class Program
    {
        static batEntities _dbContext;
        static readonly string OUT_FILE = ""output.csv"";

        static void QueryData()
        {
            var count = _dbContext.DeviceTrackings.Count();
        }

        static void SaveData()
        {
            // Save to OUT_FILE
        }
    }
}";
        var analyzer = new RefactorCsharpMCP.Core.Analysis.DependencyAnalyzer();

        // Act
        var result = analyzer.AnalyzeFieldUsage(sourceCode, "Program");

        // Assert
        result.Should().ContainKey("_dbContext");
        result["_dbContext"].UsedInMethods.Should().Contain("QueryData");
        result.Should().ContainKey("OUT_FILE");
        result["OUT_FILE"].IsReadOnly.Should().BeTrue();
    }

    [Fact]
    public void SafeDelete_OnBackupToolUnusedMethod_ShouldDelete()
    {
        // Arrange - Hypothetical unused helper method
        var sourceCode = @"namespace BackupTool
{
    class Program
    {
        static void Main(string[] args)
        {
            ProcessData();
        }

        static void ProcessData()
        {
            // Main logic
        }

        static void ObsoleteHelper()
        {
            // This method is no longer used
        }
    }
}";
        var refactoring = new SafeDelete();

        // Act
        var result = refactoring.Execute(sourceCode, "Program", "ObsoleteHelper");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().NotContain("ObsoleteHelper");
        result.RefactoredCode.Should().Contain("ProcessData");
    }

    [Fact]
    public void SafeDelete_OnBackupToolUsedMethod_ShouldFail()
    {
        // Arrange - Method that's actually used
        var sourceCode = @"namespace BackupTool
{
    class Program
    {
        static void Main(string[] args)
        {
            ProcessData();
        }

        static void ProcessData()
        {
            // Main logic
        }
    }
}";
        var refactoring = new SafeDelete();

        // Act
        var result = refactoring.Execute(sourceCode, "Program", "ProcessData");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("is referenced");
    }
}
