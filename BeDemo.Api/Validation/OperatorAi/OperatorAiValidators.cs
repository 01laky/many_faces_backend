using BeDemo.Api.Configuration;
using BeDemo.Api.Models.Requests.OperatorAi;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Validation.OperatorAi;

public sealed class CreateOperatorAiConversationRequestValidator : AbstractValidator<CreateOperatorAiConversationRequest>
{
	public CreateOperatorAiConversationRequestValidator(IOptions<OperatorAiOptions> options)
	{
		var maxTitle = 200;
		RuleFor(x => x.Title)
			.MaximumLength(maxTitle)
			.When(x => !string.IsNullOrEmpty(x.Title));
	}
}

public sealed class UpdateOperatorAiConversationRequestValidator : AbstractValidator<UpdateOperatorAiConversationRequest>
{
	public UpdateOperatorAiConversationRequestValidator()
	{
		RuleFor(x => x.Title).MaximumLength(200).When(x => x.Title != null);
	}
}

public sealed class OperatorAiMessagesQueryValidator : AbstractValidator<OperatorAiMessagesQuery>
{
	public OperatorAiMessagesQueryValidator(IOptions<OperatorAiOptions> options)
	{
		var pageSize = options.Value.MessagesPageSize;
		RuleFor(x => x.Limit).InclusiveBetween(1, pageSize);
		RuleFor(x => x.BeforeId).GreaterThan(0).When(x => x.BeforeId.HasValue);
	}
}

public sealed class OperatorAiConversationsListQueryValidator : AbstractValidator<OperatorAiConversationsListQuery>
{
	public OperatorAiConversationsListQueryValidator(IOptions<OperatorAiOptions> options)
	{
		var max = options.Value.MaxConversationsListPageSize;
		RuleFor(x => x.Limit).InclusiveBetween(1, max);
	}
}

public sealed class OperatorAiConversationsListQuery
{
	public int Limit { get; set; } = 50;
}
