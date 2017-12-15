﻿using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace NRoles.Engine {

  public sealed class RoleComposerMutator : IMutator {

    private TypeDefinition _targetType;
    private RoleCompositionMemberContainer _container;

    public RoleComposerResult ComposeRoles(MutationParameters parameters) {
      parameters.Validate();
      _targetType = parameters.SourceType;
      if (_targetType == null) throw new ArgumentException("parameters must contain a SourceType", "parameters");

      Tracer.TraceVerbose("Compose class: {0}", _targetType.ToString());

      var result = new RoleComposerResult();

      CheckComposition(result);
      if (!result.Success) { return result; }

      var roles = RetrieveRoles();
      
      var conflictDetector = new ConflictDetector(_targetType);
      {
        var memberProcessResult = conflictDetector.Process(roles);
        result.AddResult(memberProcessResult);
        if (!memberProcessResult.Success) {
          return result;
        }
      }

      if (_targetType.IsRole()) { 
        // roles are not composed
        return result;
      }
      
      {
        _container = conflictDetector.Container;
        var composeResult = ComposeRoles(roles);
        result.AddResult(composeResult);
        if (!composeResult.Success) {
          return result;
        }
      }

      return result;
    }

    #region Checks

    private void CheckComposition(RoleComposerResult result) {
      CheckRolesAreNotTypeParameters(result);
      CheckSelfTypeConstraints(result);
    }

    private void CheckRolesAreNotTypeParameters(RoleComposerResult result) {
      // TODO: create one error message per occurrence of the problem
      if (_targetType.RetrieveDirectRoles().Any(roleType => roleType is GenericParameter)) {
        result.AddMessage(Error.CompositionWithTypeParameter(_targetType));
      }
    }

    private void CheckSelfTypeConstraints(RoleComposerResult result) {
      var checker = new SelfTypeChecker(NameProvider.GetSelfTypeParameterName());
      var selfTypeCheckerResult = checker.CheckComposition(_targetType);
      result.AddResult(selfTypeCheckerResult);
    }

    #endregion

    private List<TypeReference> RetrieveRoles() {
      return _targetType.RetrieveRoles().ToList();
    }

    private IOperationResult ComposeRoles(List<TypeReference> roles) {
      var memberComposer = new RoleComposer(_targetType, roles, _container);
      return memberComposer.Compose();
    }

    IOperationResult IMutator.Mutate(MutationParameters parameters) {
      return ComposeRoles(parameters);
    }

  }

  public class RoleComposerResult : CompositeOperationResult { }
}

