﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace NRoles.Engine {

  using InstructionAndProcessor = Pair<Instruction, ILProcessor>;
  using Mono.Collections.Generic;

  static class TypeDefinitionExtensions {
    
    public static TypeAttributes RetrieveAccessibilityModifiers(this TypeDefinition sourceType) {
      TypeAttributes result = 0;
      if (sourceType.IsNested) {
        if (sourceType.IsNestedPublic) result |= TypeAttributes.NestedPublic;
        else if (sourceType.IsNestedPrivate) result |= TypeAttributes.NestedPrivate;
        else if (sourceType.IsNestedFamily) result |= TypeAttributes.NestedFamily;
        else if (sourceType.IsNestedAssembly) result |= TypeAttributes.NestedAssembly;
        else if (sourceType.IsNestedFamilyAndAssembly) result |= TypeAttributes.NestedFamANDAssem;
        else if (sourceType.IsNestedFamilyOrAssembly) result |= TypeAttributes.NestedFamORAssem;
      }
      else {
        result |= (sourceType.IsPublic ? TypeAttributes.Public : TypeAttributes.NotPublic);
      }
      return result;
    }

  }

  static class MethodDefinitionExtensions {

    public static bool RemainsInRoleInterface(this MethodDefinition method) {
      return !(
        method.IsPrivate ||
        method.IsAssembly ||
        method.IsFamilyAndAssembly ||
        method.IsConstructor ||
        method.IsStatic); // C# doesn't allow calling static methods in interfaces
    }

    public static InstructionAndProcessor FindBaseCtorCallInstruction(this MethodDefinition ctor) {
      if (ctor == null) throw new InstanceArgumentNullException();
      return ctor.FilterInstructions(i => 
          i.OpCode == OpCodes.Call && 
          IsBaseCtor((MethodReference)i.Operand, ctor.DeclaringType)
        ).
        Select(instruction => new InstructionAndProcessor(instruction, ctor.Body.GetILProcessor())).
        SingleOrDefault();
    }

    private static bool IsBaseCtor(MethodReference ctorReference, TypeDefinition type) {
      var ctor = ctorReference.Resolve();
      return ctor.IsConstructor && (
        type == null || // if the method is being built and there's no type information, return true
        ctor.DeclaringType == type.BaseType.Resolve());
    }

    private static IEnumerable<Instruction> FilterInstructions(this MethodDefinition self, Func<Instruction, bool> predicate) {
      var body = self.GetBody();
      if (body == null) yield break;
      foreach (Instruction instruction in body.Instructions) {
        if (predicate(instruction)) {
          yield return instruction;
        }
      }
    }

    public static MethodBody GetBody(this MethodDefinition self) {
      try {
        return self.HasBody ? self.Body : null;
      }
      catch (NullReferenceException) {
        // only happens when the method is NOT abstract and has no implementation -> RVA == 0
        // (TODO: only seen in the wild with an extern method without the DllImport attribute!)
        return null;
      }
    }

    #region Semantic Attributes

    // TODO: Cecil 0.9 has utility methods (eg IsGetter) in MethodDefinition for these

    public static bool IsPropertyAccessor(this MethodDefinition self) {
      if (self == null) throw new InstanceArgumentNullException();
      return self.IsGetter || self.IsSetter;
    }

    public static bool IsEventAccessor(this MethodDefinition self) {
      if (self == null) throw new InstanceArgumentNullException();
      return self.IsAddOn || self.IsRemoveOn || self.IsFire;
    }

    public static bool HasSemanticAttributes(this MethodDefinition self, MethodSemanticsAttributes attributes) {
      if (self == null) throw new InstanceArgumentNullException();
      return (self.SemanticsAttributes & attributes) > 0;
    }

    #endregion

    public static PropertyDefinition ResolveContainerProperty(this MethodDefinition self) {
      return self.DeclaringType.Properties.Single(p => p.GetMethod == self || p.SetMethod == self);
    }

    public static EventDefinition ResolveContainerEvent(this MethodDefinition self) {
      return self.DeclaringType.Events.Single(e => e.AddMethod == self || e.RemoveMethod == self || e.InvokeMethod == self);
    }
  
  }

  static class FieldInstructionsExtensions {

    public static bool IsFieldAccess(this Instruction self) {
      if (self == null) throw new InstanceArgumentNullException();
      return self.IsFieldLoad() || self.IsFieldStore();
    }

    public static bool IsFieldLoad(this Instruction self) {
      if (self == null) throw new InstanceArgumentNullException();
      return 
        self.OpCode == OpCodes.Ldfld ||
        self.OpCode == OpCodes.Ldflda || 
        self.OpCode == OpCodes.Ldsfld ||
        self.OpCode == OpCodes.Ldsflda;
    }
    
    public static bool IsFieldStore(this Instruction self) {
      if (self == null) throw new InstanceArgumentNullException();
      return self.OpCode == OpCodes.Stfld || self.OpCode == OpCodes.Stsfld;
    }

  }

  static class CustomAttributeExtensions {

    public static IEnumerable<CustomAttribute> RetrieveAttributes<TAttribute>(this ICustomAttributeProvider self) where TAttribute : Attribute {
      if (self == null) throw new InstanceArgumentNullException();
      var attributes = new HashSet<CustomAttribute>();
      foreach (var ca in self.CustomAttributes) {
        if (ca.Is<TAttribute>()) {
          attributes.Add(ca);
        }
      }
      return attributes;
    }

    public static bool IsMarkedWith<TAttribute>(this ICustomAttributeProvider self) {
      if (self == null) throw new InstanceArgumentNullException();
      return self.CustomAttributes.Any(ca => ca.Is<TAttribute>());
    }

    public static bool Is<TAttribute>(this CustomAttribute attribute) {
      return 
        attribute.AttributeType.Resolve().FullName == 
        typeof(TAttribute).FullName;
    }

    public static CustomAttribute Create<TAttribute>(this ModuleDefinition module) {
      return new CustomAttribute(
        module.Import(typeof(TAttribute).GetConstructor(Type.EmptyTypes)));
    }

  }

  static class GenericExtensions {

    // source and target must have compatible generic parameters
    public static T CopyGenericParametersAsArgumentsFrom<T>(this T target, IGenericParameterProvider source) where T : IGenericInstance, IGenericParameterProvider {
      if (target == null) throw new InstanceArgumentNullException();
      if (source == null) throw new ArgumentNullException("source");
      if (!source.HasGenericParameters) return target;
      foreach (GenericParameter parameter in source.GenericParameters) {
        // TODO? var newGenericArgument = new GenericParameter(parameter.Name, target);
        target.GenericArguments.Add(parameter);
      }
      return target; // allow chaining
    }

    public static void CopyGenericParametersFrom(this IGenericParameterProvider target, IGenericParameterProvider source) {
      if (target == null) throw new InstanceArgumentNullException();
      if (source == null) throw new ArgumentNullException("source");
      if (!source.HasGenericParameters) return;
      // TODO: custom attributes??
      target.CopyGenericParametersFrom(source.GenericParameters);
    }

    public static void CopyGenericParametersFrom(this IGenericParameterProvider target, Collection<GenericParameter> source) {
      if (target == null) throw new InstanceArgumentNullException();
      if (source == null) throw new ArgumentNullException("source");
      foreach (GenericParameter sourceParameter in source) {
        var targetParameter = new GenericParameter(sourceParameter.Name, target);
        targetParameter.CopyGenericConstraintsFrom(sourceParameter);
        target.GenericParameters.Add(targetParameter);
      }
    }

    public static void CopyGenericConstraintsFrom(this GenericParameter targetParameter, GenericParameter sourceParameter) {
      targetParameter.HasDefaultConstructorConstraint = sourceParameter.HasDefaultConstructorConstraint;
      targetParameter.HasNotNullableValueTypeConstraint = sourceParameter.HasNotNullableValueTypeConstraint;
      targetParameter.HasReferenceTypeConstraint = sourceParameter.HasReferenceTypeConstraint;
      targetParameter.IsContravariant = sourceParameter.IsContravariant;
      targetParameter.IsCovariant = sourceParameter.IsCovariant;
      targetParameter.IsNonVariant = sourceParameter.IsNonVariant;
      if (sourceParameter.HasConstraints) {
        foreach (TypeReference constraint in sourceParameter.Constraints) {
          targetParameter.Constraints.Add(constraint);
        }
      }
    }

    // source and target must have compatible generic parameters
    public static T CopyGenericArgumentsFrom<T>(this T target, IGenericInstance source) where T : IGenericInstance, IGenericParameterProvider {
      if (target == null) throw new InstanceArgumentNullException();
      if (source == null) throw new ArgumentNullException("source");
      if (target.HasGenericArguments) return target; // target already has generic arguments
      if (source.HasGenericArguments) {
        foreach (TypeReference genericArgument in source.GenericArguments) {
          target.GenericArguments.Add(genericArgument);
        }
      }
      return target; // allow chaining
    }

    public static TypeReference ResolveGenericArguments(this TypeReference self) {
      // TODO: move to the MemberResolver class!!
      if (self == null) throw new InstanceArgumentNullException();
      return self.ResolveGenericArguments(self);
    }

    public static TypeReference ResolveGenericArguments(this TypeReference self, TypeReference template) {
      // TODO: move to the MemberResolver class!!
      if (self == null) throw new InstanceArgumentNullException();
      if (template == null) throw new ArgumentNullException("template");

      if (self.Resolve() == null) return self; // TODO: resolve as a TypeReference?
      // self must refer to the same type as template or be nested within it
      if (self.Resolve() != template.Resolve()) {
        if (self.Resolve().DeclaringType == null || self.Resolve().DeclaringType != template.Resolve()) {
          return self;
        }
      }

      // if it has generic arguments, copy them
      var templateAsGenericInstance = template as IGenericInstance;
      if (templateAsGenericInstance != null && templateAsGenericInstance.HasGenericArguments) {
        return 
          new GenericInstanceType(self.Resolve()).
            CopyGenericArgumentsFrom(templateAsGenericInstance);
      }

      // if it has generic parameters, transform them into generic arguments
      if (template.HasGenericParameters) {
        return
          new GenericInstanceType(self.Resolve()).
            CopyGenericParametersAsArgumentsFrom(template);
      }

      // no generics
      return self;
    }

  }

  public static class VisitorExtensions {

    // Migration: copied from Mono.Cecil 0.6 
    public static void Accept(this MethodBody self, ICodeVisitor visitor) {
      visitor.VisitMethodBody(self);
      if (self.HasVariables) {
        visitor.VisitVariableDefinitionCollection(self.Variables);
      }
      visitor.VisitInstructionCollection(self.Instructions);
      if (self.HasExceptionHandlers) {
        visitor.VisitExceptionHandlerCollection(self.ExceptionHandlers);
      }
      if (self.Scope != null) {
        visitor.VisitScope(self.Scope);
      }
      visitor.TerminateMethodBody(self);
    }

  }

}
