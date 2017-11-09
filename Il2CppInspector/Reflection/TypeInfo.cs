﻿/*
    Copyright 2017 Katy Coe - http://www.hearthcode.org - http://www.djkaty.com

    All rights reserved.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Il2CppInspector.Reflection {
    public class TypeInfo : MemberInfo
    {
        // IL2CPP-specific data
        public Il2CppTypeDefinition Definition { get; }
        public int Index { get; }

        // Information/flags about the type
        // Undefined if the Type represents a generic type parameter
        public TypeAttributes Attributes { get; }

        // Type that this type inherits from
        private Il2CppType baseType;
        public TypeInfo BaseType => baseType != null? Assembly.Model.GetType(baseType, MemberTypes.TypeInfo) : null;

        // True if the type contains unresolved generic type parameters
        public bool ContainsGenericParameters { get; }

        // C# colloquial name of the type (if available)
        public string CSharpName {
            get {
                var s = Namespace + "." + base.Name;
                var i = Il2CppConstants.FullNameTypeString.IndexOf(s);
                var n = (i != -1 ? Il2CppConstants.CSharpTypeString[i] : base.Name);
                if (IsArray)
                    n = ElementType.CSharpName;
                var g = (GenericTypeParameters != null ? "<" + string.Join(", ", GenericTypeParameters.Select(x => x.CSharpName)) + ">" : "");
                return (IsPointer ? "void *" : "") + n + g + (IsArray ? "[]" : "");
            }
        }

        public List<ConstructorInfo> DeclaredConstructors => throw new NotImplementedException();
        public List<EventInfo> DeclaredEvents => throw new NotImplementedException();
        public List<FieldInfo> DeclaredFields { get; } = new List<FieldInfo>();
        public List<MemberInfo> DeclaredMembers => throw new NotImplementedException();
        public List<MethodInfo> DeclaredMethods { get; } = new List<MethodInfo>();
        public List<TypeInfo> DeclaredNestedTypes => throw new NotImplementedException();
        public List<PropertyInfo> DeclaredProperties => throw new NotImplementedException();

        // Method that the type is declared in if this is a type parameter of a generic method
        public MethodBase DeclaringMethod => throw new NotImplementedException();

        // Gets the type of the object encompassed or referred to by the current array, pointer or reference type
        public TypeInfo ElementType { get; }

        // Type name including namespace
        public string FullName => (IsPointer? "void *" : "")
            + Namespace
            + (Namespace.Length > 0? "." : "")
            + base.Name
            + (GenericTypeParameters != null ? "<" + string.Join(", ", GenericTypeParameters.Select(x => x.Name)) + ">" : "")
            + (IsArray? "[]" : "");

        // TODO: Alot of other generics stuff
        
        public List<TypeInfo> GenericTypeParameters { get; }

        public bool HasElementType => ElementType != null;
        public bool IsAbstract => (Attributes & TypeAttributes.Abstract) == TypeAttributes.Abstract;
        public bool IsArray { get; }
        public bool IsByRef => throw new NotImplementedException();
        public bool IsClass => (Attributes & TypeAttributes.Class) == TypeAttributes.Class;
        public bool IsEnum => throw new NotImplementedException();
        public bool IsGenericParameter { get; }
        public bool IsGenericType => throw new NotImplementedException();
        public bool IsGenericTypeDefinition => throw new NotImplementedException();
        public bool IsInterface => (Attributes & TypeAttributes.Interface) == TypeAttributes.Interface;
        public bool IsNested { get; } // TODO: Partially implemented
        public bool IsNestedPrivate => throw new NotImplementedException();
        public bool IsNestedPublic => throw new NotImplementedException();
        public bool IsPointer { get; }
        public bool IsPrimitive => throw new NotImplementedException();
        public bool IsPublic => (Attributes & TypeAttributes.Public) == TypeAttributes.Public;
        public bool IsSealed => (Attributes & TypeAttributes.Sealed) == TypeAttributes.Sealed;
        public bool IsSerializable => (Attributes & TypeAttributes.Serializable) == TypeAttributes.Serializable;
        public bool IsValueType => BaseType?.FullName == "System.ValueType";

        public override MemberTypes MemberType { get; }

        public override string Name {
            get => (IsPointer ? "void *" : "")
                + base.Name
                + (GenericTypeParameters != null? "<" + string.Join(", ", GenericTypeParameters.Select(x => x.Name)) + ">" : "")
                + (IsArray ? "[]" : "");
            protected set => base.Name = value;
        }

        public string Namespace { get; }

        // Number of dimensions of an array
        private readonly int arrayRank;
        public int GetArrayRank() => arrayRank;

        // TODO: Custom attribute stuff

        public string[] GetEnumNames() => throw new NotImplementedException();

        public TypeInfo GetEnumUnderlyingType() => throw new NotImplementedException();

        public Array GetEnumValues() => throw new NotImplementedException();

        // TODO: Event stuff

        // TODO: Generic stuff

        // Initialize from specified type index in metadata
        public TypeInfo(Il2CppInspector pkg, int typeIndex, Assembly owner) :
            base(owner) {
            Definition = pkg.TypeDefinitions[typeIndex];
            Index = typeIndex;
            Namespace = pkg.Strings[Definition.namespaceIndex];
            Name = pkg.Strings[pkg.TypeDefinitions[typeIndex].nameIndex];

            if (Definition.parentIndex >= 0)
                baseType = pkg.TypeUsages[Definition.parentIndex];

            if ((Definition.flags & Il2CppConstants.TYPE_ATTRIBUTE_SERIALIZABLE) != 0)
                Attributes |= TypeAttributes.Serializable;
            if ((Definition.flags & Il2CppConstants.TYPE_ATTRIBUTE_VISIBILITY_MASK) == Il2CppConstants.TYPE_ATTRIBUTE_PUBLIC)
                Attributes |= TypeAttributes.Public;
            if ((Definition.flags & Il2CppConstants.TYPE_ATTRIBUTE_ABSTRACT) != 0)
                Attributes |= TypeAttributes.Abstract;
            if ((Definition.flags & Il2CppConstants.TYPE_ATTRIBUTE_SEALED) != 0)
                Attributes |= TypeAttributes.Sealed;
            if ((Definition.flags & Il2CppConstants.TYPE_ATTRIBUTE_INTERFACE) != 0)
                Attributes |= TypeAttributes.Interface;

            // Not sure about this, works for now
            if (!IsInterface)
                Attributes |= TypeAttributes.Class;

            // Add all fields
            for (var f = Definition.fieldStart; f < Definition.fieldStart + Definition.field_count; f++)
                DeclaredFields.Add(new FieldInfo(pkg, f, this));

            // Add all methods
            for (var m = Definition.methodStart; m < Definition.methodStart + Definition.method_count; m++)
                DeclaredMethods.Add(new MethodInfo(pkg, m, this));

            MemberType = MemberTypes.TypeInfo;
        }

        // Initialize type from binary usage
        public TypeInfo(Il2CppReflector model, Il2CppType pType, MemberTypes memberType) : base(null) {
            var image = model.Package.Binary.Image;

            IsNested = true;
            MemberType = memberType;

            // Generic type unresolved and concrete instance types
            if (pType.type == Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST) {
                var generic = image.ReadMappedObject<Il2CppGenericClass>(pType.datapoint);
                var genericTypeDef = model.GetTypeFromIndex(generic.typeDefinitionIndex);

                Namespace = genericTypeDef.Namespace;
                Name = genericTypeDef.Name;

                // TODO: Generic* properties and ContainsGenericParameters

                // Get the instantiation
                var genericInstance = image.ReadMappedObject<Il2CppGenericInst>(generic.context.class_inst);

                // Get list of pointers to type parameters (both unresolved and concrete)
                var genericTypeParameters = image.ReadMappedArray<uint>(genericInstance.type_argv, (int)genericInstance.type_argc);

                GenericTypeParameters = new List<TypeInfo>();
                foreach (var pArg in genericTypeParameters) {
                    var argType = image.ReadMappedObject<Il2CppType>(pArg);
                    // TODO: Detect whether unresolved or concrete (add concrete to GenericTypeArguments instead)
                    // TODO: GenericParameterPosition etc. in types we generate here
                    GenericTypeParameters.Add(model.GetType(argType)); // TODO: Fix MemberType here
                }
                Attributes |= TypeAttributes.Class;
            }

            // Array with known dimensions and bounds
            if (pType.type == Il2CppTypeEnum.IL2CPP_TYPE_ARRAY) {
                var descriptor = image.ReadMappedObject<Il2CppArrayType>(pType.datapoint);
                var elementType = image.ReadMappedObject<Il2CppType>(descriptor.etype);
                ElementType = model.GetType(elementType);
                Namespace = ElementType.Namespace;
                Name = ElementType.Name;

                IsArray = true;
                arrayRank = descriptor.rank;
            }

            // Dynamically allocated array
            if (pType.type == Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY) {
                var elementType = image.ReadMappedObject<Il2CppType>(pType.datapoint);
                ElementType = model.GetType(elementType);
                Namespace = ElementType.Namespace;
                Name = ElementType.Name;

                IsArray = true;
            }

            // Unresolved generic type variable
            if (pType.type == Il2CppTypeEnum.IL2CPP_TYPE_VAR || pType.type == Il2CppTypeEnum.IL2CPP_TYPE_MVAR) {
                ContainsGenericParameters = true;
                Attributes |= TypeAttributes.Class;
                IsGenericParameter = true;
                Name = "T"; // TODO: Don't hardcode parameter name

                // TODO: GenericTypeParameters?
            }

            // Pointer type
            IsPointer = (pType.type == Il2CppTypeEnum.IL2CPP_TYPE_PTR);
        }
    }
}