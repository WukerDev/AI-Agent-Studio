using AgentStudio.Service.Protos;
using FluentValidation;

namespace AgentStudio.Service.Application;

public class SaveAgentNodeRequestValidator : AbstractValidator<SaveAgentNodeRequest>
{
    public SaveAgentNodeRequestValidator()
    {
        RuleFor(request => request.Node)
            .NotNull()
            .WithMessage("error.validation.node_required");

        When(request => request.Node != null, () =>
        {
            RuleFor(request => request.Node.Name)
                .NotEmpty()
                .WithMessage("error.validation.node_name_required");

            RuleFor(request => request.Node.Type)
                .NotEqual(NodeType.Unspecified)
                .WithMessage("error.validation.node_type_unspecified");
        });
    }
}

public class DeleteAgentNodeRequestValidator : AbstractValidator<DeleteAgentNodeRequest>
{
    public DeleteAgentNodeRequestValidator()
    {
        RuleFor(request => request.Id)
            .NotEmpty()
            .WithMessage("error.validation.node_id_required");
    }
}

public class ExecuteWorkflowRequestValidator : AbstractValidator<ExecuteWorkflowRequest>
{
    public ExecuteWorkflowRequestValidator()
    {
        RuleFor(request => request.FlowId)
            .NotEmpty()
            .WithMessage("error.validation.flow_id_required");
    }
}
