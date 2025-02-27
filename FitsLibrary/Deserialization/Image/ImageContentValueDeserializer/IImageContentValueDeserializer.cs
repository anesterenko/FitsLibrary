using System;
using System.Numerics;

namespace FitsLibrary.Deserialization.Image.ImageContentValueDeserializer;

internal interface IImageContentValueDeserializer<TData> where TData : INumber<TData>
{
    TData ParseValue(ReadOnlySpan<byte> currentValueBytes);
}
