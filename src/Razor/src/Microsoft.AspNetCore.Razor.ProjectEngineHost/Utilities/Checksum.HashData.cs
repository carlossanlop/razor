﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.AspNetCore.Razor.Utilities;

internal sealed partial class Checksum
{
    [StructLayout(LayoutKind.Explicit, Size = HashSize)]
    public readonly struct HashData(long data1, long data2, int data3) : IEquatable<HashData>
    {
        [FieldOffset(0)]
        public readonly long Data1 = data1;

        [FieldOffset(8)]
        public readonly long Data2 = data2;

        [FieldOffset(16)]
        public readonly int Data3 = data3;

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(Data1);
            writer.Write(Data2);
            writer.Write(Data3);
        }

        public override bool Equals(object? obj)
            => obj is HashData data &&
               Equals(data);

        public bool Equals(HashData other)
            => Data1 == other.Data1 &&
               Data2 == other.Data2 &&
               Data3 == other.Data3;

        public override int GetHashCode()
        {
            // The checksum is already a hash. Just provide a 4-byte value as a well-distributed hash code.
            return (int)Data1;
        }

        public static bool operator ==(HashData left, HashData right)
            => left.Equals(right);

        public static bool operator !=(HashData left, HashData right)
            => !(left == right);
    }
}
