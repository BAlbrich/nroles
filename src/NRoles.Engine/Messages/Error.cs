﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace NRoles.Engine {

  /// <summary>
  /// Represents an error message.
  /// </summary>
  public sealed class Error : Message {

    /// <summary>Error codes.</summary>
    public enum Code {

      /// <summary>
      /// Occurs when an uncaught exception is thrown from NRoles.
      /// This is probably a bug in NRoles.
      /// </summary>
      InternalError = 1,

      /// <summary>
      /// Occurs when a role has a parameterized constructor.
      /// </summary>
      RoleCannotContainParameterizedConstructor = 40,

      /// <summary>
      /// Occurs when a role inherits from a class.
      /// </summary>
      RoleInheritsFromClass = 41,

      /// <summary>
      /// Occurs when a composition does not implement an abstract role member.
      /// </summary>
      DoesNotImplementAbstractRoleMember = 42,

      /// <summary>
      /// Occurs when 2 or more members from different roles in a composition are in conflict.
      /// </summary>
      Conflict = 43,

      /// <summary>
      /// Occurs when all members from the same member group are excluded from a composition.
      /// </summary>
      AllMembersExcluded = 44,

      /// <summary>
      /// Occurs when there're methods with conflicting signatures in different roles in a composition.
      /// </summary>
      MethodsWithConflictingSignatures = 45,

      /// <summary>
      /// Occurs when a type inherits from a role.
      /// </summary>
      TypeCantInheritFromRole = 46,

      /// <summary>
      /// Occurs when a role is being instantiated. Roles are implicitly abstract and can't be instantiated.
      /// </summary>
      RoleInstantiated = 47,

      /// <summary>
      /// Occurs when a role composes itself.
      /// </summary>
      RoleComposesItself = 48,

      /// <summary>
      /// Occurs when a composition composes a role as a type parameter.
      /// </summary>
      CompositionWithTypeParameter = 49,

      /// <summary>
      /// Occurs when there're members in different roles in a composition with the same name.
      /// </summary>
      MembersWithSameName = 50,

      /// <summary>
      /// Occurs when a member declared in a role view is not found on the corresponding role.
      /// </summary>
      RoleViewMemberNotFoundInRole = 51,

      /// <summary>
      /// Occurs when a role member is aliased more than once.
      /// </summary>
      RoleMemberAliasedAgain = 52,

      /// <summary>
      /// Occurs when there's a timeout when waiting for PEVerify to complete.
      /// </summary>
      PEVerifyTimeout = 53,

      /// <summary>
      /// Occurs when PEVerify detects errors in the generated assembly.
      /// This could mean that the assembly is unverifiable (as when it has unsafe code),
      /// or that there's a bug in the roles engine.
      /// </summary>
      PEVerifyError = 54,

      /// <summary>
      /// Occurs when the PEVerify executable file doesn't exist.
      /// </summary>
      PEVerifyDoesntExist = 55,

      /// <summary>
      /// Occurs in the presence of warnings when they are being treated as errors.
      /// </summary>
      ErrorFromWarnings = 56,

      /// <summary>
      /// Occurs when a role view defines multiple roles. A role view can only define a 
      /// single role.
      /// </summary>
      RoleViewWithMultipleRoles = 57,

      /// <summary>
      /// Occurs when a role view is not defined as an interface. Role view must be 
      /// defined as interfaces.
      /// </summary>
      RoleViewIsNotAnInterface = 58,

      /// <summary>
      /// Occurs when a method in a role is a platform invoke method. This is the case with extern methods marked with the DllImport attribute. This is not supported.
      /// </summary>
      RoleHasPInvokeMethod = 59,

      /// <summary>
      /// Occurs when a composition does not provide its type to a role's self type constraint.
      /// </summary>
      SelfTypeConstraintNotSetToCompositionType = 60,

      /// <summary>
      /// Occurs when a role has a member marked as a placeholder.
      /// This is not allowed since roles can have abstract members instead.
      /// </summary>
      RoleHasPlaceholder = 61,

      /// <summary>
      /// Occurs when a role explicitly implements interface members. This scenario is not supported.
      /// </summary>
      RoleHasExplicitInterfaceImplementation = 666

    }

    private Error(Code number, string text, SequencePoint sequencePoint = null) : 
      base(MessageType.Error, (int)number, text, sequencePoint: sequencePoint) { }

    public static Message InternalError() {
      return new Error(
        Code.InternalError,
        "Oops, an internal error occurred.");
    }
    internal static Error RoleCannotContainParameterizedConstructor(object role, object constructor, SequencePoint sequencePoint) {
      return new Error(
        Code.RoleCannotContainParameterizedConstructor,
        $"Role '{role}' cannot contain parameterized constructor '{constructor}'.",
        sequencePoint);
    }
    internal static Error RoleInheritsFromClass(object role, object baseClass) {
      return new Error(
        Code.RoleInheritsFromClass,
        $"Role '{role}' cannot derive from class '{baseClass}'. Roles can only derive from object, implement interfaces and compose other roles.");
    }
    internal static Error DoesNotImplementAbstractRoleMember(object compositionClass, object abstractRoleMember) {
      return new Error(
        Code.DoesNotImplementAbstractRoleMember,
        $"'{compositionClass}' does not implement abstract role member '{abstractRoleMember}'.");
    }
    internal static Error Conflict(object composition, object member, List<RoleCompositionMember> roleMembersInConflict) {
      var roles = string.Join("', '", roleMembersInConflict.Select(rmb => rmb.Role.ToString()).ToArray());
      return new Error(
        Code.Conflict,
        $"Conflict found in role composition '{composition}' for '{member}'. The conflict comes from: '{roles}'");
    }
    internal static Error AllMembersExcluded(object compositionClass, object roleMember) {
      return new Error(
        Code.AllMembersExcluded,
        $"'{compositionClass}' excludes all role members '{roleMember}'.");
    }
    internal static Error MethodsWithConflictingSignatures(object conflictingMethods) {
      return new Error(
        Code.MethodsWithConflictingSignatures,
        $"Methods have conflicting signatures:{conflictingMethods}.");
    }
    internal static Error TypeCantInheritFromRole(object inheritingType, object roleType) {
      return new Error(
        Code.TypeCantInheritFromRole,
        $"Type '{inheritingType}' cannot inherit from role '{roleType}'.");
    }
    internal static Error RoleInstantiated(object roleType, object instantiatingLocation, SequencePoint sequencePoint) {
      return new Error(
        Code.RoleInstantiated,
        $"Role '{roleType}' is being instantiated in '{instantiatingLocation}'. Roles cannot be instantiated.",
        sequencePoint);
    }
    internal static Error RoleComposesItself(object roleType) {
      return new Error(
        Code.RoleComposesItself,
        $"Role '{roleType}' cannot compose itself.");
    }
    internal static Error CompositionWithTypeParameter(object compositionType) {
      return new Error(
        Code.CompositionWithTypeParameter,
        $"Class '{compositionType}' cannot compose a role as a type parameter.");
    }
    internal static Error MembersWithSameName(object members) {
      return new Error(
        Code.MembersWithSameName,
        $"Members can't be declared with the same name:{members}.");
    }
    internal static Error RoleViewMemberNotFoundInRole(object role, object member) {
      return new Error(
        Code.RoleViewMemberNotFoundInRole,
        $"Role view member '{member}' could not be found in the role '{role}'.");
    }
    internal static Error RoleMemberAliasedAgain(object roleView, object role, object member) {
      return new Error(
        Code.RoleMemberAliasedAgain,
        $"The role member '{member}' of role '{role}' cannot be aliased multiple times (detected at role view '{roleView}').");
    }
    internal static Error PEVerifyTimeout(int timeoutInMillis) {
      return new Error(
        Code.PEVerifyTimeout, 
        $"PEVerify took too long and had to be terminated. The current timeout is of {timeoutInMillis / 1000.0}s.");
    }
    internal static Error PEVerifyError(object description) {
      return new Error(
        Code.PEVerifyError,
        $"PEVerify found errors in the mutated assembly:\n{description}");
    }
    internal static Error PEVerifyDoesntExist(string path) {
      return new Error(
        Code.PEVerifyDoesntExist,
        $"The PEVerify supplied path '{path}' doesn't exist.");
    }
    internal static Error ErrorFromWarnings() {
      return new Error(
        Code.ErrorFromWarnings,
        "Error generated from the presence of warnings.");
    }
    internal static Message RoleViewWithMultipleRoles(object roleView, List<TypeReference> allRolesForView) {
      return new Error(
        Code.RoleViewWithMultipleRoles,
        $"The role view '{roleView}' adapts multiple roles. Use a single role per role view.");
    }
    internal static Message RoleViewIsNotAnInterface(object roleView) {
      return new Error(
        Code.RoleViewIsNotAnInterface,
        $"The role view '{roleView}' must be declared as an interface.");
    }
    internal static Error RoleHasPInvokeMethod(object method) {
      return new Error(
        Code.RoleHasPInvokeMethod,
        $"The role method '{method}' is a PInvoke method. This is not supported.");
    }
    internal static Error SelfTypeConstraintNotSetToCompositionType(object composition, object role, object selfType) {
      return new Error(
        Code.SelfTypeConstraintNotSetToCompositionType,
        $"Composition '{composition}' doesn't provide itself as the self-type parameter to role '{role}'. It uses '{selfType}' instead.");
    }
    internal static Error RoleHasPlaceholder(object member) {
      return new Error(
        Code.RoleHasPlaceholder,
        $"Role member '{member}' is marked as a placeholder. Roles cannot have placeholders, use an abstract member not marked as a placeholder instead.");
    }
    internal static Message RoleHasExplicitInterfaceImplementation(object role) {
      return new Error(
        Code.RoleHasExplicitInterfaceImplementation,
        $"The role '{role}' explicitly implements interface members. This is not supported.");
    }
  
  }

}
