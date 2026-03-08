# Proto3 Type Reference for F#

## Scalar Types

| Proto Type | F# Type | Default | Notes |
|------------|---------|---------|-------|
| `double` | `float` | `0.0` | IEEE 754 |
| `float` | `float32` | `0.0f` | IEEE 754 single |
| `int32` | `int` | `0` | Varint encoded, inefficient for negatives |
| `int64` | `int64` | `0L` | Varint encoded |
| `uint32` | `uint32` | `0u` | Varint encoded |
| `uint64` | `uint64` | `0UL` | Varint encoded |
| `sint32` | `int` | `0` | ZigZag encoded, efficient for negatives |
| `sint64` | `int64` | `0L` | ZigZag encoded |
| `fixed32` | `uint32` | `0u` | Always 4 bytes, better for values > 2^28 |
| `fixed64` | `uint64` | `0UL` | Always 8 bytes |
| `sfixed32` | `int` | `0` | Always 4 bytes |
| `sfixed64` | `int64` | `0L` | Always 8 bytes |
| `bool` | `bool` | `false` | |
| `string` | `string` | `""` | UTF-8 encoded |
| `bytes` | `byte[]` | `[||]` | Arbitrary bytes |

## Well-Known Types

| Proto Type | F# Type (FsGrpc) | F# Type (Standard) |
|------------|-------------------|---------------------|
| `google.protobuf.Timestamp` | `DateTimeOffset` | `Google.Protobuf.WellKnownTypes.Timestamp` |
| `google.protobuf.Duration` | `TimeSpan` | `Google.Protobuf.WellKnownTypes.Duration` |
| `google.protobuf.Empty` | unit-like record | `Google.Protobuf.WellKnownTypes.Empty` |
| `google.protobuf.StringValue` | `string option` | `Google.Protobuf.WellKnownTypes.StringValue` |
| `google.protobuf.Int32Value` | `int option` | `Google.Protobuf.WellKnownTypes.Int32Value` |
| `google.protobuf.BoolValue` | `bool option` | `Google.Protobuf.WellKnownTypes.BoolValue` |
| `google.protobuf.DoubleValue` | `float option` | `Google.Protobuf.WellKnownTypes.DoubleValue` |
| `google.protobuf.Struct` | `Map<string,obj>` | `Google.Protobuf.WellKnownTypes.Struct` |
| `google.protobuf.Any` | — | `Google.Protobuf.WellKnownTypes.Any` |

## Collection Types

| Proto Type | F# Type (FsGrpc) | F# Type (Standard) |
|------------|-------------------|---------------------|
| `repeated T` | `T list` | `Google.Protobuf.Collections.RepeatedField<T>` |
| `map<K,V>` | `Map<K,V>` | `Google.Protobuf.Collections.MapField<K,V>` |

## FsGrpc-Specific Mappings

| Proto Concept | F# Output |
|---------------|-----------|
| `message` | Immutable F# record with `{ ... }` syntax |
| `enum` | F# enum type |
| `oneof` | F# discriminated union |
| `optional` (proto3) | Value field (default on absence) |
| Wrapper types | F# `option` |
| Nested message | Nested F# module + record |

## protobuf-net (Code-First) Type Support

| F# Type | Attribute | Notes |
|---------|-----------|-------|
| Record | `[<ProtoContract>]` | Must have `[<ProtoMember(N)>]` on fields |
| `option<T>` | — | Requires `protobuf-net-fsharp` registration |
| DU (simple) | `[<ProtoContract>]` + `[<ProtoInclude>]` | Each case needs include |
| `T[]` (array) | `[<ProtoMember(N)>]` | Serialized as repeated. F# `list<T>` does NOT work. |
| `Dictionary<K,V>` | `[<ProtoMember(N)>]` | Serialized as map entries. F# `Map<K,V>` does NOT work. |
| `decimal` | `[<ProtoMember(N)>]` | Supported natively |
| `DateTimeOffset` | `[<ProtoMember(N)>]` | Supported natively |
| Anonymous record | — | **NOT supported** |
| Struct record | `[<ProtoContract>]` | Must be `[<Struct>]` + CLIMutable |

## Field Number Rules

- Must be positive integers (1 to 536,870,911)
- Numbers 1-15 use 1 byte on the wire; use for frequently-set fields
- Numbers 16-2047 use 2 bytes
- Numbers 19000-19999 are reserved by protobuf
- Once published, NEVER reuse or change field numbers
- Use `reserved` to retire old numbers:

```protobuf
message User {
  reserved 2, 15, 9 to 11;
  reserved "old_field", "deprecated_name";
  string name = 1;
  string email = 3;
}
```
