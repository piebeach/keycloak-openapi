﻿using System.Diagnostics;

namespace Dahag.Keycloak.OpenApiGenerator.Parsing.Resource;

[DebuggerDisplay("{Name}")]
public class RawRxJsResource
{
	public string Name { get; set; }
	public List<RawRxJsResourceAction> Actions { get; } = new();
}

public class ResourceInterpreter : JavaParserBaseVisitor<RawRxJsResource>
{
	private readonly RawRxJsResource _resource = new();
	public RawRxJsResourceAction? CurrentPending { get; private set; }

	public override RawRxJsResource VisitAnnotation(JavaParser.AnnotationContext context)
	{
		CurrentPending ??= new RawRxJsResourceAction()
		{
			Tag = _resource.Name
		};

		var actionAnnotation = new ActionAnnotationInterpreter().VisitAnnotation(context);

		if (actionAnnotation != null)
			CurrentPending.Set(actionAnnotation);

		return _resource;
	}

	public override RawRxJsResource VisitTypeTypeOrVoid(JavaParser.TypeTypeOrVoidContext context)
	{
		if (CurrentPending != null)
			CurrentPending.ReturnsType = context.typeType()?.GetText() ?? "void";

		return base.VisitTypeTypeOrVoid(context);
	}

	public override RawRxJsResource VisitFieldDeclaration(JavaParser.FieldDeclarationContext context)
	{
		CurrentPending = null;
		return base.VisitFieldDeclaration(context);
	}

	public override RawRxJsResource VisitConstructorDeclaration(JavaParser.ConstructorDeclarationContext context)
	{
		CurrentPending = null;
		return base.VisitConstructorDeclaration(context);
	}

	public override RawRxJsResource VisitMethodDeclaration(JavaParser.MethodDeclarationContext context)
	{
		if (CurrentPending != null && CurrentPending.HttpMethod == null && CurrentPending.Path != null)
		{
			CurrentPending.HttpMethod = HttpMethod.NoneFound;
			CurrentPending.ProbablyParentOfAnotherResource = true;
		} else if (CurrentPending != null && CurrentPending.HttpMethod != null && CurrentPending.Path == null)
		{
			var methodName = context.identifier().GetText();
			var maybeAsPrefix = Enum.GetName(CurrentPending.HttpMethod.Value)!;

			var implicitPath = methodName;
			if (implicitPath.StartsWith(maybeAsPrefix, StringComparison.OrdinalIgnoreCase))
				implicitPath = implicitPath[maybeAsPrefix.Length..];
			
			CurrentPending.ImplicitPath = implicitPath.ToLower();
		}
		
		if (CurrentPending != null && CurrentPending.HttpMethod != null && CurrentPending.Path == null)
		{
			var methodName = context.identifier().GetText();
			var maybeAsPrefix = Enum.GetName(CurrentPending.HttpMethod.Value)!;

			var implicitPath = methodName;
			if (implicitPath.StartsWith(maybeAsPrefix, StringComparison.OrdinalIgnoreCase))
				implicitPath = implicitPath[maybeAsPrefix.Length..];
			
			CurrentPending.ImplicitPath = implicitPath.ToLower();
		}

		if (CurrentPending != null)
		{
			var parameters = new ParameterInterpreter().VisitFormalParameters(context.formalParameters());
			CurrentPending.Parameters = parameters;
		}

		return base.VisitMethodDeclaration(context);
	}

	public override RawRxJsResource VisitMethodBody(JavaParser.MethodBodyContext context)
	{
		if (CurrentPending == null)
			return base.VisitMethodBody(context);

		CurrentPending.PersistedAtLine = context.Start.Line;
		_resource.Actions.Add(CurrentPending);
		CurrentPending = null;
		return base.VisitMethodBody(context);
	}

	public override RawRxJsResource VisitCompilationUnit(JavaParser.CompilationUnitContext context)
	{
		_resource.Name = context.typeDeclaration()[0].classDeclaration().identifier().GetText();
		return base.VisitCompilationUnit(context);
	}

	public override RawRxJsResource VisitClassBody(JavaParser.ClassBodyContext context)
	{
		base.VisitClassBody(context);
		return _resource;
	}
}