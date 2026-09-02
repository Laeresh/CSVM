using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace CSVM.Tests;

/// <summary>
/// Reads a compiled assembly's metadata and lists every type a subject namespace's types refer
/// to, signatures and method-body IL alike, so an architecture test can reject references the
/// design forbids. Metadata-only on purpose: nothing is loaded or executed, and a reference
/// hidden inside a method body is found where signature reflection would miss it.
/// </summary>
public static class AssemblyDependencyScan
{
    private static readonly Dictionary<short, OperandType> OpcodeOperands = BuildOpcodeTable();

    /// <summary>Every "subject type -&gt; referenced type" pair where the referenced type's full
    /// name is banned. Throws when no type matches the subject filter: a scan over nothing
    /// proves nothing and must not pass.</summary>
    public static IReadOnlyList<string> Violations(
        string assemblyPath,
        Func<string, bool> subjectNamespace,
        Func<string, bool> bannedTypeName)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();

        var violations = new List<string>();
        int subjects = 0;
        foreach (var handle in md.TypeDefinitions)
        {
            var type = md.GetTypeDefinition(handle);
            if (!subjectNamespace(OutermostNamespace(md, type)))
            {
                continue;
            }

            subjects++;
            var referenced = new HashSet<string>(StringComparer.Ordinal);
            CollectTypeReferences(md, pe, type, referenced);
            foreach (string name in referenced.Where(bannedTypeName).OrderBy(n => n, StringComparer.Ordinal))
            {
                violations.Add($"{FullName(md, type)} -> {name}");
            }
        }

        if (subjects == 0)
        {
            throw new InvalidOperationException(
                $"No type in {Path.GetFileName(assemblyPath)} matched the subject filter; the scan checked nothing.");
        }

