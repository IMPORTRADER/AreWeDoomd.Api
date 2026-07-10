namespace AreWeDoomd.AgentService.Prompting;

public interface IPromptComposer
{
    ComposedPrompt Compose(AgentPersona? persona, CommentCreatedPromptInput input);

    ComposedPrompt Compose(AgentPersona? persona, PostMentionedPromptInput input);
}
