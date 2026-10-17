using AgentStudio.Service.Protos;
using FluentValidation;

namespace AgentStudio.Service.Validators;

public class SaveAgentNodeRequestValidator : AbstractValidator<SaveAgentNodeRequest>
{
    public SaveAgentNodeRequestValidator()
    {
        RuleFor(request => request.Node)
            .NotNull()
            .WithMessage("error.validation.node_required");

        RuleFor(request => request.Node.Name)
            .NotEmpty()
            .WithMessage("error.validation.node_name_required");

        RuleFor(request => request.Node.Type)
            .NotEqual(NodeType.Unspecified)
            .WithMessage("error.validation.node_type_unspecified");
    }
}