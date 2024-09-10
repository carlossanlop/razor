﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp;
using Roslyn.Test.Utilities;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

[InitializeTestFile]
public abstract class ParserTestBase : IParserTest
{
    private static readonly AsyncLocal<string> _fileName = new AsyncLocal<string>();
    private static readonly AsyncLocal<bool> _isTheory = new AsyncLocal<bool>();

    // UTF-8 with BOM
    private static readonly Encoding _baselineEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
    private readonly bool _validateSpanEditHandlers;
    private readonly bool _useLegacyTokenizer;

    internal ParserTestBase(TestProject.Layer layer, bool validateSpanEditHandlers = false, bool useLegacyTokenizer = false)
    {
        TestProjectRoot = TestProject.GetProjectDirectory(GetType(), layer);
        _validateSpanEditHandlers = validateSpanEditHandlers;
        _useLegacyTokenizer = useLegacyTokenizer;
    }

    /// <summary>
    /// Set to true to autocorrect the locations of spans to appear in document order with no gaps.
    /// Use this when spans were not created in document order.
    /// </summary>
    protected bool FixupSpans { get; set; }

    protected string TestProjectRoot { get; }

    // Used by the test framework to set the 'base' name for test files.
    public static string FileName
    {
        get { return _fileName.Value; }
        set { _fileName.Value = value; }
    }

    public static bool IsTheory
    {
        get { return _isTheory.Value; }
        set { _isTheory.Value = value; }
    }

    protected int BaselineTestCount { get; set; }

    internal virtual void AssertSyntaxTreeNodeMatchesBaseline(RazorSyntaxTree syntaxTree)
    {
        var root = syntaxTree.Root;
        var diagnostics = syntaxTree.Diagnostics;
        var filePath = syntaxTree.Source.FilePath;
        if (FileName == null)
        {
            var message = $"{nameof(AssertSyntaxTreeNodeMatchesBaseline)} should only be called from a parser test ({nameof(FileName)} is null).";
            throw new InvalidOperationException(message);
        }

        if (IsTheory)
        {
            var message = $"{nameof(AssertSyntaxTreeNodeMatchesBaseline)} should not be called from a [Theory] test.";
            throw new InvalidOperationException(message);
        }

        var fileName = BaselineTestCount > 0 ? FileName + $"_{BaselineTestCount}" : FileName;
        var baselineFileName = Path.ChangeExtension(fileName, ".stree.txt");
        var baselineDiagnosticsFileName = Path.ChangeExtension(fileName, ".diag.txt");
        var baselineClassifiedSpansFileName = Path.ChangeExtension(fileName, ".cspans.txt");
        var baselineTagHelperSpansFileName = Path.ChangeExtension(fileName, ".tspans.txt");
        BaselineTestCount++;

        if (GenerateBaselines.ShouldGenerate)
        {
            // Write syntax tree baseline
            var baselineFullPath = Path.Combine(TestProjectRoot, baselineFileName);
            File.WriteAllText(baselineFullPath, SyntaxNodeSerializer.Serialize(root, _validateSpanEditHandlers), _baselineEncoding);

            // Write diagnostics baseline
            var baselineDiagnosticsFullPath = Path.Combine(TestProjectRoot, baselineDiagnosticsFileName);
            var lines = diagnostics.Select(SerializeDiagnostic).ToArray();
            if (lines.Any())
            {
                File.WriteAllLines(baselineDiagnosticsFullPath, lines, _baselineEncoding);
            }
            else if (File.Exists(baselineDiagnosticsFullPath))
            {
                File.Delete(baselineDiagnosticsFullPath);
            }

            // Write classified spans baseline
            var classifiedSpansBaselineFullPath = Path.Combine(TestProjectRoot, baselineClassifiedSpansFileName);
            File.WriteAllText(classifiedSpansBaselineFullPath, ClassifiedSpanSerializer.Serialize(syntaxTree, _validateSpanEditHandlers), _baselineEncoding);

            // Write tag helper spans baseline
            var tagHelperSpansBaselineFullPath = Path.Combine(TestProjectRoot, baselineTagHelperSpansFileName);
            var serializedTagHelperSpans = TagHelperSpanSerializer.Serialize(syntaxTree);
            if (!string.IsNullOrEmpty(serializedTagHelperSpans))
            {
                File.WriteAllText(tagHelperSpansBaselineFullPath, serializedTagHelperSpans, _baselineEncoding);
            }
            else if (File.Exists(tagHelperSpansBaselineFullPath))
            {
                File.Delete(tagHelperSpansBaselineFullPath);
            }

            return;
        }

        // Verify syntax tree
        var stFile = TestFile.Create(baselineFileName, GetType().GetTypeInfo().Assembly);
        if (!stFile.Exists())
        {
            throw new XunitException($"The resource {baselineFileName} was not found.");
        }

        var syntaxNodeBaseline = stFile.ReadAllText();
        var actualSyntaxNodes = SyntaxNodeSerializer.Serialize(root, _validateSpanEditHandlers);
        AssertEx.AssertEqualToleratingWhitespaceDifferences(syntaxNodeBaseline, actualSyntaxNodes);

        // Verify diagnostics
        var baselineDiagnostics = string.Empty;
        var diagnosticsFile = TestFile.Create(baselineDiagnosticsFileName, GetType().GetTypeInfo().Assembly);
        if (diagnosticsFile.Exists())
        {
            baselineDiagnostics = diagnosticsFile.ReadAllText();
        }

        var actualDiagnostics = string.Concat(diagnostics.Select(d => SerializeDiagnostic(d) + "\r\n"));
        Assert.Equal(baselineDiagnostics, actualDiagnostics);

        // Verify classified spans
        var classifiedSpanFile = TestFile.Create(baselineClassifiedSpansFileName, GetType().GetTypeInfo().Assembly);
        if (!classifiedSpanFile.Exists())
        {
            throw new XunitException($"The resource {baselineClassifiedSpansFileName} was not found.");
        }
        else
        {
            var classifiedSpanBaseline = classifiedSpanFile.ReadAllText();
            var actualClassifiedSpans = ClassifiedSpanSerializer.Serialize(syntaxTree, _validateSpanEditHandlers);
            AssertEx.AssertEqualToleratingWhitespaceDifferences(classifiedSpanBaseline, actualClassifiedSpans);
        }

        // Verify tag helper spans
        var tagHelperSpanFile = TestFile.Create(baselineTagHelperSpansFileName, GetType().GetTypeInfo().Assembly);
        if (tagHelperSpanFile.Exists())
        {
            var tagHelperSpanBaseline = tagHelperSpanFile.ReadAllText();
            var actualTagHelperSpans = TagHelperSpanSerializer.Serialize(syntaxTree);
            AssertEx.AssertEqualToleratingWhitespaceDifferences(tagHelperSpanBaseline, actualTagHelperSpans);
        }
    }

