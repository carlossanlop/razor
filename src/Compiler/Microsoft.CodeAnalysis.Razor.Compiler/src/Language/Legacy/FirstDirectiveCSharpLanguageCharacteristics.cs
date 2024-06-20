﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

internal class FirstDirectiveCSharpLanguageCharacteristics : LegacyCSharpLanguageCharacteristics
{
    private FirstDirectiveCSharpLanguageCharacteristics()
    {
    }

    public static new FirstDirectiveCSharpLanguageCharacteristics Instance { get; } = new FirstDirectiveCSharpLanguageCharacteristics();

    public override LegacyCSharpTokenizer CreateTokenizer(SeekableTextReader source) => new DirectiveCSharpTokenizer(source);
}