        return violations;
    }

    private static Dictionary<short, OperandType> BuildOpcodeTable()
    {
        var table = new Dictionary<short, OperandType>();
        foreach (var field in typeof(OpCodes).GetFields())
        {
            if (field.GetValue(null) is OpCode op)
            {
                table[op.Value] = op.OperandType;
            }
        }

        return table;
    }

    private static void CollectTypeReferences(
        MetadataReader md, PEReader pe, TypeDefinition type, HashSet<string> into)
    {
        var collector = new Collector(into);
        AddEntity(md, type.BaseType, collector, into);
        foreach (var implHandle in type.GetInterfaceImplementations())
        {
            AddEntity(md, md.GetInterfaceImplementation(implHandle).Interface, collector, into);
        }

        foreach (var gpHandle in type.GetGenericParameters())
        {
            foreach (var cHandle in md.GetGenericParameter(gpHandle).GetConstraints())
            {
                AddEntity(md, md.GetGenericParameterConstraint(cHandle).Type, collector, into);
            }
        }

        foreach (var fieldHandle in type.GetFields())
        {
            md.GetFieldDefinition(fieldHandle).DecodeSignature(collector, null);
        }

        foreach (var propHandle in type.GetProperties())
        {
            md.GetPropertyDefinition(propHandle).DecodeSignature(collector, null);
        }

        foreach (var methodHandle in type.GetMethods())
        {
            CollectMethod(md, pe, md.GetMethodDefinition(methodHandle), collector, into);
        }
    }

    private static void CollectMethod(
        MetadataReader md, PEReader pe, MethodDefinition method, Collector collector, HashSet<string> into)
    {
        method.DecodeSignature(collector, null);
        foreach (var gpHandle in method.GetGenericParameters())
        {
            foreach (var cHandle in md.GetGenericParameter(gpHandle).GetConstraints())
            {
                AddEntity(md, md.GetGenericParameterConstraint(cHandle).Type, collector, into);
            }
        }

        if (method.RelativeVirtualAddress == 0)
        {
            return;
        }

        var body = pe.GetMethodBody(method.RelativeVirtualAddress);
        if (!body.LocalSignature.IsNil)
        {
            md.GetStandaloneSignature(body.LocalSignature).DecodeLocalSignature(collector, null);
        }

        foreach (var region in body.ExceptionRegions)
        {
            if (!region.CatchType.IsNil)
            {
                AddEntity(md, region.CatchType, collector, into);
            }
        }

        WalkIl(md, body.GetILBytes() ?? Array.Empty<byte>(), collector, into);
    }

    // The operand token kinds carry every type a body can name: field/method owners, newobj
    // targets, typeof and cast operands, calli signatures.
    private static void WalkIl(MetadataReader md, byte[] il, Collector collector, HashSet<string> into)
    {
        int i = 0;
        while (i < il.Length)
        {
            short code = il[i] == 0xFE ? (short)((0xFE << 8) | il[i + 1]) : il[i];
            i += il[i] == 0xFE ? 2 : 1;
            switch (OpcodeOperands[code])
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    i += 1;
                    break;
                case OperandType.InlineVar:
                    i += 2;
                    break;
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.InlineSig:
                    AddEntity(md, MetadataTokens.EntityHandle(BitConverter.ToInt32(il, i)), collector, into);
                    i += 4;
                    break;
                case OperandType.InlineSwitch:
                    int targets = BitConverter.ToInt32(il, i);
                    i += 4 + (4 * targets);
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    i += 8;
                    break;
                default:
                    i += 4;
                    break;
            }
        }
    }

    private static void AddEntity(
        MetadataReader md, EntityHandle handle, Collector collector, HashSet<string> into)
    {
        if (handle.IsNil)
        {
            return;
        }

        switch (handle.Kind)
        {
            case HandleKind.TypeDefinition:
                into.Add(FullName(md, md.GetTypeDefinition((TypeDefinitionHandle)handle)));
                break;
            case HandleKind.TypeReference:
                into.Add(FullName(md, (TypeReferenceHandle)handle));
                break;
            case HandleKind.TypeSpecification:
                md.GetTypeSpecification((TypeSpecificationHandle)handle).DecodeSignature(collector, null);
                break;
            case HandleKind.MethodDefinition:
                var method = md.GetMethodDefinition((MethodDefinitionHandle)handle);
                into.Add(FullName(md, md.GetTypeDefinition(method.GetDeclaringType())));
                break;
            case HandleKind.FieldDefinition:
                var field = md.GetFieldDefinition((FieldDefinitionHandle)handle);
                into.Add(FullName(md, md.GetTypeDefinition(field.GetDeclaringType())));
                break;
            case HandleKind.MemberReference:
                var member = md.GetMemberReference((MemberReferenceHandle)handle);
                AddEntity(md, member.Parent, collector, into);
                if (member.GetKind() == MemberReferenceKind.Method)
                {
                    member.DecodeMethodSignature(collector, null);
                }
                else
                {
                    member.DecodeFieldSignature(collector, null);
                }

                break;
            case HandleKind.MethodSpecification:
                var spec = md.GetMethodSpecification((MethodSpecificationHandle)handle);
                AddEntity(md, spec.Method, collector, into);
                spec.DecodeSignature(collector, null);
                break;
            case HandleKind.StandaloneSignature:
                md.GetStandaloneSignature((StandaloneSignatureHandle)handle).DecodeMethodSignature(collector, null);
                break;
            default:
                break;
        }
    }

    private static string OutermostNamespace(MetadataReader md, TypeDefinition type)
    {
        while (type.IsNested)
        {
            type = md.GetTypeDefinition(type.GetDeclaringType());
        }

        return md.GetString(type.Namespace);
    }

    private static string FullName(MetadataReader md, TypeDefinition type)
    {
        string name = md.GetString(type.Name);
        while (type.IsNested)
        {
            type = md.GetTypeDefinition(type.GetDeclaringType());
            name = $"{md.GetString(type.Name)}/{name}";
        }

        string ns = md.GetString(type.Namespace);
        return ns.Length == 0 ? name : $"{ns}.{name}";
    }

    private static string FullName(MetadataReader md, TypeReferenceHandle handle)
    {
        var reference = md.GetTypeReference(handle);
        string name = md.GetString(reference.Name);
        while (reference.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            reference = md.GetTypeReference((TypeReferenceHandle)reference.ResolutionScope);
            name = $"{md.GetString(reference.Name)}/{name}";
        }

        string ns = md.GetString(reference.Namespace);
        return ns.Length == 0 ? name : $"{ns}.{name}";
    }

    // The signature decoder's callbacks: only named types matter, so the definition/reference
    // hooks record and every composite shape passes through.
    private sealed class Collector : ISignatureTypeProvider<object?, object?>
    {
        private readonly HashSet<string> _into;

        public Collector(HashSet<string> into) => _into = into;

        public object? GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            _into.Add(FullName(reader, reader.GetTypeDefinition(handle)));
            return null;
        }

        public object? GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            _into.Add(FullName(reader, handle));
            return null;
        }

        public object? GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
            return null;
        }

        public object? GetPrimitiveType(PrimitiveTypeCode typeCode) => null;

        public object? GetSZArrayType(object? elementType) => null;

        public object? GetArrayType(object? elementType, ArrayShape shape) => null;

        public object? GetByReferenceType(object? elementType) => null;

        public object? GetPointerType(object? elementType) => null;

        public object? GetPinnedType(object? elementType) => null;

        public object? GetGenericInstantiation(object? genericType, ImmutableArray<object?> typeArguments) => null;

        public object? GetGenericMethodParameter(object? genericContext, int index) => null;

        public object? GetGenericTypeParameter(object? genericContext, int index) => null;

        public object? GetModifiedType(object? modifier, object? unmodifiedType, bool isRequired) => null;

        public object? GetFunctionPointerType(MethodSignature<object?> signature) => null;
    }
}