    protected static string SerializeDiagnostic(RazorDiagnostic diagnostic)
    {
        var content = RazorDiagnosticSerializer.Serialize(diagnostic);
        var normalized = NormalizeNewLines(content);

        return normalized;
    }

    private static string NormalizeNewLines(string content)
    {
        return Regex.Replace(content, "(?<!\r)\n", "\r\n", RegexOptions.None, TimeSpan.FromSeconds(10));
    }

    internal virtual void BaselineTest(RazorSyntaxTree syntaxTree, bool verifySyntaxTree = true, bool ensureFullFidelity = true)
    {
        if (verifySyntaxTree)
        {
            SyntaxTreeVerifier.Verify(syntaxTree, ensureFullFidelity);
        }

        AssertSyntaxTreeNodeMatchesBaseline(syntaxTree);
    }

    internal RazorSyntaxTree ParseDocument(string document, bool designTime = false, IEnumerable<DirectiveDescriptor> directives = null, RazorParserFeatureFlags featureFlags = null, string fileKind = null, CSharpParseOptions csharpParseOptions = null)
    {
        return ParseDocument(RazorLanguageVersion.Latest, document, directives, designTime, featureFlags, fileKind);
    }

    internal virtual RazorSyntaxTree ParseDocument(RazorLanguageVersion version, string document, IEnumerable<DirectiveDescriptor> directives, bool designTime = false, RazorParserFeatureFlags featureFlags = null, string fileKind = null, CSharpParseOptions csharpParseOptions = null)
    {
        directives = directives ?? [];

        var source = TestRazorSourceDocument.Create(document, filePath: null, relativePath: null, normalizeNewLines: true);

        var options = CreateParserOptions(version, directives, designTime, _validateSpanEditHandlers, _useLegacyTokenizer, featureFlags, fileKind, csharpParseOptions);

        using var context = new ParserContext(source, options);
        using var codeParser = new CSharpCodeParser(directives, context);
        using var markupParser = new HtmlMarkupParser(context);

        codeParser.HtmlParser = markupParser;
        markupParser.CodeParser = codeParser;

        var root = markupParser.ParseDocument().CreateRed();

        var diagnostics = context.ErrorSink.GetErrorsAndClear();

        var codeDocument = RazorCodeDocument.Create(source);

        var syntaxTree = new RazorSyntaxTree(root, source, diagnostics, options);
        codeDocument.SetSyntaxTree(syntaxTree);

        var defaultDirectivePass = new DefaultDirectiveSyntaxTreePass();
        syntaxTree = defaultDirectivePass.Execute(codeDocument, syntaxTree);

        return syntaxTree;
    }

    internal virtual void ParseDocumentTest(string document)
    {
        ParseDocumentTest(document, null, false);
    }

    internal virtual void ParseDocumentTest(string document, string fileKind)
    {
        ParseDocumentTest(document, null, false, fileKind);
    }

    internal virtual void ParseDocumentTest(string document, IEnumerable<DirectiveDescriptor> directives)
    {
        ParseDocumentTest(document, directives, false);
    }

    internal virtual void ParseDocumentTest(string document, bool designTime)
    {
        ParseDocumentTest(document, null, designTime);
    }

    internal virtual void ParseDocumentTest(string document, IEnumerable<DirectiveDescriptor> directives, bool designTime, string fileKind = null)
    {
        ParseDocumentTest(RazorLanguageVersion.Latest, document, directives, designTime, fileKind);
    }

    internal virtual void ParseDocumentTest(RazorLanguageVersion version, string document, IEnumerable<DirectiveDescriptor> directives, bool designTime, string fileKind = null)
    {
        var result = ParseDocument(version, document, directives, designTime, fileKind: fileKind);

        BaselineTest(result);
    }

    private static RazorParserOptions CreateParserOptions(
        RazorLanguageVersion version,
        IEnumerable<DirectiveDescriptor> directives,
        bool designTime,
        bool enableSpanEditHandlers,
        bool useLegacyTokenizer,
        RazorParserFeatureFlags featureFlags,
        string fileKind,
        CSharpParseOptions csharpParseOptions)
    {
        fileKind ??= FileKinds.Legacy;
        return new RazorParserOptions(
            directives.ToArray(),
            designTime,
            parseLeadingDirectives: false,
            useRoslynTokenizer: !useLegacyTokenizer,
            version,
            fileKind,
            enableSpanEditHandlers,
            csharpParseOptions ?? CSharpParseOptions.Default)
        {
            FeatureFlags = featureFlags ?? RazorParserFeatureFlags.Create(version, fileKind)
        };
    }
}
